using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace BillingApi;

/// <summary>
/// Eigene Instrumentierung des Dienstes.
///
/// Die ActivitySource erzeugt fachliche Spans, der Meter fachliche Metriken.
/// Beide muessen in Program.cs registriert werden (AddSource / AddMeter),
/// sonst werden sie zwar erzeugt, aber nicht exportiert -- ein Fehler, den
/// man beim ersten Mal garantiert macht.
/// </summary>
public static class Telemetry
{
    public const string ServiceName = "billing-api";

    public static readonly ActivitySource Source = new(ServiceName);
    public static readonly Meter Meter = new(ServiceName);

    public static readonly Counter<long> EmpfangeneMesswerte =
        Meter.CreateCounter<long>(
            "billing.messwerte.empfangen",
            unit: "{Messwert}",
            description: "Anzahl entgegengenommener Messwerte");

    public static readonly Histogram<double> AbgerechneteEnergie =
        Meter.CreateHistogram<double>(
            "billing.energie.kwh",
            unit: "kWh",
            description: "Energiemenge je entgegengenommenem Messwert");
}
