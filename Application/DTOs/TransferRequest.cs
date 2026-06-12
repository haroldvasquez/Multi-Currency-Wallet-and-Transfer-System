namespace Application.DTOs;

public class TransferRequest
{
    public Guid    SourceAccountId { get; set; }
    public Guid    TargetAccountId { get; set; }
    public decimal Amount          { get; set; }
    public Guid    IdempotencyKey  { get; set; }
    public string? Description     { get; set; }
}
