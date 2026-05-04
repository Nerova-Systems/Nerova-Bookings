using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Scriban.Runtime;
using SharedKernel.Configuration;
using SharedKernel.SinglePageApp;

namespace SharedKernel.Emails;

public static class EmailRenderingConfiguration
{
    // Path segment under which email assets (images, fonts, etc.) referenced from rendered HTML
    // are served. Templates author absolute URLs like {{publicUrl}}/emails/assets/logo.png so the
    // same markup works in dev (localhost) and production (CDN/public host).
    public const string EmailStaticFilesRequestPath = "/emails/assets";

    private static string ResolveEmailsDistPath(string webAppProjectName)
    {
        // Walk up from the executing assembly looking for <webAppProjectName>/emails/dist or its
        // parent <webAppProjectName>/main.tsx marker. Mirrors SinglePageAppConfiguration so the
        // same SCS layout works for both SPA bundles and email bundles.
        var assemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        var directoryInfo = new DirectoryInfo(assemblyPath);
        while (directoryInfo is not null)
        {
            var candidate = Path.Combine(directoryInfo.FullName, webAppProjectName, "emails", "dist");
            if (Directory.Exists(candidate)) return candidate;

            var webAppCandidate = Path.Combine(directoryInfo.FullName, webAppProjectName);
            if (File.Exists(Path.Combine(webAppCandidate, "main.tsx"))) return Path.Combine(webAppCandidate, "emails", "dist");

            directoryInfo = directoryInfo.Parent;
        }

        throw new InvalidOperationException($"Could not locate the WebApp project '{webAppProjectName}' walking up from '{assemblyPath}'.");
    }

    extension(IServiceCollection services)
    {
        public IServiceCollection AddEmailRendering(string webAppProjectName)
        {
            var emailsDistPath = ResolveEmailsDistPath(webAppProjectName);
            var publicUrl = Environment.GetEnvironmentVariable(SinglePageAppConfiguration.PublicUrlKey) ?? string.Empty;

            services.AddSingleton<ScriptObject>(_ => EmailHelpers.CreateScriptObject(publicUrl));

            services.AddSingleton<IEmailTemplateLoader>(_ => new FileSystemEmailTemplateLoader(emailsDistPath, !SharedInfrastructureConfiguration.IsRunningInAzure));
            services.AddSingleton<IEmailRenderer, ScribanEmailRenderer>();

            return services;
        }
    }

    extension(IApplicationBuilder app)
    {
        public IApplicationBuilder UseEmailStaticFiles(string webAppProjectName)
        {
            var emailsDistPath = ResolveEmailsDistPath(webAppProjectName);
            if (!Directory.Exists(emailsDistPath)) Directory.CreateDirectory(emailsDistPath);

            return app.UseStaticFiles(new StaticFileOptions
                {
                    FileProvider = new PhysicalFileProvider(emailsDistPath),
                    RequestPath = EmailStaticFilesRequestPath
                }
            );
        }
    }
}
