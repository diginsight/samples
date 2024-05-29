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
        //private static ActivitySource source = new ActivitySource("AuthenticationSamplev3.MainWindow", "1.0.0");
        static Type T = typeof(MainWindow);
        private ILogger<MainWindow> logger;
        //private IClassConfigurationGetter<MainWindow> classConfigurationGetter;

        private string GetScope([CallerMemberName] string memberName = "") { return memberName; }

        static MainWindow()
        {
            var host = App.Host;
            //using var scope = host.BeginMethodScope<MainWindow>();
            //using Activity activity = TraceLogger.ActivitySource.StartActivity();
            ////using var scope = TraceLogger.ActivitySource.StartMethodActivity(logger);

            //var logger = host.GetLogger<MainWindow>();
            //using (var scope = logger.BeginMethodScope())
            //{
            //}

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

            //classConfigurationGetter.Get("SampleConfig", "");
            sampleMethod();
            await sampleMethod1Async();

            int i = 0;
            //logger.LogDebug(() => new { i, e, sender }); // , properties: new Dictionary<string, object>() { { "", "" } }


            {
                //TraceLogger.BeginNamedScope<MainWindow>("Standard code section");
                //() => new { i, e = e.GetLogString(), sender = sender.GetLogString() }
                logger.LogTrace("this is a trace trace"); // , properties: new Dictionary<string, object>() { { "", "" } }
                logger.LogDebug("this is a debug trace"); // , properties: new Dictionary<string, object>() { { "", "" } }
            }

            {
                //logger.BeginNamedScope("Optimized code section");

                //logger.LogInformation(() => "this is a Information trace", "User"); // , properties: new Dictionary<string, object>() { { "", "" } }
                //logger.LogInformation(() => "this is a Information trace", "Raw");
                //logger.LogWarning(() => "this is a Warning trace", "User.Report");
                //logger.LogError(() => $"this is a error trace", "Resource");

                //logger.LogError(() => "this is a error trace", "Resource");

                ////TraceManager.Debug("")
                //logger.LogDebug(() => "this is a trace trace", "User"); // , properties: new Dictionary<string, object>() { { "", "" } }
                //logger.LogDebug(() => "this is a debug trace", "User"); // , properties: new Dictionary<string, object>() { { "", "" } }
                //logger.LogInformation(() => "this is a debug trace", "User"); // , properties: new Dictionary<string, object>() { { "", "" } }
                //logger.LogInformation(() => "this is a Information trace", "Raw");
                //logger.LogWarning(() => "this is a Warning trace", "User.Report");
                //logger.LogError(() => "this is a error trace", "Resource");

                //logger.LogError(() => "this is a error trace", "Resource");
            }

            var guid = Guid.NewGuid();
            var uri = new Uri("http://localhost:80");
            //logger.LogDebug(new { guid, uri });
        }
        void sampleMethod()
        {
            using var activity = App.ActivitySource.StartMethodActivity(logger, new { });

            logger.LogDebug("pippo");

        }

        int i = 0;
        private void btnRun_Click(object sender, RoutedEventArgs e)
        {
            using var activity = App.ActivitySource.StartMethodActivity(logger, new { sender, e });

            try
            {

                throw new InvalidOperationException("sample ex");
            }
            catch (Exception _) { }
        }

        public int SampleMethodWithResult(int i, string s)
        {
            using var activity = App.ActivitySource.StartMethodActivity(logger, new { i, s });

            var result = 0;

            //var j = i++; logger.LogDebug(new { i, j });

            Thread.Sleep(100); logger.LogDebug($"Thread.Sleep(100); completed");
            SampleMethodNested(); logger.LogDebug($"SampleMethodNested(); completed");
            SampleMethodNested1(); logger.LogDebug($"SampleMethodNested1(); completed");

            activity.SetOutput(result);
            return result;
        }
        public void SampleMethod()
        {
            using var activity = App.ActivitySource.StartMethodActivity(logger);

            Thread.Sleep(100);
            SampleMethodNested();
            SampleMethodNested1();

        }
        public void SampleMethodNested()
        {
            using var activity = App.ActivitySource.StartMethodActivity(logger);

            Thread.Sleep(100);
        }
        public void SampleMethodNested1()
        {
            using var activity = App.ActivitySource.StartMethodActivity(logger);
                
            Thread.Sleep(10);
        }
        async Task<bool> sampleMethod1Async()
        {
            using var activity = App.ActivitySource.StartMethodActivity(logger);

            var res = true;

            await Task.Delay(0); logger.LogDebug($"await Task.Delay(0);");

            return res;

        }
    }
}
