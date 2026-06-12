using Application.DTOs;
using Application.Exceptions;
using Application.Interfaces;
using Application.Services;
using Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Update;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Tests;

/// <summary>
/// UC5 — transfer atomicity, idempotency, concurrency, cross-currency conversion.
/// </summary>
public class TransferServiceTests
{
    private readonly Mock<IAppDbContext>        _ctx          = new();
    private readonly Mock<IAccountRepository>   _accountRepo  = new();
    private readonly Mock<ITransferRepository>  _transferRepo = new();
    private readonly Mock<IExchangeRateService> _rateService  = new();
    private readonly IMemoryCache               _cache        = new MemoryCache(new MemoryCacheOptions());

    private TransferService BuildSut() => new(
        _ctx.Object,
        _accountRepo.Object,
        _transferRepo.Object,
        _rateService.Object,
        _cache,
        NullLogger<TransferService>.Instance);

    // Configures ExecuteInTransactionAsync to simply invoke the provided action.
    private void SetupTransactionToSucceed()
    {
        _ctx.Setup(c => c.ExecuteInTransactionAsync(It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>()))
            .Returns((Func<Task> action, CancellationToken _) => action());

        _ctx.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
    }

    private static Account MakeAccount(Guid id, string currency, decimal balance,
        string status = AccountStatus.Active) => new()
    {
        AccountId     = id,
        AccountNumber = $"ACC-{id.ToString("N")[..8]}",
        Currency      = currency,
        Balance       = balance,
        AccountStatus = status,
        Version       = 1
    };

    // ── Idempotency ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Transfer_DuplicateIdempotencyKey_ThrowsDuplicateTransferException()
    {
        var idempotencyKey = Guid.NewGuid();
        _transferRepo
            .Setup(r => r.GetByIdempotencyKeyAsync(idempotencyKey))
            .ReturnsAsync(new Transfer
            {
                TransferId     = Guid.NewGuid(),
                IdempotencyKey = idempotencyKey,
                TransferStatus = TransferStatus.Completed
            });

        var request = new TransferRequest
        {
            SourceAccountId = Guid.NewGuid(),
            TargetAccountId = Guid.NewGuid(),
            Amount          = 100m,
            IdempotencyKey  = idempotencyKey
        };

        await Assert.ThrowsAsync<DuplicateTransferException>(() => BuildSut().TransferAsync(request));
    }

    // ── Same-account guard ───────────────────────────────────────────────────

    [Fact]
    public async Task Transfer_SameSourceAndTarget_ThrowsSameAccountTransferException()
    {
        _transferRepo
            .Setup(r => r.GetByIdempotencyKeyAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Transfer?)null);

        var accountId = Guid.NewGuid();
        var request = new TransferRequest
        {
            SourceAccountId = accountId,
            TargetAccountId = accountId,
            Amount          = 100m,
            IdempotencyKey  = Guid.NewGuid()
        };

        await Assert.ThrowsAsync<SameAccountTransferException>(() => BuildSut().TransferAsync(request));
    }

    // ── Insufficient funds ───────────────────────────────────────────────────

    [Fact]
    public async Task Transfer_InsufficientSourceBalance_ThrowsInsufficientFundsException()
    {
        var sourceId = Guid.NewGuid();
        var targetId = Guid.NewGuid();

        _transferRepo
            .Setup(r => r.GetByIdempotencyKeyAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Transfer?)null);

        _accountRepo.Setup(r => r.GetByIdAsync(sourceId))
            .ReturnsAsync(MakeAccount(sourceId, "BOB", 50m));

        var request = new TransferRequest
        {
            SourceAccountId = sourceId,
            TargetAccountId = targetId,
            Amount          = 200m,
            IdempotencyKey  = Guid.NewGuid()
        };

        await Assert.ThrowsAsync<InsufficientFundsException>(() => BuildSut().TransferAsync(request));
    }

    // ── Source account inactive ──────────────────────────────────────────────

    [Fact]
    public async Task Transfer_InactiveSourceAccount_ThrowsAccountNotActiveException()
    {
        var sourceId = Guid.NewGuid();
        var targetId = Guid.NewGuid();

        _transferRepo
            .Setup(r => r.GetByIdempotencyKeyAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Transfer?)null);

        _accountRepo.Setup(r => r.GetByIdAsync(sourceId))
            .ReturnsAsync(MakeAccount(sourceId, "USD", 1000m, AccountStatus.Blocked));

        var request = new TransferRequest
        {
            SourceAccountId = sourceId,
            TargetAccountId = targetId,
            Amount          = 100m,
            IdempotencyKey  = Guid.NewGuid()
        };

        await Assert.ThrowsAsync<AccountNotActiveException>(() => BuildSut().TransferAsync(request));
    }

    // ── Concurrency conflict ─────────────────────────────────────────────────

    [Fact]
    public async Task Transfer_DbConcurrencyConflict_ThrowsConcurrencyConflictException()
    {
        var sourceId = Guid.NewGuid();
        var targetId = Guid.NewGuid();

        _transferRepo
            .Setup(r => r.GetByIdempotencyKeyAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Transfer?)null);
        _accountRepo.Setup(r => r.GetByIdAsync(sourceId))
            .ReturnsAsync(MakeAccount(sourceId, "BOB", 1000m));
        _accountRepo.Setup(r => r.GetByIdAsync(targetId))
            .ReturnsAsync(MakeAccount(targetId, "BOB", 0m));
        _accountRepo.Setup(r => r.AddMovementAsync(It.IsAny<Movement>())).Returns(Task.CompletedTask);
        _transferRepo.Setup(r => r.AddAsync(It.IsAny<Transfer>())).Returns(Task.CompletedTask);

        // Simulate the DB throwing a concurrency exception during the transaction commit.
        _ctx.Setup(c => c.ExecuteInTransactionAsync(It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateConcurrencyException("conflict", Array.Empty<IUpdateEntry>()));

        var request = new TransferRequest
        {
            SourceAccountId = sourceId,
            TargetAccountId = targetId,
            Amount          = 100m,
            IdempotencyKey  = Guid.NewGuid()
        };

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => BuildSut().TransferAsync(request));
    }

    // ── Same-currency transfer (no exchange rate) ────────────────────────────

    [Fact]
    public async Task Transfer_SameCurrency_DoesNotCallExchangeRateService()
    {
        var sourceId = Guid.NewGuid();
        var targetId = Guid.NewGuid();

        _transferRepo
            .Setup(r => r.GetByIdempotencyKeyAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Transfer?)null);
        _accountRepo.Setup(r => r.GetByIdAsync(sourceId))
            .ReturnsAsync(MakeAccount(sourceId, "USD", 500m));
        _accountRepo.Setup(r => r.GetByIdAsync(targetId))
            .ReturnsAsync(MakeAccount(targetId, "USD", 0m));
        _accountRepo.Setup(r => r.AddMovementAsync(It.IsAny<Movement>())).Returns(Task.CompletedTask);
        _transferRepo.Setup(r => r.AddAsync(It.IsAny<Transfer>())).Returns(Task.CompletedTask);
        SetupTransactionToSucceed();

        var request = new TransferRequest
        {
            SourceAccountId = sourceId,
            TargetAccountId = targetId,
            Amount          = 100m,
            IdempotencyKey  = Guid.NewGuid()
        };

        var result = await BuildSut().TransferAsync(request);

        Assert.Null(result.ExchangeRate);
        Assert.Null(result.ConvertedAmount);
        _rateService.Verify(s => s.GetRateAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    // ── Cross-currency transfer (exchange rate applied) ──────────────────────

    [Fact]
    public async Task Transfer_CrossCurrency_AppliesExchangeRateAndRecordsConvertedAmount()
    {
        var sourceId = Guid.NewGuid();
        var targetId = Guid.NewGuid();

        _transferRepo
            .Setup(r => r.GetByIdempotencyKeyAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Transfer?)null);
        _accountRepo.Setup(r => r.GetByIdAsync(sourceId))
            .ReturnsAsync(MakeAccount(sourceId, "BOB", 1000m));
        _accountRepo.Setup(r => r.GetByIdAsync(targetId))
            .ReturnsAsync(MakeAccount(targetId, "USD", 0m));
        _accountRepo.Setup(r => r.AddMovementAsync(It.IsAny<Movement>())).Returns(Task.CompletedTask);
        _transferRepo.Setup(r => r.AddAsync(It.IsAny<Transfer>())).Returns(Task.CompletedTask);

        // 1 BOB = 0.1447 USD (approx. inverse of 6.91)
        _rateService.Setup(s => s.GetRateAsync("BOB", "USD")).ReturnsAsync(0.1447m);

        SetupTransactionToSucceed();

        var request = new TransferRequest
        {
            SourceAccountId = sourceId,
            TargetAccountId = targetId,
            Amount          = 691m,
            IdempotencyKey  = Guid.NewGuid()
        };

        var result = await BuildSut().TransferAsync(request);

        Assert.Equal(0.1447m, result.ExchangeRate);
        // 691 × 0.1447 = 99.99 (rounded to 2 decimals)
        Assert.Equal(Math.Round(691m * 0.1447m, 2), result.ConvertedAmount);
        _rateService.Verify(s => s.GetRateAsync("BOB", "USD"), Times.Once);
    }

    // ── Source account not found ─────────────────────────────────────────────

    [Fact]
    public async Task Transfer_SourceAccountNotFound_ThrowsAccountNotFoundException()
    {
        var sourceId = Guid.NewGuid();

        _transferRepo
            .Setup(r => r.GetByIdempotencyKeyAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Transfer?)null);
        _accountRepo.Setup(r => r.GetByIdAsync(sourceId))
            .ReturnsAsync((Account?)null);

        var request = new TransferRequest
        {
            SourceAccountId = sourceId,
            TargetAccountId = Guid.NewGuid(),
            Amount          = 100m,
            IdempotencyKey  = Guid.NewGuid()
        };

        await Assert.ThrowsAsync<AccountNotFoundException>(() => BuildSut().TransferAsync(request));
    }
}
