using Application.Interfaces;
using Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public class ReportRepository : IReportRepository
{
    private readonly IAppDbContext _context;

    public ReportRepository(IAppDbContext context)
    {
        _context = context;
    }

    public async Task<bool> CustomerExistsAsync(Guid customerId) =>
        await _context.Customers.AnyAsync(c => c.CustomerId == customerId);

    public async Task<List<Account>> GetAccountsByCustomerIdAsync(Guid customerId) =>
        await _context.Accounts
            .Where(a => a.CustomerId == customerId)
            .OrderBy(a => a.AccountNumber)
            .ToListAsync();

    public async Task<List<Movement>> GetMovementsByAccountAndDateRangeAsync(
        Guid accountId, DateTime from, DateTime toExclusive) =>
        await _context.Movements
            .Where(m => m.AccountId == accountId
                     && m.CreatedAt >= from
                     && m.CreatedAt <  toExclusive)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();
}
