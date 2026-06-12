using Application.DTOs;
using Application.Exceptions;
using Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Application.Services;

public class ReportService : IReportService
{
    private readonly IReportRepository _reportRepository;
    private readonly IExchangeRateService _exchangeRateService;
    private readonly ILogger<ReportService> _logger;

    private static readonly HashSet<string> SupportedCurrencies = ["BOB", "USD"];

    public ReportService(
        IReportRepository reportRepository,
        IExchangeRateService exchangeRateService,
        ILogger<ReportService> logger)
    {
        _reportRepository = reportRepository;
        _exchangeRateService = exchangeRateService;
        _logger = logger;
    }

    public async Task<ConsolidatedBalanceResponse> GetConsolidatedBalanceAsync(
        Guid customerId,
        DateTime startDate,
        DateTime endDate,
        string currency)
    {
        var reportCurrency = currency.ToUpper();
        if (!SupportedCurrencies.Contains(reportCurrency))
            throw new UnsupportedCurrencyException(currency);

        if (!await _reportRepository.CustomerExistsAsync(customerId))
            throw new CustomerNotFoundException(customerId);

        // Fetch the canonical USD/BOB rate once; used for all currency conversions.
        var usdToBobRate = await _exchangeRateService.GetRateAsync("USD", "BOB");

        var accounts = await _reportRepository.GetAccountsByCustomerIdAsync(customerId);

        // Npgsql requires DateTime.Kind=Utc for timestamptz columns.
        // ASP.NET Core parses query-string dates as Kind=Unspecified, so we force UTC here.
        var fromUtc      = DateTime.SpecifyKind(startDate.Date,              DateTimeKind.Utc);
        var toExclusive  = DateTime.SpecifyKind(endDate.Date.AddDays(1),     DateTimeKind.Utc);

        var accountDetails = new List<AccountBalanceDetail>();
        decimal consolidatedBalance = 0m;

        foreach (var account in accounts)
        {
            var movements = await _reportRepository.GetMovementsByAccountAndDateRangeAsync(
                account.AccountId, fromUtc, toExclusive);

            decimal credits = 0m, debits = 0m;
            foreach (var m in movements)
            {
                // Direction is determined by balance change, not movement type,
                // to correctly handle both direct operations and incoming/outgoing transfers.
                if (m.NewBalance >= m.PreviousBalance)
                    credits += m.Amount;
                else
                    debits += m.Amount;
            }

            var balanceConverted = ConvertToReportCurrency(
                account.Balance, account.Currency, reportCurrency, usdToBobRate);

            consolidatedBalance += balanceConverted;

            accountDetails.Add(new AccountBalanceDetail
            {
                AccountId               = account.AccountId,
                AccountNumber           = account.AccountNumber,
                AccountCurrency         = account.Currency,
                CurrentBalance          = account.Balance,
                CurrentBalanceConverted = balanceConverted,
                PeriodMovementsCount    = movements.Count,
                PeriodTotalCredits      = credits,
                PeriodTotalDebits       = debits,
                PeriodNetChange         = credits - debits
            });
        }

        _logger.LogInformation(
            "Reporte consolidado generado. CustomerId={CustomerId} Currency={Currency} Accounts={Accounts} Total={Total}",
            customerId, reportCurrency, accounts.Count, consolidatedBalance);

        return new ConsolidatedBalanceResponse
        {
            CustomerId          = customerId,
            ReportCurrency      = reportCurrency,
            StartDate           = fromUtc,
            EndDate             = DateTime.SpecifyKind(endDate.Date, DateTimeKind.Utc),
            ExchangeRateUsed    = usdToBobRate,
            GeneratedAt         = DateTime.UtcNow,
            Accounts            = accountDetails,
            ConsolidatedBalance = Math.Round(consolidatedBalance, 2)
        };
    }

    /// <summary>
    /// Converts <paramref name="amount"/> from <paramref name="from"/> to
    /// <paramref name="to"/> using the known USD/BOB rate.
    /// </summary>
    private static decimal ConvertToReportCurrency(
        decimal amount, string from, string to, decimal usdToBobRate)
    {
        if (from == to) return amount;

        return (from, to) switch
        {
            ("USD", "BOB") => Math.Round(amount * usdToBobRate, 2),
            ("BOB", "USD") => Math.Round(amount / usdToBobRate, 2),
            _              => amount
        };
    }
}
