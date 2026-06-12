using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Models
{
    public class Customer
    {
        public Guid CustomerId { get; set; }

        public string DocumentId { get; set; } = null!;

        public string Name { get; set; } = null!;

        public string Email { get; set; } = null!;

        public virtual List<Account> Accounts { get; set; } = new List<Account>();
    }
}
