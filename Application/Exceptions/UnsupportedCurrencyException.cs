using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Exceptions
{
    public class UnsupportedCurrencyException : Exception
    {
        public UnsupportedCurrencyException(string currency)
            : base($"La moneda '{currency}' no está soportada. Solo se aceptan BOB y USD.") { }
    }
}
