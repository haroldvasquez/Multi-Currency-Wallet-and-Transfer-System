using Application.Interfaces;
using Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories
{
    public class AccountRepository : IAccountRepository
    {
        private readonly IAppDbContext _context;

        public AccountRepository(IAppDbContext context)
        {
            _context = context;
        }

        public async Task<Account?> GetByIdAsync(Guid accountId) =>
            await _context.Accounts.FirstOrDefaultAsync(a => a.AccountId == accountId);

        public async Task<bool> AccountExistsAsync(Guid accountId) =>
            await _context.Accounts.AnyAsync(a => a.AccountId == accountId);

        public async Task<bool> CustomerExistsAsync(Guid customerId) =>
            await _context.Customers.AnyAsync(c => c.CustomerId == customerId);

        public async Task<bool> AccountNumberExistsAsync(string accountNumber) =>
            await _context.Accounts.AnyAsync(a => a.AccountNumber == accountNumber);

        public async Task AddAsync(Account account) =>
            await _context.Accounts.AddAsync(account);

        public async Task AddMovementAsync(Movement movement) =>
            await _context.Movements.AddAsync(movement);

        public async Task<(List<Movement> Items, int TotalCount)> GetMovementsPagedAsync(
            Guid accountId, int page, int pageSize)
        {
            var query = _context.Movements
                .Where(m => m.AccountId == accountId)
                .OrderByDescending(m => m.CreatedAt);

            var totalCount = await query.CountAsync();
            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task SaveChangesAsync() =>
            await _context.SaveChangesAsync();
    }
}
