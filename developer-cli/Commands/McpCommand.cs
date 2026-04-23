using System.CommandLine;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using DeveloperCli.Installation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace DeveloperCli.Commands;

public class McpCommand : Command
{
    public McpCommand() : base("mcp", "Start MCP server for AI integration")
    {
        SetAction(async _ => await ExecuteAsync());
    }

    private static async Task ExecuteAsync()
    {
        McpDebugLog.Initialize();
        McpDebugLog.Log("MCP server process starting");
        McpDebugLog.Log($"Process ID: {Environment.ProcessId}");
        McpDebugLog.Log($"Working directory: {Environment.CurrentDirectory}");
        McpDebugLog.Log($"CLI source: {Configuration.SourceCodeFolder}");
        McpDebugLog.Log($"Runtime: {RuntimeInformation.FrameworkDescription}, OS: {RuntimeInformation.OSDescription}");
        McpDebugLog.Log($"Args: {string.Join(" ", Environment.GetCommandLineArgs())}");
        McpDebugLog.LogGitContext();
        McpDebugLog.DetectPreviousCrash();
        McpDebugLog.WriteSessionPid();

        AppDomain.CurrentDomain.UnhandledException += (_, args) => { McpDebugLog.Log($"UNHANDLED EXCEPTION (terminating={args.IsTerminating}): {args.ExceptionObject}"); };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            McpDebugLog.Log($"UNOBSERVED TASK EXCEPTION: {args.Exception}");
            args.SetObserved();
        };

        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            McpDebugLog.Log("ProcessExit event fired -- MCP server shutting down");
            McpDebugLog.ClearSessionPid();
        };

        Console.CancelKeyPress += (_, args) =>
        {
            McpDebugLog.Log($"CancelKeyPress received (SpecialKey={args.SpecialKey})");
            args.Cancel = true; // Let the host shut down gracefully
        };

        RegisterPosixSignalHandlers();

        try
        {
            try
            {
                await Console.Error.WriteLineAsync("[MCP] Starting MCP server...");
                await Console.Error.WriteLineAsync("[MCP] Listening on stdio for MCP communication");
            }
            catch (IOException)
            {
                McpDebugLog.Log("WARNING: Failed to write startup message to stderr (stream closed)");
            }

            var builder = Host.CreateApplicationBuilder();
            builder.Logging.AddConsole(consoleLogOptions => { consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace; });

            // Allow 5 minutes for in-flight tools (like EndToEnd) to complete during shutdown.
            builder.Services.Configure<HostOptions>(options => options.ShutdownTimeout = TimeSpan.FromMinutes(5));

            builder.Services
                .AddMcpServer()
                .WithStdioServerTransport()
                .WithToolsFromAssembly();

            // Replace the SDK's SingleSessionMcpServerHostedService (which calls StopApplication on stdin EOF)
            // with our own resilient version that keeps the process alive.
            ReplaceSingleSessionHostedService(builder.Services);

            var host = builder.Build();
            McpDebugLog.Log("MCP host built successfully, starting RunAsync");

            await host.RunAsync();

            McpDebugLog.Log("MCP host RunAsync completed normally");
        }
        catch (OperationCanceledException)
        {
            McpDebugLog.Log("MCP server stopped via cancellation (normal shutdown)");
        }
        catch (Exception exception)
        {
            McpDebugLog.Log($"FATAL EXCEPTION in MCP server: {exception}");
            throw;
        }
        finally
        {
            McpDebugLog.Log("MCP server ExecuteAsync exiting");
        }
    }

    private static void RegisterPosixSignalHandlers()
    {
        // SIGTERM and SIGHUP are not available on Windows
        PosixSignalRegistration.Create(PosixSignal.SIGTERM, context =>
            {
                McpDebugLog.Log("SIGTERM received -- process is being terminated");
                context.Cancel = true;
            }
        );

        PosixSignalRegistration.Create(PosixSignal.SIGINT, context =>
            {
                McpDebugLog.Log("SIGINT received -- process interrupted");
                context.Cancel = true;
            }
        );

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            PosixSignalRegistration.Create(PosixSignal.SIGHUP, context =>
                {
                    McpDebugLog.Log("SIGHUP received -- terminal disconnected");
                    context.Cancel = true;
                }
            );
        }
    }

    private static void ReplaceSingleSessionHostedService(IServiceCollection services)
    {
        // Remove the SDK's SingleSessionMcpServerHostedService that calls StopApplication() on stdin EOF.
        var singleSessionDescriptor = services.FirstOrDefault(descriptor =>
            descriptor.ServiceType == typeof(IHostedService) && descriptor.ImplementationType?.Name == "SingleSessionMcpServerHostedService"
        );

        if (singleSessionDescriptor is not null)
        {
            services.Remove(singleSessionDescriptor);
            McpDebugLog.Log("Removed SDK's SingleSessionMcpServerHostedService");
        }
        else
        {
            McpDebugLog.Log("WARNING: Could not find SingleSessionMcpServerHostedService to remove");
        }

        services.AddHostedService<ResilientMcpServerHostedService>();
    }
}

/// <summary>
///     Replaces the SDK's SingleSessionMcpServerHostedService. When the MCP session ends (stdin EOF),
///     this service waits for in-flight tool executions to complete instead of immediately calling
///     StopApplication(). This prevents the process from being killed mid-tool-execution when the
///     client disconnects.
/// </summary>
public sealed class ResilientMcpServerHostedService(McpServer session, IHostApplicationLifetime lifetime) : BackgroundService
{
    private static readonly TimeSpan GracePeriod = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            McpDebugLog.Log("ResilientMcpServerHostedService: session starting");
            await session.RunAsync(stoppingToken);
            McpDebugLog.Log("ResilientMcpServerHostedService: session ended (stdin closed)");
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            McpDebugLog.Log("ResilientMcpServerHostedService: session cancelled by host shutdown");
            return;
        }
        catch (Exception exception)
        {
            McpDebugLog.Log($"ResilientMcpServerHostedService: session failed -- {exception}");
        }

        // Session ended (stdin EOF). Wait for any in-flight tool executions to finish
        // before shutting down the process. Tools acquire ToolExecutionSemaphore, so we
        // try to acquire it here -- if we get it, no tools are running.
        McpDebugLog.Log($"ResilientMcpServerHostedService: waiting up to {GracePeriod.TotalSeconds}s for in-flight tools");
        var acquired = await DeveloperCliMcpTools.ToolExecutionSemaphore.WaitAsync(GracePeriod, CancellationToken.None);
        if (acquired)
        {
            DeveloperCliMcpTools.ToolExecutionSemaphore.Release();
            McpDebugLog.Log("ResilientMcpServerHostedService: no in-flight tools, shutting down");
        }
        else
        {
            McpDebugLog.Log("ResilientMcpServerHostedService: grace period expired, shutting down with tools still running");
        }

        lifetime.StopApplication();
    }
}

public static class McpDebugLog
{
    private const long MaxLogFileSize = 512 * 1024; // 500 KB
    private static string? _logFilePath;
    private static string? _sessionPidFilePath;
    private static readonly Lock Lock = new();

    public static void Initialize()
    {
        var logsDirectory = Path.Combine(Configuration.WorkspaceFolder, "developer-cli", "mcp");

        if (!Directory.Exists(logsDirectory))
        {
            Directory.CreateDirectory(logsDirectory);
        }

        _logFilePath = Path.Combine(logsDirectory, "mcp-debug.log");
        _sessionPidFilePath = Path.Combine(logsDirectory, "mcp-session.pid");

        RotateLogIfNeeded();

        Log("=== MCP Debug Log Initialized ===");
    }

    public static void Log(string message)
    {
        var logFilePath = _logFilePath;
        if (logFilePath is null) return;

        var timestamp = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var threadId = Environment.CurrentManagedThreadId;
        var entry = $"[{timestamp}] [T{threadId}] [PID:{Environment.ProcessId}] {message}\n";

        try
        {
            lock (Lock)
            {
                File.AppendAllText(logFilePath, entry);
            }
        }
        catch
        {
            // Cannot fail -- this is the last resort logger
        }
    }

    public static void LogGitContext()
    {
        try
        {
            var branch = RunGitCommand("branch --show-current");
            var commit = RunGitCommand("rev-parse --short HEAD");
            Log($"Git: branch={branch}, commit={commit}");
        }
        catch (Exception exception)
        {
            Log($"Git context unavailable: {exception.Message}");
        }
    }

    public static void DetectPreviousCrash()
    {
        if (_sessionPidFilePath is null || !File.Exists(_sessionPidFilePath)) return;

        try
        {
            var previousPidText = File.ReadAllText(_sessionPidFilePath).Trim();
            if (!int.TryParse(previousPidText, out var previousPid)) return;

            var isStillRunning = false;
            try
            {
                using var process = Process.GetProcessById(previousPid);
                isStillRunning = !process.HasExited;
            }
            catch (ArgumentException)
            {
                // Process does not exist -- it crashed or was killed
            }
            catch (InvalidOperationException)
            {
                // Process has exited between GetProcessById and HasExited check
            }

            if (!isStillRunning)
            {
                Log($"CRASH DETECTED: previous session PID {previousPid} is no longer running (no clean shutdown logged)");
            }
        }
        catch (Exception exception)
        {
            Log($"WARNING: Failed to check previous session: {exception.Message}");
        }
    }

    public static void WriteSessionPid()
    {
        if (_sessionPidFilePath is null) return;

        try
        {
            File.WriteAllText(_sessionPidFilePath, Environment.ProcessId.ToString());
        }
        catch (Exception exception)
        {
            Log($"WARNING: Failed to write session PID file: {exception.Message}");
        }
    }

    public static void ClearSessionPid()
    {
        if (_sessionPidFilePath is null) return;

        try
        {
            if (File.Exists(_sessionPidFilePath))
            {
                File.Delete(_sessionPidFilePath);
            }
        }
        catch
        {
            // Best effort -- process is exiting
        }
    }

    private static void RotateLogIfNeeded()
    {
        var logFilePath = _logFilePath;
        if (logFilePath is null) return;

        try
        {
            if (!File.Exists(logFilePath)) return;

            var fileInfo = new FileInfo(logFilePath);
            if (fileInfo.Length <= MaxLogFileSize) return;

            var previousLogFilePath = logFilePath + ".prev";
            if (File.Exists(previousLogFilePath))
            {
                File.Delete(previousLogFilePath);
            }

            File.Move(logFilePath, previousLogFilePath);
        }
        catch
        {
            // Best effort -- don't block startup over log rotation
        }
    }

    private static string RunGitCommand(string arguments)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            WorkingDirectory = Configuration.SourceCodeFolder,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        process.Start();
        var output = process.StandardOutput.ReadToEnd().Trim();
        process.WaitForExit(TimeSpan.FromSeconds(5));
        return output;
    }
}

[McpServerToolType]
public static partial class DeveloperCliMcpTools
{
    private static readonly TimeSpan ProgressInterval = TimeSpan.FromSeconds(5);

    // Serialize tool execution to prevent concurrent tool calls from crashing the stdio transport.
    // When Claude Code sends multiple tool calls simultaneously and the first completes, the client
    // closes stdin before the second finishes, killing the MCP server process.
    // Internal so ResilientMcpServerHostedService can check for in-flight tools before shutdown.
    internal static readonly SemaphoreSlim ToolExecutionSemaphore = new(1, 1);

    [McpServerTool]
    [Description("Build the solution including backend (.NET), frontend (React/TypeScript), and developer CLI projects")]
    public static async Task<string> Build(
        IProgress<ProgressNotificationValue> progress,
        CancellationToken cancellationToken,
        [Description("Backend (.NET)")] bool backend = false,
        [Description("Frontend (React/TypeScript)")]
        bool frontend = false,
        [Description("Developer CLI")] bool cli = false,
        [Description("Self-contained system, e.g., 'account' (optional)")]
        string? selfContainedSystem = null)
    {
        var args = new List<string> { "build", "--quiet" };

        if (backend) args.Add("--backend");
        if (frontend) args.Add("--frontend");
        if (cli) args.Add("--cli");

        if (selfContainedSystem is not null)
        {
            args.Add("--self-contained-system");
            args.Add(selfContainedSystem);
        }

        return await ExecuteCliCommandAsync(progress, "Build", args.ToArray(), cancellationToken);
    }

    [McpServerTool]
    [Description("Run backend (.NET) xUnit tests with optional name filtering")]
    public static async Task<string> Test(
        IProgress<ProgressNotificationValue> progress,
        CancellationToken cancellationToken,
        [Description("Backend (.NET)")] bool backend = false,
        [Description("Skip build before running tests")]
        bool noBuild = false,
        [Description("Filter tests by name")] string? filter = null,
        [Description("Self-contained system, e.g., 'account' (optional)")]
        string? selfContainedSystem = null)
    {
        var args = new List<string> { "test", "--quiet" };

        if (backend) args.Add("--backend");

        if (selfContainedSystem is not null)
        {
            args.Add("--self-contained-system");
            args.Add(selfContainedSystem);
        }

        if (noBuild) args.Add("--no-build");

        if (filter is not null)
        {
            args.Add("--filter");
            args.Add(filter);
        }

        return await ExecuteCliCommandAsync(progress, "Test", args.ToArray(), cancellationToken);
    }

    [McpServerTool]
    [Description("Format and auto-fix code style for backend (.NET), frontend (React/TypeScript), and developer CLI projects")]
    public static async Task<string> Format(
        IProgress<ProgressNotificationValue> progress,
        CancellationToken cancellationToken,
        [Description("Backend (.NET)")] bool backend = false,
        [Description("Frontend (React/TypeScript)")]
        bool frontend = false,
        [Description("Developer CLI")] bool cli = false,
        [Description("Skip build before formatting")]
        bool noBuild = false,
        [Description("Self-contained system, e.g., 'account' (optional)")]
        string? selfContainedSystem = null)
    {
        var args = new List<string> { "format", "--quiet" };

        if (backend) args.Add("--backend");
        if (frontend) args.Add("--frontend");
        if (cli) args.Add("--cli");

        if (selfContainedSystem is not null)
        {
            args.Add("--self-contained-system");
            args.Add(selfContainedSystem);
        }

        if (noBuild) args.Add("--no-build");

        return await ExecuteCliCommandAsync(progress, "Format", args.ToArray(), cancellationToken);
    }

    [McpServerTool]
    [Description("Lint code for backend (.NET), frontend (React/TypeScript), and developer CLI projects to find issues")]
    public static async Task<string> Lint(
        IProgress<ProgressNotificationValue> progress,
        CancellationToken cancellationToken,
        [Description("Backend (.NET)")] bool backend = false,
        [Description("Frontend (React/TypeScript)")]
        bool frontend = false,
        [Description("Developer CLI")] bool cli = false,
        [Description("Skip build before linting")]
        bool noBuild = false,
        [Description("Self-contained system, e.g., 'account' (optional)")]
        string? selfContainedSystem = null)
    {
        var args = new List<string> { "lint", "--quiet" };

        if (backend) args.Add("--backend");
        if (frontend) args.Add("--frontend");
        if (cli) args.Add("--cli");

        if (selfContainedSystem is not null)
        {
            args.Add("--self-contained-system");
            args.Add(selfContainedSystem);
        }

        if (noBuild) args.Add("--no-build");

        return await ExecuteCliCommandAsync(progress, "Lint", args.ToArray(), cancellationToken);
    }

    [McpServerTool]
    [Description("Start .NET Aspire AppHost at https://localhost:9000. Fails if already running -- use Restart to replace a running instance, or Stop to stop it.")]
    public static Task<string> Run()
    {
        return ExecuteAspireLifecycleCommand("run", "Run", "Aspire started successfully in detached mode at https://localhost:9000");
    }

    [McpServerTool]
    [Description("Stop any running Aspire AppHost and start a fresh instance. Use after backend changes or when hot reload breaks.")]
    public static Task<string> Restart()
    {
        return ExecuteAspireLifecycleCommand("restart", "Restart", "Aspire restarted successfully in detached mode at https://localhost:9000");
    }

    [McpServerTool]
    [Description("Stop the running Aspire AppHost.")]
    public static Task<string> Stop()
    {
        return ExecuteAspireLifecycleCommand("stop", "Stop", "Aspire stopped successfully");
    }

    private static async Task<string> ExecuteAspireLifecycleCommand(string cliCommand, string toolName, string successMessage)
    {
        McpDebugLog.Log($"TOOL INVOKED: {toolName} (no parameters)");
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var developerCliPath = Path.Combine(Configuration.SourceCodeFolder, "developer-cli");
            var args = new List<string> { "run", "--project", developerCliPath, cliCommand };

            var processStartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = string.Join(" ", args),
                WorkingDirectory = Configuration.SourceCodeFolder,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = new Process();
            process.StartInfo = processStartInfo;
            var output = new List<string>();
            var errors = new List<string>();

            process.OutputDataReceived += (_, e) =>
            {
                try
                {
                    if (e.Data is not null) output.Add(e.Data);
                }
                catch (Exception exception)
                {
                    McpDebugLog.Log($"{toolName}: error in OutputDataReceived -- {exception.Message}");
                }
            };
            process.ErrorDataReceived += (_, e) =>
            {
                try
                {
                    if (e.Data is not null) errors.Add(e.Data);
                }
                catch (Exception exception)
                {
                    McpDebugLog.Log($"{toolName}: error in ErrorDataReceived -- {exception.Message}");
                }
            };

            process.Start();
            McpDebugLog.Log($"{toolName}: spawned process PID={process.Id}");
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // Wait briefly to capture startup messages, then return (don't wait for full exit)
            await Task.Delay(TimeSpan.FromSeconds(3));

            if (process.HasExited && process.ExitCode != 0)
            {
                var result = $"Failed: {toolName}.\n\n{string.Join("\n", output)}\n{string.Join("\n", errors)}";
                McpDebugLog.Log($"TOOL FAILED: {toolName} after {stopwatch.ElapsedMilliseconds}ms -- {result}");
                return result;
            }

            McpDebugLog.Log($"TOOL COMPLETED: {toolName} after {stopwatch.ElapsedMilliseconds}ms -- {successMessage}");
            return successMessage;
        }
        catch (Exception exception)
        {
            McpDebugLog.Log($"TOOL EXCEPTION: {toolName} after {stopwatch.ElapsedMilliseconds}ms -- {exception}");
            return $"Error during {toolName}: {exception.Message}";
        }
    }

    [McpServerTool]
    [Description("Run end-to-end tests")]
    public static async Task<string> EndToEnd(
        IProgress<ProgressNotificationValue> progress,
        CancellationToken cancellationToken,
        [Description("Search terms")] string[]? searchTerms = null,
        [Description("Browser (chromium, firefox, webkit, safari, all). Defaults to chromium")]
        string browser = "chromium",
        [Description("Smoke only")] bool smoke = false,
        [Description("Skip waiting for Aspire to start (by default, retries server check up to 3 minutes)")]
        bool noWaitForAspire = false,
        [Description("Maximum retry count for flaky tests, zero for no retries")]
        int? retries = null,
        [Description("Stop after the first failure")]
        bool stopOnFirstFailure = false,
        [Description("Number of times to repeat each test")]
        int? repeatEach = null,
        [Description("Only re-run the failures")]
        bool lastFailed = false,
        [Description("Number of worker processes to use for running tests")]
        int? workers = null)
    {
        var args = new List<string> { "e2e", "--quiet" };
        if (searchTerms is { Length: > 0 }) args.AddRange(searchTerms);
        args.Add("--browser");
        args.Add(browser);

        if (smoke) args.Add("--smoke");
        if (noWaitForAspire) args.Add("--no-wait-for-aspire");
        if (retries.HasValue) args.Add($"--retries={retries.Value}");
        if (stopOnFirstFailure) args.Add("--stop-on-first-failure");
        if (repeatEach.HasValue) args.Add($"--repeat-each={repeatEach.Value}");
        if (lastFailed) args.Add("--last-failed");
        if (workers.HasValue) args.Add($"--workers={workers.Value}");

        return await ExecuteCliCommandAsync(progress, "EndToEnd", args.ToArray(), cancellationToken);
    }

    [McpServerTool]
    [Description("Send an interrupt signal to a team agent. The signal is picked up by the agent's PostToolUse hook on their next tool call. Returns an interrupt ID to use in the follow-up SendMessage. For idle/sleeping agents, also send a SendMessage to wake them.")]
    public static string SendInterruptSignal(
        [Description("Team name (e.g., 'feature-team')")]
        string teamName,
        [Description("Target agent name (e.g., 'backend', 'frontend')")]
        string agentName)
    {
        McpDebugLog.Log($"TOOL INVOKED: SendInterruptSignal teamName={teamName}, agentName={agentName}");

        try
        {
            var homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var signalsDirectory = Path.Combine(homeDirectory, ".claude", "teams", teamName, "signals");

            if (!Directory.Exists(signalsDirectory))
            {
                Directory.CreateDirectory(signalsDirectory);
            }

            var interruptId = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd:HH:mm.ss");
            var signalFilePath = Path.Combine(signalsDirectory, $"{agentName}.signal");
            File.WriteAllText(signalFilePath, $"Stop working until you see message #{interruptId}");

            McpDebugLog.Log("TOOL COMPLETED: SendInterruptSignal");
            return $"Interrupt signal sent to {agentName}. Send your message now using SendMessage with prefix #{interruptId}";
        }
        catch (Exception exception)
        {
            McpDebugLog.Log($"TOOL EXCEPTION: SendInterruptSignal -- {exception}");
            return $"Error sending interrupt signal: {exception.Message}";
        }
    }

    [McpServerTool]
    [Description("Sync AI rules from .claude to .windsurf and .cursor. CRITICAL: Always make AI rule changes in .claude folder first, then sync")]
    public static async Task<string> SyncAiRules(IProgress<ProgressNotificationValue> progress, CancellationToken cancellationToken)
    {
        return await ExecuteCliCommandAsync(progress, "SyncAiRules", ["sync-ai-rules"], cancellationToken);
    }

    private static async Task<string> ExecuteCliCommandAsync(IProgress<ProgressNotificationValue> progress, string toolName, string[] args, CancellationToken cancellationToken)
    {
        McpDebugLog.Log($"TOOL QUEUED: {toolName} -- args: [{string.Join(" ", args)}]");

        await ToolExecutionSemaphore.WaitAsync(cancellationToken);
        try
        {
            McpDebugLog.Log($"TOOL STARTED: {toolName} -- semaphore acquired");
            var stopwatch = Stopwatch.StartNew();

            var developerCliPath = Path.Combine(Configuration.SourceCodeFolder, "developer-cli");
            var allArgs = new List<string> { "run", "--project", developerCliPath, "--" };
            allArgs.AddRange(args);

            var command = $"dotnet {string.Join(" ", allArgs.Select(arg => arg.Contains(' ') ? $"\"{arg}\"" : arg))}";
            McpDebugLog.Log($"TOOL EXECUTING: {toolName} -- command: {command}");

            var processStartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = string.Join(" ", allArgs.Select(arg => arg.Contains(' ') ? $"\"{arg}\"" : arg)),
                WorkingDirectory = Configuration.SourceCodeFolder,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(processStartInfo)!;

            // Do NOT register cancellationToken to kill the child process. The SDK's CancellationToken
            // fires when the client sends notifications/cancelled OR when stdin closes. In both cases,
            // we want the child process to finish its work rather than being killed mid-execution.
            // The ResilientMcpServerHostedService waits for in-flight tools before stopping the process.
            if (cancellationToken.IsCancellationRequested)
            {
                McpDebugLog.Log($"TOOL CANCELLED BEFORE START: {toolName} -- cancellation already requested");
            }

            var stdout = new StringBuilder();
            var stderr = new StringBuilder();
            var progressCounter = 0;

            var stdoutTask = ReadStreamWithProgressAsync(process.StandardOutput, stdout, progress, toolName, stopwatch, () => progressCounter++);
            var stderrTask = ReadStreamAsync(process.StandardError, stderr);

            await Task.WhenAll(stdoutTask, stderrTask);
            // Pass CancellationToken.None -- let the process finish even if the MCP client cancelled
            await process.WaitForExitAsync(CancellationToken.None);

            var combinedOutput = $"{stdout}\n{stderr}";
            var outputLength = combinedOutput.Length;
            var truncatedOutput = outputLength > 500 ? combinedOutput[..500] + "..." : combinedOutput;
            McpDebugLog.Log($"TOOL COMPLETED: {toolName} after {stopwatch.ElapsedMilliseconds}ms -- exitCode={process.ExitCode}, outputLength={outputLength}, output: {truncatedOutput}");

            const int maxOutputLength = 50_000;
            if (combinedOutput.Length > maxOutputLength)
            {
                var half = maxOutputLength / 2;
                combinedOutput = $"{combinedOutput[..half]}\n\n... [truncated {combinedOutput.Length - maxOutputLength} characters] ...\n\n{combinedOutput[^half..]}";
            }

            return combinedOutput;
        }
        catch (OperationCanceledException)
        {
            McpDebugLog.Log($"TOOL CANCELLED: {toolName} -- cancelled while waiting for semaphore");
            return $"Tool {toolName} was cancelled while queued.";
        }
        catch (Exception exception)
        {
            McpDebugLog.Log($"TOOL EXCEPTION: {toolName} -- {exception}");
            return $"Error executing command: {exception.Message}";
        }
        finally
        {
            ToolExecutionSemaphore.Release();
            McpDebugLog.Log($"TOOL RELEASED: {toolName} -- semaphore released");
        }
    }

    private static async Task ReadStreamWithProgressAsync(StreamReader reader, StringBuilder output, IProgress<ProgressNotificationValue> progress, string toolName, Stopwatch stopwatch, Func<int> nextCounter)
    {
        var lastProgressTime = stopwatch.Elapsed;
        var totalTests = 0;
        var completedTests = 0;

        try
        {
            while (await reader.ReadLineAsync() is { } line)
            {
                output.AppendLine(line);

                // Strip ANSI escape codes for pattern matching
                var cleanLine = AnsiEscapeRegex().Replace(line, "");

                // Parse "Running N tests using M workers" to get total
                if (totalTests == 0 && cleanLine.StartsWith("Running ") && cleanLine.Contains(" tests using "))
                {
                    var parts = cleanLine.Split(' ');
                    if (parts.Length >= 2 && int.TryParse(parts[1], out var parsed))
                    {
                        totalTests = parsed;
                        McpDebugLog.Log($"PROGRESS parsed total tests: {totalTests} for {toolName}");
                    }
                }

                // Count completed tests (lines starting with check mark or cross)
                var trimmed = cleanLine.TrimStart();
                if (totalTests > 0 && trimmed.Length > 0 && (trimmed[0] == '✓' || trimmed[0] == '✘' || trimmed.StartsWith("- ✓") || trimmed.StartsWith("- ✘")))
                {
                    completedTests++;
                }

                var now = stopwatch.Elapsed;
                if (now - lastProgressTime >= ProgressInterval)
                {
                    lastProgressTime = now;
                    var counter = nextCounter();
                    try
                    {
                        if (totalTests > 0)
                        {
                            progress.Report(new ProgressNotificationValue { Progress = completedTests, Total = totalTests });
                            McpDebugLog.Log($"PROGRESS sent for {toolName} at {now.Minutes}m {now.Seconds}s ({completedTests}/{totalTests} tests)");
                        }
                        else
                        {
                            progress.Report(new ProgressNotificationValue { Progress = counter, Total = counter + 10 });
                            McpDebugLog.Log($"PROGRESS sent for {toolName} at {now.Minutes}m {now.Seconds}s (counter={counter}, no total yet)");
                        }
                    }
                    catch (Exception exception)
                    {
                        McpDebugLog.Log($"PROGRESS failed for {toolName}: {exception.Message}");
                    }
                }
            }
        }
        catch (IOException exception)
        {
            McpDebugLog.Log($"STREAM ERROR reading stdout for {toolName}: {exception.Message}");
        }
        catch (ObjectDisposedException exception)
        {
            McpDebugLog.Log($"STREAM DISPOSED reading stdout for {toolName}: {exception.Message}");
        }
    }

    [GeneratedRegex(@"\x1B\[[0-9;]*m")]
    private static partial Regex AnsiEscapeRegex();

    private static async Task ReadStreamAsync(StreamReader reader, StringBuilder output)
    {
        try
        {
            while (await reader.ReadLineAsync() is { } line)
            {
                output.AppendLine(line);
            }
        }
        catch (IOException)
        {
            // Stream closed unexpectedly -- child process may have been killed
        }
        catch (ObjectDisposedException)
        {
            // Stream disposed -- process already exited
        }
    }
}
