using System.Diagnostics.Metrics;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Instrumentation.AspNetCore;
using OpenTelemetry.Instrumentation.Http;
using Prometheus;


var builder = WebApplication.CreateBuilder(args);

const string serviceName = "roll-dice";


// Add services to the container.
builder.Services.AddControllers();

// Create a custom meter for the API
var meter = new Meter("roll-dice.Metrics", "1.0");


var httpRequestCounter = meter.CreateCounter<long>("http_requests_total", description: "Total number of HTTP requests");


builder.Logging.AddOpenTelemetry(options =>
{
    options
        .SetResourceBuilder(
            ResourceBuilder.CreateDefault()
                .AddService(serviceName))
        .AddConsoleExporter();
});

// Configure OpenTelemetry
builder.Services.AddOpenTelemetry()
    .WithTracing(tracerProviderBuilder =>
    {
        tracerProviderBuilder
            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("roll-dice"))
            .AddAspNetCoreInstrumentation() // Tracks incoming HTTP requests
            .AddHttpClientInstrumentation()
            .AddConsoleExporter(); // Optional: For debugging
    })
    .WithMetrics(meterProviderBuilder =>
    {
        meterProviderBuilder
            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("roll-dice"))
            .AddAspNetCoreInstrumentation() // Tracks incoming HTTP request metrics
            .AddHttpClientInstrumentation()
            .AddPrometheusExporter(); // Expose metrics to Prometheus
    });

// Add middleware to count HTTP requests
var app = builder.Build();

// Use the Prometheus middleware to expose the /metrics endpoint.
app.UseRouting();

app.UseHttpMetrics(); // Middleware for collecting HTTP metrics

app.UseEndpoints(endpoints =>
{
    endpoints.MapControllers();
    endpoints.MapMetrics(); // Expose the /metrics endpoint for Prometheus scraping
});



string HandleRollDice([FromServices]ILogger<Program> logger, string? player)
{
    var result = RollDice();

    if (string.IsNullOrEmpty(player))
    {
        logger.LogInformation("Anonymous player is rolling the dice: {result}", result);
    }
    else
    {
        logger.LogInformation("{player} is rolling the dice: {result}", player, result);
    }

    return result.ToString(CultureInfo.InvariantCulture);
}

int RollDice()
{
    return Random.Shared.Next(1, 7);
}

app.MapGet("/rolldice/{player?}", HandleRollDice);

app.Run();
