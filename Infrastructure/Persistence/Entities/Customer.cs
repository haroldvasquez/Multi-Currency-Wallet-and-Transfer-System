using System;
using System.Collections.Generic;

namespace Infrastructure.Persistence.Entities;

public partial class Customer
{
    public Guid CustomerId { get; set; }

    public string DocumentId { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string Email { get; set; } = null!;

    public virtual ICollection<Account> Accounts { get; set; } = new List<Account>();
}
