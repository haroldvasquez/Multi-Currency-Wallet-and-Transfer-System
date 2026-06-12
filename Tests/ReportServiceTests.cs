using Application.DTOs;
using Application.Exceptions;
using Application.Interfaces;
using Application.Services;
using Domain.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Tests;

/// <summary>
/// UC8 — consolidated balance report: currency validation, conversion both ways,
/// period filtering, and multi-account aggregation.
/// </summary>
public class ReportServiceTests
{
    private readonly Mock<IReportRepository>    _reportRepo  = new();
    private readonly Mock<IExchangeRateService> _rateService = new();

    private ReportService BuildSut() =>
        new(_reportRepo.Object, _rateService.Object, NullLogger<ReportService>.Instance);

    private static readonly DateTime Start = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime End   = new DateTime(2024, 12, 31, 0, 0, 0, DateTimeKind.Utc);

    private static Account MakeAccount(Guid id, string currency, decimal balance) => new()
    {
        AccountId     = id,
        AccountNumber = $"ACC-{id.ToString("N")[..8]}",
        Currency      = currency,
        Balance       = balance,
        AccountStatus = AccountStatus.Active
    };

    private static Movement Credit(decimal amount, decimal prev) => new()
    {
        MovementId      = Guid.NewGuid(),
        Amount          = amount,
        PreviousBalance = prev,
        NewBalance      = prev + amount,
        MovementType    = MovementType.Deposit,
        CreatedAt       = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc)
    };

    private static Movement Debit(decimal amount, decimal prev) => new()
    {
        MovementId      = Guid.NewGuid(),
        Amount          = amount,
        PreviousBalance = prev,
        NewBalance      = prev - amount,
        MovementType    = MovementType.Withdrawal,
        CreatedAt       = new DateTime(2024, 6, 15, 0, 0, 0, DateTimeKind.Utc)
    };

    // ── Currency validation ──────────────────────────────────────────────────

    [Theory]
    [InlineData("EUR")]
    [InlineData("GBP")]
    [InlineData("")]
    public async Task GetConsolidatedBalance_UnsupportedCurrency_ThrowsUnsupportedCurrencyException(string currency)
    {
        var customerId = Guid.NewGuid();
        _reportRepo.Setup(r => r.CustomerExistsAsync(customerId)).ReturnsAsync(true);

        await Assert.ThrowsAsync<UnsupportedCurrencyException>(
            () => BuildSut().GetConsolidatedBalanceAsync(customerId, Start, End, currency));
    }

    [Fact]
    public async Task GetConsolidatedBalance_CustomerNotFound_ThrowsCustomerNotFoundException()
    {
        var customerId = Guid.NewGuid();
        _rateService.Setup(s => s.GetRateAsync("USD", "BOB")).ReturnsAsync(6.91m);
        _reportRepo.Setup(r => r.CustomerExistsAsync(customerId)).ReturnsAsync(false);

        await Assert.ThrowsAsync<CustomerNotFoundException>(
            () => BuildSut().GetConsolidatedBalanceAsync(customerId, Start, End, "BOB"));
    }

    // ── Same-currency report (no conversion needed) ──────────────────────────

    [Fact]
    public async Task GetConsolidatedBalance_BobAccountsInBob_SumIsDirectBalance()
    {
        var customerId = Guid.NewGuid();
        var acc1       = MakeAccount(Guid.NewGuid(), "BOB", 300m);
        var acc2       = MakeAccount(Guid.NewGuid(), "BOB", 700m);

        _rateService.Setup(s => s.GetRateAsync("USD", "BOB")).ReturnsAsync(6.91m);
        _reportRepo.Setup(r => r.CustomerExistsAsync(customerId)).ReturnsAsync(true);
        _reportRepo.Setup(r => r.GetAccountsByCustomerIdAsync(customerId))
            .ReturnsAsync([acc1, acc2]);
        _reportRepo.Setup(r => r.GetMovementsByAccountAndDateRangeAsync(
                It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync([]);

        var result = await BuildSut().GetConsolidatedBalanceAsync(customerId, Start, End, "BOB");

        Assert.Equal("BOB", result.ReportCurrency);
        Assert.Equal(1000m, result.ConsolidatedBalance);
        Assert.Equal(2, result.Accounts.Count);
    }

    // ── Cross-currency conversion: USD account reported in BOB ───────────────

    [Fact]
    public async Task GetConsolidatedBalance_UsdAccountInBob_ConvertsWithExchangeRate()
    {
        var customerId = Guid.NewGuid();
        var usdAccount = MakeAccount(Guid.NewGuid(), "USD", 100m);

        // 1 USD = 6.91 BOB
        _rateService.Setup(s => s.GetRateAsync("USD", "BOB")).ReturnsAsync(6.91m);
        _reportRepo.Setup(r => r.CustomerExistsAsync(customerId)).ReturnsAsync(true);
        _reportRepo.Setup(r => r.GetAccountsByCustomerIdAsync(customerId))
            .ReturnsAsync([usdAccount]);
        _reportRepo.Setup(r => r.GetMovementsByAccountAndDateRangeAsync(
                It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync([]);

        var result = await BuildSut().GetConsolidatedBalanceAsync(customerId, Start, End, "BOB");

        // 100 USD × 6.91 = 691 BOB
        Assert.Equal(691m, result.ConsolidatedBalance);
        Assert.Equal(6.91m, result.ExchangeRateUsed);
        Assert.Equal(691m, result.Accounts[0].CurrentBalanceConverted);
    }

    // ── Cross-currency conversion: BOB account reported in USD ───────────────

    [Fact]
    public async Task GetConsolidatedBalance_BobAccountInUsd_ConvertsWithInverseRate()
    {
        var customerId = Guid.NewGuid();
        var bobAccount = MakeAccount(Guid.NewGuid(), "BOB", 691m);

        // 1 USD = 6.91 BOB  →  1 BOB = 1/6.91 USD
        _rateService.Setup(s => s.GetRateAsync("USD", "BOB")).ReturnsAsync(6.91m);
        _reportRepo.Setup(r => r.CustomerExistsAsync(customerId)).ReturnsAsync(true);
        _reportRepo.Setup(r => r.GetAccountsByCustomerIdAsync(customerId))
            .ReturnsAsync([bobAccount]);
        _reportRepo.Setup(r => r.GetMovementsByAccountAndDateRangeAsync(
                It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync([]);

        var result = await BuildSut().GetConsolidatedBalanceAsync(customerId, Start, End, "USD");

        // 691 / 6.91 = 100 USD (rounded to 2 decimals)
        Assert.Equal(Math.Round(691m / 6.91m, 2), result.ConsolidatedBalance);
    }

    // ── Mixed-currency accounts consolidated ─────────────────────────────────

    [Fact]
    public async Task GetConsolidatedBalance_MixedAccounts_SumsAllInTargetCurrency()
    {
        var customerId = Guid.NewGuid();
        var bobAccount = MakeAccount(Guid.NewGuid(), "BOB", 100m);
        var usdAccount = MakeAccount(Guid.NewGuid(), "USD", 100m);

        // 1 USD = 7.00 BOB  (round number for easy assertions)
        _rateService.Setup(s => s.GetRateAsync("USD", "BOB")).ReturnsAsync(7.00m);
        _reportRepo.Setup(r => r.CustomerExistsAsync(customerId)).ReturnsAsync(true);
        _reportRepo.Setup(r => r.GetAccountsByCustomerIdAsync(customerId))
            .ReturnsAsync([bobAccount, usdAccount]);
        _reportRepo.Setup(r => r.GetMovementsByAccountAndDateRangeAsync(
                It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync([]);

        var result = await BuildSut().GetConsolidatedBalanceAsync(customerId, Start, End, "BOB");

        // 100 BOB + (100 USD × 7) = 800 BOB
        Assert.Equal(800m, result.ConsolidatedBalance);
    }

    // ── Period movement aggregation ───────────────────────────────────────────

    [Fact]
    public async Task GetConsolidatedBalance_WithMovements_CorrectlyAggregatesCreditsAndDebits()
    {
        var customerId = Guid.NewGuid();
        var accountId  = Guid.NewGuid();
        var account    = MakeAccount(accountId, "BOB", 350m);

        _rateService.Setup(s => s.GetRateAsync("USD", "BOB")).ReturnsAsync(6.91m);
        _reportRepo.Setup(r => r.CustomerExistsAsync(customerId)).ReturnsAsync(true);
        _reportRepo.Setup(r => r.GetAccountsByCustomerIdAsync(customerId))
            .ReturnsAsync([account]);
        _reportRepo.Setup(r => r.GetMovementsByAccountAndDateRangeAsync(
                accountId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync([
                Credit(500m, 0m),    // deposit: +500
                Debit(150m, 500m)    // withdrawal: -150
            ]);

        var result = await BuildSut().GetConsolidatedBalanceAsync(customerId, Start, End, "BOB");

        var detail = result.Accounts[0];
        Assert.Equal(2,    detail.PeriodMovementsCount);
        Assert.Equal(500m, detail.PeriodTotalCredits);
        Assert.Equal(150m, detail.PeriodTotalDebits);
        Assert.Equal(350m, detail.PeriodNetChange);  // 500 - 150
    }

    // ── Report metadata ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetConsolidatedBalance_ResponseContainsExpectedMetadata()
    {
        var customerId = Guid.NewGuid();
        _rateService.Setup(s => s.GetRateAsync("USD", "BOB")).ReturnsAsync(6.91m);
        _reportRepo.Setup(r => r.CustomerExistsAsync(customerId)).ReturnsAsync(true);
        _reportRepo.Setup(r => r.GetAccountsByCustomerIdAsync(customerId)).ReturnsAsync([]);
        _reportRepo.Setup(r => r.GetMovementsByAccountAndDateRangeAsync(
                It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync([]);

        var result = await BuildSut().GetConsolidatedBalanceAsync(customerId, Start, End, "USD");

        Assert.Equal(customerId, result.CustomerId);
        Assert.Equal("USD",      result.ReportCurrency);
        Assert.Equal(6.91m,      result.ExchangeRateUsed);
        Assert.True(result.GeneratedAt > DateTime.UtcNow.AddSeconds(-5));
    }
}
