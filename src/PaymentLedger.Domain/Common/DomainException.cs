namespace PaymentLedger.Domain.Common;

/// <summary>
/// Raised when a business invariant is violated. <see cref="Code"/> is a stable,
/// machine-readable identifier that the API surfaces to clients.
/// </summary>
public class DomainException : Exception
{
    public DomainException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}
