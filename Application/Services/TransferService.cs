using Application.DTOs;
using Application.Exceptions;
using Application.Interfaces;
using Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Application.Services;

public class TransferService : ITransferService
{
    private readonly IAppDbContext _context;
    private readonly IAccountRepository _accountRepository;
    private readonly ITransferRepository _transferRepository;
    private readonly IExchangeRateService _exchangeRateService;
    private readonly IMemoryCache _cache;
    private readonly ILogger<TransferService> _logger;

    public TransferService(
        IAppDbContext context,
        IAccountRepository accountRepository,
        ITransferRepository transferRepository,
        IExchangeRateService exchangeRateService,
        IMemoryCache cache,
        ILogger<TransferService> logger)
    {
        _context = context;
        _accountRepository = accountRepository;
        _transferRepository = transferRepository;
        _exchangeRateService = exchangeRateService;
        _cache = cache;
        _logger = logger;
    }

    public async Task<TransferResponse> TransferAsync(TransferRequest request)
    {
        // Idempotency guard: reject duplicate keys before doing any work
        var existing = await _transferRepository.GetByIdempotencyKeyAsync(request.IdempotencyKey);
        if (existing is not null)
            throw new DuplicateTransferException(request.IdempotencyKey);

        if (request.SourceAccountId == request.TargetAccountId)
            throw new SameAccountTransferException();

        if (request.Amount <= 0)
            throw new InvalidAmountException("El monto de la transferencia debe ser mayor a 0.");

        // Validate source account
        var source = await _accountRepository.GetByIdAsync(request.SourceAccountId);
        if (source is null)
            throw new AccountNotFoundException(request.SourceAccountId);

        if (source.AccountStatus != AccountStatus.Active)
            throw new AccountNotActiveException(request.SourceAccountId, source.AccountStatus);

        if (source.Balance < request.Amount)
            throw new InsufficientFundsException(request.SourceAccountId, source.Balance, request.Amount);

        // Validate target account
        var target = await _accountRepository.GetByIdAsync(request.TargetAccountId);
        if (target is null)
            throw new AccountNotFoundException(request.TargetAccountId);

        if (target.AccountStatus != AccountStatus.Active)
            throw new AccountNotActiveException(request.TargetAccountId, target.AccountStatus);

        // Cross-currency: look up exchange rate and compute converted amount
        decimal? exchangeRate   = null;
        decimal? convertedAmount = null;
        decimal  creditAmount   = request.Amount;

        if (source.Currency != target.Currency)
        {
            exchangeRate   = await _exchangeRateService.GetRateAsync(source.Currency, target.Currency);
            creditAmount   = Math.Round(request.Amount * exchangeRate.Value, 2);
            convertedAmount = creditAmount;
        }

        // Execute debit + credit + records atomically in a DB transaction.
        // Optimistic concurrency is enforced via the `version` column: if another
        // request modifies either account between our read and our save, EF throws
        // DbUpdateConcurrencyException, which we map to 409.
        Transfer transfer = null!;
        try
        {
            await _context.ExecuteInTransactionAsync(async () =>
            {
                var sourcePrev = source.Balance;
                var targetPrev = target.Balance;

                source.Balance -= request.Amount;
                source.Version++;

                target.Balance += creditAmount;
                target.Version++;

                transfer = new Transfer
                {
                    TransferId      = Guid.NewGuid(),
                    SourceAccountId = request.SourceAccountId,
                    TargetAccountId = request.TargetAccountId,
                    Amount          = request.Amount,
                    ExchangeRate    = exchangeRate,
                    ConvertedAmount = convertedAmount,
                    IdempotencyKey  = request.IdempotencyKey,
                    TransferStatus  = Domain.Models.TransferStatus.Completed,
                    CreatedAt       = DateTime.UtcNow
                };

                await _transferRepository.AddAsync(transfer);

                await _accountRepository.AddMovementAsync(new Movement
                {
                    MovementId          = Guid.NewGuid(),
                    AccountId           = request.SourceAccountId,
                    TransferId          = transfer.TransferId,
                    MovementType        = MovementType.Transfer,
                    Amount              = request.Amount,
                    PreviousBalance     = sourcePrev,
                    NewBalance          = source.Balance,
                    MovementDescription = request.Description ?? $"Transferencia a cuenta {target.AccountNumber}",
                    CreatedAt           = DateTime.UtcNow
                });

                await _accountRepository.AddMovementAsync(new Movement
                {
                    MovementId          = Guid.NewGuid(),
                    AccountId           = request.TargetAccountId,
                    TransferId          = transfer.TransferId,
                    MovementType        = MovementType.Transfer,
                    Amount              = creditAmount,
                    PreviousBalance     = targetPrev,
                    NewBalance          = target.Balance,
                    MovementDescription = request.Description ?? $"Transferencia desde cuenta {source.AccountNumber}",
                    CreatedAt           = DateTime.UtcNow
                });

                await _context.SaveChangesAsync();
            });
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex,
                "Conflicto de concurrencia. SourceAccountId={SourceAccountId} TargetAccountId={TargetAccountId}",
                request.SourceAccountId, request.TargetAccountId);
            throw new ConcurrencyConflictException();
        }

        // Invalidate cached movement history for both accounts
        InvalidateMovementCache(request.SourceAccountId);
        InvalidateMovementCache(request.TargetAccountId);

        _logger.LogInformation(
            "Transferencia completada. TransferId={TransferId} Source={SourceAccountId} Target={TargetAccountId} Amount={Amount} SourceCurrency={SourceCurrency} ExchangeRate={ExchangeRate}",
            transfer.TransferId, request.SourceAccountId, request.TargetAccountId,
            request.Amount, source.Currency, exchangeRate);

        return new TransferResponse
        {
            TransferId      = transfer.TransferId,
            SourceAccountId = transfer.SourceAccountId,
            TargetAccountId = transfer.TargetAccountId,
            Amount          = transfer.Amount,
            ExchangeRate    = transfer.ExchangeRate,
            ConvertedAmount = transfer.ConvertedAmount,
            TransferStatus  = transfer.TransferStatus,
            CreatedAt       = transfer.CreatedAt,
            Description     = request.Description
        };
    }

    private void InvalidateMovementCache(Guid accountId)
    {
        var gen = _cache.Get<long>($"mov_gen_{accountId}");
        _cache.Set($"mov_gen_{accountId}", gen + 1);
    }
}
