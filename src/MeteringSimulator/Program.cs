using MeteringSimulator;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = Host.CreateApplicationBuilder(args);

var billingUrl = builder.Configuration.GetValue("BILLING_URL", "http://billing-api:8080")!;

builder.Services.AddHttpClient("billing", client =>
{
    client.BaseAddress = new Uri(billingUrl);
    client.Timeout = TimeSpan.FromSeconds(5);
});

builder.Services.AddHostedService<Simulator>();

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(
        serviceName: Telemetry.ServiceName,
        serviceVersion: "1.0.0"))
    .WithTracing(tracing => tracing
        .AddHttpClientInstrumentation()
        .AddSource(Telemetry.ServiceName)
        .AddOtlpExporter())
    .WithMetrics(metrics => metrics
        .AddRuntimeInstrumentation()
        .AddOtlpExporter());

builder.Build().Run();
