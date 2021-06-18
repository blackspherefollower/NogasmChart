using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Buttplug;


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

            public Dictionary<uint, bool> Vibrators = new Dictionary<uint, bool>();
            public Dictionary<uint, bool> Rotators = new Dictionary<uint, bool>();
            public Dictionary<uint, bool> Linears = new Dictionary<uint, bool>();
            public bool Enabled { get; set; }


            public ButtplugPanelDevice(ButtplugClientDevice aDev)
            {
                Enabled = true;
                IsConnected = true;
                Device = aDev ?? throw new ArgumentNullException(nameof(aDev));
                if (Device.AllowedMessages.TryGetValue(ServerMessage.Types.MessageAttributeType.VibrateCmd, out var vAttrs))
                {
                    for (uint i = 0; i < vAttrs.FeatureCount; i++)
                    {
                        Vibrators.Add(i, true);
                    }
                }
                if (Device.AllowedMessages.TryGetValue(ServerMessage.Types.MessageAttributeType.RotateCmd, out var rAttrs))
                {
                    for (uint i = 0; i < rAttrs.FeatureCount; i++)
                    {
                        Rotators.Add(i, true);
                    }
                }
                if (Device.AllowedMessages.TryGetValue(ServerMessage.Types.MessageAttributeType.LinearCmd, out var lAttrs))
                {
                    for (uint i = 0; i < lAttrs.FeatureCount; i++)
                    {
                        Linears.Add(i, true);
                    }
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

        public ButtplugPanel()
        {
            InitializeComponent();
            ButtplugConnType.Text = "WebSocket";
            DevicesTree.ItemsSource = Devices;
        }

        private void ButtplugConnType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            switch (((ComboBoxItem)ButtplugConnType.SelectedValue).Content)
            {
                case "WebSocket":
                    ButtplugTarget.Text = "ws://localhost:12345/buttplug";
                    ButtplugTarget.IsEnabled = true;
                    ButtplugTarget.Visibility = Visibility.Visible;
                    break;
                case "Embedded":
                    ButtplugTarget.Text = "";
                    ButtplugTarget.IsEnabled = false;
                    ButtplugTarget.Visibility = Visibility.Collapsed;
                    break;
                default:
                    ButtplugConnType.Text = "WebSocket";
                    break;
            }
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

                ButtplugConnType.IsEnabled = true;
                ButtplugConnType_SelectionChanged(this, null);
                Connect.Content = "Connect";
                Connect.IsEnabled = true;
                Scan_Click(this, null);
                return;
            }

            try
            {
                switch (((ComboBoxItem)ButtplugConnType.SelectedValue).Content)
                {
                    case "WebSocket":
                        {
                            ButtplugTarget.IsEnabled = false;
                            ButtplugConnType.IsEnabled = false;

                            ButtplugWebsocketConnectorOptions conn;
                            try
                            {
                                conn = new ButtplugWebsocketConnectorOptions(new Uri(ButtplugTarget.Text));
                            }
                            catch (UriFormatException e1)
                            {
                                MessageBox.Show($"Uri Error: {e1.Message}", "Buttplug Error", MessageBoxButton.OK,
                                    MessageBoxImage.Error);
                                Connect.IsEnabled = true;
                                ButtplugTarget.IsEnabled = true;
                                ButtplugConnType_SelectionChanged(this, null);
                                return;
                            }

                            _client = new ButtplugClient("NogasmChart");
                            _client.DeviceAdded += ClientOnDeviceAdded;
                            _client.DeviceRemoved += ClientOnDeviceRemoved;
                            _client.ServerDisconnect += ClientOnServerDisconnect;
                            _client.ErrorReceived += ClientOnErrorReceived;
                            _client.ScanningFinished += ClientOnScanFinished;
                            await _client.ConnectAsync(conn);

                            break;
                        }
                    case "Embedded":
                        {
                            ButtplugTarget.IsEnabled = false;
                            ButtplugConnType.IsEnabled = false;

                            ButtplugEmbeddedConnectorOptions conn = new ButtplugEmbeddedConnectorOptions();
                            conn.ServerName = "NogasmChart";

                            _client = new ButtplugClient("NogasmChart");
                            _client.DeviceAdded += ClientOnDeviceAdded;
                            _client.DeviceRemoved += ClientOnDeviceRemoved;
                            _client.ServerDisconnect += ClientOnServerDisconnect;
                            _client.ErrorReceived += ClientOnErrorReceived;
                            _client.ScanningFinished += ClientOnScanFinished;
                            await _client.ConnectAsync(conn);

                            break;
                        }
                    default:
                        MessageBox.Show("Invalid Connection type!", "Buttplug Error", MessageBoxButton.OK,
                            MessageBoxImage.Error);
                        Connect.IsEnabled = true;
                        return;
                }
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

                ButtplugConnType.IsEnabled = true;
                ButtplugConnType_SelectionChanged(this, null);
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
                return;
            }

            if (_client.IsScanning)
            {
                await _client.StopScanningAsync();
                Scan.Content = "Scan";
            }
            else
            {
                await _client.StartScanningAsync();
                Scan.Content = "Stop Scanning";
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
                    ButtplugConnType.IsEnabled = true;
                    ButtplugConnType_SelectionChanged(this, null);
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

        public void SendVibrateCmd(double aSpeed)
        {
            if (!(_client?.Connected ?? false)) { return; }

            foreach (var dev in Devices.Where(dev => dev.Value.Vibrators.Any() && dev.Value.Enabled && dev.Value.IsConnected))
            {
                dev.Value.Device.SendVibrateCmd(aSpeed);
            }
        }

        public void SendRotateCmd(double aSpeed, bool aClockwise)
        {
            if (!(_client?.Connected ?? false)) { return; }

            foreach (var dev in Devices.Where(dev => dev.Value.Rotators.Any() && dev.Value.Enabled && dev.Value.IsConnected))
            {
                dev.Value.Device.SendRotateCmd(aSpeed, aClockwise);
            }
        }

        public void SendLinearCmd(uint aDuration, double aPosition)
        {
            if (!(_client?.Connected ?? false)) { return; }

            foreach (var dev in Devices.Where(dev => dev.Value.Linears.Any() && dev.Value.Enabled && dev.Value.IsConnected))
            {
                dev.Value.Device.SendLinearCmd(aDuration, aPosition);
            }
        }

        public void SendOscillateCmd(double aSpeed)
        {
            // Force the range
            aSpeed = Math.Min(Math.Max(aSpeed, 0.0), 1.0);
            lock (_oscillationLock)
            {
                if (aSpeed > NogasmChartProperties.Default.LinearSpeedThreshold)
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

                var durationRange = Math.Max(NogasmChartProperties.Default.LinearDurationMax - NogasmChartProperties.Default.LinearDurationMin, 0);
                var durationMod = (uint)Math.Round((1 - _oscillationSpeed) * durationRange);
                _oscillationDirection = !_oscillationDirection;
                var duration = durationMod + NogasmChartProperties.Default.LinearDurationMin;
                SendLinearCmd(
                    (uint)Math.Round(duration / NogasmChartProperties.Default.LinearTimeMultiplier),
                    Math.Min(Math.Max(_oscillationDirection ? NogasmChartProperties.Default.LinearPositionMax :
                        NogasmChartProperties.Default.LinearPositionMin, 0.0), 1.0));
                _oscillationTimer.Change(duration, Timeout.Infinite);
            }
        }
    }
}
