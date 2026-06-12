using Application.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Infrastructure.Services;

/// <summary>
/// Fetches the USD/BOB exchange rate from the HexaRate external API.
/// Caches the result for <see cref="CacheTtl"/> and falls back to a hardcoded
/// rate if the external service is unavailable (timeout or error).
/// </summary>
public sealed class ExchangeRateService : IExchangeRateService
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<ExchangeRateService> _logger;

    // Fallback rate when the external API is unreachable
    private const decimal UsdToBobFallback = 6.91m;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(1);
    private const string BaseUrl = "https://hexarate.paikama.co/api/rates";

    public ExchangeRateService(
        HttpClient httpClient,
        IMemoryCache cache,
        ILogger<ExchangeRateService> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _logger = logger;
    }

    public async Task<decimal> GetRateAsync(string fromCurrency, string toCurrency)
    {
        if (fromCurrency == toCurrency) return 1m;

        var cacheKey = $"xrate_{fromCurrency}_{toCurrency}";
        if (_cache.TryGetValue(cacheKey, out decimal cached))
        {
            _logger.LogDebug("Tipo de cambio {From}/{To}={Rate} (desde caché).", fromCurrency, toCurrency, cached);
            return cached;
        }

        // The API is directional; for BOB→USD we query USD/BOB and invert.
        var (queryFrom, queryTo, invert) = ResolveQuery(fromCurrency, toCurrency);

        try
        {
            var url      = $"{BaseUrl}/{queryFrom}/{queryTo}/latest";
            var response = await _httpClient.GetFromJsonAsync<HexaRateResponse>(url);

            if (response?.Data?.Mid is not decimal rawRate || rawRate <= 0)
                throw new InvalidOperationException("La API de tipo de cambio devolvió una respuesta inválida.");

            var result = invert ? Math.Round(1m / rawRate, 8) : rawRate;
            _cache.Set(cacheKey, result, CacheTtl);

            _logger.LogInformation(
                "Tipo de cambio obtenido de API. {From}/{To}={Rate}.",
                fromCurrency, toCurrency, result);

            return result;
        }
        catch (Exception ex)
        {
            var fallback = invert
                ? Math.Round(1m / UsdToBobFallback, 8)
                : UsdToBobFallback;

            _logger.LogWarning(ex,
                "Error al consultar tipo de cambio {From}/{To}. Usando tasa de respaldo {Rate}.",
                fromCurrency, toCurrency, fallback);

            return fallback;
        }
    }

    /// <summary>
    /// Maps any supported pair to the canonical USD/BOB query direction plus
    /// an inversion flag so we avoid maintaining two API call paths.
    /// </summary>
    private static (string queryFrom, string queryTo, bool invert) ResolveQuery(
        string from, string to) =>
        (from, to) switch
        {
            ("BOB", "USD") => ("USD", "BOB", true),
            _              => (from, to, false)
        };

    // ── HexaRate response shape ──────────────────────────────────────────────

    private sealed class HexaRateResponse
    {
        public HexaRateData? Data { get; set; }
    }

    private sealed class HexaRateData
    {
        [JsonPropertyName("mid")]
        public decimal? Mid { get; set; }
    }
}
