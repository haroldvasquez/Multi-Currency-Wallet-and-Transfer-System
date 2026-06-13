using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[Authorize]
[Route("api/tipo-cambio")]
[ApiController]
public class ExchangeRateController : ControllerBase
{
    private readonly IExchangeRateService _exchangeRateService;

    public ExchangeRateController(IExchangeRateService exchangeRateService)
    {
        _exchangeRateService = exchangeRateService;
    }

    /// <summary>UC7 — Tipo de cambio vigente USD/BOB desde HexaRate (caché 1h, fallback a tasa de respaldo).</summary>
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var rate = await _exchangeRateService.GetRateAsync("USD", "BOB");
        return Ok(new { from = "USD", to = "BOB", rate });
    }
}
