using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Principal;

namespace ProphetOps.Setup;

/// One-click installer for the agency's PC.
///
/// Without this the app only runs when somebody logs in and double-clicks it, which means the
/// office loses the system every time the PC restarts and nobody thinks to start it again. This
/// registers it as a Windows Service so it comes up during boot, at the login screen, before any
/// password is typed.
internal static class Program
{
    private const string ServiceName = "ProphetOps";
    private const string DisplayName = "ProphetOps";
    private const string Executable = "ProphetOps.Api.exe";
    private const string FirewallRule = "ProphetOps (port 5099)";
    private const string DefaultTarget = @"C:\ProphetOps";
    private const int Port = 5099;

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

        Console.WriteLine($"Installing to {target}");
        Console.WriteLine();

        // A running service holds its own exe open, so it has to go down before anything is copied.
        var upgrade = ServiceExists();
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

        Console.WriteLine();
        Ok("ProphetOps has been removed.");
        Console.WriteLine();
        Console.WriteLine($"  The application folder and the database were left in place on purpose,");
        Console.WriteLine($"  so no records are lost. Delete {DefaultTarget} by hand if you are sure.");
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

    /// The address the staff type on their own phones and PCs.
    private static string? LocalAddress() =>
        NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up
                        && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .SelectMany(n => n.GetIPProperties().UnicastAddresses)
            .Select(a => a.Address)
            .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork
                                 && !a.ToString().StartsWith("169.254"))
            ?.ToString();

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
        Console.WriteLine("  On this PC          http://localhost:5099");

        var address = LocalAddress();
        if (address is not null)
        {
            Console.WriteLine($"  On other devices    http://{address}:{Port}");
        }

        Console.WriteLine();
        Console.WriteLine("  It starts by itself whenever this PC is switched on, even before");
        Console.WriteLine("  anyone signs in to Windows.");
        Console.WriteLine();
        Console.WriteLine($"  Installed in        {target}");
        Console.WriteLine($"  Daily backups in    {Path.Combine(target, "backups")}");
        Console.WriteLine();
        Console.WriteLine("  To remove it later, run this installer again with --uninstall");

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
