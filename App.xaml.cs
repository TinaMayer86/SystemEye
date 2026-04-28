using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Windows;
using SystemEye.Models;
using SystemEye.Services;
using SystemEye.ViewModels;

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
                loggingBuilder.AddFile("Logs/SystemEye_{Date}.txt", LogLevel.Warning);
            });

            // Basis-Services registrieren
            services.AddSingleton<ConfigService>();
            services.AddSingleton<HardwareService>();
            services.AddSingleton<ExportService>();
            services.AddSingleton<MainViewModel>();
            services.AddSingleton<ApiService>();

            var tempProvider = services.BuildServiceProvider();
            var configService = tempProvider.GetRequiredService<ConfigService>();

            AppConfig appConfig;
            try
            {
                // Lädt die Datei beim Start
                appConfig = configService.LoadConfigAsync().GetAwaiter().GetResult();
            }
            catch
            {
                appConfig = new AppConfig(); // Fallback 
            }

            services.AddSingleton(provider =>
            {
                var logger = provider.GetRequiredService<ILogger<DatabaseService>>();
                return new DatabaseService(appConfig.Database, logger);
            });
            services.AddTransient<MainViewModel>();

            return services.BuildServiceProvider();
        }

    }
}
