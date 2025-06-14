// Services/CompanionWorkerService.cs - STEP 2 ATUALIZADO
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using InventorBOMExtractor.Configuration;
using InventorBOMExtractor.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace InventorBOMExtractor.Services
{
    public class CompanionWorkerService : BackgroundService
    {
        private readonly ILogger<CompanionWorkerService> _logger;
        private readonly IInventorConnectionService _inventorConnection;
        private readonly IApiCommunicationService _apiCommunication;
        private readonly IWorkDrivenMonitoringService _workDrivenMonitoring;   // ✅ NOVO
        private readonly IWorkSessionService _workSessionService;              // ✅ NOVO
        private readonly CompanionSettings _settings;

        public CompanionWorkerService(
            ILogger<CompanionWorkerService> logger,
            IOptions<CompanionSettings> settings,
            IInventorConnectionService inventorConnection,
            IApiCommunicationService apiCommunication,
            IWorkDrivenMonitoringService workDrivenMonitoring,                 // ✅ NOVO
            IWorkSessionService workSessionService)                            // ✅ NOVO
        {
            _logger = logger;
            _settings = settings.Value;
            _inventorConnection = inventorConnection;
            _apiCommunication = apiCommunication;
            _workDrivenMonitoring = workDrivenMonitoring;                       // ✅ NOVO
            _workSessionService = workSessionService;                           // ✅ NOVO
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("🚀 Companion Service STEP 2 INICIANDO...");
            _logger.LogInformation($"Configurações: API={_settings.ApiBaseUrl}, Interval={_settings.CycleIntervalMs}ms");
            _logger.LogInformation("=== COMPANION SERVICE EXECUTANDO ===");

            // Aguarda inicialização
            await Task.Delay(5000, stoppingToken);

            // ✅ STEP 2 - Inicia work-driven monitoring
            try
            {
                await _workDrivenMonitoring.StartAsync(stoppingToken);
                _logger.LogInformation("✅ Work-Driven Monitoring iniciado");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro ao iniciar Work-Driven Monitoring");
            }

            // Loop principal
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await PerformServiceCycle();
                    await Task.Delay(_settings.CycleIntervalMs, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Service cancelado - parando...");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro no ciclo do service");
                    await Task.Delay(_settings.ErrorRetryDelayMs, stoppingToken);
                }
            }

            // ✅ STEP 2 - Para work-driven monitoring
            try
            {
                await _workDrivenMonitoring.StopAsync(stoppingToken);
                _logger.LogInformation("✅ Work-Driven Monitoring parado");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao parar Work-Driven Monitoring");
            }
        }

        private async Task PerformServiceCycle()
        {
            _logger.LogDebug("Iniciando ciclo do service...");

            // 1. Garante conexão com Inventor
            await EnsureInventorConnection();

            // 2. ✅ STEP 2 - Envia heartbeat com work sessions
            await SendEnhancedHeartbeat();

            _logger.LogDebug("Ciclo do service concluído");
        }

        private async Task EnsureInventorConnection()
        {
            try
            {
                if (!_inventorConnection.IsConnected)
                {
                    _logger.LogInformation("Tentando conectar ao Inventor...");
                    await _inventorConnection.ConnectAsync();
                }
                else
                {
                    // Testa se conexão ainda está válida
                    var connectionValid = await _inventorConnection.TestConnectionAsync();
                    if (!connectionValid)
                    {
                        _logger.LogWarning("Conexão com Inventor perdida - tentando reconectar...");
                        await _inventorConnection.ReconnectAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao garantir conexão com Inventor");
            }
        }

        // ✅ STEP 2 - Heartbeat melhorado com work sessions
        private async Task SendEnhancedHeartbeat()
        {
            try
            {
                // Obtém sessões ativas
                var activeSessions = await _workSessionService.GetActiveWorkSessionsAsync();
                
                // Obtém estatísticas do dia
                var todayStats = await _workSessionService.GetDailyStatisticsAsync(DateTime.Today);

                // Cria heartbeat melhorado
                var heartbeat = new EnhancedCompanionHeartbeat
                {
                    CompanionId = Environment.MachineName,
                    Timestamp = DateTime.UtcNow,
                    Status = "RUNNING",
                    InventorConnected = _inventorConnection.IsConnected,
                    InventorVersion = _inventorConnection.InventorVersion,
                    
                    // ✅ STEP 2 - Dados de work sessions
                    ActiveWorkSessions = activeSessions.Count,
                    ActiveWatchers = _workDrivenMonitoring.ActiveWatchersCount,
                    TodayStats = new
                    {
                        TotalSessions = todayStats.TotalSessions,
                        TotalWorkTime = todayStats.TotalWorkTime.ToString(@"hh\:mm\:ss"),
                        TotalSaves = todayStats.TotalSaves,
                        ActiveEngineers = todayStats.ActiveEngineers,
                        ActiveProjects = todayStats.ActiveProjects
                    }
                };

                // Converte para CompanionHeartbeat básico para compatibilidade
                var basicHeartbeat = new CompanionHeartbeat
                {
                    CompanionId = heartbeat.CompanionId,
                    Timestamp = heartbeat.Timestamp,
                    Status = heartbeat.Status,
                    InventorConnected = heartbeat.InventorConnected,
                    InventorVersion = heartbeat.InventorVersion,
                    Message = $"STEP 2: {heartbeat.ActiveWorkSessions} sessões ativas, {heartbeat.ActiveWatchers} watchers"
                };

                await _apiCommunication.SendHeartbeatAsync(basicHeartbeat);

                _logger.LogDebug($"Heartbeat enviado - Sessões: {heartbeat.ActiveWorkSessions}, Watchers: {heartbeat.ActiveWatchers}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao enviar heartbeat melhorado");
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("🔴 Companion Service parando...");

            try
            {
                // Para work-driven monitoring primeiro
                await _workDrivenMonitoring.StopAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao parar work-driven monitoring");
            }

            await base.StopAsync(cancellationToken);
            _logger.LogInformation("✅ Companion Service parado");
        }
    }

    // ✅ STEP 2 - Heartbeat melhorado (compatível com Step 1)
    public class EnhancedCompanionHeartbeat
    {
        public string CompanionId { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string Status { get; set; } = string.Empty;
        public bool InventorConnected { get; set; }
        public string? InventorVersion { get; set; }
        
        // Dados específicos Step 2
        public int ActiveWorkSessions { get; set; }
        public int ActiveWatchers { get; set; }
        public object? TodayStats { get; set; }
    }
}