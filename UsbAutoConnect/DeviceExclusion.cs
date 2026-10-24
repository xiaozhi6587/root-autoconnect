
namespace UsbAutoConnect;

public sealed record DeviceExclusion(string Key, string Name, string Server, string Hardware, string Identifier, DateTimeOffset AddedAt)
{
    public bool Matches(RemoteDevice device)
    {
        // Interpret JSON keys so legacy escaped Unicode keys continue matching across runtime versions.
        try { return (JsonData.Deserialize<string[]>(Key) ?? []).SequenceEqual(JsonData.Deserialize<string[]>(KeyFor(device)) ?? [], StringComparer.OrdinalIgnoreCase); }
        catch { return false; }
    }
    public static DeviceExclusion FromDevice(RemoteDevice device) => new(KeyFor(device), device.Name,
        Host(device.Server), device.Hardware,
        !string.IsNullOrWhiteSpace(device.Serial) ? "序列号：" + device.Serial
            : !string.IsNullOrWhiteSpace(device.Port) ? "USB 端口：" + device.Port : "名称：" + device.Name,
        DateTimeOffset.Now);
    private static string KeyFor(RemoteDevice device)
    {
        // Callback TCP ports and numeric list IDs can change on reconnect. Never use them for a saved rule.
        var parts = new[] { Host(device.Server), device.Vid, device.Pid,
            !string.IsNullOrWhiteSpace(device.Serial) ? "serial" : !string.IsNullOrWhiteSpace(device.Port) ? "port" : "name",
            !string.IsNullOrWhiteSpace(device.Serial) ? device.Serial : !string.IsNullOrWhiteSpace(device.Port) ? device.Port : device.Name };
        return JsonData.Serialize(parts.Select(s => s.Trim().ToUpperInvariant()).ToArray());
    }
    private static string Host(string address)
    {
        var colon = address.LastIndexOf(':');
        return colon > 0 && int.TryParse(address.Substring(colon + 1), out _) ? address.Substring(0, colon) : address;
    }
}
