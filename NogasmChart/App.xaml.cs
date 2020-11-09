using System;
using System.Collections.Generic;
using System.IO;
using System.Resources;
using System.Windows;
using System.Windows.Threading;
using System.Xml;
using Newtonsoft.Json;
using Sentry;

namespace NogasmChart
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // define application exception handler
            AppDomain.CurrentDomain.UnhandledException +=
                AppUnhandledException;

            // defer other startup processing to base class
            base.OnStartup(e);
        }

        void AppUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var sentryUrl = "";
            try
            {
                using (Stream stream = GetType().Assembly.GetManifestResourceStream("NogasmChart.secrets.json"))
                {
                    using (var reader = new StreamReader(stream))
                    {
                        var res = JsonConvert.DeserializeObject<Dictionary<String, String>>(reader.ReadToEnd());
                        res.TryGetValue("sentryUrl", out sentryUrl);
                    }
                }
            }
            catch (Exception e1)
            {
                Console.WriteLine(e1.Message);
            }

            if (sentryUrl.Length == 0 || MessageBox.Show(
                    "A crash was detected! We'd like to send a crash report to the developers, but since this could contain sensitive information, we need your permission. Click yes send the report.",
                    "Crash detected", MessageBoxButton.YesNo, MessageBoxImage.Error) != MessageBoxResult.Yes) return;
            using (SentrySdk.Init(sentryUrl))
            {
                SentrySdk.CaptureException(e.ExceptionObject as Exception);
            }
        }
    }
}
