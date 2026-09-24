using System.Diagnostics;
using System.Net.Http.Json;

namespace MeteringSimulator;

/// <summary>
/// Erzeugt im Takt Viertelstundenwerte fuer einige Zaehlpunkte und schickt
/// sie an die Billing-API. Der HTTP-Aufruf wird von der HttpClient-
/// Instrumentierung automatisch als Client-Spanne erfasst; die fachliche
/// Spanne darum herum erzeugen wir selbst.
/// </summary>
public class Simulator(
    IHttpClientFactory httpClientFactory,
    ILogger<Simulator> logger,
    IConfiguration configuration) : BackgroundService
{
    private static readonly string[] Zaehlpunkte =
    [
        "AT0010000000000000000000000001",
        "AT0010000000000000000000000002",
        "AT0010000000000000000000000003"
    ];

    private readonly TimeSpan _takt = TimeSpan.FromSeconds(
        configuration.GetValue("SIMULATOR_INTERVAL_SECONDS", 2));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var client = httpClientFactory.CreateClient("billing");
        var zufall = new Random();

        while (!stoppingToken.IsCancellationRequested)
        {
            var zaehlpunkt = Zaehlpunkte[zufall.Next(Zaehlpunkte.Length)];

            using (var activity = Telemetry.Source.StartActivity("Messwert erzeugen"))
            {
                // Grob ein Haushaltsprofil: nachts wenig, tagsueber mehr.
                var stunde = DateTimeOffset.Now.Hour;
                var grundlast = stunde is >= 6 and <= 22 ? 0.25 : 0.08;
                var kwh = Math.Round(grundlast + zufall.NextDouble() * 0.15, 4);

                activity?.SetTag("zaehlpunkt.nummer", zaehlpunkt);
                activity?.SetTag("messwert.kwh", kwh);

                var messwert = new
                {
                    Zaehlpunkt = zaehlpunkt,
                    Kwh = kwh,
                    Zeitpunkt = DateTimeOffset.UtcNow
                };

                try
                {
                    var antwort = await client.PostAsJsonAsync("/messwerte", messwert, stoppingToken);
                    activity?.SetTag("http.antwort", (int)antwort.StatusCode);
                }
                catch (Exception ex)
                {
                    // Fehler gehoeren in den Trace, nicht nur ins Log.
                    activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                    logger.LogWarning(ex, "Messwert konnte nicht gesendet werden");
                }
            }

            await Task.Delay(_takt, stoppingToken);
        }
    }
}
