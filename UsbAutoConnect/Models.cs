using System.Text.RegularExpressions;

namespace UsbAutoConnect;

public sealed record RemoteDevice(string ServerId, string DeviceId, string Server, string Name,
    string Vid, string Pid, string Serial, string Port, string Mode, string Status, string ServerStatus)
{
    public string Id => $"{ServerId}-{DeviceId}";
    public string Identity => $"{Server}|{Id}|{Vid}|{Pid}|{Serial}|{Port}";
    public string Hardware => $"{Vid}:{Pid}";
    public bool IsExcluded { get; init; }
    public string ConnectionPolicy => IsExcluded ? "不自动连接" : "允许自动连接";
    public string ExclusionAction => IsExcluded ? "恢复自动连接此设备" : "不自动连接此设备";
    public bool Available => Status.Equals("available for connection", StringComparison.OrdinalIgnoreCase)
        && ServerStatus.Equals("connected", StringComparison.OrdinalIgnoreCase);
    public bool Connected => Status.Equals("connected", StringComparison.OrdinalIgnoreCase);
    public string DisplayStatus => Status.ToLowerInvariant() switch
    {
        "connected" => "已连接", "available for connection" => "可连接",
        "connecting" => "连接中", "disconnected" => "已断开", _ => Status
    };
    public bool Matches(string filter) => string.IsNullOrWhiteSpace(filter) ||
        $"{Server} {Name} {Vid} {Pid} {Serial} {Port} {Id}".Contains(filter.Trim(), StringComparison.OrdinalIgnoreCase);
}

public static class DeviceListParser
{
    private static readonly Regex ServerLine = new(@"^\s*(\d+):\s*USB server at\s+(.+?)(?:\s+-\s+.*)?\s*$", RegexOptions.IgnoreCase);
    private static readonly Regex DeviceLine = new(@"^\s*[|`+\\\-\s]*[-+]\s*(\d+):\s*(.+)$");
    private static readonly Regex StateLine = new(@"Mode:\s*(.*?)\s+Status:\s*(.+)$", RegexOptions.IgnoreCase);

    public static IReadOnlyList<RemoteDevice> Parse(string output)
    {
        // Refuse to interpret an error, unrelated tool output, or partial response as an empty device list.
        if (!output.Contains("OPERATION SUCCESSFUL", StringComparison.OrdinalIgnoreCase)
            || !output.Contains("List of USB Servers", StringComparison.OrdinalIgnoreCase))
            throw new FormatException("未收到有效的设备列表。" + Environment.NewLine + output.Trim());
        var devices = new List<RemoteDevice>();
        string serverId = "", server = "", serverStatus = "";
        string? deviceId = null;
        string name = "", vid = "", pid = "", serial = "", port = "", mode = "", status = "";
        void Flush()
        {
            if (deviceId != null)
            {
                if (string.IsNullOrEmpty(status)) throw new FormatException("设备状态缺失，已停止本轮连接。请查看原始输出。");
                devices.Add(new(serverId, deviceId, server, name, vid, pid, serial, port, mode, status, serverStatus));
            }
            deviceId = null; name = vid = pid = serial = port = mode = status = "";
        }
        foreach (var line in output.Split('\n'))
        {
            var match = ServerLine.Match(line.TrimEnd('\r'));
            if (match.Success) { Flush(); serverId = match.Groups[1].Value; server = match.Groups[2].Value; serverStatus = ""; continue; }
            match = DeviceLine.Match(line.TrimEnd('\r'));
            if (match.Success && serverId.Length > 0) { Flush(); deviceId = match.Groups[1].Value; name = match.Groups[2].Value.Trim(); continue; }
            match = StateLine.Match(line);
            if (match.Success)
            {
                if (deviceId == null) serverStatus = match.Groups[2].Value.Trim();
                else { mode = match.Groups[1].Value.Trim(); status = match.Groups[2].Value.Trim(); }
            }
            if (deviceId != null)
            {
                string Field(string field) => Regex.Match(line, $@"\b{field}:\s*(\S+)", RegexOptions.IgnoreCase).Groups[1].Value;
                if (Field("Vid") is { Length: > 0 } v) vid = v;
                if (Field("Pid") is { Length: > 0 } p) pid = p;
                if (Field("Serial") is { Length: > 0 } s) serial = s;
                if (Field("Port") is { Length: > 0 } u) port = u;
            }
        }
        Flush();
        return devices;
    }
}
