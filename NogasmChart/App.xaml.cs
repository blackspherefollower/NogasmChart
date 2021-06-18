using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using Newtonsoft.Json;
using Sentry;

namespace NogasmChart
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App
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
                    using (var reader = new StreamReader(stream ?? throw new InvalidOperationException()))
                    {
                        var res = JsonConvert.DeserializeObject<Dictionary<String, String>>(reader.ReadToEnd());
                        res.TryGetValue("sentryUrl", out sentryUrl);
                    }
                }
            }
            catch (ArgumentException e1)
            {
                Console.WriteLine(e1.Message);
            }
            catch (JsonException e1)
            {
                Console.WriteLine(e1.Message);
            }
            catch (FileLoadException e1)
            {
                Console.WriteLine(e1.Message);
            }
            catch (FileNotFoundException e1)
            {
                Console.WriteLine(e1.Message);
            }
            catch (BadImageFormatException e1)
            {
                Console.WriteLine(e1.Message);
            }
            catch (NotImplementedException e1)
            {
                Console.WriteLine(e1.Message);
            }
            catch (InvalidOperationException e1)
            {
                Console.WriteLine(e1.Message);
            }

            if (sentryUrl == null || sentryUrl.Length == 0 || MessageBox.Show(
              "A crash was detected! We'd like to send a crash report to the developers, but since this could contain sensitive information, we need your permission. Click yes send the report.",
              "Crash detected", MessageBoxButton.YesNo, MessageBoxImage.Error) != MessageBoxResult.Yes) return;
            using (SentrySdk.Init(sentryUrl))
            {
                SentrySdk.CaptureException(e.ExceptionObject as Exception);
            }
        }
    }
}
