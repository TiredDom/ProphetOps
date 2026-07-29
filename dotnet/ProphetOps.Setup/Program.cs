using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Principal;

namespace ProphetOps.Setup;

/// One-click installer for the agency's PC. Registers the application as a Windows Service so
/// it starts during boot, at the login screen, before any password is typed.
internal static class Program
{
    private const string ServiceName = "ProphetOps";
    private const string DisplayName = "ProphetOps";
    private const string Executable = "ProphetOps.Api.exe";
    private const string FirewallRule = "ProphetOps (port 5099)";
    private const string DefaultTarget = @"C:\ProphetOps";
    private const int Port = 5099;

    /// So the agency removes it from Settings the way they would any other program, rather than
    /// being told to type a command.
    private const string UninstallKey =
        @"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\ProphetOps";

    private const string UninstallerName = "Uninstall ProphetOps.exe";

    /// Files that belong to the agency, not to us. A reinstall must never take their records with
    /// it, so these are left alone if they are already on the machine.
    private static readonly string[] Preserve =
    [
        "prophetops.db", "prophetops.db-wal", "prophetops.db-shm",
    ];

    private static int Main(string[] args)
    {
        Console.Title = "ProphetOps Setup";
        Banner();

        try
        {
            if (!IsAdministrator())
            {
                // The manifest should have prevented this. Belt and braces.
                Fail("This installer has to be run as an administrator.");
                return Finish(1);
            }

            var target = args.FirstOrDefault(a => !a.StartsWith('-')) ?? DefaultTarget;

            if (args.Any(a => a.Equals("--uninstall", StringComparison.OrdinalIgnoreCase)))
            {
                Uninstall();
                return Finish(0);
            }

            Install(target);
            return Finish(0);
        }
        catch (Exception ex)
        {
            Fail(ex.Message);
            Console.WriteLine();
            Console.WriteLine("Nothing was left half-installed that a second run cannot fix.");
            Console.WriteLine("Run this installer again, or send this message to the developers.");
            return Finish(1);
        }
    }

    private static void Install(string target)
    {
        var payload = Path.Combine(AppContext.BaseDirectory, "app");
        if (!Directory.Exists(Path.Combine(payload)) || !File.Exists(Path.Combine(payload, Executable)))
        {
            throw new InvalidOperationException(
                $"Could not find the application files.\n\n" +
                $"Expected an 'app' folder next to this installer, containing {Executable}.\n" +
                $"Looked in: {payload}\n\n" +
                $"Copy the whole folder off the USB drive, then run the installer from there.");
        }

        // Otherwise the service registers, fails to bind, and looks like a broken installer.
        var upgrade = ServiceExists();
        if (!upgrade && PortIsTaken())
        {
            throw new InvalidOperationException(
                $"Something is already using port {Port} on this PC.\n\n" +
                $"ProphetOps needs that port. Close whatever is using it and run this again.\n" +
                $"To see what it is, open a Command Prompt and run:\n" +
                $"    netstat -ano | findstr :{Port}");
        }

        Console.WriteLine($"Installing to {target}");
        Console.WriteLine();

        // A running service holds its own exe open, so it has to go down before anything is copied.
        if (upgrade)
        {
            Step("Stopping the running service");
            Sc("stop", ServiceName);
            WaitForStopped();
        }

        Step("Copying the application");
        var kept = CopyTree(payload, target);
        if (kept > 0)
        {
            Console.WriteLine($"        kept {kept} existing data file(s) — the agency's records were not touched");
        }

        var exe = Path.Combine(target, Executable);

        Step(upgrade ? "Updating the Windows Service" : "Registering the Windows Service");
        if (upgrade)
        {
            // sc.exe takes 'key= value' as two tokens; the space after '=' is its syntax, not a typo.
            Sc("config", ServiceName, "binPath=", exe, "start=", "auto");
        }
        else
        {
            Sc("create", ServiceName, "binPath=", exe, "start=", "auto", "DisplayName=", DisplayName);
            Sc("description", ServiceName,
                "ProphetOps decision support system for Renan-Tina Travel and Tours.");
        }

        // Without this a transient fault leaves the office with no system until someone notices.
        Sc("failure", ServiceName, "reset=", "86400", "actions=", "restart/60000/restart/60000/restart/60000");

        Step("Allowing other devices through the firewall");
        Firewall();

        Step("Keeping the PC awake when the lid is closed");
        LidStaysAwake();

        Step("Adding a ProphetOps shortcut to the desktop and Start menu");
        Shortcuts();

        Step("Listing ProphetOps under Settings, Apps");
        RegisterUninstaller(target);

        Step("Starting ProphetOps");
        Sc("start", ServiceName);

        Console.WriteLine();
        if (WaitForHttp(TimeSpan.FromSeconds(90)))
        {
            Done(target);
        }
        else
        {
            Warn("The service was installed but did not answer within 90 seconds.");
            Console.WriteLine();
            Console.WriteLine("  It may still be setting up its database on this first run.");
            Console.WriteLine($"  Try opening http://localhost:{Port} in a browser in a minute.");
            Console.WriteLine($"  To check on it:   sc.exe query {ServiceName}");
        }
    }

    private static void Uninstall()
    {
        Console.WriteLine("Removing ProphetOps from this PC.");
        Console.WriteLine();

        if (ServiceExists())
        {
            Step("Stopping the service");
            ScTry("stop", ServiceName);
            WaitForStopped();

            Step("Removing the service");
            Sc("delete", ServiceName);
        }
        else
        {
            Step("No service registered — nothing to remove");
        }

        Step("Removing the firewall rule");
        Netsh(ignoreFailure: true, "advfirewall", "firewall", "delete", "rule", $"name={FirewallRule}");

        Step("Removing it from the list of installed programs");
        Run("reg", true, "delete", UninstallKey, "/f");

        Step("Removing the shortcuts");
        foreach (var link in ShortcutPaths())
        {
            try
            {
                if (File.Exists(link)) File.Delete(link);
            }
            catch
            {
                // Untidy, not a failure worth stopping for.
            }
        }

        Console.WriteLine();
        Ok("ProphetOps has been removed from this PC.");
        Console.WriteLine();
        Console.WriteLine("  Your records have NOT been deleted. They are still in:");
        Console.WriteLine();
        Console.WriteLine($"      {DefaultTarget}");
        Console.WriteLine();
        Console.WriteLine("  Keep that folder if you may want ProphetOps back, or if you want to");
        Console.WriteLine("  give the bookings and expenses to another system. Installing again");
        Console.WriteLine("  picks up exactly where you left off.");
        Console.WriteLine();
        Console.WriteLine("  If you are certain you will never need the records again, you can");
        Console.WriteLine("  delete that folder yourself in File Explorer.");
    }

    // ---------- steps ----------

    /// Copies the payload over the target, leaving the agency's own files where they are.
    private static int CopyTree(string source, string target)
    {
        Directory.CreateDirectory(target);
        var kept = 0;

        foreach (var from in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, from);
            var to = Path.Combine(target, relative);

            if (Preserve.Contains(Path.GetFileName(to), StringComparer.OrdinalIgnoreCase)
                && File.Exists(to))
            {
                kept++;
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(to)!);
            File.Copy(from, to, overwrite: true);
        }

        return kept;
    }

    /// Opens the port for the office network only. A blanket rule would also expose the system on
    /// whatever public WiFi the laptop is carried onto.
    private static void Firewall()
    {
        Netsh(ignoreFailure: true, "advfirewall", "firewall", "delete", "rule", $"name={FirewallRule}");
        Netsh(ignoreFailure: false,
            "advfirewall", "firewall", "add", "rule",
            $"name={FirewallRule}", "dir=in", "action=allow",
            "protocol=TCP", $"localport={Port}", "profile=private,domain");
    }

    /// Puts a copy of this installer beside the application and points Windows at it, so ProphetOps
    /// can be removed from Settings without the USB drive it arrived on.
    private static void RegisterUninstaller(string target)
    {
        var uninstaller = Path.Combine(target, UninstallerName);

        try
        {
            var self = Environment.ProcessPath;
            if (self is not null && !string.Equals(self, uninstaller, StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(self, uninstaller, overwrite: true);
            }
        }
        catch (Exception ex)
        {
            Warn($"Could not place the uninstaller: {ex.Message}");
            return;
        }

        var size = 0L;
        try
        {
            size = new DirectoryInfo(target)
                .EnumerateFiles("*", SearchOption.AllDirectories)
                .Sum(f => f.Length) / 1024;
        }
        catch
        {
            // Only used for the size column in Settings.
        }

        var values = new (string Name, string Type, string Data)[]
        {
            ("DisplayName", "REG_SZ", "ProphetOps"),
            ("DisplayVersion", "REG_SZ", "1.0"),
            ("Publisher", "REG_SZ", "ProphetOps"),
            ("InstallLocation", "REG_SZ", target),
            ("DisplayIcon", "REG_SZ", Path.Combine(target, Executable)),
            ("UninstallString", "REG_SZ", $"\"{uninstaller}\" --uninstall"),
            ("EstimatedSize", "REG_DWORD", size.ToString()),
            ("NoModify", "REG_DWORD", "1"),
            ("NoRepair", "REG_DWORD", "1"),
        };

        foreach (var v in values)
        {
            Run("reg", true, "add", UninstallKey, "/v", v.Name, "/t", v.Type, "/d", v.Data, "/f");
        }
    }

    /// A .url file rather than a real shortcut: plain text, no COM interop, opens like a bookmark.
    private static void Shortcuts()
    {
        foreach (var link in ShortcutPaths())
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(link)!);
                File.WriteAllText(link,
                    "[InternetShortcut]\r\n" +
                    $"URL=http://localhost:{Port}\r\n" +
                    "IconIndex=0\r\n");
            }
            catch (Exception ex)
            {
                Warn($"Could not create {Path.GetFileName(link)}: {ex.Message}");
            }
        }
    }

    /// For every user of the PC, since the owner and the staff may sign in under different accounts.
    private static IEnumerable<string> ShortcutPaths()
    {
        var publicProfile = Environment.GetEnvironmentVariable("PUBLIC");
        if (!string.IsNullOrWhiteSpace(publicProfile))
            yield return Path.Combine(publicProfile, "Desktop", "ProphetOps.url");

        yield return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
            "Programs", "ProphetOps.url");
    }

    private static bool PortIsTaken() =>
        IPGlobalProperties.GetIPGlobalProperties()
            .GetActiveTcpListeners()
            .Any(e => e.Port == Port);

    /// The office PC is a laptop. Closing the lid would otherwise suspend it, and with it every
    /// booking the staff are trying to record from their own devices.
    private static void LidStaysAwake()
    {
        // 0 = do nothing. Plugged in only; on battery the laptop should still be allowed to sleep.
        Run("powercfg", ignoreFailure: true,
            "/setacvalueindex", "SCHEME_CURRENT", "4f971e89-eebd-4455-a8de-9e59040e7347",
            "5ca83367-6e45-459f-a27b-476b1d01c936", "0");
        Run("powercfg", ignoreFailure: true, "/setactive", "SCHEME_CURRENT");
    }

    // ---------- waiting ----------

    private static void WaitForStopped()
    {
        var deadline = DateTime.UtcNow.AddSeconds(45);
        while (DateTime.UtcNow < deadline)
        {
            var state = Capture("sc.exe", "query", ServiceName);
            if (!state.Contains("RUNNING") && !state.Contains("STOP_PENDING")) return;
            Thread.Sleep(1000);
        }
    }

    /// A first run creates and seeds the database before it starts listening, so this waits on a
    /// real response rather than on the service reporting 'started'.
    private static bool WaitForHttp(TimeSpan timeout)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        var deadline = DateTime.UtcNow + timeout;
        var spinner = new[] { '|', '/', '-', '\\' };
        var tick = 0;

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var response = http.GetAsync($"http://localhost:{Port}/").GetAwaiter().GetResult();
                if (response.IsSuccessStatusCode)
                {
                    Console.Write("\r                                        \r");
                    return true;
                }
            }
            catch
            {
                // Not up yet.
            }

            Console.Write($"\r  {spinner[tick++ % 4]} waiting for ProphetOps to answer...");
            Thread.Sleep(1000);
        }

        Console.Write("\r                                        \r");
        return false;
    }

    // ---------- process helpers ----------

    private static bool ServiceExists() => !Capture("sc.exe", "query", ServiceName).Contains("1060");

    private static void Sc(params string[] args) => Run("sc.exe", false, args);

    /// For calls that are allowed to fail, such as stopping a service that is already stopped.
    private static void ScTry(params string[] args) => Run("sc.exe", true, args);

    private static void Netsh(bool ignoreFailure, params string[] args) =>
        Run("netsh", ignoreFailure, args);

    private static void Run(string file, bool ignoreFailure, params string[] args)
    {
        var info = new ProcessStartInfo(file)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var a in args) info.ArgumentList.Add(a);

        using var process = Process.Start(info)
            ?? throw new InvalidOperationException($"Could not run {file}.");

        var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0 && !ignoreFailure)
        {
            throw new InvalidOperationException(
                $"{file} {string.Join(' ', args)} failed with code {process.ExitCode}.\n{output.Trim()}");
        }
    }

    private static string Capture(string file, params string[] args)
    {
        var info = new ProcessStartInfo(file)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var a in args) info.ArgumentList.Add(a);

        using var process = Process.Start(info);
        if (process is null) return string.Empty;

        var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();
        return output;
    }

    private static bool IsAdministrator() =>
        OperatingSystem.IsWindows()
        && new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);

    /// The addresses staff type on their own phones and PCs. Adapters carrying a gateway come
    /// first, since those are the ones actually on the office network; virtual adapters from
    /// VirtualBox, VMware or WSL have none and would hand out an address nothing can reach.
    private static List<string> LocalAddresses()
    {
        var found = new List<(int Rank, string Address)>();

        foreach (var n in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (n.OperationalStatus != OperationalStatus.Up) continue;
            if (n.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
                continue;

            var properties = n.GetIPProperties();
            var routable = properties.GatewayAddresses
                .Any(g => g.Address is { } a && a.AddressFamily == AddressFamily.InterNetwork
                          && !a.Equals(IPAddress.Any));

            var physical = n.NetworkInterfaceType is NetworkInterfaceType.Wireless80211
                or NetworkInterfaceType.Ethernet or NetworkInterfaceType.GigabitEthernet;

            foreach (var unicast in properties.UnicastAddresses)
            {
                var address = unicast.Address;
                if (address.AddressFamily != AddressFamily.InterNetwork) continue;

                var text = address.ToString();
                if (text.StartsWith("169.254") || text.StartsWith("127.")) continue;

                var rank = routable ? 0 : physical ? 1 : 2;
                found.Add((rank, text));
            }
        }

        return found
            .OrderBy(f => f.Rank)
            .Select(f => f.Address)
            .Distinct()
            .ToList();
    }

    // ---------- output ----------

    private static void Banner()
    {
        Console.WriteLine();
        Console.WriteLine("  ProphetOps Setup");
        Console.WriteLine("  Decision support for Renan-Tina Travel and Tours");
        Console.WriteLine("  " + new string('-', 58));
        Console.WriteLine();
    }

    private static void Step(string message)
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write("  ... ");
        Console.ResetColor();
        Console.WriteLine(message);
    }

    private static void Ok(string message)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write("  OK  ");
        Console.ResetColor();
        Console.WriteLine(message);
    }

    private static void Warn(string message)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write("  !   ");
        Console.ResetColor();
        Console.WriteLine(message);
    }

    private static void Fail(string message)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Write("  X   ");
        Console.ResetColor();
        Console.WriteLine(message);
    }

    private static void Done(string target)
    {
        Ok("ProphetOps is installed and running.");
        Console.WriteLine();
        Console.WriteLine($"  On this PC          http://localhost:{Port}");

        var addresses = LocalAddresses();
        if (addresses.Count > 0)
        {
            Console.WriteLine($"  On other devices    http://{addresses[0]}:{Port}");
            foreach (var extra in addresses.Skip(1).Take(2))
            {
                Console.WriteLine($"       or             http://{extra}:{Port}");
            }
            Console.WriteLine();
            Console.WriteLine("  Phones and other PCs must be on the same office network as this PC.");
            Console.WriteLine("  Mobile data will not reach it.");
        }
        else
        {
            Warn("This PC has no network address, so other devices cannot reach it yet.");
            Console.WriteLine("  Connect it to the office network and the address will work.");
        }

        Console.WriteLine();
        Console.WriteLine("  A ProphetOps shortcut is on the desktop and in the Start menu.");
        Console.WriteLine();
        Console.WriteLine("  It starts by itself whenever this PC is switched on, even before");
        Console.WriteLine("  anyone signs in to Windows.");
        Console.WriteLine();
        Console.WriteLine($"  Installed in        {target}");
        Console.WriteLine($"  Daily backups in    {Path.Combine(target, "backups")}");
        Console.WriteLine();
        Console.WriteLine("  To remove it later:  Settings  >  Apps  >  ProphetOps  >  Uninstall");

        try
        {
            Process.Start(new ProcessStartInfo($"http://localhost:{Port}") { UseShellExecute = true });
        }
        catch
        {
            // No default browser, or no interactive session. The address is printed above.
        }
    }

    private static int Finish(int code)
    {
        Console.WriteLine();
        Console.WriteLine("  Press any key to close this window.");
        try
        {
            Console.ReadKey(intercept: true);
        }
        catch (InvalidOperationException)
        {
            // Launched without a console (scripted install). Nothing to wait for.
        }

        return code;
    }
}
