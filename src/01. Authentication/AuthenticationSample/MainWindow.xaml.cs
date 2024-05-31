#region using
using Diginsight.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Resources;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
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

        private string GetScope([CallerMemberName] string memberName = "") { return memberName; }

        static MainWindow()
        {
            var host = App.Host;


        }
        public MainWindow(ILogger<MainWindow> logger)
        {
            this.logger = logger;
            using var activity = App.ActivitySource.StartMethodActivity(logger, new { logger });

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
    }
}
