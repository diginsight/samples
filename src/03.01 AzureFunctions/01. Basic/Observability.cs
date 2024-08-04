using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Azure.Functions.Samples.DependencyInjectionBasic;

internal static class Observability
{
    public static readonly ActivitySource ActivitySource = new ActivitySource(Assembly.GetExecutingAssembly().GetName().Name);
}
