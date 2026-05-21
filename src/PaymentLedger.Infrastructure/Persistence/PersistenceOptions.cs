namespace PaymentLedger.Infrastructure.Persistence;

public enum PersistenceProvider
{
    InMemory = 0,
    Postgres = 1,
}

public sealed class PersistenceOptions
{
    public const string SectionName = "Persistence";

    public const string ConnectionStringName = "Ledger";

    public PersistenceProvider Provider { get; set; } = PersistenceProvider.InMemory;
}
