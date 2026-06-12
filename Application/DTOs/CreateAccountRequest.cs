using System;
using System.Collections.Generic;
using System.Text;

namespace Application.DTOs
{
    public class CreateAccountRequest
    {
        public Guid CustomerId { get; set; }
        public string Currency { get; set; } = string.Empty;
        public decimal InitialBalance { get; set; }
    }
}
