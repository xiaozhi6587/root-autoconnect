using System.Windows;
using System.IO;

namespace UsbAutoConnect;
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += (_, error) =>
        {
            error.Handled = true;
            var path = Path.Combine(Settings.DataDirectory, "startup-error.log");
            try
            {
                Directory.CreateDirectory(Settings.DataDirectory);
                File.AppendAllText(path, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {error.Exception}{Environment.NewLine}");
            }
            catch { path = "诊断日志未能保存"; }
            MessageBox.Show("程序遇到异常：" + error.Exception.GetBaseException().Message + "\n\n诊断信息：" + path,
                "root包 逆向自动连接助手", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(-1);
        };
        ThemeManager.Initialize(Settings.Load().Theme == "Dark");
        base.OnStartup(e);
    }
}
