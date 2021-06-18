using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace NogasmChart
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class SettingsDialog : Window
    {
        public SettingsDialog()
        {
            InitializeComponent();
            Settings.ItemsSource = NogasmChartProperties.Default.Properties;
            NogasmChartProperties.Default.PropertyChanged += (sender, args) => Settings.Items.Refresh();
        }

        private void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            // todo: implement updates
        }
    }
}
