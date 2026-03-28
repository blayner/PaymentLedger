namespace PaymentLedger.Domain.Abstractions;

public sealed class ConcurrencyException : Exception
{
    public ConcurrencyException(Guid streamId, long expectedVersion, long actualVersion)
        : base($"Stream {streamId} expected at version {expectedVersion} but was at {actualVersion}.")
    {
        StreamId = streamId;
        ExpectedVersion = expectedVersion;
        ActualVersion = actualVersion;
    }

    public Guid StreamId { get; }

    public long ExpectedVersion { get; }

    public long ActualVersion { get; }
}
