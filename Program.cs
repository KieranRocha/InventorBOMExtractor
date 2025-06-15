// Program.cs - MODIFICAÇÃO DO SEU CÓDIGO EXISTENTE
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using InventorBOMExtractor.Services;
using InventorBOMExtractor.Configuration;
// 🆕 ADICIONAR ESTAS LINHAS
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.OpenApi.Models; // ✅ ADICIONADO PARA SWAGGER INFO

namespace InventorBOMExtractor
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            // Configuração do Serilog (manter como está)
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .WriteTo.File("logs/companion-.log", rollingInterval: Serilog.RollingInterval.Day,
                              outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            try
            {
                Log.Information("=== COMPANION SERVICE + WEB API INICIANDO ===");

                // 🆕 VERIFICAR SE DEVE RODAR WEB API
                if (args.Contains("--web-api") || args.Contains("--console"))
                {
                    Log.Information("🌐 Modo WEB API + SERVICE");
                    await RunWithWebAPI(args);
                }
                else
                {
                    Log.Information("⚙️ Modo Windows Service apenas");
                    await RunAsWindowsService(args);
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

        // 🆕 MÉTODO NOVO - Web API + Windows Service HÍBRIDO
        private static async Task RunWithWebAPI(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            
            // Adiciona logging do Serilog ao builder do ASP.NET Core
            builder.Host.UseSerilog();
            
            // 🔄 CONFIGURAR SEUS SERVICES EXISTENTES
            ConfigureExistingServices(builder.Services, builder.Configuration);
            
            // 🆕 ADICIONAR SERVIÇOS WEB API
            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { 
                    Title = "CAD Companion API", 
                    Version = "v2.0",
                    Description = "API para integração com Autodesk Inventor. Permite extrair listas de materiais (BOM), gerenciar arquivos e monitorar atividades."
                });
            });
            
            // ✅ CORREÇÃO: Política de CORS mais flexível para desenvolvimento
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowAllPolicy", policy =>
                {
                    policy.SetIsOriginAllowed(origin => true) // Permite qualquer origem, incluindo 'null' (file://)
                          .AllowAnyHeader()
                          .AllowAnyMethod()
                          .AllowCredentials();
                });
            });

            var app = builder.Build();

            // 🆕 CONFIGURAR PIPELINE WEB API
            app.UseCors("AllowAllPolicy"); // Usa a nova política
            
            // Redirecionamento de / para /swagger
            app.Use(async (context, next) => {
                if (context.Request.Path == "/")
                {
                    context.Response.Redirect("/swagger");
                    return;
                }
                await next();
            });

            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "CAD Companion API v1");
                c.RoutePrefix = "swagger"; 
            });
            
            app.UseRouting();
            app.MapControllers();

            // 🆕 ENDPOINT DE HEALTH CHECK SIMPLES
            app.MapGet("/health", () => new { 
                status = "healthy", 
                timestamp = DateTime.Now,
                service = "CAD Companion"
            });

            Log.Information("🌐 Web API iniciada em http://localhost:5000");
            Log.Information("📋 Swagger UI: http://localhost:5000/swagger");
            Log.Information("🏥 Health Check: http://localhost:5000/health");

            // Roda o CompanionWorkerService em background
            // O próprio Host já cuida de iniciar os IHostedService
            await app.RunAsync("http://localhost:5000");
        }

        // 🔄 MÉTODO EXISTENTE (manter como está)
        private static async Task RunAsWindowsService(string[] args)
        {
            var host = CreateHostBuilder(args).Build();
            Log.Information("Executando como WINDOWS SERVICE");
            await host.RunAsync();
        }

        // 🆕 MÉTODO PARA CONFIGURAR SEUS SERVICES EXISTENTES
        private static void ConfigureExistingServices(IServiceCollection services, IConfiguration configuration)
        {
            // ✅ SUAS CONFIGURAÇÕES EXISTENTES
            services.Configure<CompanionSettings>(
                configuration.GetSection("CompanionSettings"));

            // ✅ SEUS SERVICES EXISTENTES
            services.AddSingleton<InventorBomExtractor>();
            services.AddSingleton<IInventorConnectionService, InventorConnectionService>();
            services.AddHttpClient<IApiCommunicationService, ApiCommunicationService>();
            services.AddSingleton<IInventorDocumentEventService, InventorDocumentEventService>();
            services.AddSingleton<IWorkDrivenMonitoringService, WorkDrivenMonitoringService>();
            services.AddSingleton<IDocumentProcessingService, DocumentProcessingService>();
            services.AddSingleton<IWorkSessionService, WorkSessionService>();

            // ✅ HOSTED SERVICE (manter - continua rodando em background)
            services.AddHostedService<CompanionWorkerService>();
        }

        // 🔄 SEU MÉTODO EXISTENTE (manter como está)
        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseWindowsService(options =>
                {
                    options.ServiceName = "InventorCompanionService";
                })
                .UseSerilog()
                .ConfigureServices((hostContext, services) =>
                {
                    ConfigureExistingServices(services, hostContext.Configuration);
                });
    }
}