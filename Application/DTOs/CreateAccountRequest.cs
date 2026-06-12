using System.ComponentModel.DataAnnotations;

namespace Application.DTOs;

public class CreateAccountRequest
{
    [Required]
    public Guid CustomerId { get; set; }

    [Required(ErrorMessage = "La moneda es requerida.")]
    [StringLength(3, MinimumLength = 3, ErrorMessage = "La moneda debe ser BOB o USD (3 caracteres).")]
    public string Currency { get; set; } = string.Empty;

    [Range(0.0, (double)decimal.MaxValue, ErrorMessage = "El saldo inicial debe ser mayor o igual a 0.")]
    public decimal InitialBalance { get; set; }
}
