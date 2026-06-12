using Domain.Models;

namespace Application.Interfaces
{
    public interface IAccountRepository
    {
        Task<Account?> GetByIdAsync(Guid accountId);
        Task<bool> CustomerExistsAsync(Guid customerId);
        Task<bool> AccountNumberExistsAsync(string accountNumber);
        Task AddAsync(Account account);
        Task AddMovementAsync(Movement movement);
        Task SaveChangesAsync();
    }
}
