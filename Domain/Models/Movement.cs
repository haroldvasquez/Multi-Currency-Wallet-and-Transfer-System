using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Models
{
    public class Movement
    {
        public Guid MovementId { get; set; }

        public Guid AccountId { get; set; }

        public Guid? TransferId { get; set; }

        public string MovementType { get; set; } = null!;

        public decimal Amount { get; set; }

        public decimal PreviousBalance { get; set; }

        public decimal NewBalance { get; set; }

        public string? MovementDescription { get; set; }

        public DateTime CreatedAt { get; set; }

        public virtual Account Account { get; set; } = null!;

        public virtual Transfer? Transfer { get; set; }
    }
}
