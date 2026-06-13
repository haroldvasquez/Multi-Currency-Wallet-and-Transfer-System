using Application.DTOs;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[Authorize]
[Route("api/transfers")]
[ApiController]
public class TransferController : ControllerBase
{
    private readonly ITransferService _transferService;

    public TransferController(ITransferService transferService)
    {
        _transferService = transferService;
    }

    /// <summary>UC5 — Transferencia entre cuentas. Requiere el encabezado <c>Idempotency-Key: &lt;uuid&gt;</c>.</summary>
    [HttpPost]
    public async Task<IActionResult> Transfer([FromBody] TransferRequest request)
    {
        if (!Request.Headers.TryGetValue("Idempotency-Key", out var rawKey)
            || !Guid.TryParse(rawKey, out var idempotencyKey))
        {
            return BadRequest(new { error = "Se requiere el encabezado 'Idempotency-Key' con un UUID válido." });
        }

        request.IdempotencyKey = idempotencyKey;

        var result = await _transferService.TransferAsync(request);
        return Ok(result);
    }
}
