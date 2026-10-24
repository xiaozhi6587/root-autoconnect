using System.IO;
using Microsoft.Win32;

namespace UsbAutoConnect;

public sealed class Settings
{
    public string ToolPath { get; set; } = "";
    public string Theme { get; set; } = "Light";
    public int IntervalSeconds { get; set; } = 2;
    public int RetrySeconds { get; set; } = 15;
    public string Filter { get; set; } = "";
    public List<DeviceExclusion> Exclusions { get; set; } = [];
    public bool IsExcluded(RemoteDevice device) => Exclusions.Any(rule => rule.Matches(device));
    public bool CanAutoConnect(RemoteDevice device) => device.Available && device.Matches(Filter) && !IsExcluded(device);
    public static string DataDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UsbAutoConnect");
    public static Settings Load()
    {
        try
        {
            var settings = JsonData.Deserialize<Settings>(File.ReadAllText(Path.Combine(DataDirectory, "settings.json"))) ?? new();
            settings.Exclusions ??= [];
            settings.ToolPath ??= "";
            settings.Filter ??= "";
            settings.Theme = settings.Theme == "Dark" ? "Dark" : "Light";
            return settings;
        }
        catch { return new(); }
    }
    public void Save()
    {
        Directory.CreateDirectory(DataDirectory);
        var path = Path.Combine(DataDirectory, "settings.json");
        File.WriteAllText(path + ".tmp", JsonData.Serialize(this));
        Compatibility.ReplaceFile(path + ".tmp", path);
    }
}
