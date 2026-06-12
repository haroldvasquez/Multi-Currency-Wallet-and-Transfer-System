namespace Application.Interfaces;

public interface IExchangeRateService
{
    /// <summary>
    /// Returns units of <paramref name="toCurrency"/> per one unit of <paramref name="fromCurrency"/>.
    /// Supports BOB and USD only.
    /// </summary>
    Task<decimal> GetRateAsync(string fromCurrency, string toCurrency);
}
