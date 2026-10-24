using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace UsbAutoConnect;

public static class ToolLocator
{
    public static string DescribeInstallation(string? candidate)
    {
        var tool = FindExisting(new[] { candidate });
        if (tool == null) return "尚未定位技师端";
        try
        {
            var main = Path.Combine(Path.GetDirectoryName(tool) ?? "", "usbredirector-technician.exe");
            var version = File.Exists(main) ? FileVersionInfo.GetVersionInfo(main).FileVersion : "";
            return "技师版" + (string.IsNullOrWhiteSpace(version) ? "" : " " + version.Replace(',', '.'));
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException or System.ComponentModel.Win32Exception)
        { return "技师版（版本暂不可读）"; }
    }
    public static string? FindExisting(IEnumerable<string?> candidates)
    {
        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate)) continue;
            try
            {
                var path = Path.GetFullPath(Environment.ExpandEnvironmentVariables(candidate!.Trim().Trim('"')));
                if (Path.GetFileName(path).Equals("usbtechsh.exe", StringComparison.OrdinalIgnoreCase) && File.Exists(path)) return path;
            }
            catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException) { }
        }
        return null;
    }

    public static Task<string?> FindAsync(string? preferred, CancellationToken token, bool deep = true) => Task.Run(() =>
    {
        token.ThrowIfCancellationRequested();
        var saved = FindExisting(new[] { preferred });
        if (saved != null) return saved;
        var quick = FindExisting(QuickCandidates(token));
        if (quick != null || !deep) return quick;
        var drives = DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed && d.IsReady).Select(d => d.RootDirectory.FullName);
        return SearchDirectories(drives, token);
    }, token);

    public static string? SearchDirectories(IEnumerable<string> roots, CancellationToken token, int maxDirectories = 120_000)
    {
        var queue = new Queue<string>(roots);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var skipped = new HashSet<string>(new[] { "Windows", "$Recycle.Bin", "System Volume Information", ".git", "node_modules", ".codex", ".nuget", "WinSxS", "Packages", "Temp" }, StringComparer.OrdinalIgnoreCase);
        for (var count = 0; queue.Count > 0 && count < maxDirectories; count++)
        {
            token.ThrowIfCancellationRequested();
            var directory = queue.Dequeue();
            if (!visited.Add(directory)) continue;
            var found = FindExisting(new[] { Path.Combine(directory, "usbtechsh.exe") });
            if (found != null) return found;
            try
            {
                foreach (var child in Directory.EnumerateDirectories(directory))
                {
                    token.ThrowIfCancellationRequested();
                    if (!skipped.Contains(Path.GetFileName(child)) && (File.GetAttributes(child) & FileAttributes.ReparsePoint) == 0)
                        queue.Enqueue(child);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
        return null;
    }

    private static IEnumerable<string?> QuickCandidates(CancellationToken token)
    {
        var candidates = new List<string?>();
        void AddDirectory(string? directory) { if (!string.IsNullOrWhiteSpace(directory)) candidates.Add(Path.Combine(directory, "usbtechsh.exe")); }
        AddDirectory(AppContext.BaseDirectory);
        foreach (var name in new[] { "usbredirector-technician", "usbredirectortechssrv", "usbtechsh" })
        {
            foreach (var process in Process.GetProcessesByName(name))
            {
                using (process)
                    try { AddDirectory(Path.GetDirectoryName(process.MainModule?.FileName)); }
                    catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or NotSupportedException) { }
            }
        }
        foreach (var hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            token.ThrowIfCancellationRequested();
            try
            {
                using var root = RegistryKey.OpenBaseKey(hive, view);
                using var uninstall = root.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
                foreach (var key in uninstall?.GetSubKeyNames() ?? [])
                {
                    using var app = uninstall!.OpenSubKey(key);
                    if (app?.GetValue("DisplayName") is not string displayName || !displayName.Contains("USB Redirector Technician", StringComparison.OrdinalIgnoreCase)) continue;
                    AddDirectory(app.GetValue("InstallLocation") as string);
                    var icon = app.GetValue("DisplayIcon") as string;
                    if (icon != null) AddDirectory(Path.GetDirectoryName(icon.Split(',')[0].Trim('"')));
                }
                foreach (var executable in new[] { "usbredirector-technician.exe", "usbtechsh.exe" })
                {
                    using var appPath = root.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\" + executable);
                    if (appPath?.GetValue("") is string value) AddDirectory(Path.GetDirectoryName(value.Trim('"')));
                    AddDirectory(appPath?.GetValue("Path") as string);
                }
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException or ArgumentException) { }
        }
        foreach (var root in new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) })
        {
            AddDirectory(Path.Combine(root, "USB Redirector Technician Edition"));
            AddDirectory(Path.Combine(root, "Programs", "USB Redirector Technician Edition"));
        }
        foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
            if (!string.IsNullOrWhiteSpace(directory)) AddDirectory(directory.Trim('"'));
        // Read shortcut targets without launching the target or executing a script.
        foreach (var folder in new[] { Environment.SpecialFolder.Programs, Environment.SpecialFolder.CommonPrograms, Environment.SpecialFolder.DesktopDirectory, Environment.SpecialFolder.CommonDesktopDirectory })
        {
            token.ThrowIfCancellationRequested();
            try
            {
                foreach (var shortcut in EnumerateShortcuts(Environment.GetFolderPath(folder), token)
                    .Where(p => Path.GetFileName(p).Contains("USB", StringComparison.OrdinalIgnoreCase)))
                {
                    object? shell = null, link = null;
                    try
                    {
                        var type = Type.GetTypeFromProgID("WScript.Shell");
                        if (type == null) continue;
                        shell = Activator.CreateInstance(type);
                        link = ((dynamic)shell!).CreateShortcut(shortcut);
                        string target = ((dynamic)link).TargetPath;
                        if (Path.GetFileName(target).Contains("usb", StringComparison.OrdinalIgnoreCase)) AddDirectory(Path.GetDirectoryName(target));
                    }
                    catch (Exception ex) when (ex is COMException or ArgumentException or IOException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException) { }
                    finally { if (link != null && Marshal.IsComObject(link)) Marshal.FinalReleaseComObject(link); if (shell != null && Marshal.IsComObject(shell)) Marshal.FinalReleaseComObject(shell); }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { }
        }
        return candidates;
    }
    private static IEnumerable<string> EnumerateShortcuts(string root, CancellationToken token)
    {
        var found = new List<string>();
        var queue = new Queue<(string Directory, int Depth)>(); queue.Enqueue((root, 0));
        while (queue.Count > 0)
        {
            token.ThrowIfCancellationRequested();
            var (directory, depth) = queue.Dequeue();
            try
            {
                found.AddRange(Directory.EnumerateFiles(directory, "*.lnk", SearchOption.TopDirectoryOnly));
                if (depth < 5)
                    foreach (var child in Directory.EnumerateDirectories(directory))
                        if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) == 0) queue.Enqueue((child, depth + 1));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { }
        }
        return found;
    }
}
