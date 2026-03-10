using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace HttpMonitorApp
{
    public class RequestLog
    {
        public DateTime Timestamp { get; set; }
        public string TimestampString => Timestamp.ToString("HH:mm:ss.fff");
        public string Method { get; set; } = "";
        public string Url { get; set; } = "";
        public int StatusCode { get; set; }
        public long ProcessingTimeMs { get; set; }
        public string Details { get; set; } = "";
    }

    public class ChartBar
    {
        public int Count { get; set; }
        public double Height => Count * 10;
        public string Label { get; set; } = "";
    }

    public partial class MainWindow : Window
    {
        // ЯВНО ОБЪЯВЛЯЕМ ЭЛЕМЕНТЫ ИНТЕРФЕЙСА
        private DataGrid _logsGrid;
        private ItemsControl _chartItems;

        private HttpListener? _listener;
        private bool _isServerRunning = false;
        private DateTime _serverStartTime;
        private ConcurrentBag<string> _savedMessages = new();
        private readonly string _logFilePath = "logs.txt";
        private readonly object _fileLock = new object();

        private ObservableCollection<RequestLog> _logs = new();
        private ObservableCollection<ChartBar> _chartData = new();
        private int _totalGets = 0;
        private int _totalPosts = 0;
        private long _totalProcessingTime = 0;

        private static readonly HttpClient _httpClient = new HttpClient();

        public MainWindow()
        {
            InitializeComponent();
            
            // Находим элементы из XAML
            _logsGrid = this.FindControl<DataGrid>("LogsGrid")!;
            _chartItems = this.FindControl<ItemsControl>("ChartItems")!;
            
            // Привязываем данные
            _logsGrid.ItemsSource = _logs;
            _chartItems.ItemsSource = _chartData;
            
            var filterMethod = this.FindControl<ComboBox>("FilterMethod");
            var filterStatus = this.FindControl<ComboBox>("FilterStatus");
            if (filterMethod != null) filterMethod.SelectedIndex = 0;
            if (filterStatus != null) filterStatus.SelectedIndex = 0;
        }

        private void StartServer_Click(object? sender, RoutedEventArgs e)
        {
            string port = this.FindControl<TextBox>("PortInput")?.Text ?? "8080";
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://localhost:{port}/");

            try
            {
                _listener.Start();
                _isServerRunning = true;
                _serverStartTime = DateTime.Now;

                this.FindControl<Button>("BtnStartServer")!.IsEnabled = false;
                this.FindControl<Button>("BtnStopServer")!.IsEnabled = true;
                this.FindControl<TextBlock>("ServerStatusTxt")!.Text = $"Статус: Запущен (порт {port})";

                Task.Run(ListenLoopAsync);
            }
            catch (Exception ex)
            {
                this.FindControl<TextBlock>("ServerStatusTxt")!.Text = $"Ошибка: {ex.Message}";
            }
        }

        private void StopServer_Click(object? sender, RoutedEventArgs e)
        {
            _isServerRunning = false;
            _listener?.Stop();
            _listener?.Close();

            this.FindControl<Button>("BtnStartServer")!.IsEnabled = true;
            this.FindControl<Button>("BtnStopServer")!.IsEnabled = false;
            this.FindControl<TextBlock>("ServerStatusTxt")!.Text = "Статус: Остановлен";
        }

        private async Task ListenLoopAsync()
        {
            while (_isServerRunning)
            {
                try
                {
                    var context = await _listener!.GetContextAsync();
                    _ = Task.Run(() => ProcessRequestAsync(context));
                }
                catch (HttpListenerException) { /* Игнорируем при остановке */ }
            }
        }

        private async Task ProcessRequestAsync(HttpListenerContext context)
        {
            var sw = Stopwatch.StartNew();
            var req = context.Request;
            var res = context.Response;
            string details = "";
            int statusCode = 200;

            try
            {
                res.AddHeader("Access-Control-Allow-Origin", "*");

                if (req.HttpMethod == "GET")
                {
                    TimeSpan uptime = DateTime.Now - _serverStartTime;
                    string responseJson = $"{{ \"uptime_seconds\": {(int)uptime.TotalSeconds}, \"requests\": {_logs.Count} }}";
                    
                    byte[] buffer = Encoding.UTF8.GetBytes(responseJson);
                    res.ContentType = "application/json";
                    res.ContentLength64 = buffer.Length;
                    await res.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                    
                    details = "Статистика отправлена";
                }
                else if (req.HttpMethod == "POST")
                {
                    using var reader = new StreamReader(req.InputStream, req.ContentEncoding);
                    string body = await reader.ReadToEndAsync();
                    
                    string uniqueId = Guid.NewGuid().ToString();
                    _savedMessages.Add(body);

                    string responseJson = $"{{ \"status\": \"success\", \"id\": \"{uniqueId}\" }}";
                    byte[] buffer = Encoding.UTF8.GetBytes(responseJson);
                    res.ContentType = "application/json";
                    res.ContentLength64 = buffer.Length;
                    await res.OutputStream.WriteAsync(buffer, 0, buffer.Length);

                    details = $"Сохранено: {body.Replace(Environment.NewLine, " ")}";
                }
                else
                {
                    statusCode = 405;
                    res.StatusCode = statusCode;
                    details = "Метод не поддерживается";
                }
            }
            catch (Exception ex)
            {
                statusCode = 500;
                res.StatusCode = statusCode;
                details = $"Ошибка: {ex.Message}";
            }
            finally
            {
                res.Close();
                sw.Stop();
            }

            var log = new RequestLog
            {
                Timestamp = DateTime.Now,
                Method = req.HttpMethod ?? "UNKNOWN",
                Url = req.RawUrl ?? "/",
                StatusCode = statusCode,
                ProcessingTimeMs = sw.ElapsedMilliseconds,
                Details = details
            };

            await SaveLogAndUpdateUI(log);
        }

        private async Task SaveLogAndUpdateUI(RequestLog log)
        {
            string logLine = $"[{log.TimestampString}] {log.Method} {log.Url} | Статус: {log.StatusCode} | Время: {log.ProcessingTimeMs}мс | Детали: {log.Details}";
            lock (_fileLock)
            {
                File.AppendAllText(_logFilePath, logLine + Environment.NewLine);
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _logs.Insert(0, log);

                if (log.Method == "GET") _totalGets++;
                if (log.Method == "POST") _totalPosts++;
                _totalProcessingTime += log.ProcessingTimeMs;

                this.FindControl<TextBlock>("StatTotalTxt")!.Text = $"Всего запросов: {_logs.Count}";
                this.FindControl<TextBlock>("StatGetTxt")!.Text = $"GET запросов: {_totalGets}";
                this.FindControl<TextBlock>("StatPostTxt")!.Text = $"POST запросов: {_totalPosts}";
                this.FindControl<TextBlock>("StatTimeTxt")!.Text = $"Ср. время обработки: {(_totalProcessingTime / _logs.Count)} мс";

                UpdateChart();
                ApplyFilters();
            });
        }

        private void UpdateChart()
        {
            var grouped = _logs.GroupBy(l => l.Timestamp.ToString("HH:mm"))
                               .OrderBy(g => g.Key)
                               .TakeLast(10);
            
            _chartData.Clear();
            foreach (var group in grouped)
            {
                _chartData.Add(new ChartBar { Count = group.Count(), Label = group.Key });
            }
        }

        private void Filter_Changed(object? sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void ApplyFilters()
        {
            if (_logs == null || _logsGrid == null) return;
            
            var filterMethod = this.FindControl<ComboBox>("FilterMethod")?.SelectedIndex ?? 0;
            var filterStatus = this.FindControl<ComboBox>("FilterStatus")?.SelectedIndex ?? 0;

            var filtered = _logs.AsEnumerable();

            if (filterMethod == 1) filtered = filtered.Where(l => l.Method == "GET");
            if (filterMethod == 2) filtered = filtered.Where(l => l.Method == "POST");

            if (filterStatus == 1) filtered = filtered.Where(l => l.StatusCode == 200);
            if (filterStatus == 2) filtered = filtered.Where(l => l.StatusCode != 200);

            _logsGrid.ItemsSource = filtered.ToList();
        }

        private async void ClientSend_Click(object? sender, RoutedEventArgs e)
        {
            var resBox = this.FindControl<TextBox>("ClientResponseTxt")!;
            resBox.Text = "Отправка запроса...";

            string url = this.FindControl<TextBox>("ClientUrlInput")?.Text ?? "";
            string method = ((ComboBoxItem)this.FindControl<ComboBox>("ClientMethodCombo")!.SelectedItem!).Content?.ToString() ?? "GET";
            string body = this.FindControl<TextBox>("ClientBodyInput")?.Text ?? "";

            try
            {
                HttpResponseMessage response;

                if (method == "GET")
                {
                    response = await _httpClient.GetAsync(url);
                }
                else
                {
                    var content = new StringContent(body, Encoding.UTF8, "application/json");
                    response = await _httpClient.PostAsync(url, content);
                }

                string responseBody = await response.Content.ReadAsStringAsync();
                
                var sb = new StringBuilder();
                sb.AppendLine($"СТАТУС: {(int)response.StatusCode} {response.ReasonPhrase}");
                sb.AppendLine("--- ЗАГОЛОВКИ ---");
                foreach (var header in response.Headers)
                {
                    sb.AppendLine($"{header.Key}: {string.Join(", ", header.Value)}");
                }
                sb.AppendLine("--- ТЕЛО ОТВЕТА ---");
                sb.AppendLine(responseBody);

                resBox.Text = sb.ToString();
            }
            catch (Exception ex)
            {
                resBox.Text = $"ОШИБКА КЛИЕНТА:\n{ex.Message}";
            }
        }
    }
}