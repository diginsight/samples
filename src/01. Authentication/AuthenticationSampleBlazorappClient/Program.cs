using AuthenticationSampleBlazorappClient;
using AuthenticationSampleBlazorappClient.Shared;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var consoleProvider = new TraceLoggerConsoleProvider();
builder.Logging.SetMinimumLevel(LogLevel.Trace);
builder.Logging.ClearProviders();
builder.Logging.AddProvider(consoleProvider);

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

var graohConfiguration = builder.Configuration.GetSection("MicrosoftGraph");
var baseUrl = string.Join("/", graohConfiguration["BaseUrl"], graohConfiguration["Version"]);
var scopes = graohConfiguration.GetSection("Scopes").Get<List<string>>();

//builder.Services.AddMicrosoftGraphClient("https://graph.microsoft.com/User.Read");
builder.Services.AddGraphClient(baseUrl, scopes);

builder.Services.AddMsalAuthentication(options =>
{
    builder.Configuration.Bind("AzureAd", options.ProviderOptions.Authentication);
    options.ProviderOptions.DefaultAccessTokenScopes.Add("https://graph.microsoft.com/User.Read");
});




await builder.Build().RunAsync();
