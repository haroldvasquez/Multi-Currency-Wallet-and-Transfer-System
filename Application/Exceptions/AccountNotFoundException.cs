namespace Application.Exceptions
{
    public class AccountNotFoundException : Exception
    {
        public AccountNotFoundException(Guid accountId)
            : base($"No se encontró una cuenta con ID '{accountId}'.") { }
    }
}
