using System.Diagnostics;

namespace MeteringSimulator;

public static class Telemetry
{
    public const string ServiceName = "metering-simulator";

    public static readonly ActivitySource Source = new(ServiceName);
}
