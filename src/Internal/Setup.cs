using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.IO;
using Datafordelen.Address;
using Datafordelen.GeoData;
using Datafordelen.BBR;
using Datafordelen.Config;
using Datafordelen.Ftp;
using Datafordelen.Kafka;
using Serilog;
using Serilog.Formatting.Compact;

namespace Datafordelen.Internal
{
    public static class Setup
    {
        public static ServiceProvider Configure()
        {
            var builder = new ConfigurationBuilder();
            var configuration = SetupAppSettings(builder);

            var serviceProvider = new ServiceCollection();
            ConfigureServices(serviceProvider, configuration);

            return serviceProvider.BuildServiceProvider();
        }

        private static IConfigurationRoot SetupAppSettings(ConfigurationBuilder builder)
        {
            return builder
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();
        }

        private static void ConfigureServices(ServiceCollection serviceCollection, IConfigurationRoot configuration)
        {
            serviceCollection.Configure<AppSettings>(configuration.GetSection("AppSettings"));
            serviceCollection.AddSingleton<Startup, Startup>();
            serviceCollection.AddSingleton<IGeoDataService, GeoDataService>();
            serviceCollection.AddSingleton<IAddressService, AddressService>();
            serviceCollection.AddSingleton<IBBRService,BBRService>();
            serviceCollection.AddSingleton<IFTPClient, FTPClient>();
            serviceCollection.AddSingleton<IKafkaProducer, KafkaProducer>();
            serviceCollection.AddLogging(configure =>
            {
                var loggingConfiguration = new ConfigurationBuilder()
                   .AddEnvironmentVariables().Build();

                var logger = new LoggerConfiguration()
                    .ReadFrom.Configuration(loggingConfiguration)
                    .Enrich.FromLogContext()
                    .WriteTo.Console(new CompactJsonFormatter())
                    .CreateLogger();

                configure.AddSerilog(logger, true);
            });
        }
    }
}
