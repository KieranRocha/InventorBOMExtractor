// Program.cs - ATUALIZAÇÃO STEP 2
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using InventorBOMExtractor.Services;
using InventorBOMExtractor.Configuration;

namespace InventorBOMExtractor
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            // Configuração do Serilog
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .WriteTo.File("logs/companion-.log", rollingInterval: Serilog.RollingInterval.Day)
                .CreateLogger();

            try
            {
                Log.Information("=== COMPANION SERVICE STEP 2 INICIANDO ===");

                var host = CreateHostBuilder(args).Build();

                // Verifica modo de execução
                if (args.Contains("--console"))
                {
                    Log.Information("Executando em modo CONSOLE");
                    await host.RunAsync();
                }
                else
                {
                    Log.Information("Executando como WINDOWS SERVICE");
                    await host.RunAsync();
                }
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Aplicação falhou durante startup");
                return;
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseWindowsService(options =>
                {
                    options.ServiceName = "InventorCompanionService";
                })
                .UseSerilog()
                .ConfigureServices((hostContext, services) =>
                {
                    // ✅ CONFIGURAÇÕES
                    services.Configure<CompanionSettings>(
                        hostContext.Configuration.GetSection("CompanionSettings"));

                    // ✅ STEP 1 - Services existentes
                    services.AddSingleton<InventorBomExtractor>();
                    services.AddSingleton<IInventorConnectionService, InventorConnectionService>();
                    services.AddHttpClient<IApiCommunicationService, ApiCommunicationService>();

                    // ✅ STEP 2 - Novos services
                    services.AddSingleton<IInventorDocumentEventService, InventorDocumentEventService>();
                    services.AddSingleton<IWorkDrivenMonitoringService, WorkDrivenMonitoringService>();
                    services.AddSingleton<IDocumentProcessingService, DocumentProcessingService>();
                    services.AddSingleton<IWorkSessionService, WorkSessionService>();

                    // ✅ HOSTED SERVICE - Sempre por último
                    services.AddHostedService<CompanionWorkerService>();
                });
    }
}