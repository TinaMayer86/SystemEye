using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Windows;
using SystemEye.Services;

namespace SystemEye
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public new static App Current => (App)Application.Current;
        public IServiceProvider Services { get; }

        public App()
        {
            Services = ConfigureServices();
        }

        private static IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.ClearProviders();
                loggingBuilder.AddDebug();

                loggingBuilder.AddFile("Logs/log-{Date}.txt", LogLevel.Warning);
            });
            services.AddSingleton<ConfigService>();

            var configService = new ConfigService();
            var appConfig = configService.LoadConfigAsync().GetAwaiter().GetResult();


            return services.BuildServiceProvider();
        }
    }
}
