using Application.DTOs;
using Application.Exceptions;
using Application.Interfaces;
using Application.Services;
using Domain.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Tests;

/// <summary>
/// UC1 — currency validation, UC3 — deposit, UC4 — withdrawal (insufficient balance).
/// </summary>
public class AccountServiceTests
{
    private readonly Mock<IAccountRepository> _repo = new();
    private readonly IMemoryCache             _cache = new MemoryCache(new MemoryCacheOptions());

    private AccountService BuildSut() =>
        new(_repo.Object, _cache, NullLogger<AccountService>.Instance);

    // ── UC1: Currency validation ─────────────────────────────────────────────

    [Theory]
    [InlineData("EUR")]
    [InlineData("GBP")]
    [InlineData("ARS")]
    public async Task CreateAccount_UnsupportedCurrency_ThrowsUnsupportedCurrencyException(string currency)
    {
        var request = new CreateAccountRequest
        {
            CustomerId     = Guid.NewGuid(),
            Currency       = currency,
            InitialBalance = 0
        };

        await Assert.ThrowsAsync<UnsupportedCurrencyException>(
            () => BuildSut().CreateAccountAsync(request));
    }

    [Theory]
    [InlineData("BOB")]
    [InlineData("USD")]
    public async Task CreateAccount_SupportedCurrency_ReturnsAccountWithCorrectCurrencyAndBalance(string currency)
    {
        var customerId = Guid.NewGuid();
        _repo.Setup(r => r.CustomerExistsAsync(customerId)).ReturnsAsync(true);
        _repo.Setup(r => r.AddAsync(It.IsAny<Account>())).Returns(Task.CompletedTask);
        _repo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

        var request = new CreateAccountRequest
        {
            CustomerId     = customerId,
            Currency       = currency,
            InitialBalance = 500
        };

        var result = await BuildSut().CreateAccountAsync(request);

        Assert.Equal(currency, result.Currency);
        Assert.Equal(customerId, result.CustomerId);
        Assert.Equal(500, result.Balance);
        Assert.Equal(AccountStatus.Active, result.AccountStatus);
    }

    [Fact]
    public async Task CreateAccount_NegativeInitialBalance_ThrowsInvalidBalanceException()
    {
        var request = new CreateAccountRequest
        {
            CustomerId     = Guid.NewGuid(),
            Currency       = "BOB",
            InitialBalance = -1
        };

        await Assert.ThrowsAsync<InvalidBalanceException>(
            () => BuildSut().CreateAccountAsync(request));
    }

    [Fact]
    public async Task CreateAccount_CustomerNotFound_ThrowsCustomerNotFoundException()
    {
        var customerId = Guid.NewGuid();
        _repo.Setup(r => r.CustomerExistsAsync(customerId)).ReturnsAsync(false);

        var request = new CreateAccountRequest
        {
            CustomerId = customerId,
            Currency   = "BOB"
        };

        await Assert.ThrowsAsync<CustomerNotFoundException>(
            () => BuildSut().CreateAccountAsync(request));
    }

    // ── UC3: Deposit ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Deposit_ValidRequest_ReturnsMovementWithCorrectBalances()
    {
        var accountId = Guid.NewGuid();
        _repo.Setup(r => r.GetByIdAsync(accountId)).ReturnsAsync(new Account
        {
            AccountId     = accountId,
            AccountNumber = "ACC-TEST-001",
            Balance       = 100m,
            Currency      = "USD",
            AccountStatus = AccountStatus.Active
        });
        _repo.Setup(r => r.AddMovementAsync(It.IsAny<Movement>())).Returns(Task.CompletedTask);
        _repo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

        var result = await BuildSut().DepositAsync(accountId, new DepositRequest { Amount = 75m });

        Assert.Equal(75m,  result.Amount);
        Assert.Equal(100m, result.PreviousBalance);
        Assert.Equal(175m, result.NewBalance);
        Assert.Equal(MovementType.Deposit, result.MovementType);
    }

    [Fact]
    public async Task Deposit_AmountZero_ThrowsInvalidAmountException()
    {
        await Assert.ThrowsAsync<InvalidAmountException>(
            () => BuildSut().DepositAsync(Guid.NewGuid(), new DepositRequest { Amount = 0 }));
    }

    [Fact]
    public async Task Deposit_InactiveAccount_ThrowsAccountNotActiveException()
    {
        var accountId = Guid.NewGuid();
        _repo.Setup(r => r.GetByIdAsync(accountId)).ReturnsAsync(new Account
        {
            AccountId     = accountId,
            AccountNumber = "ACC-TEST-002",
            Balance       = 500m,
            Currency      = "BOB",
            AccountStatus = AccountStatus.Inactive
        });

        await Assert.ThrowsAsync<AccountNotActiveException>(
            () => BuildSut().DepositAsync(accountId, new DepositRequest { Amount = 100m }));
    }

    // ── UC4: Insufficient balance / withdrawal ────────────────────────────────

    [Fact]
    public async Task Withdraw_InsufficientBalance_ThrowsInsufficientFundsException()
    {
        var accountId = Guid.NewGuid();
        _repo.Setup(r => r.GetByIdAsync(accountId)).ReturnsAsync(new Account
        {
            AccountId     = accountId,
            AccountNumber = "ACC-TEST-003",
            Balance       = 50m,
            Currency      = "BOB",
            AccountStatus = AccountStatus.Active
        });

        await Assert.ThrowsAsync<InsufficientFundsException>(
            () => BuildSut().WithdrawAsync(accountId, new WithdrawalRequest { Amount = 100m }));
    }

    [Fact]
    public async Task Withdraw_ExactBalance_Succeeds()
    {
        var accountId = Guid.NewGuid();
        _repo.Setup(r => r.GetByIdAsync(accountId)).ReturnsAsync(new Account
        {
            AccountId     = accountId,
            AccountNumber = "ACC-TEST-004",
            Balance       = 200m,
            Currency      = "BOB",
            AccountStatus = AccountStatus.Active
        });
        _repo.Setup(r => r.AddMovementAsync(It.IsAny<Movement>())).Returns(Task.CompletedTask);
        _repo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

        var result = await BuildSut().WithdrawAsync(accountId, new WithdrawalRequest { Amount = 200m });

        Assert.Equal(200m, result.PreviousBalance);
        Assert.Equal(0m,   result.NewBalance);
    }

    [Fact]
    public async Task Withdraw_InactiveAccount_ThrowsAccountNotActiveException()
    {
        var accountId = Guid.NewGuid();
        _repo.Setup(r => r.GetByIdAsync(accountId)).ReturnsAsync(new Account
        {
            AccountId     = accountId,
            AccountNumber = "ACC-TEST-005",
            Balance       = 500m,
            Currency      = "BOB",
            AccountStatus = AccountStatus.Blocked
        });

        await Assert.ThrowsAsync<AccountNotActiveException>(
            () => BuildSut().WithdrawAsync(accountId, new WithdrawalRequest { Amount = 100m }));
    }

    [Fact]
    public async Task Withdraw_AmountZero_ThrowsInvalidAmountException()
    {
        await Assert.ThrowsAsync<InvalidAmountException>(
            () => BuildSut().WithdrawAsync(Guid.NewGuid(), new WithdrawalRequest { Amount = 0 }));
    }

    [Fact]
    public async Task Withdraw_ValidRequest_ReturnsMovementWithCorrectBalances()
    {
        var accountId = Guid.NewGuid();
        _repo.Setup(r => r.GetByIdAsync(accountId)).ReturnsAsync(new Account
        {
            AccountId     = accountId,
            AccountNumber = "ACC-TEST-006",
            Balance       = 300m,
            Currency      = "USD",
            AccountStatus = AccountStatus.Active
        });
        _repo.Setup(r => r.AddMovementAsync(It.IsAny<Movement>())).Returns(Task.CompletedTask);
        _repo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

        var result = await BuildSut().WithdrawAsync(accountId, new WithdrawalRequest { Amount = 80m });

        Assert.Equal(80m,  result.Amount);
        Assert.Equal(300m, result.PreviousBalance);
        Assert.Equal(220m, result.NewBalance);
        Assert.Equal(MovementType.Withdrawal, result.MovementType);
    }
}
