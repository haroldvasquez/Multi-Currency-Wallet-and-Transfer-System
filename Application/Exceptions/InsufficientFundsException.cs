namespace Application.Exceptions
{
    public class InsufficientFundsException : Exception
    {
        public InsufficientFundsException(Guid accountId, decimal balance, decimal requested)
            : base($"Saldo insuficiente en la cuenta '{accountId}'. Saldo disponible: {balance:F2}, monto solicitado: {requested:F2}.") { }
    }
}
