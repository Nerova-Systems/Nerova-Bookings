using Main.Database;
using Main.Features.Appointments;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SharedKernel.Configuration;

namespace Main;

public static class Configuration
{
    public static Assembly Assembly => Assembly.GetExecutingAssembly();

    extension(IHostApplicationBuilder builder)
    {
        public IHostApplicationBuilder AddMainInfrastructure()
        {
            // Infrastructure is configured separately from other Infrastructure services to allow mocking in tests
            return builder.AddSharedInfrastructure<MainDbContext>("main-database");
        }
    }

    extension(IServiceCollection services)
    {
        public IServiceCollection AddMainServices()
        {
            services.AddScoped<IPaystackClient, PaystackClient>();
            return services.AddSharedServices<MainDbContext>([Assembly]);
        }
    }
}
