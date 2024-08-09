using System.Diagnostics;
using System.Reflection;

namespace AzureProvisioning;

internal static class Observability
{
    public static readonly ActivitySource ActivitySource = new (Assembly.GetExecutingAssembly().GetName().Name!);
}
