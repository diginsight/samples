using Azure.Core;
using Microsoft.Identity.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AuthenticationSampleClient
{
    //public class CustomTokenCredential : TokenCredential
    //{
    //    private readonly ClientCredentialProvider _provider;
    //    private readonly string[] _scopes;
    //    private readonly IAccount _account;

    //    public CustomTokenCredential(ClientCredentialProvider provider, string[] scopes, IAccount account)
    //    {
    //        _provider = provider;
    //        _scopes = scopes;
    //        _account = account;
    //    }

    //    public override async ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
    //    {
    //        var token = await _provider.GetTokenForUserAsync(_scopes, _account);
    //        return new AccessToken(token.AccessToken, token.ExpiresOn);
    //    }

    //    public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
    //    {
    //        return GetTokenAsync(requestContext, cancellationToken).Result;
    //    }
    //}

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
          "api://7ceef305-d076-4f0c-8d2c-ed7810935a8f/access_as_user"
        };
    }
}