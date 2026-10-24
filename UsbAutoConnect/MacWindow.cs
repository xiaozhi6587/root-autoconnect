using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows.Media.Imaging;

namespace UsbAutoConnect;

public class MacWindow : Window
{
    static MacWindow()
    {
        CommandManager.RegisterClassCommandBinding(typeof(MacWindow), new CommandBinding(SystemCommands.CloseWindowCommand, (s, _) => ((Window)s).Close()));
        CommandManager.RegisterClassCommandBinding(typeof(MacWindow), new CommandBinding(SystemCommands.MinimizeWindowCommand, (s, _) => ((Window)s).WindowState = WindowState.Minimized));
        CommandManager.RegisterClassCommandBinding(typeof(MacWindow), new CommandBinding(SystemCommands.MaximizeWindowCommand, (s, _) =>
        {
            var window = (Window)s;
            window.WindowState = window.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }));
    }
    public MacWindow()
    {
        Style = (Style)Application.Current.FindResource("MacWindow");
        Icon = BitmapFrame.Create(new Uri("pack://application:,,,/RootAutoConnect;component/Assets/app.ico"));
        SourceInitialized += (_, _) => UpdateWindowRegion();
        SizeChanged += (_, _) => Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(UpdateWindowRegion));
        StateChanged += (_, _) => Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(UpdateWindowRegion));
        DpiChanged += (_, _) => Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(UpdateWindowRegion));
    }
    private void UpdateWindowRegion()
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero || WindowState == WindowState.Minimized) return;
        if (WindowState == WindowState.Maximized) { SetWindowRgn(handle, IntPtr.Zero, true); return; }
        if (!GetWindowRect(handle, out var bounds)) return;
        var radius = (int)Math.Round(20 * VisualTreeHelper.GetDpi(this).DpiScaleX);
        var region = CreateRoundRectRgn(0, 0, bounds.Right - bounds.Left + 1, bounds.Bottom - bounds.Top + 1, radius, radius);
        // Native clipping removes the HWND corners as well as the WPF content. No layered transparency or black corner fill.
        if (region != IntPtr.Zero && SetWindowRgn(handle, region, true) == 0) DeleteObject(region);
    }
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetWindowRect(IntPtr window, out NativeRect rect);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateRoundRectRgn(int left, int top, int right, int bottom, int width, int height);
    [DllImport("user32.dll")] private static extern int SetWindowRgn(IntPtr window, IntPtr region, [MarshalAs(UnmanagedType.Bool)] bool redraw);
    [DllImport("gdi32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DeleteObject(IntPtr value);
}

public static class MacDialog
{
    public static void Show(Window owner, string message, string title)
    {
        var window = new MacWindow { Owner = owner, Title = title, Width = 560, Height = 300,
            MinWidth = 360, MinHeight = 220, WindowStartupLocation = WindowStartupLocation.CenterOwner, ShowInTaskbar = false };
        var panel = new DockPanel { Margin = new Thickness(18, 14, 18, 14) };
        var button = new Button { Content = "好", MinWidth = 72, Style = (Style)Application.Current.FindResource("AccentButton"),
            HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0), IsDefault = true, IsCancel = true };
        button.Click += (_, _) => window.Close();
        DockPanel.SetDock(button, Dock.Bottom); panel.Children.Add(button);
        panel.Children.Add(new TextBox { Text = message, IsReadOnly = true, TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto, FontSize = 13, BorderThickness = new Thickness(0), Padding = new Thickness(8) });
        window.Content = panel; window.ShowDialog();
    }
}
