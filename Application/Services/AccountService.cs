using Application.DTOs;
using Application.Exceptions;
using Application.Interfaces;
using Domain.Models;

namespace Application.Services
{
    public class AccountService : IAccountService
    {
        private readonly IAccountRepository _accountRepository;

        private static readonly HashSet<string> SupportedCurrencies = new() { "BOB", "USD" };

        public AccountService(IAccountRepository accountRepository)
        {
            _accountRepository = accountRepository;
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

            // EF Core tracks the balance update on `account` and the new movement;
            // SaveChangesAsync wraps both in a single implicit transaction.
            await _accountRepository.AddMovementAsync(movement);
            await _accountRepository.SaveChangesAsync();

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

            return MapToMovementResponse(movement);
        }

        // ── Mappers ─────────────────────────────────────────────────────────────

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
