using System.CommandLine;
using SharedKernel.Configuration;
using Spectre.Console;

namespace DeveloperCli.Commands;

public sealed class FacebookOAuthCommand : Command
{
    public FacebookOAuthCommand() : base("facebook-oauth", "Show the exact Meta dashboard and local Aspire settings for Facebook Login for Business")
    {
        Subcommands.Add(new FacebookOAuthSetupCommand());
    }

    private sealed class FacebookOAuthSetupCommand : Command
    {
        private readonly Option<string?> _appIdOption = new("--app-id") { Description = "Facebook App ID from Meta for Developers" };
        private readonly Option<string?> _configIdOption = new("--config-id") { Description = "Facebook Login for Business configuration ID from Meta for Developers" };
        private readonly Option<string?> _publicUrlOption = new("--public-url") { Description = "Public URL used for Facebook OAuth callbacks" };

        public FacebookOAuthSetupCommand() : base("setup", "Print the Facebook Login for Business setup checklist")
        {
            Options.Add(_appIdOption);
            Options.Add(_configIdOption);
            Options.Add(_publicUrlOption);

            SetAction(parseResult => Execute(
                parseResult.GetValue(_appIdOption),
                parseResult.GetValue(_configIdOption),
                parseResult.GetValue(_publicUrlOption)
            ));
        }

        private static void Execute(string? appId, string? configId, string? publicUrl)
        {
            var setup = FacebookOAuthSetup.Create(appId, configId, publicUrl);

            AnsiConsole.MarkupLine("[bold blue]Facebook Login for Business setup[/]");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[yellow]Use this for Meta business login, not consumer social login.[/]");
            AnsiConsole.MarkupLine("[grey]Requested permissions are controlled by the Meta business login configuration, not by OAuth scope parameters.[/]");
            AnsiConsole.WriteLine();

            WriteMetaDashboardPanel(setup);
            WriteAspirePanel(setup);
            WriteReminderPanel();
        }

        private static void WriteMetaDashboardPanel(FacebookOAuthSetup setup)
        {
            AnsiConsole.MarkupLine("[bold]Meta dashboard values[/]");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[grey]App Domains[/]");
            Console.WriteLine(setup.AppDomain);
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[grey]Valid OAuth Redirect URIs[/]");
            Console.WriteLine(setup.LoginCallbackUrl);
            Console.WriteLine(setup.SignupCallbackUrl);
            Console.WriteLine(setup.LinkCallbackUrl);
            AnsiConsole.WriteLine();
        }

        private static void WriteAspirePanel(FacebookOAuthSetup setup)
        {
            var appId = string.IsNullOrWhiteSpace(setup.AppId) ? "<your Meta App ID>" : setup.AppId;
            var configId = string.IsNullOrWhiteSpace(setup.ConfigId) ? "<your Meta business login configuration ID>" : setup.ConfigId;
            var localSettings = $"""
                                 facebook-oauth-enabled = true
                                 facebook-oauth-client-id = {appId}
                                 facebook-oauth-client-secret = <your Meta App Secret>
                                 OAUTH_FACEBOOK_LOGIN_CONFIGURATION_ID = {configId}
                                 """;

            if (!setup.UsesDefaultPublicUrl)
            {
                localSettings += $"{Environment.NewLine}OAUTH_FACEBOOK_PUBLIC_URL = {setup.PublicUrl}";
            }

            AnsiConsole.Write(new Panel(Markup.Escape(localSettings))
                .Header("Local Aspire values")
                .Expand()
            );
            AnsiConsole.WriteLine();
        }

        private static void WriteReminderPanel()
        {
            AnsiConsole.Write(new Panel(
                    """
                    After updating Meta and Aspire values, restart Aspire.
                    If Meta rejects localhost, use a public HTTPS URL such as ngrok and pass it with --public-url.
                    """
                )
                .Header("Next step")
                .Expand()
            );
        }
    }
}

internal sealed record FacebookOAuthSetup(
    string? AppId,
    string? ConfigId,
    string PublicUrl,
    string AppDomain,
    string LoginCallbackUrl,
    string SignupCallbackUrl,
    string LinkCallbackUrl,
    bool UsesDefaultPublicUrl
)
{
    public static FacebookOAuthSetup Create(string? appId, string? configId, string? publicUrl)
    {
        var defaultPublicUrl = $"https://localhost:{PortAllocation.Load().AppGateway}";
        var resolvedPublicUrl = string.IsNullOrWhiteSpace(publicUrl) ? defaultPublicUrl : publicUrl.Trim();
        resolvedPublicUrl = resolvedPublicUrl.TrimEnd('/');
        var appDomain = ResolveAppDomain(resolvedPublicUrl);

        return new FacebookOAuthSetup(
            appId,
            configId,
            resolvedPublicUrl,
            appDomain,
            BuildCallbackUrl(resolvedPublicUrl, "login"),
            BuildCallbackUrl(resolvedPublicUrl, "signup"),
            BuildCallbackUrl(resolvedPublicUrl, "link"),
            resolvedPublicUrl == defaultPublicUrl
        );
    }

    private static string ResolveAppDomain(string publicUrl)
    {
        if (!Uri.TryCreate(publicUrl, UriKind.Absolute, out var uri) || string.IsNullOrWhiteSpace(uri.Host))
        {
            var escapedPublicUrl = Markup.Escape(publicUrl);
            AnsiConsole.MarkupLine("[red]Error:[/] --public-url must be an absolute URL. Received: [yellow]" + escapedPublicUrl + "[/]");
            Environment.Exit(1);
        }

        return uri.Host;
    }

    private static string BuildCallbackUrl(string publicUrl, string loginType)
    {
        return $"{publicUrl}/api/account/authentication/Facebook/{loginType}/callback";
    }
}
