using Application.DTOs;

namespace Application.Interfaces;

public interface IReportService
{
    Task<ConsolidatedBalanceResponse> GetConsolidatedBalanceAsync(
        Guid customerId,
        DateTime startDate,
        DateTime endDate,
        string currency);
}
