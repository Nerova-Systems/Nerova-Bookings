using System.CommandLine;

namespace DeveloperCli.Commands.ClaudeCommand;

internal sealed class SendInterruptSignalCommand : Command
{
    private readonly Option<string> _agentOption = new("--agent", "-a") { Description = "Target agent name", Required = true };
    private readonly Option<string> _teamOption = new("--team", "-t") { Description = "Team name", Required = true };

    public SendInterruptSignalCommand() : base("send-interrupt-signal", "Send an interrupt signal to a team agent")
    {
        Options.Add(_teamOption);
        Options.Add(_agentOption);
        SetAction(parseResult => Execute(parseResult.GetValue(_teamOption)!, parseResult.GetValue(_agentOption)!));
    }

    private static void Execute(string team, string agent)
    {
        var homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var signalsDirectory = Path.Combine(homeDirectory, ".claude", "teams", team, "signals");
        Directory.CreateDirectory(signalsDirectory);

        var interruptId = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd:HH:mm.ss");
        var signalFilePath = Path.Combine(signalsDirectory, $"{agent}.signal");
        File.WriteAllText(signalFilePath, $"Stop working until you see message #{interruptId}");

        Console.WriteLine($"#{interruptId}");
    }
}
