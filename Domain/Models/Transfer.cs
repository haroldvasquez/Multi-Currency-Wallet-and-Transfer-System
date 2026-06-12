using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Models
{
    public class Transfer
    {
        public Guid TransferId { get; set; }

        public Guid SourceAccountId { get; set; }

        public Guid TargetAccountId { get; set; }

        public decimal Amount { get; set; }

        public decimal? ExchangeRate { get; set; }

        public decimal? ConvertedAmount { get; set; }

        public Guid IdempotencyKey { get; set; }

        public DateTime CreatedAt { get; set; }

        public string TransferStatus { get; set; } = null!;

        public virtual List<Movement> Movements { get; set; } = new List<Movement>();

        public virtual Account SourceAccount { get; set; } = null!;

        public virtual Account TargetAccount { get; set; } = null!;
    }
}
