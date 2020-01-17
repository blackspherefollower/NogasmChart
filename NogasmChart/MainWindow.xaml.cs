using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Forms;
using InteractiveDataDisplay.WPF;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace NogasmChart
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, IDisposable
    {
        private SerialPort port = null;
        ObservableCollection<double> average = new ObservableCollection<double>();
        ObservableCollection<double> presure = new ObservableCollection<double>();
        ObservableCollection<double> vibe = new ObservableCollection<double>();
        ObservableCollection<double> time = new ObservableCollection<double>();
        private string _buffer = "";
        private StreamWriter w = null;
        private string logFile = null;
        private long startTime = 0;
        private readonly Regex nogasmRegex = new Regex(@"^(-?\d+(\.\d+)?),(\d+(\.\d+)?),(\d+(\.\d+)?)$");
        private DateTimeOffset last = DateTimeOffset.Now;

        public MainWindow()
        {
            InitializeComponent();
            foreach (var portName in SerialPort.GetPortNames())
            {
                ComPort.Items.Add(portName);
            }

            MenuFileSave.IsEnabled = false;

            ComPort.SelectedItem = SerialPort.GetPortNames();
        }

        private void StartStop_Click(object sender, RoutedEventArgs e)
        {
            void exceptHandle(Exception ex)
            {
                ComPort.IsEnabled = true;
                MessageBox.Show("Error opening com port", ex.Message, MessageBoxButton.OK, MessageBoxImage.Error);
                Console.WriteLine($"Exception on port open: {ex.Message}\n{ex.StackTrace}");
                port?.Close();
                port = null;
                w?.WriteLine($"Exception on port open: {ex.Message}\n{ex.StackTrace}");
                w?.Close();
                w = null;
                MenuFileSave.IsEnabled = true;
                MenuFileNew.IsEnabled = true;
                MenuFileOpen.IsEnabled = true;
            }

            if (port == null)
            {
                ComPort.IsEnabled = false;
                try
                {
                    MenuFileSave.IsEnabled = false;
                    MenuFileNew.IsEnabled = false;
                    MenuFileOpen.IsEnabled = false;

                    if (logFile == null)
                    {
                        logFile = $"nogasm.{DateTimeOffset.Now.ToUnixTimeMilliseconds()}.log";
                        startTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                    }

                    w = File.AppendText(logFile);
                    port = new SerialPort((string) ComPort.SelectedItem, 115200);
                    port.DataReceived += PortOnDataReceived;
                    port.Open();
                }
                catch (IOException ex)
                {
                    exceptHandle(ex);
                }
                catch (UnauthorizedAccessException ex)
                {
                    exceptHandle(ex);
                }
                catch (InvalidOperationException ex)
                {
                    exceptHandle(ex);
                }
                catch (ArgumentException ex)
                {
                    exceptHandle(ex);
                }
            }
            else
            {
                port?.Close();
                port = null;
                w?.Flush();
                ComPort.IsEnabled = true;
                w?.Close();
                w = null;
                MenuFileSave.IsEnabled = true;
                MenuFileNew.IsEnabled = true;
                MenuFileOpen.IsEnabled = true;
            }
        }

        private void Orgasm_Click(object sender, RoutedEventArgs e)
        {
            // Record the time of orgasm
            var time = DateTimeOffset.Now.ToUnixTimeMilliseconds() - startTime;
            var text = time + ":user:Orgasm";
            Console.WriteLine(text);
            w?.WriteLine(text);
            var oGraph = new LineGraph();
            oGraph.Description = "Orgasm";
            Dispatcher?.Invoke(() =>
            {
                Lines.Children.Add(oGraph);
                oGraph.Plot( new double[] { time, time }, new double[] { 0, 4000 });
            });
        }

        private void PortOnDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            _buffer += port.ReadExisting().Replace("\r", "");
            var off = 0;
            while ((off = _buffer.IndexOf('\n')) != -1)
            {
                var line = _buffer.Substring(0, off);
                _buffer = _buffer.Substring(off+1);

                var now = DateTimeOffset.Now.ToUnixTimeMilliseconds() - startTime;
                var text = now + ":nogasm:" + line;
                Console.WriteLine(text);
                w?.WriteLine(text);
                Match m = nogasmRegex.Match(line);
                if (m.Success)
                {
                    average.Add(Convert.ToDouble(m.Groups[1].Value, new NumberFormatInfo()));
                    presure.Add(Convert.ToDouble(m.Groups[3].Value, new NumberFormatInfo()));
                    vibe.Add(Convert.ToDouble(m.Groups[5].Value, new NumberFormatInfo()));
                    time.Add(now);
                }
            }

            if (DateTimeOffset.Now.Subtract(last).TotalMilliseconds > 100)
            {
                last = DateTimeOffset.Now;
                Dispatcher?.Invoke(() =>
                {
                    AverageGraph.Plot(time, average);
                    PressureGraph.Plot(time, presure);
                    MototGraph.Plot(time, vibe);
                });
            }
        }

        private void MenuFileNew_Click(object sender, RoutedEventArgs e)
        {
            if (port != null)
            {
                StartStop_Click(sender, e);
            }

            logFile = null;
            average = new ObservableCollection<double>();
            presure = new ObservableCollection<double>();
            vibe = new ObservableCollection<double>();
            time = new ObservableCollection<double>();

            last = DateTimeOffset.Now;
            Dispatcher?.Invoke(() =>
            {
                AverageGraph.Plot(time, average);
                PressureGraph.Plot(time, presure);
                MototGraph.Plot(time, vibe);
            });
        }

        private void MenuFileOpen_Click(object sender, RoutedEventArgs e)
        {

        }

        private void MenuFileSave_Click(object sender, RoutedEventArgs e)
        {
            if (logFile == null || !File.Exists(logFile))
            {
                MessageBox.Show("No data to save!", "NogasmChart Chart", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var save = new Microsoft.Win32.SaveFileDialog
            {
                FileName = logFile,
                DefaultExt = ".log",
                Filter = "Logs (.log)|*.log"
            };

            // Process save file dialog box results
            if (save.ShowDialog() == true)
            {
                File.Copy(logFile, save.FileName, true);
            }
        }

        private void MenuHelpAbout_Click(object sender, RoutedEventArgs e)
        {
            new AboutWindow().Show();
        }

        private void MenuFileExit_Click(object sender, RoutedEventArgs e)
        {
            var result =
                MessageBox.Show(
                    "Still receiving input! Are you sure you want to quit?",
                    "Nogasm Chart",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                Application.Current.Shutdown();
            }
        }

        void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            if (port == null)
            {
                return;
            }

            var result =
                MessageBox.Show(
                    "Still receiving input! Are you sure you want to quit?",
                    "Nogasm Chart",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
            if (result == MessageBoxResult.No)
            {
                // If user doesn't want to close, cancel closure
                e.Cancel = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool full)
        {
            w?.Dispose();
            port?.Dispose();
        }
    }
}
