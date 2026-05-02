using System.CommandLine;
using DeveloperCli.Commands.ClaudeCommand;

namespace DeveloperCli.Commands;

public class ClaudeCommandCommand : Command
{
    public ClaudeCommandCommand() : base("claude-command", "Commands invoked by Claude Code skills")
    {
        Subcommands.Add(new SendInterruptSignalCommand());
    }
}
