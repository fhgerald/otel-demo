using System.Collections.Concurrent;
using BillingApi;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// OpenTelemetry
//
// Endpunkt und Zugangsdaten kommen ueber Umgebungsvariablen:
//   OTEL_EXPORTER_OTLP_ENDPOINT   z. B. https://ingress.<region>.dash0.com
//   OTEL_EXPORTER_OTLP_HEADERS    z. B. Authorization=Bearer%20<token>
// Ist nichts gesetzt, exportiert der Dienst nach localhost:4317 und laeuft
// trotzdem -- fehlende Telemetrie legt die Anwendung nicht lahm.
// ---------------------------------------------------------------------------
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(
        serviceName: Telemetry.ServiceName,
        serviceVersion: "1.0.0"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddSource(Telemetry.ServiceName)
        .AddOtlpExporter())
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddRuntimeInstrumentation()
        .AddMeter(Telemetry.ServiceName)
        .AddOtlpExporter());

var app = builder.Build();

// Bewusst im Arbeitsspeicher: Das Beispiel soll die Telemetrie zeigen,
// nicht Persistenz. Im Labor tritt hier OctoMesh an die Stelle der Liste.
var speicher = new ConcurrentBag<Messwert>();

app.MapPost("/messwerte", (Messwert messwert) =>
{
    // Ein eigener Span innerhalb der automatisch erzeugten Server-Spanne.
    using var activity = Telemetry.Source.StartActivity("Messwert verarbeiten");
    activity?.SetTag("zaehlpunkt.nummer", messwert.Zaehlpunkt);
    activity?.SetTag("messwert.kwh", messwert.Kwh);

    speicher.Add(messwert);

    Telemetry.EmpfangeneMesswerte.Add(1,
        new KeyValuePair<string, object?>("zaehlpunkt", messwert.Zaehlpunkt));
    Telemetry.AbgerechneteEnergie.Record(messwert.Kwh);

    return Results.Accepted();
});

app.MapGet("/abrechnung", () =>
{
    using var activity = Telemetry.Source.StartActivity("Abrechnung berechnen");

    var messwerte = speicher.ToArray();
    var summe = messwerte.Sum(m => m.Kwh);

    activity?.SetTag("abrechnung.anzahl", messwerte.Length);
    activity?.SetTag("abrechnung.kwh", summe);

    return Results.Ok(new
    {
        Anzahl = messwerte.Length,
        Kwh = Math.Round(summe, 3)
    });
});

app.MapGet("/health", () => Results.Ok("ok"));

app.Run();

/// <summary>Ein Viertelstundenwert eines Zaehlpunkts.</summary>
public record Messwert(string Zaehlpunkt, double Kwh, DateTimeOffset Zeitpunkt);
