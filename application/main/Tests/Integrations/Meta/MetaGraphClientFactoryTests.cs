using FluentAssertions;
using Main.Integrations.Meta;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Main.Tests.Integrations.Meta;

public sealed class MetaGraphClientFactoryTests
{
    [Fact]
    public void GetClient_WhenNotConfiguredInProduction_ShouldUseUnconfiguredClient()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHttpContextAccessor();
        services.AddKeyedScoped<IMetaGraphClient, MockMetaGraphClient>("mock-meta");
        services.AddKeyedScoped<IMetaGraphClient, UnconfiguredMetaGraphClient>("unconfigured-meta");
        services.AddKeyedScoped<IMetaGraphClient, MetaGraphClient>("meta");
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment(Environments.Production));

        using var provider = services.BuildServiceProvider();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Meta:AppId"] = "",
                    ["Meta:AppSecret"] = ""
                }
            )
            .Build();

        var factory = new MetaGraphClientFactory(provider, configuration, new HttpContextAccessor(), provider.GetRequiredService<IHostEnvironment>());

        var client = factory.GetClient();

        client.Should().BeOfType<UnconfiguredMetaGraphClient>();
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "Main.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
