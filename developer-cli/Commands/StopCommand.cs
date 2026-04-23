using System.CommandLine;

namespace DeveloperCli.Commands;

/// <summary>
///     Command to stop a running Aspire AppHost instance.
/// </summary>
public class StopCommand : Command
{
    public StopCommand() : base("stop", "Stops the running Aspire AppHost")
    {
        SetAction(_ => Execute());
    }

    private static void Execute()
    {
        RunCommand.StopAspire();
    }
}
