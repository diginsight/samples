using System.Reflection;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using Diginsight.Diagnostics;

namespace Page2Clipboard;

internal static class Observability
{
    public static readonly ActivitySource ActivitySource = new(Assembly.GetExecutingAssembly().GetName().Name!);
    public static ILoggerFactory? LoggerFactory => LoggerFactoryStaticAccessor.LoggerFactory;
    //static Observability() => ObservabilityRegistry.RegisterComponent(factory => LoggerFactory = factory);
}
