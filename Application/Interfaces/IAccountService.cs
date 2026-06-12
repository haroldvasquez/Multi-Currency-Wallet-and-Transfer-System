using Application.DTOs;

namespace Application.Interfaces
{
    public interface IAccountService
    {
        Task<AccountResponse> CreateAccountAsync(CreateAccountRequest request);
        Task<AccountResponse> GetByIdAsync(Guid id);
        Task<MovementResponse> DepositAsync(Guid accountId, DepositRequest request);
    }
}
