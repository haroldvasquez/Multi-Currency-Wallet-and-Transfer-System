namespace Application.Exceptions;

public class SameAccountTransferException : Exception
{
    public SameAccountTransferException()
        : base("La cuenta de origen y destino no pueden ser la misma.") { }
}
