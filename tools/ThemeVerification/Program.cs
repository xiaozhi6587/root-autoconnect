using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using System.IO;
using System.Xml.Linq;
using System.Windows.Markup;
using UsbAutoConnect;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        try { Run(); return 0; }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    private static void Run()
    {
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
#if NETFRAMEWORK
        var target = (System.Runtime.Versioning.TargetFrameworkAttribute)typeof(MacWindow).Assembly.GetCustomAttributes(typeof(System.Runtime.Versioning.TargetFrameworkAttribute), false).Single();
        Require(target.FrameworkName == ".NETFramework,Version=v4.7.1", "application targets the .NET Framework 4.7.1 reference contract");
#endif
        var sourcePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "UsbAutoConnect", "App.xaml"));
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var source = XDocument.Load(sourcePath);
        var dictionary = new XElement(presentation + "ResourceDictionary",
            new XAttribute(XNamespace.Xmlns + "x", "http://schemas.microsoft.com/winfx/2006/xaml"),
            new XAttribute(XNamespace.Xmlns + "local", "clr-namespace:UsbAutoConnect;assembly=RootAutoConnect"),
            new XAttribute(XNamespace.Xmlns + "shell", "clr-namespace:System.Windows.Shell;assembly=PresentationFramework"),
            source.Root!.Element(presentation + "Application.Resources")!.Elements());
        app.Resources = (ResourceDictionary)XamlReader.Parse(dictionary.ToString().Replace("clr-namespace:UsbAutoConnect\"", "clr-namespace:UsbAutoConnect;assembly=RootAutoConnect\""));
        ThemeManager.Initialize(false);
        foreach (var path in new[] { "", "  ", "bad|path", "C:\\missing\\usbtechsh.exe" })
        {
            var startupWindow = new MainWindow(new Settings { ToolPath = path });
            Require(startupWindow.Title == "root包 逆向自动连接助手", "main window initializes with missing or invalid saved path");
            startupWindow.Close();
        }
        var surface = (SolidColorBrush)ThemeManager.Brush("#FFFFFF");
        var light = surface.Color;
        Require(!surface.IsFrozen, "shared theme brushes remain mutable after resource lookup");
        ThemeManager.Apply(true, false);
        Require(surface.Color != light && ReferenceEquals(surface, ThemeManager.Brush("#FFFFFF")), "theme switch recolors existing brushes without replacing them");
        ThemeManager.Apply(false, true);
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(380) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start(); Dispatcher.PushFrame(frame);
        Require(surface.Color == light, "animated theme transition reaches the light endpoint");
        ThemeManager.Apply(true); ThemeManager.Apply(false); ThemeManager.Apply(true);
        var reverseFrame = new DispatcherFrame();
        var reverseTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(380) };
        reverseTimer.Tick += (_, _) => { reverseTimer.Stop(); reverseFrame.Continue = false; };
        reverseTimer.Start(); Dispatcher.PushFrame(reverseFrame);
        Require(surface.Color != light && ThemeManager.IsDark, "rapid theme reversals settle on the last selected theme");
        foreach (var key in app.Resources.Keys.OfType<string>().Where(key => key.StartsWith("Tone")))
            Require(app.Resources[key] is SolidColorBrush { IsFrozen: false }, "mutable palette " + key, quiet: true);
        Console.WriteLine("PASS entire light/dark palette remains animated and mutable");
        var cornerWindow = new MacWindow { Width = 240, Height = 160, Left = -10000, Top = -10000, ShowActivated = false, ShowInTaskbar = false };
        cornerWindow.Show();
        var handle = new WindowInteropHelper(cornerWindow).Handle;
        var region = CreateRectRgn(0, 0, 0, 0);
        try { Require(GetWindowRgn(handle, region) > 0 && !PtInRegion(region, 0, 0) && PtInRegion(region, 50, 2) && !cornerWindow.AllowsTransparency,
            "native window region clips corners without layered transparency"); }
        finally { DeleteObject(region); cornerWindow.Close(); }
        app.Shutdown();
    }
    [DllImport("gdi32.dll")] private static extern IntPtr CreateRectRgn(int left, int top, int right, int bottom);
    [DllImport("user32.dll")] private static extern int GetWindowRgn(IntPtr window, IntPtr region);
    [DllImport("gdi32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool PtInRegion(IntPtr region, int x, int y);
    [DllImport("gdi32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DeleteObject(IntPtr value);
    private static void Require(bool condition, string message, bool quiet = false)
    {
        if (!condition) throw new Exception(message);
        if (!quiet) Console.WriteLine("PASS " + message);
    }
}

