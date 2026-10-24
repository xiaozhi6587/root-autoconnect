using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Xml.Linq;
using UsbAutoConnect;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        try { Run(args); return 0; } catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
    private static void Run(string[] args)
    {
        // Render the actual WPF windows with documentation-only sample data. Never read customer devices or save user settings.
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var output = args.Length > 0 ? Path.GetFullPath(args[0]) : Path.Combine(root, "docs", "screenshots");
        Directory.CreateDirectory(output);
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var source = XDocument.Load(Path.Combine(root, "UsbAutoConnect", "App.xaml"));
        var dictionary = new XElement(presentation + "ResourceDictionary",
            new XAttribute(XNamespace.Xmlns + "x", "http://schemas.microsoft.com/winfx/2006/xaml"),
            new XAttribute(XNamespace.Xmlns + "local", "clr-namespace:UsbAutoConnect;assembly=RootAutoConnect"),
            new XAttribute(XNamespace.Xmlns + "shell", "clr-namespace:System.Windows.Shell;assembly=PresentationFramework"),
            source.Root!.Element(presentation + "Application.Resources")!.Elements());
        app.Resources = (ResourceDictionary)XamlReader.Parse(dictionary.ToString().Replace("clr-namespace:UsbAutoConnect\"", "clr-namespace:UsbAutoConnect;assembly=RootAutoConnect\""));
        ThemeManager.Initialize(false);
        var devices = new[]
        {
            new RemoteDevice("1", "1", "192.0.2.10:32038", "Android Device", "18d1", "4ee7", "DEMO-001", "1-1", "manual-connect", "connected", "connected"),
            new RemoteDevice("1", "2", "192.0.2.10:32038", "USB Serial Adapter", "0403", "6001", "DEMO-002", "1-2", "manual-connect", "available for connection", "connected"),
            new RemoteDevice("2", "1", "192.0.2.20:32038", "USB Debug Device", "1a86", "7523", "DEMO-003", "2-1", "manual-connect", "available for connection", "connected")
        };
        var settings = new Settings { Exclusions = new() { DeviceExclusion.FromDevice(devices[2]) } };
        var window = new MainWindow(settings) { Width = 900, Height = 460, Left = -10000, Top = -10000, WindowStartupLocation = WindowStartupLocation.Manual, ShowActivated = false, ShowInTaskbar = false };
        // Disable the production Loaded handler before showing this separate documentation process.
        var loaded = typeof(MainWindow).GetMethod("Window_Loaded", BindingFlags.Instance | BindingFlags.NonPublic)!;
        window.Loaded -= (RoutedEventHandler)Delegate.CreateDelegate(typeof(RoutedEventHandler), window, loaded);
        typeof(MainWindow).GetMethod("UpdateDevices", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, new object[] { devices });
        T Find<T>(string name) where T : FrameworkElement => (T)window.FindName(name);
        Find<TextBlock>("StateText").Text = "● 自动连接中";
        Find<TextBlock>("StateText").Foreground = ThemeManager.Brush("#18713D");
        Find<Border>("StateBadge").Background = ThemeManager.Brush("#E2F4E8");
        Find<Button>("StartButton").Content = "■ 停止自动连接";
        Find<Button>("StartButton").Background = ThemeManager.Brush("#D84949");
        Find<Button>("StartButton").BorderBrush = ThemeManager.Brush("#D84949");
        Find<TextBlock>("UpdatedText").Text = "更新于 12:30:04";
        Find<TextBlock>("VersionText").Text = "v1.5.1 · 演示设备数据";
        Find<TextBlock>("RequestText").Text = "请求 2 次";
        Find<TextBlock>("LatestLogText").Text = "监控中 · 排除设备不会自动连接";
        Find<TextBlock>("LatestLogText").Foreground = ThemeManager.Brush("#0862C7");
        Find<ListBox>("LogBox").ItemsSource = new ObservableCollection<LogEntry>
        {
            new("12:30:01", "操作", "已开始监控，每 2 秒扫描远程设备。", "#0862C7", "#E6F0FF"),
            new("12:30:02", "成功", "Android Device 已确认连接。", "#18713D", "#E5F5EA"),
            new("12:30:03", "提醒", "USB Debug Device 已排除，跳过自动连接。", "#995900", "#FFF0D6"),
            new("12:30:04", "错误", "USB Serial Adapter 连接失败，15 秒后重试。", "#C12B30", "#FDEBED")
        };
        window.Show(); Pump();
        Save(window, Path.Combine(output, "light.png"));
        ThemeManager.Apply(true, false);
        Find<System.Windows.Shapes.Path>("MoonIcon").Opacity = 0; Find<System.Windows.Shapes.Path>("SunIcon").Opacity = 1;
        Pump(); Save(window, Path.Combine(output, "dark.png"));
        var manager = new ExclusionsWindow(settings.Exclusions, _ => true) { Left = -10000, Top = -10000, WindowStartupLocation = WindowStartupLocation.Manual, ShowActivated = false, ShowInTaskbar = false };
        manager.Show(); Pump(); Save(manager, Path.Combine(output, "exclusions.png"));
        manager.Close(); window.Close(); app.Shutdown();
        Console.WriteLine("Documentation screenshots created: " + output);
    }
    private static void Pump()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }
    private static void Save(Window window, string path)
    {
        window.UpdateLayout();
        var visual = (FrameworkElement)VisualTreeHelper.GetChild(window, 0);
        var width = (int)Math.Ceiling(visual.ActualWidth); var height = (int)Math.Ceiling(visual.ActualHeight);
        visual.Clip = new RectangleGeometry(new Rect(0, 0, width, height), 10, 10);
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); bitmap.Render(visual);
        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(path); png.Save(file);
    }
}
