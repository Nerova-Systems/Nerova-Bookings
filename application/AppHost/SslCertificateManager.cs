using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.UserSecrets;

namespace AppHost;

public static class SslCertificateManager
{
    private const string CertificateDomain = "localhost";

    private const string RequiredSanDnsName = "*.dev.localhost";

    private static string UserSecretsId => Assembly.GetEntryAssembly()!.GetCustomAttribute<UserSecretsIdAttribute>()!.UserSecretsId;

    private static string GetCertificateLocation(string domain)
    {
        var userFolder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return $"{userFolder}/.aspnet/dev-certs/https/{domain}.pfx";
    }

    private static void CreateNewSelfSignedDeveloperCertificate(string certificateLocation, string password)
    {
        if (File.Exists(certificateLocation))
        {
            Console.WriteLine($"Certificate {certificateLocation} exists but the stored password is invalid. Creating a new certificate.");

            File.Delete(certificateLocation);
        }
        else
        {
            var certificateDirectory = Path.GetDirectoryName(certificateLocation)!;
            if (!Directory.Exists(certificateDirectory))
            {
                Console.WriteLine($"Certificate directory {certificateDirectory} does not exist. Creating it.");

                Directory.CreateDirectory(certificateDirectory);
            }
        }

        Process.Start(new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"dev-certs https --trust --export-path {certificateLocation} --password {password}",
                RedirectStandardOutput = false,
                RedirectStandardError = false,
                UseShellExecute = false
            }
        )!.WaitForExit();
    }

    private static bool HasRequiredSan(X509Certificate2 certificate)
    {
        var sanExtension = certificate.Extensions.OfType<X509SubjectAlternativeNameExtension>().FirstOrDefault();
        return sanExtension is not null && sanExtension.EnumerateDnsNames().Any(name => name == RequiredSanDnsName);
    }

    private static void CleanExistingDeveloperCertificates()
    {
        Process.Start(new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "dev-certs https --clean",
                RedirectStandardOutput = false,
                RedirectStandardError = false,
                UseShellExecute = false
            }
        )!.WaitForExit();
    }

    extension(IDistributedApplicationBuilder builder)
    {
        public async Task<string> CreateSslCertificateIfNotExists(CancellationToken cancellationToken = default)
        {
            var config = new ConfigurationBuilder().AddUserSecrets(UserSecretsId).Build();

            const string certificatePasswordKey = "certificate-password";
            var certificatePassword = config[certificatePasswordKey]
                                      ?? await builder.CreateStablePassword(certificatePasswordKey).Resource.GetValueAsync(cancellationToken)
                                      ?? throw new InvalidOperationException("Failed to retrieve or create certificate password.");

            var certificateLocation = GetCertificateLocation(CertificateDomain);
            try
            {
                var certificate = X509CertificateLoader.LoadPkcs12FromFile(certificateLocation, certificatePassword);
                if (certificate.NotAfter < TimeProvider.System.GetUtcNow().UtcDateTime)
                {
                    Console.WriteLine($"Certificate {certificateLocation} is expired. Creating a new certificate.");
                    CreateNewSelfSignedDeveloperCertificate(certificateLocation, certificatePassword);
                }
                else if (!HasRequiredSan(certificate))
                {
                    Console.WriteLine($"Certificate {certificateLocation} is missing the {RequiredSanDnsName} SAN. Cleaning existing dev-certs and creating a new certificate.");
                    CleanExistingDeveloperCertificates();
                    CreateNewSelfSignedDeveloperCertificate(certificateLocation, certificatePassword);
                }
            }
            catch (CryptographicException)
            {
                CreateNewSelfSignedDeveloperCertificate(certificateLocation, certificatePassword);
            }

            return certificatePassword;
        }
    }
}
