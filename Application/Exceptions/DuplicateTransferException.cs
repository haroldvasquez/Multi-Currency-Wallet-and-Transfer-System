namespace Application.Exceptions;

public class DuplicateTransferException : Exception
{
    public DuplicateTransferException(Guid idempotencyKey)
        : base($"Ya existe una transferencia con la clave de idempotencia '{idempotencyKey}'.") { }
}
