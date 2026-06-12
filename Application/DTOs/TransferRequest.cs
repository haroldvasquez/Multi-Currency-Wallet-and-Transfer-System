using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Application.DTOs;

public class TransferRequest
{
    [Required]
    public Guid SourceAccountId { get; set; }

    [Required]
    public Guid TargetAccountId { get; set; }

    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "El monto debe ser mayor a 0.")]
    public decimal Amount { get; set; }

    // Set from the Idempotency-Key HTTP header by the controller — not part of the JSON body.
    [JsonIgnore]
    public Guid IdempotencyKey { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }
}
