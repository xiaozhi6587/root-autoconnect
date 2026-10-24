using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Microsoft.Win32;

namespace UsbAutoConnect;

public partial class MainWindow : MacWindow
{
    private readonly Dictionary<string, DateTimeOffset> _nextAttempt = [];
    private readonly ObservableCollection<LogEntry> _logEntries = [];
    private CancellationTokenSource? _monitor;
    private Task? _loop;
    private Task? _stopTask;
    private CancellationTokenSource? _discovery;
    private bool _busy, _closing;
    private int _requests;
    private string _lastError = "";
    private Settings _settings;
    private IReadOnlyList<RemoteDevice> _devices = [];
    public MainWindow() : this(Settings.Load()) { }
    public MainWindow(Settings settings)
    {
        _settings = settings;
        InitializeComponent();
        UpdateThemeIcon(false);
        LogBox.ItemsSource = _logEntries;
        PathBox.Text = _settings.ToolPath;
        IntervalBox.Text = Compatibility.Clamp(_settings.IntervalSeconds, 1, 60).ToString();
        RetryBox.Text = Compatibility.Clamp(_settings.RetrySeconds, 5, 600).ToString();
        FilterBox.Text = _settings.Filter;
        ShowVersion();
        ManageButton.Content = $"排除管理 ({_settings.Exclusions.Count})";
    }
    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        Log("就绪。仅扫描不会连接设备；点击“开始自动连接”后生效。");
        if (await LocateAsync(false)) await ScanOnceAsync();
    }
    private async void Locate_Click(object sender, RoutedEventArgs e) => await LocateAsync(true);
    private async Task<bool> EnsureToolAsync() => ToolLocator.FindExisting(new[] { PathBox.Text }) != null || await LocateAsync(false);
    private async Task<bool> LocateAsync(bool force)
    {
        if (_busy || _monitor != null) return false;
        _busy = true;
        Configuration.IsEnabled = StartButton.IsEnabled = ScanButton.IsEnabled = false;
        using var discovery = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        _discovery = discovery;
        try
        {
            VersionText.Text = "正在自动定位技师端…";
            var path = await ToolLocator.FindAsync(force ? null : PathBox.Text.Trim(), discovery.Token);
            if (path == null)
            {
                SettingsExpander.IsExpanded = true;
                Log("未找到技师端命令行，请先安装或运行技师端，再点“自动定位”；也可手动浏览。");
                return false;
            }
            PathBox.Text = path;
            _settings.ToolPath = path;
            try { _settings.Save(); } catch (Exception ex) { Log("定位路径保存失败：" + ex.Message); }
            Log("已定位技师端：" + path);
            return true;
        }
        catch (OperationCanceledException)
        {
            SettingsExpander.IsExpanded = true;
            Log("自动定位已停止。可运行技师端后重试，或手动浏览安装目录。");
            return false;
        }
        catch (Exception ex) { Log("自动定位失败：" + ex.Message); SettingsExpander.IsExpanded = true; return false; }
        finally
        {
            _discovery = null; _busy = false;
            Configuration.IsEnabled = StartButton.IsEnabled = ScanButton.IsEnabled = true;
            ShowVersion();
        }
    }
    private void Theme_Click(object sender, RoutedEventArgs e)
    {
        ThemeManager.Apply(!ThemeManager.IsDark);
        UpdateThemeIcon(true);
        _settings.Theme = ThemeManager.IsDark ? "Dark" : "Light";
        try { _settings.Save(); } catch (Exception ex) { Log("主题设置保存失败：" + ex.Message); }
    }
    private void UpdateThemeIcon(bool animate)
    {
        var dark = ThemeManager.IsDark;
        var duration = TimeSpan.FromMilliseconds(animate && SystemParameters.ClientAreaAnimation ? 240 : 0);
        var angle = ThemeRotation.Angle;
        ThemeRotation.BeginAnimation(RotateTransform.AngleProperty, null);
        ThemeRotation.Angle = animate ? angle + 180 : 0;
        if (animate && duration > TimeSpan.Zero)
            ThemeRotation.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(angle, angle + 180, duration)
            { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }, FillBehavior = FillBehavior.Stop });
        void Fade(UIElement icon, double opacity)
        {
            var from = icon.Opacity;
            icon.BeginAnimation(UIElement.OpacityProperty, null);
            icon.Opacity = opacity;
            if (duration > TimeSpan.Zero) icon.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(from, opacity, duration) { FillBehavior = FillBehavior.Stop });
        }
        Fade(MoonIcon, dark ? 0 : 1); Fade(SunIcon, dark ? 1 : 0);
        var label = dark ? "切换浅色模式" : "切换深色模式";
        ThemeButton.ToolTip = label;
        System.Windows.Automation.AutomationProperties.SetName(ThemeButton, label);
    }
    private void Settings_Expanded(object sender, RoutedEventArgs e)
    {
        if (LogExpander != null) LogExpander.IsExpanded = false;
    }
    private void Log_Expanded(object sender, RoutedEventArgs e)
    {
        SettingsExpander.IsExpanded = false;
    }
    private void ShowVersion()
    {
        VersionText.Text = ToolLocator.DescribeInstallation(PathBox.Text);
    }
    private bool ReadSettings()
    {
        if (!File.Exists(PathBox.Text.Trim()) || !Path.GetFileName(PathBox.Text.Trim()).Equals("usbtechsh.exe", StringComparison.OrdinalIgnoreCase))
        { SettingsExpander.IsExpanded = true; MacDialog.Show(this, "请选择技师版安装目录中的 usbtechsh.exe。", "程序路径无效"); return false; }
        if (!int.TryParse(IntervalBox.Text, out var interval) || interval < 1 || interval > 60
            || !int.TryParse(RetryBox.Text, out var retry) || retry < 5 || retry > 600)
        { SettingsExpander.IsExpanded = true; MacDialog.Show(this, "扫描间隔应为 1–60 秒，重试间隔应为 5–600 秒。", "检查设置"); return false; }
        _settings = new Settings { ToolPath = PathBox.Text.Trim(), IntervalSeconds = interval, RetrySeconds = retry, Filter = FilterBox.Text.Trim(), Exclusions = _settings.Exclusions, Theme = _settings.Theme };
        try { _settings.Save(); } catch (Exception ex) { Log("设置保存失败：" + ex.Message); }
        ShowVersion();
        return true;
    }
    private async void StartStop_Click(object sender, RoutedEventArgs e)
    {
        if (_monitor != null) { await StopAsync(); return; }
        if (_busy || !await EnsureToolAsync() || !ReadSettings()) return;
        _nextAttempt.Clear(); _lastError = "";
        _stopTask = null;
        _monitor = new CancellationTokenSource();
        Configuration.IsEnabled = false; ScanButton.IsEnabled = false;
        StartButton.Content = "■  停止自动连接";
        SetMonitorState("● 自动连接中", "#18713D", "#E2F4E8");
        StartButton.Background = ThemeManager.Brush("#D84949");
        StartButton.BorderBrush = StartButton.Background;
        Log($"已开始监控，每 {_settings.IntervalSeconds} 秒扫描；筛选：{(_settings.Filter.Length == 0 ? "全部设备" : _settings.Filter)}。");
        _loop = MonitorAsync(_monitor.Token);
        await _loop;
    }
    private async Task MonitorAsync(CancellationToken token)
    {
        var client = new RedirectorClient(_settings.ToolPath);
        try
        {
            while (true)
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    var devices = await client.ListAsync(token);
                    UpdateDevices(devices);
                    var liveKeys = devices.Select(d => d.Identity).ToHashSet();
                    foreach (var key in _nextAttempt.Keys.Where(k => !liveKeys.Contains(k)).ToArray()) _nextAttempt.Remove(key);
                    foreach (var device in devices.Where(d => _settings.CanAutoConnect(d)))
                    {
                        token.ThrowIfCancellationRequested();
                        if (_nextAttempt.TryGetValue(device.Identity, out var next) && next > DateTimeOffset.UtcNow) continue;
                        // Refresh each target immediately before connecting; never use stale server/device IDs from an earlier cycle.
                        var current = (await client.ListAsync(token)).FirstOrDefault(d => d.Id == device.Id && d.Identity == device.Identity);
                        // Exclusion rules can change while the list command is awaiting its response.
                        if (current == null || !_settings.CanAutoConnect(current)) continue;
                        _requests++; RequestText.Text = $"请求 {_requests} 次";
                        _nextAttempt[device.Identity] = DateTimeOffset.UtcNow.AddSeconds(_settings.RetrySeconds);
                        Log($"连接请求 → [{device.Id}] {device.Name}");
                        try { await client.ConnectAsync(current, token); Log($"[{device.Id}] 命令已受理，等待设备状态确认。"); }
                        catch (Exception ex) when (ex is not OperationCanceledException) { Log($"[{device.Id}] 连接失败，{_settings.RetrySeconds} 秒后可重试：{ex.Message}"); }
                    }
                    var refreshed = await client.ListAsync(token);
                    foreach (var d in refreshed.Where(d => d.Connected && _nextAttempt.ContainsKey(d.Identity)))
                    { Log($"已确认连接 → [{d.Id}] {d.Name}"); _nextAttempt.Remove(d.Identity); }
                    UpdateDevices(refreshed);
                    _lastError = "";
                    SetMonitorState("● 自动连接中", "#18713D", "#E2F4E8");
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    if (_lastError != ex.Message) { Log("扫描失败：" + ex.Message); _lastError = ex.Message; }
                    SetMonitorState("● 扫描失败 · 等待重试", "#C12B30", "#FDEBED");
                }
                await Task.Delay(TimeSpan.FromSeconds(_settings.IntervalSeconds), token);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    }
    private Task StopAsync() => _stopTask ??= StopCoreAsync();
    private async Task StopCoreAsync()
    {
        if (_monitor == null) return;
        StartButton.IsEnabled = false;
        _monitor.Cancel();
        if (_loop != null) await _loop;
        _monitor.Dispose(); _monitor = null; _loop = null;
        StartButton.Content = "▶  开始自动连接"; StartButton.IsEnabled = true;
        Configuration.IsEnabled = true; ScanButton.IsEnabled = true;
        SetMonitorState("● 已暂停", "#6E6E73", "#E8E9ED");
        StartButton.Background = ThemeManager.Brush("#007AFF");
        StartButton.BorderBrush = StartButton.Background;
        Log("已停止扫描和后续连接请求。已建立的连接继续保留；已发送的请求可能仍由技师端完成。");
    }
    private async void Scan_Click(object sender, RoutedEventArgs e) => await ScanOnceAsync();
    private async Task ScanOnceAsync()
    {
        if (_busy || _monitor != null || !await EnsureToolAsync() || !ReadSettings()) return;
        _busy = true; ScanButton.IsEnabled = StartButton.IsEnabled = Configuration.IsEnabled = false;
        try { var devices = await new RedirectorClient(_settings.ToolPath).ListAsync(CancellationToken.None); UpdateDevices(devices); Log($"扫描完成：{devices.Count} 个设备，{devices.Count(d => d.Connected)} 个已连接。"); }
        catch (Exception ex) { Log("扫描失败：" + ex.Message); }
        finally { _busy = false; ScanButton.IsEnabled = StartButton.IsEnabled = Configuration.IsEnabled = true; }
    }
    private void UpdateDevices(IReadOnlyList<RemoteDevice> devices)
    {
        _devices = devices;
        var selectedIdentity = (DevicesGrid.SelectedItem as RemoteDevice)?.Identity;
        var rows = devices.Select(d => d with { IsExcluded = _settings.IsExcluded(d) }).ToArray();
        DevicesGrid.ItemsSource = rows;
        DevicesGrid.SelectedItem = rows.FirstOrDefault(d => d.Identity == selectedIdentity);
        TotalText.Text = devices.Count.ToString(); ConnectedText.Text = devices.Count(d => d.Connected).ToString();
        AvailableText.Text = devices.Count(d => _settings.CanAutoConnect(d)).ToString();
        ManageButton.Content = $"排除管理 ({_settings.Exclusions.Count})";
        UpdatedText.Text = $"更新于 {DateTime.Now:HH:mm:ss}";
        EmptyText.Visibility = devices.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }
    private void DeviceRow_RightClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is DataGridRow row) row.IsSelected = true;
    }
    private void DeviceRow_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not DataGridRow row || row.ContextMenu != null) return;
        var menu = new ContextMenu();
        var toggle = new MenuItem { ToolTip = "控制助手后续是否自动连接，已建立的连接会保留。" };
        toggle.Click += ToggleExclusion_Click;
        var details = new MenuItem { Header = "查看设备信息" };
        details.Click += DeviceDetails_Click;
        var manage = new MenuItem { Header = "排除设备管理…" };
        manage.Click += ManageExclusions_Click;
        menu.Items.Add(toggle); menu.Items.Add(new Separator()); menu.Items.Add(details); menu.Items.Add(manage);
        menu.Opened += (_, _) =>
        {
            // Capture the current row when opening, including when WPF recycles a row after a scan.
            menu.DataContext = row.DataContext;
            if (row.DataContext is RemoteDevice device)
                toggle.Header = _settings.IsExcluded(device) ? "恢复自动连接此设备" : "不自动连接此设备";
        };
        row.ContextMenu = menu;
    }
    private void ToggleExclusion_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as MenuItem)?.DataContext is not RemoteDevice device) return;
        var wasExcluded = _settings.IsExcluded(device);
        var rules = _settings.Exclusions.Where(rule => !rule.Matches(device)).ToList();
        if (!wasExcluded) rules.Add(DeviceExclusion.FromDevice(device));
        if (SaveExclusions(rules)) Log($"{(wasExcluded ? "已恢复自动连接" : "已设为不自动连接")} → {device.Name}。已建立的连接保留。");
    }
    private bool SaveExclusions(List<DeviceExclusion> rules)
    {
        var previous = _settings.Exclusions;
        _settings.Exclusions = rules;
        try { _settings.Save(); }
        catch (Exception ex)
        {
            _settings.Exclusions = previous;
            Log("排除名单保存失败：" + ex.Message);
            MacDialog.Show(this, "排除名单未保存，原有规则继续生效。\n" + ex.Message, "保存失败");
            return false;
        }
        UpdateDevices(_devices);
        return true;
    }
    private void ManageExclusions_Click(object sender, RoutedEventArgs e)
    {
        var manager = new ExclusionsWindow(_settings.Exclusions, rules =>
        {
            if (!SaveExclusions(rules)) return false;
            Log("已更新排除名单，当前排除 " + rules.Count + " 个设备。");
            return true;
        }) { Owner = this };
        manager.ShowDialog();
    }
    private void DeviceDetails_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as MenuItem)?.DataContext is not RemoteDevice device) return;
        MacDialog.Show(this, $"设备：{device.Name}\n编号：{device.Id}\n远程地址：{device.Server}\nVID:PID：{device.Hardware}\n序列号：{device.Serial}\nUSB 端口：{device.Port}\n状态：{device.DisplayStatus}\n策略：{(_settings.IsExcluded(device) ? "不自动连接" : "允许自动连接")}", "设备信息");
    }
    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "USB Redirector 命令行|usbtechsh.exe", FileName = "usbtechsh.exe", CheckFileExists = true };
        if (dialog.ShowDialog(this) == true) { PathBox.Text = dialog.FileName; ShowVersion(); }
    }
    private async void Raw_Click(object sender, RoutedEventArgs e)
    {
        if (_monitor != null || _busy) { MacDialog.Show(this, "请先停止自动连接，待扫描结束后查看原始输出。", "原始输出"); return; }
        if (!await EnsureToolAsync() || !ReadSettings()) return;
        _busy = true; StartButton.IsEnabled = ScanButton.IsEnabled = false;
        try
        {
            var raw = await new RedirectorClient(_settings.ToolPath).RunAsync(CancellationToken.None, "-list", "devices");
            new MacWindow { Owner = this, Title = "USB Redirector 原始输出（只读）", Width = 760, Height = 420, WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = new TextBox { Text = raw, IsReadOnly = true, AcceptsReturn = true, FontFamily = new FontFamily("Consolas"), FontSize = 13, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto } }.ShowDialog();
        }
        catch (Exception ex) { Log(ex.Message); }
        finally { _busy = false; StartButton.IsEnabled = ScanButton.IsEnabled = true; }
    }
    private void Logs_Click(object sender, RoutedEventArgs e)
    {
        try { Directory.CreateDirectory(Settings.DataDirectory); Process.Start(new ProcessStartInfo("explorer.exe", Settings.DataDirectory) { UseShellExecute = true }); }
        catch (Exception ex) { Log("打开日志失败：" + ex.Message); }
    }
    private void Log(string text)
    {
        var entry = LogEntry.Create(text);
        var line = $"[{entry.Time}] {text}";
        LatestLogText.Text = line.Replace(Environment.NewLine, " ");
        LatestLogText.Foreground = ThemeManager.Brush(entry.Color);
        LatestLogText.ToolTip = line;
        _logEntries.Add(entry); while (_logEntries.Count > 150) _logEntries.RemoveAt(0);
        LogBox.ScrollIntoView(entry);
        try { Directory.CreateDirectory(Settings.DataDirectory); File.AppendAllText(Path.Combine(Settings.DataDirectory, $"{DateTime.Now:yyyy-MM-dd}.log"), line + Environment.NewLine); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
    private void SetMonitorState(string text, string foreground, string background)
    {
        StateText.Text = text;
        StateText.Foreground = ThemeManager.Brush(foreground);
        StateBadge.Background = ThemeManager.Brush(background);
    }
    private async void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_closing) return;
        _discovery?.Cancel();
        if (_busy) { e.Cancel = true; Log("正在读取设备，请等待本轮扫描结束后关闭。"); return; }
        if (_monitor == null) return;
        e.Cancel = true;
        await StopAsync(); _closing = true; Close();
    }
}

