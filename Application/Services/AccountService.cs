using Application.DTOs;
using Application.Exceptions;
using Application.Interfaces;
using Domain.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Application.Services
{
    public class AccountService : IAccountService
    {
        private readonly IAccountRepository _accountRepository;
        private readonly IMemoryCache _cache;
        private readonly ILogger<AccountService> _logger;

        private static readonly TimeSpan MovementCacheTtl = TimeSpan.FromMinutes(5);
        private static readonly HashSet<string> SupportedCurrencies = new() { "BOB", "USD" };

        public AccountService(
            IAccountRepository accountRepository,
            IMemoryCache cache,
            ILogger<AccountService> logger)
        {
            _accountRepository = accountRepository;
            _cache = cache;
            _logger = logger;
        }

        public async Task<AccountResponse> CreateAccountAsync(CreateAccountRequest request)
        {
            if (!SupportedCurrencies.Contains(request.Currency.ToUpper()))
                throw new UnsupportedCurrencyException(request.Currency);

            if (request.InitialBalance < 0)
                throw new InvalidBalanceException("El saldo inicial debe ser mayor o igual a 0.");

            bool customerExists = await _accountRepository.CustomerExistsAsync(request.CustomerId);
            if (!customerExists)
                throw new CustomerNotFoundException(request.CustomerId);

            var account = new Account
            {
                AccountId      = Guid.NewGuid(),
                CustomerId     = request.CustomerId,
                AccountNumber  = GenerateAccountNumber(),
                Currency       = request.Currency.ToUpper(),
                OpeningBalance = request.InitialBalance,
                Balance        = request.InitialBalance,
                AccountStatus  = AccountStatus.Active,
                Version        = 1
            };

            await _accountRepository.AddAsync(account);
            await _accountRepository.SaveChangesAsync();

            _logger.LogInformation(
                "Cuenta creada. AccountId={AccountId} AccountNumber={AccountNumber} Currency={Currency} CustomerId={CustomerId}",
                account.AccountId, account.AccountNumber, account.Currency, account.CustomerId);

            return MapToAccountResponse(account);
        }

        public async Task<AccountResponse> GetByIdAsync(Guid id)
        {
            var account = await _accountRepository.GetByIdAsync(id);
            if (account is null)
                throw new AccountNotFoundException(id);
            return MapToAccountResponse(account);
        }

        public async Task<MovementResponse> DepositAsync(Guid accountId, DepositRequest request)
        {
            if (request.Amount <= 0)
                throw new InvalidAmountException("El monto del depósito debe ser mayor a 0.");

            var account = await _accountRepository.GetByIdAsync(accountId);
            if (account is null)
                throw new AccountNotFoundException(accountId);

            if (account.AccountStatus != AccountStatus.Active)
                throw new AccountNotActiveException(accountId, account.AccountStatus);

            var previousBalance = account.Balance;
            account.Balance    += request.Amount;
            account.Version++;

            var movement = new Movement
            {
                MovementId          = Guid.NewGuid(),
                AccountId           = accountId,
                MovementType        = MovementType.Deposit,
                Amount              = request.Amount,
                PreviousBalance     = previousBalance,
                NewBalance          = account.Balance,
                MovementDescription = request.Description ?? "Depósito",
                CreatedAt           = DateTime.UtcNow
            };

            await _accountRepository.AddMovementAsync(movement);
            await _accountRepository.SaveChangesAsync();

            InvalidateMovementCache(accountId);

            _logger.LogInformation(
                "Depósito realizado. AccountId={AccountId} Amount={Amount} PreviousBalance={PreviousBalance} NewBalance={NewBalance} MovementId={MovementId}",
                accountId, request.Amount, previousBalance, account.Balance, movement.MovementId);

            return MapToMovementResponse(movement);
        }

        public async Task<MovementResponse> WithdrawAsync(Guid accountId, WithdrawalRequest request)
        {
            if (request.Amount <= 0)
                throw new InvalidAmountException("El monto del retiro debe ser mayor a 0.");

            var account = await _accountRepository.GetByIdAsync(accountId);
            if (account is null)
                throw new AccountNotFoundException(accountId);

            if (account.AccountStatus != AccountStatus.Active)
                throw new AccountNotActiveException(accountId, account.AccountStatus);

            if (account.Balance < request.Amount)
                throw new InsufficientFundsException(accountId, account.Balance, request.Amount);

            var previousBalance = account.Balance;
            account.Balance    -= request.Amount;
            account.Version++;

            var movement = new Movement
            {
                MovementId          = Guid.NewGuid(),
                AccountId           = accountId,
                MovementType        = MovementType.Withdrawal,
                Amount              = request.Amount,
                PreviousBalance     = previousBalance,
                NewBalance          = account.Balance,
                MovementDescription = request.Description ?? "Retiro",
                CreatedAt           = DateTime.UtcNow
            };

            await _accountRepository.AddMovementAsync(movement);
            await _accountRepository.SaveChangesAsync();

            InvalidateMovementCache(accountId);

            _logger.LogInformation(
                "Retiro realizado. AccountId={AccountId} Amount={Amount} PreviousBalance={PreviousBalance} NewBalance={NewBalance} MovementId={MovementId}",
                accountId, request.Amount, previousBalance, account.Balance, movement.MovementId);

            return MapToMovementResponse(movement);
        }

        public async Task<PagedResponse<MovementResponse>> GetMovementsAsync(Guid accountId, int page, int pageSize)
        {
            if (!await _accountRepository.AccountExistsAsync(accountId))
                throw new AccountNotFoundException(accountId);

            var cacheKey = BuildMovementCacheKey(accountId, page, pageSize);

            if (_cache.TryGetValue(cacheKey, out PagedResponse<MovementResponse>? cached) && cached is not null)
            {
                _logger.LogDebug(
                    "Historial de movimientos servido desde caché. AccountId={AccountId} Page={Page} PageSize={PageSize}",
                    accountId, page, pageSize);
                return cached;
            }

            var (items, totalCount) = await _accountRepository.GetMovementsPagedAsync(accountId, page, pageSize);

            var response = new PagedResponse<MovementResponse>
            {
                Page      = page,
                PageSize  = pageSize,
                TotalCount = totalCount,
                Items     = items.Select(MapToMovementResponse).ToList()
            };

            _cache.Set(cacheKey, response, MovementCacheTtl);

            _logger.LogDebug(
                "Historial de movimientos cargado de BD. AccountId={AccountId} Page={Page} PageSize={PageSize} TotalCount={TotalCount}",
                accountId, page, pageSize, totalCount);

            return response;
        }

        // ── Cache helpers ────────────────────────────────────────────────────────

        // Generation-based cache invalidation: incrementing the generation makes all
        // cached pages for this account unreachable without needing to enumerate keys.
        private string BuildMovementCacheKey(Guid accountId, int page, int pageSize)
        {
            var gen = _cache.Get<long>($"mov_gen_{accountId}");
            return $"mov_{accountId}_{gen}_{page}_{pageSize}";
        }

        private void InvalidateMovementCache(Guid accountId)
        {
            var gen = _cache.Get<long>($"mov_gen_{accountId}");
            _cache.Set($"mov_gen_{accountId}", gen + 1);
        }

        // ── Mappers ──────────────────────────────────────────────────────────────

        private static AccountResponse MapToAccountResponse(Account account) => new()
        {
            AccountId      = account.AccountId,
            CustomerId     = account.CustomerId,
            AccountNumber  = account.AccountNumber,
            Currency       = account.Currency,
            OpeningBalance = account.OpeningBalance,
            Balance        = account.Balance,
            AccountStatus  = account.AccountStatus
        };

        private static MovementResponse MapToMovementResponse(Movement movement) => new()
        {
            MovementId      = movement.MovementId,
            AccountId       = movement.AccountId,
            MovementType    = movement.MovementType,
            Amount          = movement.Amount,
            PreviousBalance = movement.PreviousBalance,
            NewBalance      = movement.NewBalance,
            Description     = movement.MovementDescription,
            CreatedAt       = movement.CreatedAt
        };

        // ── Helpers ──────────────────────────────────────────────────────────────

        private static string GenerateAccountNumber()
        {
            var datePart   = DateTime.UtcNow.ToString("yyyyMMdd");
            var randomPart = Guid.NewGuid().ToString("N")[..8].ToUpper();
            return $"ACC-{datePart}-{randomPart}";
        }
    }
}
