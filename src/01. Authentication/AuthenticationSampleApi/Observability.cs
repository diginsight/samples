using Diginsight.Diagnostics;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace AuthenticationSampleApi;

internal static class Observability
{
    public static readonly ActivitySource ActivitySource = new(Assembly.GetExecutingAssembly().GetName().Name!);
    public static ILoggerFactory? LoggerFactory => LoggerFactoryStaticAccessor.LoggerFactory;
    //static Observability() => ObservabilityRegistry.RegisterComponent(factory => LoggerFactory = factory);
}
