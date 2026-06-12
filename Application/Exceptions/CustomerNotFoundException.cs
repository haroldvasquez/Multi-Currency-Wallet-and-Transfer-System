using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Exceptions
{
    public class CustomerNotFoundException : Exception
    {
        public CustomerNotFoundException(Guid customerId)
        : base($"No se encontró un cliente con ID '{customerId}'.") { }
    }
}
