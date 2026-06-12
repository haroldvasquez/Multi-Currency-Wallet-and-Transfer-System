using Application.Interfaces;
using Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public class TransferRepository : ITransferRepository
{
    private readonly IAppDbContext _context;

    public TransferRepository(IAppDbContext context)
    {
        _context = context;
    }

    public async Task<Transfer?> GetByIdempotencyKeyAsync(Guid idempotencyKey) =>
        await _context.Transfers.FirstOrDefaultAsync(t => t.IdempotencyKey == idempotencyKey);

    public async Task AddAsync(Transfer transfer) =>
        await _context.Transfers.AddAsync(transfer);
}
