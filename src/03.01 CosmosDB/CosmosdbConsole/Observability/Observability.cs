using System.Reflection;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using Diginsight.Diagnostics;

namespace CosmosdbConsole;

internal static class Observability
{
    public static readonly ActivitySource ActivitySource = new(Assembly.GetExecutingAssembly().GetName().Name!);
    public static ILoggerFactory LoggerFactory { get; set; } = null!;
    static Observability() => ObservabilityRegistry.RegisterComponent(factory => LoggerFactory = factory);
}
