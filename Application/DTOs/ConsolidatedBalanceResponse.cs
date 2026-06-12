namespace Application.DTOs;

public class ConsolidatedBalanceResponse
{
    public Guid                      CustomerId       { get; set; }
    public string                    ReportCurrency   { get; set; } = null!;
    public DateTime                  StartDate        { get; set; }
    public DateTime                  EndDate          { get; set; }
    public decimal                   ExchangeRateUsed { get; set; }
    public DateTime                  GeneratedAt      { get; set; }
    public List<AccountBalanceDetail> Accounts        { get; set; } = [];
    public decimal                   ConsolidatedBalance { get; set; }
}
