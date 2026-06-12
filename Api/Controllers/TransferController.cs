using Application.DTOs;
using Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[Route("api/transfers")]
[ApiController]
public class TransferController : ControllerBase
{
    private readonly ITransferService _transferService;

    public TransferController(ITransferService transferService)
    {
        _transferService = transferService;
    }

    /// <summary>UC5 — Transferencia entre cuentas.</summary>
    [HttpPost]
    public async Task<IActionResult> Transfer([FromBody] TransferRequest request)
    {
        var result = await _transferService.TransferAsync(request);
        return Ok(result);
    }
}
