using UsbAutoConnect;

var passed = 0;
void Check(bool condition, string label) { if (!condition) throw new Exception(label); Console.WriteLine("PASS " + label); passed++; }
foreach (var invalidPath in new string?[] { null, "", "   ", "\"\"", "\0", "C:\\missing\\usbtechsh.exe", "bad|path" })
    Check(ToolLocator.DescribeInstallation(invalidPath) == "尚未定位技师端", "version display tolerates missing or invalid tool paths");
var text = """
================= USB REDIRECTOR OPERATION SUCCESSFUL ===============
List of USB Servers and remote devices:
   1: USB server at 192.0.2.1:39971 - callback connection
      Mode: manual-connect   Status: connected
   |
   |-  10: 测试手机 USB Mass Storage Device
           Vid: 19d2   Pid: ffcc   Serial: ABCDEF
           Mode: manual-connect   Status: available for connection
   `-  11: Connected Phone
           Vid: 1234   Pid: 0001   Port: 2-3
           Mode: auto-connect   Status: connected
   2: USB server at 192.0.2.2:32038
      Mode: manual-connect   Status: disconnected
   `-  1: Offline device
           Mode: manual-connect   Status: available for connection
""";
var devices = DeviceListParser.Parse(text);
Check(devices.Count == 3, "multiple servers and tree branches");
Check(devices[0].Id == "1-10" && devices[0].Available && devices[0].Server == "192.0.2.1:39971", "available device and callback address");
Check(devices[0].Vid == "19d2" && devices[0].Pid == "ffcc" && devices[0].Serial == "ABCDEF", "hardware fields");
Check(devices[1].Connected && !devices[1].Available && devices[1].Port == "2-3", "never reconnect connected devices");
Check(!devices[2].Available && devices[2].Serial == "", "offline servers excluded and fields reset");
Check(devices[0].Matches("FFCC") && devices[0].Matches("测试手机") && !devices[0].Matches("unmatched"), "filter by hardware and Unicode name");
var exclusion = DeviceExclusion.FromDevice(devices[0]);
var rulesRoundTrip = JsonData.Deserialize<List<DeviceExclusion>>(JsonData.Serialize(new[] { exclusion }))!;
var blockedSettings = new Settings { Exclusions = rulesRoundTrip };
Check(!blockedSettings.CanAutoConnect(devices[0]) && rulesRoundTrip[0].Matches(devices[0]), "persisted exclusion prevents an available device from connecting");
Check(exclusion.Matches(devices[0] with { ServerId = "44", DeviceId = "99", Server = "192.0.2.1:55555" }), "exclusion survives list ID and callback port changes");
Check(!exclusion.Matches(devices[0] with { Serial = "OTHER" }) && !exclusion.Matches(devices[0] with { Server = "192.0.2.77:39971" }), "exclusion does not block another serial or host");
var portRule = DeviceExclusion.FromDevice(devices[1]);
Check(portRule.Matches(devices[1] with { DeviceId = "88" }) && !portRule.Matches(devices[1] with { Port = "8-1" }), "serial-less devices fall back to USB port");
var unnamedPort = devices[2] with { ServerStatus = "connected" };
Check(DeviceExclusion.FromDevice(unnamedPort).Matches(unnamedPort with { DeviceId = "40" }), "device name fallback without serial or port");
blockedSettings.Exclusions.Clear();
Check(blockedSettings.CanAutoConnect(devices[0]), "restoring exclusion allows a device again");
Check(DeviceListParser.Parse("OPERATION SUCCESSFUL\nList of USB Servers and remote devices:").Count == 0, "valid empty list");
try { DeviceListParser.Parse("OPERATION FAILED\nAccess denied"); throw new Exception("error accepted"); } catch (FormatException) { Check(true, "reject CLI errors"); }
try { DeviceListParser.Parse("OPERATION SUCCESSFUL\nList of USB Servers\n1: USB server at 192.0.2.1:1\n `- 10: incomplete"); throw new Exception("partial accepted"); } catch (FormatException) { Check(true, "reject truncated device data"); }
#if NETFRAMEWORK
const string fakeFramework = "net471";
#else
const string fakeFramework = "net10.0";
#endif
var fakePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "FakeTool", "bin", "Release", fakeFramework, "usbtechsh.exe"));
var client = new RedirectorClient(fakePath);
var fakeDevices = await client.ListAsync(CancellationToken.None);
Check(fakeDevices.Count == 1 && fakeDevices[0].Available, "process execution and list parsing");
await client.ConnectAsync(fakeDevices[0], CancellationToken.None);
Check(true, "connection command arguments and success response");
var quotedArguments = new[] { "with spaces", "quoted\"value", "C:\\path ending\\", "", "中文参数" };
var echoed = await client.RunAsync(CancellationToken.None, new[] { "-echo" }.Concat(quotedArguments).ToArray());
Check(System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(echoed)) == string.Join("\n", quotedArguments), "command arguments preserve spaces, quotes, empty values, slashes and Unicode");
Environment.SetEnvironmentVariable("USB_AUTO_TEST_MODE", "fail");
try { await client.ListAsync(CancellationToken.None); throw new Exception("failure accepted"); } catch (IOException) { Check(true, "nonzero exit and stderr handled"); }
Environment.SetEnvironmentVariable("USB_AUTO_TEST_MODE", "wait");
using (var cancel = new CancellationTokenSource(350))
{
    var watch = System.Diagnostics.Stopwatch.StartNew();
    try { await client.ListAsync(cancel.Token); throw new Exception("cancel ignored"); } catch (OperationCanceledException) { Check(watch.Elapsed.TotalSeconds < 5, "stop cancels command promptly"); }
}
Environment.SetEnvironmentVariable("USB_AUTO_TEST_MODE", null);
var temporaryParent = Path.GetFullPath(Path.GetTempPath());
var locatorRoot = Path.GetFullPath(Path.Combine(temporaryParent, "RootAutoConnect-Verification-" + Guid.NewGuid().ToString("N")));
try
{
    var portable = Path.Combine(locatorRoot, "自定义目录", "工具", "Technician");
    Directory.CreateDirectory(portable);
    var childMarker = Path.Combine(locatorRoot, "child-pid.txt");
    Environment.SetEnvironmentVariable("USB_AUTO_TEST_CHILD_PID", childMarker);
    Environment.SetEnvironmentVariable("USB_AUTO_TEST_MODE", "tree");
    using (var cancelTree = new CancellationTokenSource(1500))
    {
        try { await client.ListAsync(cancelTree.Token); throw new Exception("tree cancel ignored"); }
        catch (OperationCanceledException) { }
    }
    Environment.SetEnvironmentVariable("USB_AUTO_TEST_MODE", null);
    Environment.SetEnvironmentVariable("USB_AUTO_TEST_CHILD_PID", null);
    var childPid = int.Parse(File.ReadAllText(childMarker));
    bool childStopped;
    try { using var child = System.Diagnostics.Process.GetProcessById(childPid); childStopped = child.HasExited || child.WaitForExit(3000); }
    catch (ArgumentException) { childStopped = true; }
    Check(childStopped, "cancellation terminates command child processes as well as the parent");
    var portableTool = Path.Combine(portable, "usbtechsh.exe");
    File.WriteAllText(portableTool, "test fixture, not an executable");
    Check(ToolLocator.FindExisting(new[] { Path.Combine(locatorRoot, "old", "usbtechsh.exe"), portableTool }) == portableTool, "invalid saved path falls through to relocated tool");
    Check(ToolLocator.FindExisting(new[] { "\"" + portableTool + "\"" }) == portableTool, "quoted paths and Unicode directories accepted");
    File.WriteAllText(Path.Combine(portable, "wrong.exe"), "fixture");
    Check(ToolLocator.FindExisting(new[] { Path.Combine(portable, "wrong.exe") }) == null, "unrelated executable rejected");
    Check(ToolLocator.SearchDirectories(new[] { locatorRoot }, CancellationToken.None) == portableTool, "bounded discovery finds nonstandard nested portable folder");
    Check(ToolLocator.SearchDirectories(new[] { locatorRoot }, CancellationToken.None, 1) == null, "search respects directory budget");
    using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
    try { ToolLocator.SearchDirectories(new[] { locatorRoot }, cancelled.Token); throw new Exception("search cancel ignored"); }
    catch (OperationCanceledException) { Check(true, "discovery cancellation is immediate"); }
    var themed = new Settings { ToolPath = portableTool, Theme = "Dark", Exclusions = new() { exclusion } };
    var savedTheme = JsonData.Deserialize<Settings>(JsonData.Serialize(themed))!;
    Check(savedTheme.Theme == "Dark" && savedTheme.Exclusions.Count == 1 && savedTheme.ToolPath == portableTool, "theme, path and exclusions persist together");
}
finally
{
    Environment.SetEnvironmentVariable("USB_AUTO_TEST_MODE", null);
    Environment.SetEnvironmentVariable("USB_AUTO_TEST_CHILD_PID", null);
    if (!locatorRoot.StartsWith(temporaryParent, StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(locatorRoot).StartsWith("RootAutoConnect-Verification-"))
        throw new InvalidOperationException("Unexpected cleanup path");
    if (Directory.Exists(locatorRoot)) Directory.Delete(locatorRoot, true);
}
var palettePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "UsbAutoConnect", "Assets", "palette.json"));
var palette = JsonData.Deserialize<Dictionary<string, string[]>>(File.ReadAllText(palettePath))!;
var legacySettings = JsonData.Deserialize<Settings>("{\"ToolPath\":\"C:\\\\旧目录\\\\usbtechsh.exe\",\"Theme\":\"Dark\",\"IntervalSeconds\":2,\"RetrySeconds\":15,\"Filter\":\"手机\",\"Exclusions\":[{\"Key\":\"[\\\"192.0.2.2\\\",\\\"\\\",\\\"\\\",\\\"NAME\\\",\\\"\\\\u6D4B\\\\u8BD5\\\"]\",\"Name\":\"测试\",\"Server\":\"192.0.2.2\",\"Hardware\":\":\",\"Identifier\":\"名称：测试\",\"AddedAt\":\"2026-10-24T23:40:58+08:00\"}]}")!;
Check(legacySettings.Theme == "Dark" && legacySettings.Filter == "手机" && legacySettings.Exclusions.Count == 1 && legacySettings.Exclusions[0].AddedAt.Offset == TimeSpan.FromHours(8), "legacy settings and ISO timestamp remain compatible");
Check(legacySettings.Exclusions[0].Matches(devices[2] with { Name = "测试" }), "legacy escaped Unicode exclusion keys remain effective");
Check(palette.Count > 40 && palette.Values.All(pair => pair.Length == 2 && pair.All(color => System.Text.RegularExpressions.Regex.IsMatch(color, "^#[A-F0-9]{6}([A-F0-9]{2})?$"))), "every theme color has a valid light and dark pair");
if (args.Contains("--live"))
{
    using var discovery = new CancellationTokenSource(TimeSpan.FromSeconds(15));
    var discovered = await ToolLocator.FindAsync(@"Z:\missing\usbtechsh.exe", discovery.Token, false);
    Check(discovered != null, "installed tool found automatically despite stale path");
    var live = await new RedirectorClient(discovered!).ListAsync(CancellationToken.None);
    Check(true, $"real installed CLI read-only scan: {live.Count} devices");
}
Console.WriteLine($"All {passed} checks passed.");
