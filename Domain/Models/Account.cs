using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Models
{
    public class Account
    {
        public Guid AccountId { get; set; }

        public Guid CustomerId { get; set; }

        public string AccountNumber { get; set; } = null!;

        public string Currency { get; set; } = null!;

        public decimal OpeningBalance { get; set; }

        public decimal Balance { get; set; }

        public string AccountStatus { get; set; } = null!;

        public long Version { get; set; }

        public virtual Customer Customer { get; set; } = null!;

        public virtual List<Movement> Movements { get; set; } = new List<Movement>();

        public virtual List<Transfer> TransferSourceAccounts { get; set; } = new List<Transfer>();

        public virtual List<Transfer> TransferTargetAccounts { get; set; } = new List<Transfer>();
    }
}
