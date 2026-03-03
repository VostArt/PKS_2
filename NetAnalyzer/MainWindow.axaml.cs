using Avalonia.Controls;
using Avalonia.Interactivity;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

namespace NetAnalyzer
{
    public partial class MainWindow : Window
    {
        private ObservableCollection<string> _history = new ObservableCollection<string>();
        private Uri? _currentUri;

        public MainWindow()
        {
            InitializeComponent();
            HistoryList.ItemsSource = _history;
        }

        private void Window_Opened(object? sender, EventArgs e)
        {
            LoadInterfaces();
        }

        private void LoadInterfaces()
        {
            try
            {
                var interfaces = NetworkInterface.GetAllNetworkInterfaces();
                InterfacesList.ItemsSource = interfaces;
                if (interfaces.Length > 0) InterfacesList.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                TxtIpInfo.Text = $"Ошибка: {ex.Message}";
            }
        }

        private void Interface_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (InterfacesList.SelectedItem is NetworkInterface nic)
            {
                TxtName.Text = nic.Name;
                TxtType.Text = nic.NetworkInterfaceType.ToString();
                try { TxtMac.Text = nic.GetPhysicalAddress().ToString(); } catch { TxtMac.Text = "N/A"; }
                TxtStatus.Text = nic.OperationalStatus.ToString();
                
                double speed = nic.Speed;
                TxtSpeed.Text = speed > 1_000_000_000 ? $"{speed / 1e9:F2} Gbps" : $"{speed / 1e6:F2} Mbps";

                var ipProps = nic.GetIPProperties();
                var sb = new StringBuilder();

                foreach (var ip in ipProps.UnicastAddresses)
                {
                    if (ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        sb.AppendLine($"IP: {ip.Address}");
                        sb.AppendLine($"Mask: {ip.IPv4Mask}");
                        sb.AppendLine("------------------");
                    }
                }
                TxtIpInfo.Text = sb.Length > 0 ? sb.ToString() : "IPv4 не найден";
            }
        }

        // Добавили '?', чтобы можно было вызывать как Analyze_Click(null, null)
        private void Analyze_Click(object? sender, RoutedEventArgs? e)
        {
            string rawUrl = UrlInput.Text?.Trim() ?? "";
            TxtPingResult.Text = ""; 

            if (string.IsNullOrEmpty(rawUrl)) return;

            if (!rawUrl.StartsWith("http") && !rawUrl.StartsWith("ftp")) rawUrl = "https://" + rawUrl;

            try
            {
                _currentUri = new Uri(rawUrl);

                ResScheme.Text = _currentUri.Scheme;
                ResHost.Text = _currentUri.Host;
                ResPort.Text = _currentUri.Port.ToString();
                ResPath.Text = _currentUri.AbsolutePath;
                ResQuery.Text = _currentUri.Query;
                ResFragment.Text = _currentUri.Fragment;

                BtnPing.IsEnabled = true;

                if (!_history.Contains(rawUrl)) _history.Insert(0, rawUrl);
            }
            catch (UriFormatException)
            {
                TxtPingResult.Text = "ОШИБКА: Некорректный формат URL.";
                BtnPing.IsEnabled = false;
            }
        }

        private async void PingDns_Click(object? sender, RoutedEventArgs? e)
        {
            if (_currentUri == null) return;
            TxtPingResult.Text = "Выполняется проверка...";
            var sb = new StringBuilder();

            try
            {
                var hostEntry = await Dns.GetHostEntryAsync(_currentUri.Host);
                sb.AppendLine($"Host: {hostEntry.HostName}");
                foreach (var ip in hostEntry.AddressList) sb.AppendLine($"IP: {ip}");
                
                using (var ping = new Ping())
                {
                    var reply = await ping.SendPingAsync(_currentUri.Host);
                    sb.AppendLine(reply.Status == IPStatus.Success 
                        ? $"Ping: {reply.RoundtripTime} ms (TTL {reply.Options?.Ttl})" 
                        : $"Ping ошибка: {reply.Status}");
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"Ошибка: {ex.Message}");
            }
            TxtPingResult.Text = sb.ToString();
        }

        private void History_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (HistoryList.SelectedItem is string url)
            {
                UrlInput.Text = url;
                Analyze_Click(null, null);
            }
        }
    }
}