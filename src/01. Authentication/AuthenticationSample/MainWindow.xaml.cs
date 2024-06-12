#region using
using Diginsight.CAOptions;
using Diginsight.Diagnostics;
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
using Metrics = System.Collections.Generic.Dictionary<string, object>; // $$$
#endregion

namespace AuthenticationSample
{
    /// <summary>Interaction logic for MainWindow.xaml</summary>
    public partial class MainWindow : Window
    {
        static Type T = typeof(MainWindow);
        private ILogger<MainWindow> logger;
        private readonly IClassAwareOptionsMonitor<AppSettingsOptions> appSettingsOptionsMonitor;
        private readonly IClassAwareOptionsMonitor<FeatureFlagOptions> featureFlagOptionsMonitor;
        private readonly IClassAwareOptionsMonitor<AzureOptions> azureOptionsMonitor;

        private string GetScope([CallerMemberName] string memberName = "") { return memberName; }



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





        static MainWindow()
        {
            var host = App.Host;


        }
        public MainWindow(ILogger<MainWindow> logger,
                          IClassAwareOptionsMonitor<AppSettingsOptions> appSettingsOptionsMonitor,
                          IClassAwareOptionsMonitor<FeatureFlagOptions> featureFlagOptionsMonitor,
                          IClassAwareOptionsMonitor<AzureOptions> azureOptionsMonitor
               )
        {
            this.logger = logger;
            using var activity = App.ActivitySource.StartMethodActivity(logger, new { logger });
            
            this.featureFlagOptionsMonitor = featureFlagOptionsMonitor;
            this.appSettingsOptionsMonitor = appSettingsOptionsMonitor;
            this.azureOptionsMonitor = azureOptionsMonitor;

            InitializeComponent();
        }
        private async void MainWindow_Initialized(object sender, EventArgs e)
        {
            using var activity = App.ActivitySource.StartMethodActivity(logger, new { sender, e });


        }


        int i = 0;
        private void btnRun_Click(object sender, RoutedEventArgs e)
        {
            using var activity = App.ActivitySource.StartMethodActivity(logger, new { sender, e });

            try
            {


            }
            catch (Exception _) { }
        }

        private async void Login_Click(object sender, RoutedEventArgs e)
        {
            using var activity = App.ActivitySource.StartMethodActivity(logger, new { sender, e });

            try
            {
                var clientId = azureOptionsMonitor.CurrentValue.ClientId;
                var tenantId = azureOptionsMonitor.CurrentValue.TenantId;
                var redirectUri = azureOptionsMonitor.CurrentValue.RedirectUri;

                var app = PublicClientApplicationBuilder
                            .Create(clientId)
                            .WithAuthority(AzureCloudInstance.AzurePublic, tenantId)
                            .WithRedirectUri(redirectUri)
                            .Build();
                string[] scopes = { "user.read" };
                AuthenticationResult result = await app.AcquireTokenInteractive(scopes).ExecuteAsync();

                this.AuthenticationResult = result;
                this.ClaimsPrincipal = result.ClaimsPrincipal;
                var identity = this.ClaimsPrincipal.Identity; // getClaim name
            }
            catch (Exception _) { }
        }
    }
}
