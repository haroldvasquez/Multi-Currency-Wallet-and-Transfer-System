using Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[Route("api/reportes")]
[ApiController]
public class ReportController : ControllerBase
{
    private readonly IReportService _reportService;

    public ReportController(IReportService reportService)
    {
        _reportService = reportService;
    }

    /// <summary>
    /// UC8 — Reporte consolidado de saldo para un cliente en el período indicado.
    /// Todos los saldos se convierten a la moneda solicitada (BOB o USD) usando el tipo de cambio vigente.
    /// </summary>
    /// <param name="customerId">Identificador del cliente.</param>
    /// <param name="startDate">Fecha de inicio del período (ej. 2024-01-01).</param>
    /// <param name="endDate">Fecha de fin del período, inclusive (ej. 2024-12-31).</param>
    /// <param name="currency">Moneda de consolidación: BOB o USD.</param>
    [HttpGet("balance-consolidado")]
    public async Task<IActionResult> GetConsolidatedBalance(
        [FromQuery] Guid customerId,
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate,
        [FromQuery] string currency)
    {
        if (startDate.Date > endDate.Date)
            return BadRequest(new { error = "La fecha de inicio no puede ser posterior a la fecha de fin." });

        var result = await _reportService.GetConsolidatedBalanceAsync(
            customerId, startDate, endDate, currency);

        return Ok(result);
    }
}
