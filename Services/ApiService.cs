using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net.NetworkInformation;
using SystemEye.ViewModels;

namespace SystemEye.Services
{
    public class ApiService
    {
        private IHost? _host;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ApiService> _logger;

        public ApiService(IServiceProvider serviceProvider, ILogger<ApiService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public bool IsRunning => _host != null;

        public async Task StartApiAsync(int port = 5000)
        {
            try
            {
                if (IsRunning) return;

                if (IsPortInUse(port))
                {
                    throw new Exception($"Port {port} ist bereits belegt.");
                }

                var builder = WebApplication.CreateBuilder();

                // Logger aufräumen
                builder.Logging.ClearProviders();
                builder.Logging.AddConsole();

                builder.Services.AddEndpointsApiExplorer();
                builder.Services.AddSwaggerGen();

                builder.WebHost.ConfigureKestrel(o => o.ListenAnyIP(port));

                var app = builder.Build();

                // Middleware für Fehlerbehandlung
                app.Use(async (context, next) =>
                {
                    try
                    {
                        await next();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "API Laufzeit-Fehler bei {Path}", context.Request.Path);
                        context.Response.StatusCode = 500;
                        await context.Response.WriteAsJsonAsync(new
                        {
                            error = "Internal Server Error",
                            message = ex.Message,
                            timestamp = DateTime.Now
                        });
                    }
                });

                app.UseSwagger();
                app.UseSwaggerUI();

                app.MapGet("/sensors", () =>
                {
                    var viewModel = _serviceProvider.GetRequiredService<MainViewModel>();
                    return viewModel.CurrentSensors;
                })
                .WithSummary("Live-Sensordaten abrufen")
                .WithDescription("Gibt eine Liste aller aktuell aktiven Hardware-Sensoren zurück.");

                app.MapGet("/hardware", () =>
                {
                    var viewModel = _serviceProvider.GetRequiredService<MainViewModel>();
                    return viewModel.SystemInformation;
                })
                .WithSummary("Hardware-Details abrufen")
                .WithDescription("Gibt die detaillierte Hardware-Konfiguration des Systems zurück.");

                await app.StartAsync();

                _host = app;
                _logger.LogInformation("REST-API erfolgreich gestartet auf Port {Port}", port);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kritischer Fehler beim Starten der API");
                _host = null;
                throw;
            }
        }

        public async Task StopApiAsync()
        {
            if (_host is WebApplication app)
            {
                try
                {
                    await app.StopAsync(TimeSpan.FromSeconds(5));
                    await app.DisposeAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Fehler beim Stoppen der REST-API.");
                }
                finally
                {
                    _host = null;
                }
            }
        }

        private bool IsPortInUse(int port)
        {
            var ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();
            var tcpConnInfoArray = ipGlobalProperties.GetActiveTcpListeners();

            foreach (var endpoint in tcpConnInfoArray)
            {
                if (endpoint.Port == port) return true;
            }
            return false;
        }
    }
}