using FluentAssertions;
using SharedKernel.Configuration;
using Xunit;

namespace SharedKernel.Tests.Configuration;

public sealed class PortAllocationTests : IDisposable
{
    private const int DefaultBasePort = 9000;

    private readonly string _temporaryDirectory = Path.Combine(Path.GetTempPath(), $"PortAllocationTests-{Guid.NewGuid():N}");

    public PortAllocationTests()
    {
        Directory.CreateDirectory(_temporaryDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_temporaryDirectory)) Directory.Delete(_temporaryDirectory, true);
    }

    [Fact]
    public void LoadFrom_WhenPortFileMissingAndGitIsFile_ShouldPickFreeWorktreeCandidate()
    {
        // Arrange: a worktree has `.git` as a FILE (not a directory)
        File.WriteAllText(Path.Combine(_temporaryDirectory, ".git"), "gitdir: /some/other/path");

        // Act
        var allocation = PortAllocation.LoadFrom(_temporaryDirectory);

        // Assert
        PortAllocation.WorktreeCandidateBasePorts.Should().Contain(allocation.BasePort);
        File.Exists(Path.Combine(_temporaryDirectory, ".workspace", "port.txt")).Should().BeTrue();
    }

    [Fact]
    public void LoadFrom_WhenPortFileMissingAndGitIsDirectory_ShouldPickDefaultBasePort()
    {
        // Arrange: a normal repository root has `.git` as a DIRECTORY
        Directory.CreateDirectory(Path.Combine(_temporaryDirectory, ".git"));

        // Act
        var allocation = PortAllocation.LoadFrom(_temporaryDirectory);

        // Assert
        allocation.BasePort.Should().Be(DefaultBasePort);
        File.Exists(Path.Combine(_temporaryDirectory, ".workspace", "port.txt")).Should().BeTrue();
    }
}
