using Domain.Models;

namespace Application.Interfaces;

public interface ITransferRepository
{
    Task<Transfer?> GetByIdempotencyKeyAsync(Guid idempotencyKey);
    Task AddAsync(Transfer transfer);
}
