using System;
using System.Collections.Generic;

namespace Infrastructure.Persistence.Entities;

public partial class Transfer
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

    public virtual ICollection<Movement> Movements { get; set; } = new List<Movement>();

    public virtual Account SourceAccount { get; set; } = null!;

    public virtual Account TargetAccount { get; set; } = null!;
}
