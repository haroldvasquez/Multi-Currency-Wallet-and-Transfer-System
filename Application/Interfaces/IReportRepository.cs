using Domain.Models;

namespace Application.Interfaces;

public interface IReportRepository
{
    Task<bool> CustomerExistsAsync(Guid customerId);
    Task<List<Account>> GetAccountsByCustomerIdAsync(Guid customerId);
    Task<List<Movement>> GetMovementsByAccountAndDateRangeAsync(
        Guid accountId, DateTime from, DateTime toExclusive);
}
