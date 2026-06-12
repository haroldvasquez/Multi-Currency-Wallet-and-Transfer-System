namespace Application.DTOs;

public class TransferResponse
{
    public Guid     TransferId      { get; set; }
    public Guid     SourceAccountId { get; set; }
    public Guid     TargetAccountId { get; set; }
    public decimal  Amount          { get; set; }
    public decimal? ExchangeRate    { get; set; }
    public decimal? ConvertedAmount { get; set; }
    public string   TransferStatus  { get; set; } = null!;
    public DateTime CreatedAt       { get; set; }
    public string?  Description     { get; set; }
}
