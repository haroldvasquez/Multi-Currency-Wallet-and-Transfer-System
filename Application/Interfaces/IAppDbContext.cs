using Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace Application.Interfaces
{
    public interface IAppDbContext
    {
        DbSet<Account> Accounts { get; set; }
        DbSet<Customer> Customers { get; set; }
        DbSet<Movement> Movements { get; set; }
        DbSet<Transfer> Transfers { get; set; }
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

        /// <summary>Wraps <paramref name="action"/> in an explicit database transaction.</summary>
        Task ExecuteInTransactionAsync(Func<Task> action, CancellationToken cancellationToken = default);
    }
}
