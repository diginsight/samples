#region using
using Diginsight.Options;
using Diginsight.Diagnostics;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using OpenTelemetry.Resources;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
//using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using Metrics = System.Collections.Generic.Dictionary<string, object>;
using Window = System.Windows.Window; // $$$
using AuthenticationToken = Microsoft.Datasync.Client.AuthenticationToken;
using Microsoft.Identity.Client.Platforms.Features.DesktopOs.Kerberos;
using Azure.Identity;
using Azure.Core;
using System.Net.Http;
using Microsoft.AspNetCore.Http;
using Refit;
using static System.Formats.Asn1.AsnWriter;
using System.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Microsoft.Extensions.Configuration;
using AuthenticationSampleClient.Models;
using Microsoft.Extensions.Options;
using Diginsight.Components;
#endregion

namespace AuthenticationSampleClient
{
    /// <summary>Interaction logic for MainWindow.xaml</summary>
    public partial class MainWindow : Window
    {
        static Type T = typeof(MainWindow);
        private readonly IServiceProvider serviceProvider;
        private ILogger<MainWindow> logger;
        private readonly IClassAwareOptionsMonitor<AppSettingsOptions> appSettingsOptionsMonitor;
        private readonly IClassAwareOptionsMonitor<FeatureFlagOptions> featureFlagOptionsMonitor;
        private readonly IClassAwareOptionsMonitor<AzureAdOptions> azureAdOptionsMonitor;
        private readonly IOptionsMonitor<HttpClientOptions> authenticationSampleApiOptionsMonitor;
        private readonly HttpClientOptions authenticationSampleApiOptions;

        private readonly IHttpClientFactory httpClientFactory;
        private readonly HttpClient httpClient;

        private string GetScope([CallerMemberName] string memberName = "") { return memberName; }

        public static IPublicClientApplication IdentityClient { get; set; }
        #region AuthenticationResult
        public AuthenticationResult AuthenticationResult
        {
            get { return (AuthenticationResult)GetValue(AuthenticationResultProperty); }
            set { SetValue(AuthenticationResultProperty, value); }
        }
        public static readonly DependencyProperty AuthenticationResultProperty = DependencyProperty.Register("AuthenticationResult", typeof(AuthenticationResult), typeof(MainWindow), new PropertyMetadata());
        #endregion
        #region ClaimsPrincipal
        public ClaimsPrincipal ClaimsPrincipal
        {
            get { return (ClaimsPrincipal)GetValue(ClaimsPrincipalProperty); }
            set { SetValue(ClaimsPrincipalProperty, value); }
        }
        public static readonly DependencyProperty ClaimsPrincipalProperty = DependencyProperty.Register("ClaimsPrincipal", typeof(ClaimsPrincipal), typeof(MainWindow), new PropertyMetadata());
        #endregion

        #region .ctor
        static MainWindow()
        {
            var logger = App.ObservabilityManager.LoggerFactory.CreateLogger<MainWindow>();
            using var activity = Observability.ActivitySource.StartMethodActivity(logger);

            var host = App.Host;
        }
        public MainWindow(
                          IServiceProvider serviceProvider,
                          ILogger<MainWindow> logger,
                          IClassAwareOptionsMonitor<AppSettingsOptions> appSettingsOptionsMonitor,
                          IClassAwareOptionsMonitor<FeatureFlagOptions> featureFlagOptionsMonitor,
                          IClassAwareOptionsMonitor<AzureAdOptions> azureAdOptionsMonitor,
                          IHttpClientFactory httpClientFactory,
                          HttpClient httpClient,
                          IHttpContextAccessor httpContextAccessor)
        {
            this.serviceProvider = serviceProvider;
            this.logger = logger;
            using var activity = Observability.ActivitySource.StartMethodActivity(logger, new { logger });

            this.featureFlagOptionsMonitor = featureFlagOptionsMonitor;
            this.appSettingsOptionsMonitor = appSettingsOptionsMonitor;
            this.azureAdOptionsMonitor = azureAdOptionsMonitor;
            this.httpClientFactory = httpClientFactory;

            this.authenticationSampleApiOptions = serviceProvider.GetRequiredService<IOptionsMonitor<HttpClientOptions>>().Get("AuthenticationSampleApi");

            var clientId = azureAdOptionsMonitor.CurrentValue.ClientId;
            var tenantId = azureAdOptionsMonitor.CurrentValue.TenantId;
            var redirectUri = azureAdOptionsMonitor.CurrentValue.RedirectUri;
            
            var app = PublicClientApplicationBuilder
                        .Create(clientId)
                        .WithAuthority(AzureCloudInstance.AzurePublic, tenantId)
                        .WithRedirectUri(redirectUri)
                        .Build();

            IdentityClient = app;

            this.httpClient = httpClient;

            InitializeComponent();
        }
        #endregion

        private void MainWindow_Initialized(object sender, EventArgs e)
        {
            using var activity = Observability.ActivitySource.StartMethodActivity(logger, new { sender, e });


        }

        int i = 0;
        private async void btnRestSharpCall_Click(object sender, RoutedEventArgs e)
        {
            using var activity = Observability.ActivitySource.StartMethodActivity(logger, new { sender, e });

            try
            {
                var credential = DelegatedTokenCredential.Create(null, async (tokenRequestContext, cancellationToken) =>
                {
                    AuthenticationResult result = await GetAuthenticationToken();
                    return new AccessToken(result.AccessToken, result.ExpiresOn);
                });

                //var authenticationSampleApiOptions = authenticationSampleApiOptionsMonitor.CurrentValue;
                
                var service = new RestSharpService(credential);
                var res = await service.Get($"{authenticationSampleApiOptions.BaseUrl}api/Plants/getplants", null, null);

            }
            catch (Exception _) { }
        }
        private async void btnHttpClientCall_Click(object sender, RoutedEventArgs e)
        {
            using var activity = Observability.ActivitySource.StartMethodActivity(logger, new { sender, e });

            try
            {
                var credential = DelegatedTokenCredential.Create(null, async (tokenRequestContext, cancellationToken) =>
                {
                    AuthenticationResult result = await GetAuthenticationToken();
                    return new AccessToken(result.AccessToken, result.ExpiresOn);
                });

                //var authenticationSampleApiOptions = authenticationSampleApiOptionsMonitor.CurrentValue;
                string action = $"{authenticationSampleApiOptions.BaseUrl}api/Plants/getplants";
                //using HttpResponseMessage responseMessage = await httpClient.GetAsync("api/v1/timezones");
                using var request = new HttpRequestMessage(HttpMethod.Get, action);
                // request.Content = MakeJsonContent(profileImage);
                var token = await credential.GetTokenAsync(new TokenRequestContext(Constants.Scopes), CancellationToken.None);
                request.Headers.Add("Authorization", $"Bearer {token.Token}");
                using var responseMessage = await httpClient.SendAsync(request);

                var requestString = request?.Content != null ? await request?.Content?.ReadAsStringAsync() : null;
                await ThrowExceptionIfNotSuccessAsync(responseMessage);

                var plants = await ReadDeserializedContentAsync<IEnumerable<Plant>>(responseMessage);

            }
            catch (Exception _) { }
        }
        private async void btnRefItCall_Click(object sender, RoutedEventArgs e)
        {
            using var activity = Observability.ActivitySource.StartMethodActivity(logger, new { sender, e });

            var credential = DelegatedTokenCredential.Create(null, async (tokenRequestContext, cancellationToken) =>
            {
                AuthenticationResult result = await GetAuthenticationToken();
                return new AccessToken(result.AccessToken, result.ExpiresOn);
            });
            //var authenticationSampleApiOptions = authenticationSampleApiOptionsMonitor.CurrentValue;
            HttpClient client = new HttpClient { BaseAddress = authenticationSampleApiOptions.BaseUrl };
            var token = await credential.GetTokenAsync(new TokenRequestContext(Constants.Scopes), CancellationToken.None);
            if (client.DefaultRequestHeaders.Contains("Authorization")) { client.DefaultRequestHeaders.Remove("Authorization"); }
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token.Token}");
            var restApiService = RestService.For<IAuthenticationSampleApi>(client);

            //var settings = new RefitSettings();
            //settings.AuthorizationHeaderValueGetter = async (r, c) => {
            //    var token = await credential.GetTokenAsync(new TokenRequestContext(Constants.Scopes), CancellationToken.None);
            //    var bearer = "Bearer " + token.Token;
            //    r.Headers.Add("Authorization", bearer);
            //    return bearer;
            //};
            //var builder = RequestBuilder.ForType<IAuthenticationSampleApi>(settings);
            //var restApiService = RestService.For<IAuthenticationSampleApi>(client, builder); 


            var result1 = await restApiService.GetPlants();

            activity.SetOutput(result1);
        }


        private async void Login_Click(object sender, RoutedEventArgs e)
        {
            using var activity = Observability.ActivitySource.StartMethodActivity(logger, new { sender, e });

            try
            {
                string[] scopes = Constants.Scopes;
                AuthenticationResult result = await GetAuthenticationToken();
                this.AuthenticationResult = result;
                this.ClaimsPrincipal = result.ClaimsPrincipal;
                var identity = this.ClaimsPrincipal.Identity; // getClaim name
            }
            catch (Exception _) { }
        }

        public async Task<AuthenticationResult> GetAuthenticationToken()
        {
            using var activity = Observability.ActivitySource.StartMethodActivity(logger);

            var accounts = await IdentityClient.GetAccountsAsync();
            AuthenticationResult? result = null;
            try
            {
                result = await IdentityClient.AcquireTokenSilent(Constants.Scopes, accounts.FirstOrDefault())
                    .ExecuteAsync();
            }
            catch (MsalUiRequiredException)
            {
                result = await IdentityClient
                    .AcquireTokenInteractive(Constants.Scopes)
                    //.WithUseEmbeddedWebView()
                    .ExecuteAsync();
            }
            catch (Exception ex)
            {
                // Display the error text - probably as a pop-up
                Debug.WriteLine($"Error: Authentication failed: {ex.Message}");
            }

            var name = result?.ClaimsPrincipal?.Claims?.FirstOrDefault(c => c.Type == "name")?.Value;
            return result;
        }

        protected static async Task ThrowExceptionIfNotSuccessAsync(HttpResponseMessage responseMessage, params HttpStatusCode[] additionalSuccessCodes)
        {
            if (!responseMessage.IsSuccessStatusCode && additionalSuccessCodes?.Contains(responseMessage.StatusCode) != true)
            {
                throw await MakeExceptionAsync(responseMessage);
            }
        }
        protected static async Task<DownstreamApiException> MakeExceptionAsync(HttpResponseMessage responseMessage)
        {
            //TraceLogger.LogError($"Error '{responseMessage.StatusCode}' calling endpoint '{responseMessage.RequestMessage!.RequestUri}'");

            var requestContent = responseMessage.RequestMessage?.Content;
            if (requestContent != null)
            {
                var requestString = await requestContent.ReadAsStringAsync();
                //TraceLogger.LogError($"Request body: {requestString}");
            }

            var responseString = await responseMessage.Content.ReadAsStringAsync();
            //TraceLogger.LogError($"Response content  ({(double)responseString.Length / 1024:#,##0} KB): {responseString}");

            HttpRequestMessage requestMessage = responseMessage.RequestMessage!;
            return new DownstreamApiException(
                requestMessage.Method,
                requestMessage.RequestUri!,
                responseMessage.StatusCode,
                await responseMessage.Content.ReadAsByteArrayAsync()
            );
        }

        protected static async Task<TContent> ReadDeserializedContentAsync<TContent>(HttpResponseMessage responseMessage)
        {
            //if (DumpFullRequestResponse)
            //{
            //    var requestContent = responseMessage.RequestMessage?.Content;
            //    if (requestContent != null)
            //    {
            //        var requestString = await requestContent.ReadAsStringAsync();
            //        TraceLogger.LogDebug($"Request body ({(double)requestString.Length / 1024:#,##0} KB): {requestString}", null, new Dictionary<string, object>() { { "MaxMessageLen", 0 } });
            //    }
            //}

            string rawContent = await responseMessage.Content.ReadAsStringAsync();
            //if (DumpFullRequestResponse)
            //{
            //    TraceLogger.LogDebug($"Response content  ({(double)rawContent.Length / 1024:#,##0} KB): {rawContent}", null, new Dictionary<string, object>() { { "MaxMessageLen", LONGMESSAGELEN } });
            //}

            return responseMessage.Content.Headers.ContentType?.MediaType == "text/plain" && typeof(TContent) == typeof(string)
                ? (TContent)(object)rawContent
                : JsonConvert.DeserializeObject<TContent>(rawContent, Statics.SerializerSettings);
        }

    }

    file static class Statics
    {
        public static readonly JsonSerializerSettings SerializerSettings = new()
        {
            ContractResolver = new DefaultContractResolver() { NamingStrategy = new CamelCaseNamingStrategy() },
            DateTimeZoneHandling = DateTimeZoneHandling.Utc,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
        };
    }
}
