using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Exceptions
{
    public class InvalidBalanceException:Exception
    {
        public InvalidBalanceException(string message) : base(message) { }
    }
}
