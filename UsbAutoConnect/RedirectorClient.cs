using System.Diagnostics;
using System.IO;
using System.Text;

namespace UsbAutoConnect;

public sealed class RedirectorClient(string executable)
{
    public async Task<string> RunAsync(CancellationToken token, params string[] args)
    {
        token.ThrowIfCancellationRequested();
        if (!File.Exists(executable)) throw new FileNotFoundException("找不到 usbtechsh.exe，请选择技师版安装目录里的命令行程序。", executable);
        if (!Path.GetFileName(executable).Equals("usbtechsh.exe", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("请选择 Technician Edition 自带的 usbtechsh.exe。");
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = NativeEncoding(), StandardErrorEncoding = NativeEncoding(),
            WorkingDirectory = Path.GetDirectoryName(executable)!
        };
#if NETFRAMEWORK
        start.Arguments = string.Join(" ", args.Select(Compatibility.QuoteArgument));
#else
        foreach (var arg in args) start.ArgumentList.Add(arg);
#endif
        using var process = Process.Start(start) ?? throw new IOException("无法启动 USB Redirector 命令行。");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        // Drain both streams concurrently so that a full stderr buffer cannot deadlock the process.
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        try
        {
            await Compatibility.WaitForExitAsync(process, timeout.Token);
            var output = (await stdout) + Environment.NewLine + (await stderr);
            if (process.ExitCode != 0 || output.Contains("OPERATION FAILED", StringComparison.OrdinalIgnoreCase))
                throw new IOException($"命令执行失败（{process.ExitCode}）：{output.Trim()}");
            return output.Trim();
        }
        catch (OperationCanceledException)
        {
            try { ProcessTree.Kill(process); } catch (InvalidOperationException) { }
            try { await Task.WhenAll(stdout, stderr); } catch (OperationCanceledException) { } catch (IOException) { }
            if (token.IsCancellationRequested) throw;
            throw new TimeoutException("USB Redirector 命令超过 10 秒未响应，已终止本次命令。");
        }
    }
    public async Task<IReadOnlyList<RemoteDevice>> ListAsync(CancellationToken token) => DeviceListParser.Parse(await RunAsync(token, "-list", "devices"));
    public async Task ConnectAsync(RemoteDevice device, CancellationToken token)
    {
        if (!device.Available) throw new InvalidOperationException("设备当前不可连接。");
        var output = await RunAsync(token, "-connect", device.Id);
        if (!output.Contains("OPERATION SUCCESSFUL", StringComparison.OrdinalIgnoreCase))
            throw new IOException("未收到连接成功响应：" + output);
    }
    private static Encoding NativeEncoding()
    {
#if !NETFRAMEWORK
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
#endif
        return Encoding.GetEncoding((int)GetOEMCP());
    }
    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern uint GetOEMCP();
}
