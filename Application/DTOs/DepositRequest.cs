using System.ComponentModel.DataAnnotations;

namespace Application.DTOs;

public class DepositRequest
{
    [Required]
    [Range(0.01, (double)decimal.MaxValue, ErrorMessage = "El monto del depósito debe ser mayor a 0.")]
    public decimal Amount { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }
}
