var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new { service = "PaymentLedger", status = "bootstrapping" }));

app.Run();

public partial class Program
{
}
