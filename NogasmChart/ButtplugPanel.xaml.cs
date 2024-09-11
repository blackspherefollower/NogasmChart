using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using Buttplug;
using Buttplug.Client;
using Buttplug.Core;
using Buttplug.Core.Messages;


namespace NogasmChart
{

    /// <summary>
    /// Interaction logic for ButtplugPanel.xaml
    /// </summary>
    public partial class ButtplugPanel : INotifyPropertyChanged
    {
        public class ButtplugPanelDevice
        {
            public ButtplugClientDevice Device { get; internal set; }
            public string Name => Device.Name;
            public bool IsConnected { get; internal set; }

            public Dictionary<uint, (ActuatorType, bool)> Scalars = new Dictionary<uint, (ActuatorType, bool)>();
            public Dictionary<uint, bool> Rotators = new Dictionary<uint, bool>();
            public Dictionary<uint, bool> Linears = new Dictionary<uint, bool>();
            public bool Enabled { get; set; }


            public ButtplugPanelDevice(ButtplugClientDevice aDev)
            {
                Enabled = true;
                IsConnected = true;
                Device = aDev ?? throw new ArgumentNullException(nameof(aDev));
                if (Device.MessageAttributes.ScalarCmd != null)
                {
                    foreach (var a in Device.MessageAttributes.ScalarCmd)
                    {
                        Scalars.Add(a.Index, (a.ActuatorType, true));
                    }
                }

                foreach (var a in Device.RotateAttributes)
                {
                    Rotators.Add(a.Index, true);
                }
                foreach (var a in Device.LinearAttributes)
                {
                    Linears.Add(a.Index, true);
                }
            }

        }

        private ButtplugClient _client;
        internal ObservableConcurrentDictionary<uint, ButtplugPanelDevice> Devices = new ObservableConcurrentDictionary<uint, ButtplugPanelDevice>();

        // Oscillation properties
        private object _oscillationLock = new object();
        private double _oscillationSpeed;
        private Timer _oscillationTimer;
        private bool _oscillationDirection;

        public event PropertyChangedEventHandler PropertyChanged;
        public bool IsScanning { get; internal set; }

        public ButtplugPanel()
        {
            InitializeComponent();
            ButtplugTarget.Text = "ws://localhost:12345/buttplug";
            ButtplugTarget.IsEnabled = true;
            ButtplugTarget.Visibility = Visibility.Visible;
            DevicesTree.ItemsSource = Devices;
        }

        private async void Connect_Click(object sender, RoutedEventArgs e)
        {
            Connect.IsEnabled = false;

            if (_client != null)
            {
                try
                {
                    await _client.StopScanningAsync();
                }
                catch (Exception)
                {
                    // no-op: ignore failures, just stop if possible
                }

                await _client.DisconnectAsync();
                _client = null;

                var devs = Devices.Keys;
                foreach (var dev in devs)
                {
                    Devices.Remove(dev);
                }
                
                Connect.Content = "Connect";
                Connect.IsEnabled = true;
                Scan_Click(this, null);
                return;
            }

            try
            {
                ButtplugTarget.IsEnabled = false;

                IButtplugClientConnector conn;
                try
                {
                    conn = new ButtplugWebsocketConnector(new Uri(ButtplugTarget.Text));
                }
                catch (UriFormatException e1)
                {
                    MessageBox.Show($"Uri Error: {e1.Message}", "Buttplug Error", MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    Connect.IsEnabled = true;
                    return;
                }

                _client = new ButtplugClient("NogasmChart");
                _client.DeviceAdded += ClientOnDeviceAdded;
                _client.DeviceRemoved += ClientOnDeviceRemoved;
                _client.ServerDisconnect += ClientOnServerDisconnect;
                _client.ErrorReceived += ClientOnErrorReceived;
                _client.ScanningFinished += ClientOnScanFinished;
                await _client.ConnectAsync(conn);
            }
            catch (Exception e1)
            {
                MessageBox.Show($"Something went wrong: {e1.Message}", "Buttplug Error", MessageBoxButton.OK,
                    MessageBoxImage.Error);
                try
                {
                    if (_client != null)
                    {
                        await _client.DisconnectAsync();
                    }
                }
                catch (Exception)
                {
                    // no-op: cleanup only
                }

                _client = null;
                
                Connect.Content = "Connect";
                Connect.IsEnabled = true;
                Scan_Click(this, null);
                return;
            }

            Connect.Content = "Disconnect";
            Connect.IsEnabled = true;

            try
            {
                Scan.IsEnabled = false;
                await _client.StartScanningAsync();
                Scan.Content = "Stop Scanning";
                Scan.IsEnabled = true;
                IsScanning = true;
            }
            catch (Exception e1)
            {
                MessageBox.Show($"Something went wrong: {e1.Message}", "Buttplug Error", MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private async void ClientOnScanFinished(object sender, EventArgs e)
        {
            try
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    IsScanning = false;
                    Scan.Content = "Scan";
                });
            }
            catch (TaskCanceledException)
            {
                //no-op: app shutdown
            }
        }

        private async void Scan_Click(object sender, EventArgs e)
        {
            Scan.IsEnabled = false;

            if (_client == null)
            {
                Scan.Content = "Scan";
                IsScanning = false;
                return;
            }

            if (IsScanning)
            {
                await _client.StopScanningAsync();
                Scan.Content = "Scan";
                IsScanning = false;
            }
            else
            {
                await _client.StartScanningAsync();
                Scan.Content = "Stop Scanning";
                IsScanning = true;
            }

            Scan.IsEnabled = true;
        }

        private void ClientOnErrorReceived(object sender, ButtplugExceptionEventArgs e)
        {
            MessageBox.Show($"Something went wrong: {e.Exception.Message}", "Buttplug Error", MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        private async void ClientOnServerDisconnect(object sender, EventArgs e)
        {
            try
            {
                if (_client != null)
                {
                    await _client.DisconnectAsync();
                }
            }
            catch (Exception)
            {
                // no-op: we were just trying to be graceful
            }

            _client = null;
            var devs = Devices.Keys;
            foreach (var dev in devs)
            {
                Devices.Remove(dev);
            }

            try
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    Connect.Content = "Connect";
                    Connect.IsEnabled = true;
                    Scan_Click(this, null);
                });
            }
            catch (TaskCanceledException)
            {
                //no-op: app shutdown
            }
        }

        private void ClientOnDeviceRemoved(object sender, DeviceRemovedEventArgs e)
        {
            if (Devices.TryGetValue(e.Device.Index, out var dev))
            {
                dev.IsConnected = false;
            }
            Dispatcher.Invoke(() => { DevicesTree.Items.Refresh(); });
        }

        private void ClientOnDeviceAdded(object sender, DeviceAddedEventArgs e)
        {
            if (Devices.TryGetValue(e.Device.Index, out var dev))
            {
                if (!dev.Name.Equals(e.Device.Name, StringComparison.Ordinal))
                {
                    Devices.Remove(e.Device.Index);
                    Devices.Add(e.Device.Index, new ButtplugPanelDevice(e.Device));
                    return;
                }

                dev.Device = e.Device;
                dev.IsConnected = true;
            }
            else
            {
                Devices.Add(e.Device.Index, new ButtplugPanelDevice(e.Device));
            }
        }

        public void SendScalarCmd(double aSpeed)
        {
            if (!(_client?.Connected ?? false)) { return; }
            
            foreach (var dev in Devices.Where(dev => dev.Value.Scalars.Any() && dev.Value.Enabled && dev.Value.IsConnected))
            {
                var subCmds = dev.Value.Scalars.Where((attr) => attr.Value.Item2).Select((attr) =>
                    new ScalarCmd.ScalarSubcommand(attr.Key, aSpeed, attr.Value.Item1)).ToList();
                if (subCmds.Any())
                {
                    dev.Value.Device.ScalarAsync(subCmds);
                }
            }
        }

        public void SendRotateCmd(double aSpeed, bool aClockwise)
        {
            if (!(_client?.Connected ?? false)) { return; }
            
            foreach (var dev in Devices.Where(dev => dev.Value.Rotators.Any() && dev.Value.Enabled && dev.Value.IsConnected))
            {
                dev.Value.Device.RotateAsync(aSpeed, aClockwise);
            }
        }

        public void SendLinearCmd(uint aDuration, double aPosition)
        {
            if (!(_client?.Connected ?? false)) { return; }
            
            foreach (var dev in Devices.Where(dev => dev.Value.Linears.Any() && dev.Value.Enabled && dev.Value.IsConnected))
            {
                dev.Value.Device.LinearAsync(aDuration, aPosition);
            }
        }

        public void SendOscillateCmd(double aSpeed)
        {
            // Force the range
            aSpeed = Math.Min(Math.Max(aSpeed, 0.0), 1.0);
            lock (_oscillationLock)
            {
                if (aSpeed > NogasmChartProperties.Default["LinearSpeedThreshold"]?.DoubleValue)
                {
                    _oscillationSpeed = aSpeed;
                    if (_oscillationTimer == null)
                    {
                        _oscillationTimer = new Timer(OscillateTimerCallback, null, 0, Timeout.Infinite);
                    }
                }
                else
                {
                    _oscillationSpeed = 0;
                    _oscillationTimer?.Change(Timeout.Infinite, Timeout.Infinite);
                    _oscillationTimer = null;
                }
            }
        }

        private void OscillateTimerCallback(object state)
        {
            lock (_oscillationLock)
            {
                if (_client == null)
                    _oscillationTimer = null;
                if (_oscillationTimer == null)
                    return;

                var durationRange = Math.Max(NogasmChartProperties.Default["LinearDurationMax"].IntValue - NogasmChartProperties.Default["LinearDurationMin"].IntValue, 0);
                var durationMod = (uint)Math.Round((1.0 - _oscillationSpeed) * durationRange);
                _oscillationDirection = !_oscillationDirection;
                var duration = durationMod + NogasmChartProperties.Default["LinearDurationMin"].IntValue;

                SendLinearCmd(
                    (uint)duration,
                    Math.Min(Math.Max(_oscillationDirection ? NogasmChartProperties.Default["LinearPositionMax"].DoubleValue :
                        NogasmChartProperties.Default["LinearPositionMin"].DoubleValue, 0.0), 1.0));
                _oscillationTimer.Change((long)Math.Round(duration * NogasmChartProperties.Default["LinearDurationDelayMultiplier"].DoubleValue), Timeout.Infinite);
            }
        }
    }
}
