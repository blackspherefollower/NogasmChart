using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using Buttplug.Client;
using Buttplug.Client.Connectors.WebsocketConnector;
using Buttplug.Core.Messages;
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
        List<double> average = new List<double>();
        List<double> presure = new List<double>();
        List<double> vibe = new List<double>();
        List<double> time = new List<double>();

        List<double> output = new List<double>();
        List<double> outtime = new List<double>();
        private string _buffer = "";
        private StreamWriter w = null;
        private string logFile = null;
        private long startTime = 0;
        private readonly Regex nogasmRegex = new Regex(@"^(-?\d+(\.\d+)?),(\d+(\.\d+)?),(\d+(\.\d+)?)$");
        private readonly Regex logRegex = new Regex(@"^(\d+):(nogasm:|user:orgasm|output:)(.*)$");
        private DateTimeOffset _last_input = DateTimeOffset.Now;
        private DateTimeOffset _last_output = DateTimeOffset.Now;

        public event EventHandler<NogasmDataPointArgs> OnNogasmDataPoint;
        public event EventHandler<OrgasmDataPointArgs> OnOrgasmDataPoint;

        private IInputAnalyser _analyser = null;
        private ButtplugClient _client;

        public MainWindow()
        {
            InitializeComponent();
            foreach (var portName in SerialPort.GetPortNames())
            {
                ComPort.Items.Add(portName);
            }

            MenuFileSave.IsEnabled = false;

            ComPort.SelectedItem = SerialPort.GetPortNames();

            // ToDo: Make this selectable
            _analyser = new NogasmMotorDirectAnalyser();
            _analyser.OutputChange += AnalyserOnOutputChange;
            OnOrgasmDataPoint += _analyser.HandleOrgasmData;
            var nogasmAnalyser = (INogasmInputAnalyser) _analyser;
            if (nogasmAnalyser != null)
            {
                OnNogasmDataPoint += nogasmAnalyser.HandleNogasmData;
            }
        }

        private void AnalyserOnOutputChange(object sender, OutputChangeArgs e)
        {
            var last = output.Last() / 1000;
            if (DateTimeOffset.Now.Subtract(_last_output).TotalMilliseconds > 100 || // throttle
                (last > 0.01 && e.Intensity < 0.01) || // stop
                Math.Abs(e.Intensity - last) > 0.25 ) // 25% change
            {
                _last_output = DateTimeOffset.Now;
                var t = _last_output.ToUnixTimeMilliseconds();
                output.Add(e.Intensity * 1000);
                outtime.Add(t - startTime);

                var text = t + ":output:" + e.Intensity;
                Console.WriteLine(text);
                w?.WriteLine(text);

                if (output.Count > 10000)
                {
                    var range = output.Count - 1000;
                    output.RemoveRange(0, range);
                    outtime.RemoveRange(0, range);
                }

                Dispatcher?.Invoke(() =>
                {
                    OutputGraph.Plot(outtime, output);
                });

                if (_client?.Connected ?? false)
                {
                    var vibes = _client.Devices.Where(d => d.AllowedMessages.ContainsKey(typeof(VibrateCmd)) && !d.Name.Contains("Vibratis")).ToList();
                    foreach (var vibe in vibes)
                    {
                        vibe.SendVibrateCmd(e.Intensity);
                    }
                    var rotators = _client.Devices.Where(d => d.AllowedMessages.ContainsKey(typeof(RotateCmd))).ToList();
                    foreach (var rotator in rotators)
                    {
                        rotator.SendRotateCmd(e.Intensity, true);
                    }
                }
            }
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
            var text = time + ":user:orgasm";
            Console.WriteLine(text);
            w?.WriteLine(text);
            var oGraph = new LineGraph();
            oGraph.Description = "Orgasm";
            Dispatcher?.Invoke(() =>
            {
                Lines.Children.Add(oGraph);
                oGraph.Plot( new double[] { time, time }, new double[] { 0, 4000 });
                OnOrgasmDataPoint?.Invoke(this, new OrgasmDataPointArgs(time));
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
                var m = nogasmRegex.Match(line);
                if (m.Success)
                {
                    average.Add(Convert.ToDouble(m.Groups[5].Value, new NumberFormatInfo()));
                    presure.Add(Convert.ToDouble(m.Groups[3].Value, new NumberFormatInfo()));
                    vibe.Add(Convert.ToDouble(m.Groups[1].Value, new NumberFormatInfo()));
                    time.Add(now);
                    OnNogasmDataPoint?.Invoke(this, new NogasmDataPointArgs(now, 
                        Convert.ToDouble(m.Groups[3].Value, new NumberFormatInfo()), 
                        Convert.ToDouble(m.Groups[5].Value, new NumberFormatInfo()), 
                        Convert.ToDouble(m.Groups[1].Value, new NumberFormatInfo())));

                    if (average.Count > 10000)
                    {
                        var range = average.Count - 1000;
                        average.RemoveRange(0, range);
                        presure.RemoveRange(0, range);
                        vibe.RemoveRange(0, range);
                        time.RemoveRange(0, range);
                    }

                }
            }

            if (DateTimeOffset.Now.Subtract(_last_input).TotalMilliseconds > 100)
            {
                _last_input = DateTimeOffset.Now;
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
            average = new List<double>();
            presure = new List<double>();
            vibe = new List<double>();
            time = new List<double>();

            output = new List<double>();
            outtime = new List<double>();

            var oGraphs = new List<LineGraph>();
            foreach (var graph in Lines.Children)
            {
                if (graph is LineGraph g && g.Description.Contains("Orgasm"))
                {
                    oGraphs.Add(g);
                }
            }

            foreach (var g in oGraphs)
            {
                Lines.Children.Remove(g);
            }

            _last_input = DateTimeOffset.Now;
            Dispatcher?.Invoke(() =>
            {
                AverageGraph.Plot(time, average);
                PressureGraph.Plot(time, presure);
                MototGraph.Plot(time, vibe);
                OutputGraph.Plot(outtime, output);
            });
        }

        private void MenuFileOpen_Click(object sender, RoutedEventArgs e)
        {
            //ToDo: Dirty check

            var open = new Microsoft.Win32.OpenFileDialog
            {
                DefaultExt = ".log",
                Filter = "Logs (.log)|*.log"
            };

            // Process save file dialog box results
            if (open.ShowDialog() == true)
            {
                if (port != null)
                {
                    StartStop_Click(sender, e);
                }

                logFile = null;
                average = new List<double>();
                presure = new List<double>();
                vibe = new List<double>();
                time = new List<double>();

                output = new List<double>();
                outtime = new List<double>();

                var oGraphs = new List<LineGraph>();
                foreach (var graph in Lines.Children)
                {
                    if (graph is LineGraph g && g.Description.Contains("Orgasm"))
                    {
                        oGraphs.Add(g);
                    }
                }

                foreach (var g in oGraphs)
                {
                    Lines.Children.Remove(g);
                }

                _last_input = DateTimeOffset.Now;
                Dispatcher?.Invoke(() =>
                {
                    AverageGraph.Plot(time, average);
                    PressureGraph.Plot(time, presure);
                    MototGraph.Plot(time, vibe);
                    OutputGraph.Plot(outtime, output);
                });

                //ToDo: Background this?
                StreamReader stream = null;
                try
                {
                    stream = new StreamReader(File.OpenRead(open.FileName));
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error on opening file: {ex.Message}", "Error Opening File", MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }

                try
                {
                    string line;
                    while ((line = stream?.ReadLine()) != null)
                    {
                        //ToDo: Abstract log event consumer
                        var m = logRegex.Match(line.ToLower(CultureInfo.CurrentCulture));
                        if (m.Success)
                        {
                            if (!long.TryParse(m.Groups[1].Value, out var t)) continue;

                            switch (m.Groups[2].Value)
                            {
                                case "nogasm:":
                                    var m2 = nogasmRegex.Match(m.Groups[3].Value);
                                    if (m2.Success)
                                    {
                                        average.Add(Convert.ToDouble(m2.Groups[5].Value, new NumberFormatInfo()));
                                        presure.Add(Convert.ToDouble(m2.Groups[3].Value, new NumberFormatInfo()));
                                        vibe.Add(Convert.ToDouble(m2.Groups[1].Value, new NumberFormatInfo()));
                                        time.Add(t);

                                        if (time.Count > 2 &&
                                            Math.Abs(average[average.Count - 2] - average[average.Count - 1]) < 0.001 &&
                                            Math.Abs(presure[presure.Count - 2] - presure[presure.Count - 1]) < 0.001 &&
                                            Math.Abs(vibe[vibe.Count - 2] - vibe[vibe.Count - 1]) < 0.001)
                                        {
                                            time.RemoveAt(time.Count - 1);
                                            average.RemoveAt(time.Count - 1);
                                            presure.RemoveAt(time.Count - 1);
                                            vibe.RemoveAt(time.Count - 1);
                                        }
                                    }

                                    break;

                                case "user:orgasm":
                                    var oGraph = new LineGraph();
                                    oGraph.Description = "Orgasm";
                                    Dispatcher?.Invoke(() =>
                                    {
                                        Lines.Children.Add(oGraph);
                                        oGraph.Plot(new double[] {t, t}, new double[] {0, 4000});
                                    });
                                    break;

                                case "output:":
                                    if (double.TryParse(m.Groups[3].Value, out var val))
                                    {
                                        output.Add(val * 1000);
                                        outtime.Add(t);
                                    }
                                    break;
                            }
                        }
                    }

                    Dispatcher?.Invoke(() =>
                    {
                        AverageGraph.Plot(time, average);
                        PressureGraph.Plot(time, presure);
                        MototGraph.Plot(time, vibe);
                        OutputGraph.Plot(outtime, output);
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error parsing file: {ex.Message}", "Error Opening File", MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }

                stream?.Close();
            }
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

        private async void Buttplug_Click(object sender, RoutedEventArgs e)
        {
            if (_client == null)
            {
                //ToDo: Make this customizable
                _client = new ButtplugClient("NogasmChart", new ButtplugWebsocketConnector(new Uri("ws://localhost:12345")));
                _client.ServerDisconnect += (o, args) =>
                {
                    _client = null;
                };
                _client.ErrorReceived += (o, args) =>
                {
                    var time = DateTimeOffset.Now.ToUnixTimeMilliseconds() - startTime;
                    var text = time + ":buttplug:" + args.Exception.ButtplugErrorMessage.ErrorCode + ": " +
                               args.Exception.ButtplugErrorMessage.ErrorMessage;
                    Console.WriteLine(text);
                };

                try
                {
                    await _client.ConnectAsync();
                    await _client?.StartScanningAsync();
                }
                catch (Exception ex)
                {
                    _client = null;
                }


            }
            else
            {
                try
                {
                    await _client?.DisconnectAsync();
                }
                finally
                {
                    _client = null;
                }
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
