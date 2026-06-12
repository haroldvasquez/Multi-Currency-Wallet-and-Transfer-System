namespace Application.Exceptions
{
    public class AccountNotActiveException : Exception
    {
        public AccountNotActiveException(Guid accountId, string status)
            : base($"La cuenta '{accountId}' no puede operar. Estado actual: '{status}'.") { }
    }
}
