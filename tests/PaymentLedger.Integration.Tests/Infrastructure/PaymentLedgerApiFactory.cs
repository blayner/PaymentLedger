using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace PaymentLedger.Integration.Tests.Infrastructure;

/// <summary>Hosts the real API in memory, wired to a dedicated PostgreSQL database.</summary>
public sealed class PaymentLedgerApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    public string ConnectionString { get; } = connectionString;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Persistence:Provider", "Postgres");
        builder.UseSetting("ConnectionStrings:Ledger", ConnectionString);
        builder.UseSetting("Outbox:PollingInterval", "00:00:00.200");
    }
}
