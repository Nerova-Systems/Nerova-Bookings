using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace Main.Tests.ArchitectureTests;

/// <summary>
///     Enforces the AI Front Desk ground rules (docs/agentic-system-spec.md §0/§6.5): agent code
///     orchestrates the deterministic core through MediatR only — never repositories or the DbContext —
///     and the prerelease durable runtime is never referenced.
/// </summary>
public sealed class AgentArchitectureTests
{
    [Fact]
    public void AgentToolsAndPersona_ShouldNotDependOnDbContextOrRepositories()
    {
        // Act
        var result = Types
            .InAssembly(Configuration.Assembly)
            .That().ResideInNamespace("Main.Features.Receptionist.Agent")
            .ShouldNot().HaveDependencyOnAny("Main.Database", "Microsoft.EntityFrameworkCore")
            .GetResult();

        // Assert
        var failingTypes = string.Join(", ", result.FailingTypes?.Select(t => t.FullName) ?? []);
        result.IsSuccessful.Should().BeTrue($"Agent code must call IMediator only. Offenders: {failingTypes}");
    }

    [Fact]
    public void AgentTools_ShouldNotDependOnRepositoryInterfaces()
    {
        // The tool catalog dispatches commands/queries via IMediator; repository access would bypass
        // validation, permissions, and tenant scoping (spec §0.3).
        var agentTypes = Types
            .InAssembly(Configuration.Assembly)
            .That().ResideInNamespace("Main.Features.Receptionist.Agent")
            .GetTypes();

        foreach (var type in agentTypes)
        {
            var repositoryDependencies = type.GetConstructors()
                .SelectMany(constructor => constructor.GetParameters())
                .Where(parameter => parameter.ParameterType.Name.EndsWith("Repository"))
                .Select(parameter => $"{type.Name}({parameter.ParameterType.Name})")
                .ToArray();

            repositoryDependencies.Should().BeEmpty($"agent types must not inject repositories: {string.Join(", ", repositoryDependencies)}");
        }
    }

    [Fact]
    public void Solution_ShouldNotReferenceDurableTaskRuntime()
    {
        // The spec forbids Microsoft.Agents.AI.DurableTask (spec §0.5): PostgreSQL aggregates are the
        // checkpoints and the inbound message/webhook is the resume trigger.
        var referencedAssemblies = Configuration.Assembly.GetReferencedAssemblies();

        referencedAssemblies.Should().NotContain(assembly => assembly.Name!.Contains("DurableTask"));
    }
}
