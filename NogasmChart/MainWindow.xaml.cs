using System;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Ports;
using System.Text.RegularExpressions;
using System.Windows;
using InteractiveDataDisplay.WPF;

namespace NogasmChart
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private SerialPort port = null;
        ObservableCollection<double> average = new ObservableCollection<double>();
        ObservableCollection<double> presure = new ObservableCollection<double>();
        ObservableCollection<double> vibe = new ObservableCollection<double>();
        ObservableCollection<double> time = new ObservableCollection<double>();
        private string _buffer = "";
        private StreamWriter w = null;
        private long startTime = 0;
        private readonly Regex nogasmRegex = new Regex(@"^(-?\d+(\.\d+)?),(\d+(\.\d+)?),(\d+(\.\d+)?)$");

        public MainWindow()
        {
            InitializeComponent();
            foreach (var portName in SerialPort.GetPortNames())
            {
                ComPort.Items.Add(portName);
            }

            ComPort.SelectedItem = SerialPort.GetPortNames();
        }

        private void StartStop_Click(object sender, RoutedEventArgs e)
        {
            if (port == null)
            {
                ComPort.IsEnabled = false;
                try
                {
                    port = new SerialPort((string) ComPort.SelectedItem, 115200);
                    port.DataReceived += PortOnDataReceived;
                    port.Open();
                    startTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                    w = File.AppendText($"nogasm.{DateTime.UtcNow.ToLongDateString()}.log");
                }
                catch (Exception ex)
                {
                    ComPort.IsEnabled = true;
                    MessageBox.Show("Error opening com port", ex.Message, MessageBoxButton.OK, MessageBoxImage.Error);
                    Console.WriteLine($"Exception on port open: {ex.Message}\n{ex.StackTrace}");
                    port?.Close();
                    port = null;
                    w?.WriteLine($"Exception on port open: {ex.Message}\n{ex.StackTrace}");
                    w.Close();
                    w = null;
                }
            }
            else
            {
                port?.Close();
                port = null;
                w.Flush();
                ComPort.IsEnabled = true;
                w.Close();
                w = null;
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
            Dispatcher.Invoke(() =>
            {
                Lines.Children.Add(oGraph);
                oGraph.Plot( new double[] { time, time }, new double[] { 0, 4000 });
            });
        }

        private void PortOnDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            Dispatcher.Invoke(() =>
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
                        average.Add(Convert.ToDouble(m.Groups[1].Value));
                        presure.Add(Convert.ToDouble(m.Groups[3].Value));
                        vibe.Add(Convert.ToDouble(m.Groups[5].Value));
                        time.Add(now);
                    }
                }

                AverageGraph.Plot(time, average);
                PressureGraph.Plot(time, presure);
                MototGraph.Plot(time, vibe);
            });
        }
    }
}
