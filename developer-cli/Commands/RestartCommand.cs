using System.CommandLine;
using DeveloperCli.Installation;
using SharedKernel.Configuration;

namespace DeveloperCli.Commands;

/// <summary>
///     Command to stop any running Aspire AppHost instance and start a fresh one.
/// </summary>
public class RestartCommand : Command
{
    public RestartCommand() : base("restart", "Stops any running Aspire AppHost and starts a fresh instance")
    {
        var watchOption = new Option<bool>("--watch", "-w") { Description = "Enable watch mode for hot reload" };
        var attachOption = new Option<bool>("--attach", "-a") { Description = "Keep the CLI process attached to the Aspire process (detached is the default)" };
        var publicUrlOption = new Option<string?>("--public-url") { Description = "Set the PUBLIC_URL environment variable for the app (e.g., https://example.ngrok-free.app)" };

        Options.Add(watchOption);
        Options.Add(attachOption);
        Options.Add(publicUrlOption);

        SetAction(parseResult => Execute(
                parseResult.GetValue(watchOption),
                parseResult.GetValue(attachOption),
                parseResult.GetValue(publicUrlOption)
            )
        );
    }

    private static void Execute(bool watch, bool attach, string? publicUrl)
    {
        Prerequisite.Ensure(Prerequisite.Dotnet, Prerequisite.Node, Prerequisite.Docker);

        // Skip stop in a fresh checkout (no port.txt) -- nothing to stop, and the check would otherwise false-positive on another stack.
        if (PortAllocation.PortFileExists(Configuration.SourceCodeFolder) && RunCommand.IsAspireRunning())
        {
            RunCommand.StopAspire();
        }

        RunCommand.CheckForPortConflicts();

        RunCommand.StartAspireAppHost(watch, attach, publicUrl);
    }
}
