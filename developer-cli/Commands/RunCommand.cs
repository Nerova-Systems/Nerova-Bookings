using System.CommandLine;
using System.Diagnostics;
using System.Text.Json;
using DeveloperCli.Installation;
using DeveloperCli.Utilities;
using SharedKernel.Configuration;
using Spectre.Console;

namespace DeveloperCli.Commands;

/// <summary>
///     Command to start the Aspire AppHost. Use <c>restart</c> to start a fresh instance or <c>stop</c> to stop it.
/// </summary>
public class RunCommand : Command
{
    public RunCommand() : base("run", "Runs Aspire AppHost (use --watch for hot reload)")
    {
        var watchOption = new Option<bool>("--watch", "-w") { Description = "Enable watch mode for hot reload" };
        var attachOption = new Option<bool>("--attach", "-a") { Description = "Keep the CLI process attached to the Aspire process (detached is the default)" };
        var publicUrlOption = new Option<string?>("--public-url") { Description = "Set the PUBLIC_URL environment variable and serve the whole app from that origin (e.g., https://example.ngrok-free.app)" };
        var facebookOAuthPublicUrlOption = new Option<string?>("--facebook-oauth-public-url") { Description = "Set the Facebook/Meta OAuth callback URL. Use 'auto' to start or reuse a free ngrok tunnel without changing PUBLIC_URL." };

        Options.Add(watchOption);
        Options.Add(attachOption);
        Options.Add(publicUrlOption);
        Options.Add(facebookOAuthPublicUrlOption);

        SetAction(parseResult => Execute(
                parseResult.GetValue(watchOption),
                parseResult.GetValue(attachOption),
                parseResult.GetValue(publicUrlOption),
                parseResult.GetValue(facebookOAuthPublicUrlOption)
            )
        );
    }

    // The CLI binary is published outside the repo, so PortAllocation.Load (which walks up from
    // AppContext.BaseDirectory) cannot find the repo. Use the CLI's known SourceCodeFolder instead.
    internal static PortAllocation Ports => PortAllocation.LoadFrom(Configuration.SourceCodeFolder);

    internal static int AspirePort => Ports.Aspire;

    internal static int DashboardPort => Ports.OtelEndpoint;

    internal static int ResourceServicePort => Ports.ResourceService;

    private static void Execute(bool watch, bool attach, string? publicUrl, string? facebookOAuthPublicUrl)
    {
        Prerequisite.Ensure(Prerequisite.Dotnet, Prerequisite.Node, Prerequisite.Docker);

        // Refuse if Aspire is already on the currently configured port.
        // Skipped in a fresh worktree (no port.txt) where the check would false-positive on another worktree's stack.
        if (PortAllocation.PortFileExists(Configuration.SourceCodeFolder) && IsAspireRunning())
        {
            var alias = Configuration.AliasName;
            AnsiConsole.MarkupLine($"[yellow]Aspire AppHost is already running on port {AspirePort}. Run '{alias} stop' to stop it or '{alias} restart' to start a fresh instance.[/]");
            Environment.Exit(1);
        }

        CheckForPortConflicts();

        StartAspireAppHost(watch, attach, publicUrl, facebookOAuthPublicUrl);
    }

    internal static bool IsAspireRunning()
    {
        // Check the main Aspire port
        if (Configuration.IsWindows)
        {
            // Windows: Check all Aspire ports
            var aspirePortsToCheck = new[] { AspirePort, DashboardPort, ResourceServicePort };
            foreach (var port in aspirePortsToCheck)
            {
                var portCheckCommand = $"""powershell -Command "Get-NetTCPConnection -LocalPort {port} -State Listen -ErrorAction SilentlyContinue" """;
                var result = ProcessHelper.StartProcess(portCheckCommand, redirectOutput: true, exitOnError: false);

                if (!string.IsNullOrWhiteSpace(result))
                {
                    return true;
                }
            }
        }
        else
        {
            // macOS/Linux: Original logic - only check main port
            var portCheckCommand = $"lsof -i :{AspirePort} -sTCP:LISTEN -t";
            var result = ProcessHelper.StartProcess(portCheckCommand, redirectOutput: true, exitOnError: false);
            if (!string.IsNullOrWhiteSpace(result))
            {
                return true;
            }
        }

        // Also check if there are any dotnet processes running AppHost for THIS project (both run and watch modes)
        if (Configuration.IsWindows)
        {
            var escapedPath = Configuration.SourceCodeFolder.Replace("\\", "\\\\");
            var appHostProcesses = ProcessHelper.StartProcess($$"""powershell -Command "Get-Process dotnet -ErrorAction SilentlyContinue | Where-Object {$_.CommandLine -like '*AppHost*' -and $_.CommandLine -like '*{{escapedPath}}*'} | Select-Object Id" """, redirectOutput: true, exitOnError: false);
            return !string.IsNullOrWhiteSpace(appHostProcesses) && appHostProcesses.Contains("Id");
        }

        var pidsOutput = ProcessHelper.StartProcess("pgrep -f dotnet.*AppHost", redirectOutput: true, exitOnError: false);
        if (string.IsNullOrWhiteSpace(pidsOutput)) return false;

        foreach (var pid in pidsOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var commandLine = ProcessHelper.StartProcess($"ps -p {pid} -o args=", redirectOutput: true, exitOnError: false).Trim();
            if (commandLine.Contains(Configuration.SourceCodeFolder, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    internal static void CheckForPortConflicts()
    {
        // Check if any Aspire port is held by a process from a different project
        var ports = new[] { AspirePort, DashboardPort, ResourceServicePort };
        var conflictSource = ports
            .SelectMany(GetListeningProcessCommandLines)
            .Select(FindProjectRoot)
            .FirstOrDefault(root => root is not null);

        if (conflictSource is null) return;

        AnsiConsole.MarkupLine($"[red]Aspire ports are in use by another project: {conflictSource}[/]");
        AnsiConsole.MarkupLine("[red]Stop that instance first, then try again.[/]");
        Environment.Exit(1);
    }

    private static string[] GetListeningProcessCommandLines(int port)
    {
        if (Configuration.IsWindows)
        {
            var output = ProcessHelper.StartProcess($"""powershell -Command "Get-NetTCPConnection -LocalPort {port} -State Listen -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty OwningProcess" """, redirectOutput: true, exitOnError: false).Trim();
            if (string.IsNullOrWhiteSpace(output)) return [];

            var commandLine = ProcessHelper.StartProcess($"""powershell -Command "(Get-Process -Id {output} -ErrorAction SilentlyContinue).CommandLine" """, redirectOutput: true, exitOnError: false).Trim();
            return string.IsNullOrWhiteSpace(commandLine) ? [] : [commandLine];
        }

        var processIds = ProcessHelper.StartProcess($"lsof -i :{port} -sTCP:LISTEN -t", redirectOutput: true, exitOnError: false).Trim();
        if (string.IsNullOrWhiteSpace(processIds)) return [];

        return processIds
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(id => ProcessHelper.StartProcess($"ps -p {id} -o args=", redirectOutput: true, exitOnError: false).Trim())
            .Where(args => !string.IsNullOrWhiteSpace(args))
            .ToArray();
    }

    private static string? FindProjectRoot(string commandLine)
    {
        // Command lines contain paths like .../SomeProject/application/AppHost/...
        var separator = commandLine.Contains('\\') ? "\\" : "/";
        var marker = $"{separator}application{separator}";
        var index = commandLine.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index == -1) return null;

        var pathStart = commandLine.LastIndexOf(' ', index) + 1;
        return commandLine[pathStart..index];
    }

    internal static void StopAspire()
    {
        StopAspire(Configuration.SourceCodeFolder, Ports);
    }

    internal static void StopAspire(string sourceCodeFolder, PortAllocation portAllocation)
    {
        AnsiConsole.MarkupLine($"[blue]Stopping Aspire AppHost on base port {portAllocation.BasePort}...[/]");

        if (Configuration.IsWindows)
        {
            // Kill dotnet and rsbuild-node processes listening on any port in the explicit allocation.
            var allPorts = portAllocation.AllPorts;
            var netstatOutput = ProcessHelper.StartProcess("""cmd /c "netstat -ano | findstr LISTENING" """, redirectOutput: true, exitOnError: false);
            if (!string.IsNullOrWhiteSpace(netstatOutput))
            {
                var processedPids = new HashSet<string>();

                foreach (var line in netstatOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 5) continue;

                    var address = parts[1];
                    var portIndex = address.LastIndexOf(':');
                    if (portIndex == -1) continue;

                    if (!int.TryParse(address[(portIndex + 1)..], out var port) || !allPorts.Contains(port)) continue;

                    var pid = parts[^1].Trim();
                    if (string.IsNullOrWhiteSpace(pid)) continue;
                    if (!int.TryParse(pid, out _)) continue;
                    if (!processedPids.Add(pid)) continue;

                    var processName = ProcessHelper.StartProcess($"""powershell -Command "(Get-Process -Id {pid} -ErrorAction SilentlyContinue).Name" """, redirectOutput: true, exitOnError: false);

                    if (processName.Contains("dotnet", StringComparison.OrdinalIgnoreCase) ||
                        processName.Contains("rsbuild-node", StringComparison.OrdinalIgnoreCase))
                    {
                        ProcessHelper.StartProcess($"taskkill /F /PID {pid}", redirectOutput: true, exitOnError: false);
                    }
                }
            }

            // Kill specific Aspire-related processes
            var processesToKill = new[] { "Aspire.Dashboard", "dcp", "dcpproc" };
            foreach (var processName in processesToKill)
            {
                ProcessHelper.StartProcess($"taskkill /F /IM {processName}.exe", redirectOutput: true, exitOnError: false);
            }

            foreach (var processId in FindAppHostProcesses(sourceCodeFolder))
            {
                KillProcessTree(processId);
            }
        }
        else
        {
            // Find AppHost processes for this worktree, then kill each one and all its children
            // (children include Aspire infrastructure: dcp, dcpproc, Aspire.Dashboard, etc.)
            foreach (var processId in FindAppHostProcesses(sourceCodeFolder))
            {
                KillProcessTree(processId);
            }
        }

        // Wait a moment for processes to terminate
        Thread.Sleep(TimeSpan.FromSeconds(2));

        AnsiConsole.MarkupLine("[green]Aspire AppHost stopped.[/]");
    }

    private static string[] FindAppHostProcesses(string sourceCodeFolder)
    {
        if (Configuration.IsWindows)
        {
            var escapedSourceCodeFolder = sourceCodeFolder.Replace("'", "''");
            var windowsOutput = ProcessHelper.StartProcess($"""powershell -Command "Get-CimInstance Win32_Process -Filter 'Name = ''dotnet.exe''' | Where-Object CommandLine -like '*AppHost.csproj*' | Where-Object CommandLine -like '*{escapedSourceCodeFolder}*' | Select-Object -ExpandProperty ProcessId" """, redirectOutput: true, exitOnError: false);
            if (string.IsNullOrWhiteSpace(windowsOutput)) return [];

            return windowsOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        var output = ProcessHelper.StartProcess("pgrep -f dotnet.*AppHost", redirectOutput: true, exitOnError: false);
        if (string.IsNullOrWhiteSpace(output)) return [];

        return output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(processId =>
                {
                    var commandLine = ProcessHelper.StartProcess($"ps -p {processId} -o args=", redirectOutput: true, exitOnError: false).Trim();
                    return commandLine.Contains(sourceCodeFolder, StringComparison.OrdinalIgnoreCase);
                }
            )
            .ToArray();
    }

    internal static void KillProcessTree(string processId)
    {
        if (Configuration.IsWindows)
        {
            ProcessHelper.StartProcess($"taskkill /T /F /PID {processId}", redirectOutput: true, exitOnError: false);
            return;
        }

        var children = ProcessHelper.StartProcess($"pgrep -P {processId}", redirectOutput: true, exitOnError: false);
        if (!string.IsNullOrWhiteSpace(children))
        {
            foreach (var childId in children.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                KillProcessTree(childId);
            }
        }

        ProcessHelper.StartProcess($"kill -9 {processId}", redirectOutput: true, exitOnError: false);
    }

    internal static void StartAspireAppHost(bool watch, bool attach, string? publicUrl, string? facebookOAuthPublicUrl = null)
    {
        var mode = watch ? "watch" : "run";
        AnsiConsole.MarkupLine($"[blue]Starting Aspire AppHost in {mode} mode ({(attach ? "attached" : "detached")})...[/]");

        var localPublicUrl = $"https://app.dev.localhost:{Ports.AppGateway}";
        var effectivePublicUrl = publicUrl ?? localPublicUrl;
        if (publicUrl is not null)
        {
            AnsiConsole.MarkupLine($"[blue]Using PUBLIC_URL: {publicUrl}[/]");
            AnsiConsole.MarkupLine("[yellow]PUBLIC_URL changes the SPA origin, CSP, asset URLs, and HMR origin. Use --facebook-oauth-public-url auto for OAuth-only tunneling.[/]");
        }
        else if (Environment.GetEnvironmentVariable("PUBLIC_URL") is { Length: > 0 } inheritedPublicUrl &&
                 !string.Equals(inheritedPublicUrl.TrimEnd('/'), localPublicUrl, StringComparison.OrdinalIgnoreCase))
        {
            AnsiConsole.MarkupLine($"[yellow]Ignoring inherited PUBLIC_URL={Markup.Escape(inheritedPublicUrl)}. Using local app origin {localPublicUrl}.[/]");
        }

        var resolvedFacebookOAuthPublicUrl = ResolveFacebookOAuthPublicUrl(facebookOAuthPublicUrl);

        var appHostProjectPath = Path.Combine(Configuration.ApplicationFolder, "AppHost", "AppHost.csproj");
        var command = watch
            ? $"dotnet watch --non-interactive --project {appHostProjectPath}"
            : $"dotnet run --project {appHostProjectPath}";

        // AppHost reads .workspace/port.txt itself and overrides the Aspire dashboard env vars
        // before CreateBuilder. These env vars are forwarded only for the new AppHost process.
        var envVars = new List<(string Name, string Value)> { ("PUBLIC_URL", effectivePublicUrl) };
        if (resolvedFacebookOAuthPublicUrl is not null) envVars.Add(("OAUTH_FACEBOOK_PUBLIC_URL", resolvedFacebookOAuthPublicUrl));

        if (attach)
        {
            ProcessHelper.StartProcess(command, Configuration.ApplicationFolder, waitForExit: true, environmentVariables: envVars.ToArray());
            return;
        }

        var logPath = Path.Combine(Configuration.WorkspaceFolder, "developer-cli", "aspire-apphost.log");
        Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
        if (File.Exists(logPath)) File.Delete(logPath);

        var detachedCommand = Configuration.IsWindows
            ? $"cmd /c start \"\" /min cmd /c \"{command} > \"{logPath}\" 2>&1\""
            : Configuration.IsMacOs
                ? $"sh -c \"script -q -t 0 '{logPath}' {command} > /dev/null 2>&1 &\""
                : $"sh -c \"script -q -f -c '{command}' '{logPath}' > /dev/null 2>&1 &\"";

        ProcessHelper.StartProcess(detachedCommand, Configuration.ApplicationFolder, waitForExit: false, environmentVariables: envVars.ToArray());

        TailLogUntilReady(logPath);
    }

    private static void TailLogUntilReady(string logPath)
    {
        const string readyMarker = "Distributed application started.";
        const string misleadingShutdownHint = " Press Ctrl+C to shut down.";
        var deadline = DateTime.UtcNow.AddSeconds(60);
        var offset = 0L;
        var sawFirstLine = false;

        AnsiConsole.MarkupLine("[dim]Waiting for AppHost output...[/]");

        while (DateTime.UtcNow < deadline)
        {
            if (File.Exists(logPath))
            {
                using var stream = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                stream.Seek(offset, SeekOrigin.Begin);
                using var reader = new StreamReader(stream);

                while (reader.ReadLine() is { } line)
                {
                    sawFirstLine = true;
                    var displayLine = line.Replace(misleadingShutdownHint, "").TrimEnd();
                    AnsiConsole.WriteLine(displayLine);

                    if (line.Contains(readyMarker))
                    {
                        AnsiConsole.MarkupLine("[green]Aspire AppHost is ready.[/]");
                        AnsiConsole.MarkupLine($"[dim]Stop with:[/] [yellow]{Configuration.AliasName} stop[/]");
                        AnsiConsole.MarkupLine($"[dim]Logs:[/] {logPath}");
                        return;
                    }
                }

                offset = stream.Position;
            }

            Thread.Sleep(sawFirstLine ? 100 : 300);
        }

        AnsiConsole.MarkupLine($"[yellow]Aspire did not report ready within 60s. Check {logPath}[/]");
    }

    private static string? ResolveFacebookOAuthPublicUrl(string? facebookOAuthPublicUrl)
    {
        if (string.IsNullOrWhiteSpace(facebookOAuthPublicUrl)) return null;

        var resolvedPublicUrl = string.Equals(facebookOAuthPublicUrl, "auto", StringComparison.OrdinalIgnoreCase)
            ? StartOrReuseNgrokTunnel()
            : StartOrReuseConcreteNgrokTunnel(facebookOAuthPublicUrl.TrimEnd('/'));

        AnsiConsole.MarkupLine($"[blue]Using OAUTH_FACEBOOK_PUBLIC_URL: {resolvedPublicUrl}[/]");
        PrintFacebookOAuthDashboardValues(resolvedPublicUrl);
        return resolvedPublicUrl;
    }

    private static string StartOrReuseNgrokTunnel()
    {
        EnsureNgrokInstalled();

        var existingTunnel = TryGetNgrokTunnelUrl();
        if (existingTunnel is not null)
        {
            AnsiConsole.MarkupLine($"[yellow]Reusing existing ngrok tunnel: {existingTunnel}[/]");
            return existingTunnel;
        }

        AnsiConsole.MarkupLine("[blue]Starting free ngrok tunnel for Facebook/Meta OAuth callbacks...[/]");
        StartNgrokProcess(null);
        return WaitForNgrokTunnelUrl();
    }

    private static string StartOrReuseConcreteNgrokTunnel(string publicUrl)
    {
        if (!Uri.TryCreate(publicUrl, UriKind.Absolute, out var uri) || string.IsNullOrWhiteSpace(uri.Host))
        {
            AnsiConsole.MarkupLine($"[red]--facebook-oauth-public-url must be 'auto' or an absolute URL. Received: {Markup.Escape(publicUrl)}[/]");
            Environment.Exit(1);
        }

        if (!uri.Host.Contains(".ngrok-free.app", StringComparison.OrdinalIgnoreCase) &&
            !uri.Host.Contains(".ngrok.io", StringComparison.OrdinalIgnoreCase))
        {
            return publicUrl;
        }

        EnsureNgrokInstalled();

        var existingTunnel = TryGetNgrokTunnelUrl();
        if (string.Equals(existingTunnel, publicUrl, StringComparison.OrdinalIgnoreCase))
        {
            AnsiConsole.MarkupLine($"[yellow]Reusing existing ngrok tunnel: {existingTunnel}[/]");
            return publicUrl;
        }

        if (existingTunnel is not null)
        {
            AnsiConsole.MarkupLine($"[red]Ngrok is already running with a different URL: {existingTunnel}[/]");
            AnsiConsole.MarkupLine("[red]Stop ngrok or use --facebook-oauth-public-url auto to use the active free tunnel.[/]");
            Environment.Exit(1);
        }

        AnsiConsole.MarkupLine($"[blue]Starting ngrok tunnel for {publicUrl}...[/]");
        StartNgrokProcess(uri.Host);

        var actualUrl = WaitForNgrokTunnelUrl();
        if (!string.Equals(actualUrl, publicUrl, StringComparison.OrdinalIgnoreCase))
        {
            AnsiConsole.MarkupLine($"[red]Ngrok started with '{actualUrl}' instead of requested '{publicUrl}'.[/]");
            AnsiConsole.MarkupLine("[red]Free ngrok accounts cannot recreate arbitrary/reserved URLs. Use --facebook-oauth-public-url auto and update Meta with the printed URL.[/]");
            Environment.Exit(1);
        }

        return publicUrl;
    }

    private static void EnsureNgrokInstalled()
    {
        var ngrokVersion = ProcessHelper.StartProcess("ngrok version", redirectOutput: true, exitOnError: false);
        if (!ngrokVersion.Contains("ngrok version", StringComparison.OrdinalIgnoreCase))
        {
            AnsiConsole.MarkupLine("[red]Ngrok is not installed. Install ngrok from https://ngrok.com/download, then rerun with --facebook-oauth-public-url auto.[/]");
            Environment.Exit(1);
        }
    }

    private static void StartNgrokProcess(string? hostname)
    {
        var arguments = hostname is null
            ? $"http https://localhost:{Ports.AppGateway} --host-header app.dev.localhost:{Ports.AppGateway}"
            : $"http --url={hostname} https://localhost:{Ports.AppGateway} --host-header app.dev.localhost:{Ports.AppGateway}";

        var processStartInfo = new ProcessStartInfo
        {
            FileName = "ngrok",
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        if (Configuration.IsWindows)
        {
            processStartInfo.WindowStyle = ProcessWindowStyle.Hidden;
        }

        ProcessHelper.StartProcess(processStartInfo, waitForExit: false, exitOnError: false);
    }

    private static string WaitForNgrokTunnelUrl()
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            var tunnelUrl = TryGetNgrokTunnelUrl();
            if (tunnelUrl is not null) return tunnelUrl;

            Thread.Sleep(500);
        }

        AnsiConsole.MarkupLine("[red]Ngrok did not expose a tunnel at http://127.0.0.1:4040/api/tunnels within 15 seconds.[/]");
        AnsiConsole.MarkupLine("[red]Run 'ngrok http https://localhost:9000 --host-header app.dev.localhost:9000' manually, then rerun with --facebook-oauth-public-url auto.[/]");
        Environment.Exit(1);
        return string.Empty;
    }

    private static string? TryGetNgrokTunnelUrl()
    {
        try
        {
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            var json = httpClient.GetStringAsync("http://127.0.0.1:4040/api/tunnels").GetAwaiter().GetResult();
            using var document = JsonDocument.Parse(json);
            var tunnels = document.RootElement.GetProperty("tunnels");

            foreach (var tunnel in tunnels.EnumerateArray())
            {
                if (!tunnel.TryGetProperty("public_url", out var publicUrlElement)) continue;
                var publicUrl = publicUrlElement.GetString();
                if (string.IsNullOrWhiteSpace(publicUrl) || !publicUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) continue;
                return publicUrl.TrimEnd('/');
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    private static void PrintFacebookOAuthDashboardValues(string publicUrl)
    {
        var callbackBase = $"{publicUrl}/api/account/authentication/Facebook";
        AnsiConsole.Write(new Panel(Markup.Escape($"""
                                                   App Domain:
                                                   {new Uri(publicUrl).Host}

                                                   Valid OAuth Redirect URIs:
                                                   {callbackBase}/login/callback
                                                   {callbackBase}/signup/callback
                                                   {callbackBase}/link/callback
                                                   """))
            .Header("Meta dashboard values")
            .Expand()
        );
    }
}
