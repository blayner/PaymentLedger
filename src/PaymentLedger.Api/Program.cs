using System.Text.Json.Serialization;
using PaymentLedger.Api.ErrorHandling;
using PaymentLedger.Api.Observability;
using PaymentLedger.Application;
using PaymentLedger.Infrastructure;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.AddObservability();

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

builder.Services
    .AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddOpenApi();

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseSerilogRequestLogging(options =>
    options.GetLevel = (context, _, exception) =>
        exception is not null || context.Response.StatusCode >= 500
            ? Serilog.Events.LogEventLevel.Error
            : context.Request.Path.StartsWithSegments("/health")
                ? Serilog.Events.LogEventLevel.Verbose
                : Serilog.Events.LogEventLevel.Information);
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    // OpenAPI document at /openapi/v1.json
    app.MapOpenApi();
}

app.MapHealthEndpoints();
app.MapControllers();

app.Run();

public partial class Program
{
}
