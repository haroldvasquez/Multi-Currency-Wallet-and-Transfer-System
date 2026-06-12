namespace Application.Exceptions;

public class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException()
        : base("Conflicto de concurrencia al procesar la transferencia. Intente nuevamente.") { }
}
