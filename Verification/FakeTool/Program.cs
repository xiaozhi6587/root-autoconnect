if (args.FirstOrDefault() == "-child")
{
    File.WriteAllText(Environment.GetEnvironmentVariable("USB_AUTO_TEST_CHILD_PID")!, System.Diagnostics.Process.GetCurrentProcess().Id.ToString());
    await Task.Delay(30000); return;
}
if (Environment.GetEnvironmentVariable("USB_AUTO_TEST_MODE") == "tree")
{
    using var child = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(System.Diagnostics.Process.GetCurrentProcess().MainModule!.FileName, "-child") { UseShellExecute = false, CreateNoWindow = true });
    await Task.Delay(30000); return;
}
if (Environment.GetEnvironmentVariable("USB_AUTO_TEST_MODE") == "wait") { await Task.Delay(30000); return; }
if (Environment.GetEnvironmentVariable("USB_AUTO_TEST_MODE") == "fail") { Console.Error.WriteLine("OPERATION FAILED: device unavailable"); Environment.Exit(2); }
if (args.SequenceEqual(new[] { "-list", "devices" }))
    Console.WriteLine("OPERATION SUCCESSFUL\nList of USB Servers and remote devices:\n  1: USB server at 192.0.2.1:32038 - callback connection\n Mode: manual-connect Status: connected\n `- 10: Test Device\n Vid: 19d2 Pid: ffcc Serial: TEST\n Mode: manual-connect Status: available for connection");
else if (args.SequenceEqual(new[] { "-connect", "1-10" })) Console.WriteLine("OPERATION SUCCESSFUL");
else if (args.FirstOrDefault() == "-echo") Console.WriteLine(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(string.Join("\n", args.Skip(1)))));
else { Console.Error.WriteLine("unexpected arguments"); Environment.Exit(3); }
