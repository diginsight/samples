using Azure.Core;
using Microsoft.Identity.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AuthenticationSampleClient;

public static class Constants
{
    public static Type T = typeof(Constants);
    public const string LOGGINGCONFIGSECTION = "Logging";
    public const string APPINSIGHTSCONFIGSECTION = "ApplicationInsights";
    public const string APPINSIGHTSINSTRUMENTATIONKEY = "ApplicationInsights:InstrumentationKey";
    public const string APPINSIGHTSCONNECTIONSTRING = "ApplicationInsights:ConnectionString";
    public const string ENABLEREQUESTTRACKINGTELEMETRYMODULE = "ApplicationInsights:EnableRequestTrackingTelemetryModule";
    public const string INCLUDEOPERATIONID = "ApplicationInsights:IncludeOperationId";
    public const string INCLUDEREQUESTBODY = "ApplicationInsights:IncludeRequestBody";
    public const string INCLUDEHEADERS = "ApplicationInsights:IncludeHeaders";

    /// <summary>
    /// The base URI for the Datasync service.
    /// </summary>
    public static string ServiceUri = "https://localhost";

    ///// <summary>
    ///// The application (client) ID for the native app within Microsoft Entra ID
    ///// </summary>
    //public static string ApplicationId = "<client-id>";

    /// <summary>
    /// The list of scopes to request
    /// </summary>
    public static string[] Scopes = new[]
    {
      //"api://7ceef305-d076-4f0c-8d2c-ed7810935a8f/access_as_user",
      "api://9c90b0e2-405f-44c1-9610-e7803621e68a/access_as_user"
      //"api://f8e6e695-51dc-44df-80f0-00e1f144da4c/access_as_user"
    };
}