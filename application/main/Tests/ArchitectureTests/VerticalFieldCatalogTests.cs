using Main.Features.Clients.Domain;
using Xunit;

namespace Main.Tests.ArchitectureTests;

/// <summary>
///     Catalog invariants from docs/vertical-template-fields-spec.md §2: keys are unique within each
///     vertical, Sensitive fields can never be exposed to any agent, choice fields always carry options,
///     and keys are stable snake_case identifiers.
/// </summary>
public sealed class VerticalFieldCatalogTests
{
    [Fact]
    public void Catalog_ForEveryVertical_ShouldHaveUniqueKeys()
    {
        foreach (var vertical in VerticalFieldCatalog.AllVerticals)
        {
            var keys = VerticalFieldCatalog.For(vertical).Select(definition => definition.Key).ToArray();
            Assert.Equal(keys.Length, keys.Distinct(StringComparer.Ordinal).Count());
        }
    }

    [Fact]
    public void Catalog_SensitiveFields_ShouldNeverBeAgentAccessible()
    {
        foreach (var vertical in VerticalFieldCatalog.AllVerticals)
        {
            foreach (var definition in VerticalFieldCatalog.For(vertical).Where(d => d.Sensitivity == VerticalFieldSensitivity.Sensitive))
            {
                Assert.Equal(VerticalFieldAgentAccess.None, definition.AgentAccess);
            }
        }
    }

    [Fact]
    public void Catalog_ChoiceFields_ShouldAlwaysCarryOptions()
    {
        foreach (var vertical in VerticalFieldCatalog.AllVerticals)
        {
            foreach (var definition in VerticalFieldCatalog.For(vertical).Where(d => d.Kind is VerticalFieldKind.Choice or VerticalFieldKind.MultiChoice))
            {
                Assert.NotEmpty(definition.Options);
            }
        }
    }

    [Fact]
    public void Catalog_Keys_ShouldBeStableSnakeCase()
    {
        foreach (var vertical in VerticalFieldCatalog.AllVerticals)
        {
            foreach (var definition in VerticalFieldCatalog.For(vertical))
            {
                Assert.Matches("^[a-z][a-z0-9_]*$", definition.Key);
            }
        }
    }

    [Fact]
    public void Catalog_EveryNonOtherVertical_ShouldHaveFields()
    {
        foreach (var vertical in VerticalFieldCatalog.AllVerticals.Where(vertical => vertical != SharedKernel.Domain.NerovaVertical.Other))
        {
            Assert.NotEmpty(VerticalFieldCatalog.For(vertical));
        }
    }
}
