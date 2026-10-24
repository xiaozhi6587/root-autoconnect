using System.Diagnostics;
#if NETFRAMEWORK
using System.Runtime.InteropServices;
#endif

namespace UsbAutoConnect;

internal static class ProcessTree
{
    public static void Kill(Process process)
    {
#if NETFRAMEWORK
        var snapshot = CreateToolhelp32Snapshot(2, 0);
        try
        {
            if (snapshot != new IntPtr(-1))
            {
                var entries = new List<(int Id, int Parent)>();
                var entry = new ProcessEntry { Size = (uint)Marshal.SizeOf(typeof(ProcessEntry)) };
                if (Process32First(snapshot, ref entry))
                do { entries.Add(((int)entry.ProcessId, (int)entry.ParentProcessId)); } while (Process32Next(snapshot, ref entry));
                var descendants = new List<int> { process.Id };
                for (var index = 0; index < descendants.Count && index < 4096; index++)
                    descendants.AddRange(entries.Where(e => e.Parent == descendants[index] && !descendants.Contains(e.Id)).Select(e => e.Id));
                for (var index = descendants.Count - 1; index > 0; index--)
                {
                    try
                    {
                        using var child = Process.GetProcessById(descendants[index]);
                        if (child.StartTime >= process.StartTime) child.Kill();
                    }
                    catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or System.ComponentModel.Win32Exception) { }
                }
            }
        }
        finally { if (snapshot != new IntPtr(-1)) CloseHandle(snapshot); }
        if (!process.HasExited) process.Kill();
#else
        if (!process.HasExited) process.Kill(entireProcessTree: true);
#endif
    }
#if NETFRAMEWORK
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ProcessEntry
    {
        public uint Size, Usage, ProcessId;
        public UIntPtr DefaultHeapId;
        public uint ModuleId, Threads, ParentProcessId;
        public int Priority;
        public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string Executable;
    }
    [DllImport("kernel32.dll")] private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint processId);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool Process32First(IntPtr snapshot, ref ProcessEntry entry);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool Process32Next(IntPtr snapshot, ref ProcessEntry entry);
    [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CloseHandle(IntPtr handle);
#endif
}
