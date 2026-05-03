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
    /// <summary>
    /// Verwaltet den Lebenszyklus der internen REST‑API, einschließlich Starten,
    /// Stoppen und Fehlerbehandlung. Stellt die HTTP‑Endpunkte für Live‑Sensorwerte
    /// und Hardware‑Informationen bereit und konfiguriert Logging, Swagger sowie
    /// die Kestrel‑Serverumgebung.
    /// </summary>
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

        /// <summary>
        /// Startet die interne REST‑API, konfiguriert den Kestrel‑Webserver,
        /// richtet Swagger sowie die HTTP‑Endpunkte ein und aktiviert die
        /// zentrale Fehlerbehandlung. Der Startvorgang wird nur ausgeführt,
        /// wenn die API noch nicht läuft und der gewünschte Port verfügbar ist.
        /// </summary>
        /// <param name="port">
        /// Der TCP‑Port, auf dem die REST‑API gestartet werden soll.
        /// Standardwert ist 5000.
        /// </param>
        /// <returns>
        /// Ein Task, das den asynchronen Startvorgang der API repräsentiert.
        /// </returns>
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

                builder.Logging.ClearProviders(); // Wirft alle Logger raus
                builder.Logging.AddConsole(); // Fügt den Logger für die Konsole hinzu

                builder.Services.AddEndpointsApiExplorer(); // Sucht im code nach API-Routen(endpunkten)
                builder.Services.AddSwaggerGen(); // Sammelt informationen für die API

                builder.WebHost.ConfigureKestrel(o => o.ListenAnyIP(port)); // Macht die API im ganzen netzwerk sichtbar

                var app = builder.Build();

                app.Use(async (context, next) =>
                {
                    try
                    {
                        await next();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "API Laufzeit-Fehler bei {Path}", context.Request.Path);

                        if (!context.Response.HasStarted)
                        {
                            context.Response.StatusCode = 500;
                            await context.Response.WriteAsJsonAsync(new
                            {
                                error = "Internal Server Error",
                                message = ex.Message,
                                timestamp = DateTime.Now
                            });
                        }
                    }
                });

                app.UseSwagger();
                app.UseSwaggerUI();

                app.MapGet("/sensors", () =>
                {
                    var viewModel = _serviceProvider.GetRequiredService<MainViewModel>();
                    return viewModel.LiveVM.CurrentSensors.ToList();
                })
                .WithSummary("Live-Sensordaten abrufen")
                .WithDescription("Gibt eine Liste aller aktuell aktiven Hardware-Sensoren zurück.");

                app.MapGet("/hardware", () =>
                {
                    var viewModel = _serviceProvider.GetRequiredService<MainViewModel>();
                    return viewModel.InfoVM.SystemInformation.ToList();
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

        /// <summary>
        /// Stoppt die laufende REST‑API und führt einen kontrollierten Shutdown des
        /// Kestrel‑Webservers durch. Ausstehende Vorgänge erhalten eine kurze
        /// Abschlussfrist, bevor die Ressourcen freigegeben und der Host zurückgesetzt
        /// werden.
        /// </summary>
        /// <returns>
        /// Ein Task, das den asynchronen Beendigungsprozess der API repräsentiert.
        /// </returns>
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

        /// <summary>
        /// Prüft, ob der angegebene TCP‑Port bereits von einem anderen Prozess
        /// verwendet wird. Die Methode durchsucht dazu alle aktiven TCP‑Listener
        /// des Systems und gibt an, ob der Port belegt ist.
        /// </summary>
        /// <param name="port">
        /// Der zu prüfende TCP‑Port.
        /// </param>
        /// <returns>
        /// <c>true</c>, wenn der Port bereits verwendet wird; andernfalls <c>false</c>.
        /// </returns>
        private static bool IsPortInUse(int port)
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