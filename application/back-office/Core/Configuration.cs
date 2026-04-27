using BackOffice.Database;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SharedKernel.Configuration;

namespace BackOffice;

public static class Configuration
{
    public static Assembly Assembly => Assembly.GetExecutingAssembly();

    extension(IHostApplicationBuilder builder)
    {
        public IHostApplicationBuilder AddBackOfficeInfrastructure()
        {
            // Infrastructure is configured separately from other Infrastructure services to allow mocking in tests
            return builder.AddSharedInfrastructure<BackOfficeDbContext>("back-office-database");
        }
    }

    extension(IServiceCollection services)
    {
        public IServiceCollection AddBackOfficeServices()
        {
            services.AddHttpClient("AccountInternal", client =>
                {
                    client.BaseAddress = new Uri(Environment.GetEnvironmentVariable("ACCOUNT_API_URL") ?? "https://localhost:9100");
                    client.Timeout = TimeSpan.FromSeconds(30);
                }
            );

            return services.AddSharedServices<BackOfficeDbContext>([Assembly]);
        }
    }
}
