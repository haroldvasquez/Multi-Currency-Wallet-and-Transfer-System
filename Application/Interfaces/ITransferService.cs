using Application.DTOs;

namespace Application.Interfaces;

public interface ITransferService
{
    Task<TransferResponse> TransferAsync(TransferRequest request);
}
