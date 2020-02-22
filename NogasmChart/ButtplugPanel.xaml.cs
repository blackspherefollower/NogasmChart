using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Buttplug.Client;
using Buttplug.Client.Connectors;
using Buttplug.Client.Connectors.WebsocketConnector;
using Buttplug.Core;
using Buttplug.Core.Messages;
using Buttplug.Server;
using DeviceAddedEventArgs = Buttplug.Client.DeviceAddedEventArgs;

namespace NogasmChart
{

    /// <summary>
    /// Interaction logic for ButtplugPanel.xaml
    /// </summary>
    public partial class ButtplugPanel : UserControl, INotifyPropertyChanged
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
                if (Device.AllowedMessages.TryGetValue(typeof(VibrateCmd), out var vAttrs) && vAttrs.FeatureCount != null)
                {
                    for (uint i = 0; i < vAttrs.FeatureCount; i++)
                    {
                        Vibrators.Add(i, true);
                    }
                }
                if (Device.AllowedMessages.TryGetValue(typeof(RotateCmd), out var rAttrs) && rAttrs.FeatureCount != null)
                {
                    for (uint i = 0; i < rAttrs.FeatureCount; i++)
                    {
                        Rotators.Add(i, true);
                    }
                }
                if (Device.AllowedMessages.TryGetValue(typeof(LinearCmd), out var lAttrs) && lAttrs.FeatureCount != null)
                {
                    for (uint i = 0; i < lAttrs.FeatureCount; i++)
                    {
                        Linears.Add(i, true);
                    }
                }
            }

        }

        private ButtplugClient _client = null;
        internal Dictionary<uint, ButtplugPanelDevice> Devices = new Dictionary<uint, ButtplugPanelDevice>();

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
                    break;
                case "IPC":
                    ButtplugTarget.Text = "ButtplugPort";
                    ButtplugTarget.IsEnabled = true;
                    break;
                case "Embedded":
                    ButtplugTarget.Text = "";
                    ButtplugTarget.IsEnabled = false;
                    break;
                default:
                    ButtplugConnType.Text = "WebSocket";
                    break;
            }
        }

        private async void Connect_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            Connect.IsEnabled = false;

            if (_client != null)
            {
                await _client.DisconnectAsync();
                _client = null;
                Devices.Clear();

                ButtplugConnType.IsEnabled = false;
                ButtplugConnType_SelectionChanged(this, null);
                Connect.Content = "Connect";
                Connect.IsEnabled = true;
                return;
            }

            IButtplugClientConnector conn = null;
            switch (((ComboBoxItem)ButtplugConnType.SelectedValue).Content)
            {
                case "WebSocket":
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

                    ButtplugTarget.IsEnabled = false;
                    ButtplugConnType.IsEditable = false;
                    break;
                case "IPC":
                    ButtplugTarget.IsEnabled = false;
                    ButtplugConnType.IsEditable = false;
                    conn = new ButtplugClientIPCConnector(ButtplugTarget.Text);
                    break;
                case "Embedded":
                    ButtplugTarget.Text = "";
                    ButtplugTarget.IsEnabled = false;
                    ButtplugConnType.IsEditable = false;
                    //conn = new ButtplugEmbeddedConnector(new ButtplugServer());
                    //break;
                    return;
                default:
                    MessageBox.Show("Invalid Connection type!", "Buttplug Error", MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    Connect.IsEnabled = true;
                    return;
            }

            try
            {
                _client = new ButtplugClient("NogasmChart", conn);
                _client.DeviceAdded += ClientOnDeviceAdded;
                _client.DeviceRemoved += ClientOnDeviceRemoved;
                _client.ServerDisconnect += ClientOnServerDisconnect;
                _client.ErrorReceived += ClientOnErrorReceived;
                await _client.ConnectAsync();
                await _client.StartScanningAsync();
            }
            catch (Exception e1)
            {
                MessageBox.Show($"Something went wrong: {e1.Message}", "Buttplug Error", MessageBoxButton.OK,
                    MessageBoxImage.Error);
                try
                {
                    await _client?.DisconnectAsync();
                }
                catch (Exception)
                {
                }

                _client = null;

                ButtplugConnType.IsEnabled = false;
                ButtplugConnType_SelectionChanged(this, null);
                Connect.Content = "Connect";
            }

            Connect.Content = "Disconnect";
            Connect.IsEnabled = true;
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
                await _client.DisconnectAsync();
            } catch (Exception) { }

            _client = null;
            Devices.Clear();

            Dispatcher.Invoke(() =>
            {
                ButtplugConnType.IsEnabled = false;
                ButtplugConnType_SelectionChanged(this, null);
                Connect.Content = "Connect";
                Connect.IsEnabled = true;
            });
            return;
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
                if (dev.Name.Equals(e.Device.Name, StringComparison.Ordinal))
                {
                    dev.Device = e.Device;
                    dev.IsConnected = true;
                }
                else
                {
                    dev = new ButtplugPanelDevice(e.Device);
                }
            }
            else
            {
                Devices.Add(e.Device.Index, new ButtplugPanelDevice(e.Device));
            }
            Dispatcher.Invoke(() => { DevicesTree.Items.Refresh(); });
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
    }
}
