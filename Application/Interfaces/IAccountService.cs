using Application.DTOs;

namespace Application.Interfaces
{
    public interface IAccountService
    {
        Task<AccountResponse> CreateAccountAsync(CreateAccountRequest request);
        Task<AccountResponse> GetByIdAsync(Guid id);
        Task<MovementResponse> DepositAsync(Guid accountId, DepositRequest request);
        Task<MovementResponse> WithdrawAsync(Guid accountId, WithdrawalRequest request);
        Task<PagedResponse<MovementResponse>> GetMovementsAsync(Guid accountId, int page, int pageSize);
    }
}
