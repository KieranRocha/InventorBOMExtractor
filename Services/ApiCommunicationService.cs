// Services/ApiCommunicationService.cs - STEP 2 EXPANDIDO
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using InventorBOMExtractor.Configuration;
using InventorBOMExtractor.Models;
using System;
using System.Net.Http;
using System.Text.Json;
using System.Text;
using System.Threading.Tasks;

namespace InventorBOMExtractor.Services
{
    public interface IApiCommunicationService
    {
        // STEP 1 - Métodos existentes
        Task SendHeartbeatAsync(CompanionHeartbeat heartbeat);
        
        // STEP 2 - Novos métodos
        Task SendBOMDataAsync(BOMDataWithContext bomData);
        Task SendPartDataAsync(PartDataWithContext partData);
        Task SendDocumentActivityAsync(DocumentActivity activity);
        Task SendWorkSessionStartedAsync(WorkSession workSession);
        Task SendWorkSessionEndedAsync(WorkSession workSession);
        Task SendWorkSessionUpdatedAsync(WorkSession workSession, string updateReason);
        
        // Health check
        Task<bool> TestConnectionAsync();
    }

    public class ApiCommunicationService : IApiCommunicationService
    {
        private readonly ILogger<ApiCommunicationService> _logger;
        private readonly HttpClient _httpClient;
        private readonly CompanionSettings _settings;

        public ApiCommunicationService(
            ILogger<ApiCommunicationService> logger,
            HttpClient httpClient,
            IOptions<CompanionSettings> settings)
        {
            _logger = logger;
            _httpClient = httpClient;
            _settings = settings.Value;
            
            // Configura HttpClient
            _httpClient.BaseAddress = new Uri(_settings.ApiBaseUrl);
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        #region STEP 1 - Heartbeat

        public async Task SendHeartbeatAsync(CompanionHeartbeat heartbeat)
        {
            try
            {
                _logger.LogDebug("Enviando heartbeat para API...");

                var json = JsonSerializer.Serialize(heartbeat, GetJsonOptions());
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("/api/companion/heartbeat", content);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogDebug("✅ Heartbeat enviado com sucesso");
                }
                else
                {
                    _logger.LogWarning($"⚠️ API retornou erro no heartbeat: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao enviar heartbeat para API");
            }
        }

        #endregion

        #region STEP 2 - BOM and Document Data

        public async Task SendBOMDataAsync(BOMDataWithContext bomData)
        {
            try
            {
                _logger.LogInformation($"📤 Enviando BOM para API: {bomData.ProjectName} - {bomData.AssemblyFileName}");

                var json = JsonSerializer.Serialize(bomData, GetJsonOptions());
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("/api/projects/bom-data", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    _logger.LogInformation($"✅ BOM enviado com sucesso: {bomData.TotalItems} itens");
                    
                    // TODO: Processar resposta se necessário
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"❌ Erro ao enviar BOM: {response.StatusCode} - {errorContent}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao enviar BOM para API: {bomData.AssemblyFileName}");
            }
        }

        public async Task SendPartDataAsync(PartDataWithContext partData)
        {
            try
            {
                _logger.LogDebug($"📤 Enviando dados de part para API: {partData.PartFileName}");

                var json = JsonSerializer.Serialize(partData, GetJsonOptions());
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("/api/projects/part-data", content);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogDebug($"✅ Dados de part enviados: {partData.PartFileName}");
                }
                else
                {
                    _logger.LogWarning($"⚠️ Erro ao enviar dados de part: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao enviar dados de part: {partData.PartFileName}");
            }
        }

        public async Task SendDocumentActivityAsync(DocumentActivity activity)
        {
            try
            {
                _logger.LogDebug($"📤 Enviando atividade de documento: {activity.FileName} - {activity.EventType}");

                var json = JsonSerializer.Serialize(activity, GetJsonOptions());
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("/api/documents/activity", content);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogDebug($"✅ Atividade enviada: {activity.FileName}");
                }
                else
                {
                    _logger.LogDebug($"⚠️ Erro ao enviar atividade: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, $"Erro ao enviar atividade: {activity.FileName}");
            }
        }

        #endregion

        #region STEP 2 - Work Sessions

        public async Task SendWorkSessionStartedAsync(WorkSession workSession)
        {
            try
            {
                _logger.LogInformation($"📤 Enviando início de work session: {workSession.FileName}");

                var payload = new
                {
                    Type = "WORK_SESSION_STARTED",
                    Data = workSession,
                    Timestamp = DateTime.UtcNow
                };

                var json = JsonSerializer.Serialize(payload, GetJsonOptions());
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("/api/work-sessions/started", content);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation($"✅ Work session iniciada enviada: {workSession.Id}");
                }
                else
                {
                    _logger.LogWarning($"⚠️ Erro ao enviar work session iniciada: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao enviar work session iniciada: {workSession.Id}");
            }
        }

        public async Task SendWorkSessionEndedAsync(WorkSession workSession)
        {
            try
            {
                _logger.LogInformation($"📤 Enviando fim de work session: {workSession.FileName} - Duração: {workSession.Duration:hh\\:mm\\:ss}");

                var payload = new
                {
                    Type = "WORK_SESSION_ENDED",
                    Data = workSession,
                    Timestamp = DateTime.UtcNow,
                    Summary = new
                    {
                        DurationMinutes = workSession.Duration?.TotalMinutes ?? 0,
                        SaveCount = workSession.SaveCount,
                        Productive = workSession.SaveCount > 0,
                        SessionQuality = CalculateSessionQuality(workSession)
                    }
                };

                var json = JsonSerializer.Serialize(payload, GetJsonOptions());
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("/api/work-sessions/ended", content);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation($"✅ Work session finalizada enviada: {workSession.Id}");
                }
                else
                {
                    _logger.LogWarning($"⚠️ Erro ao enviar work session finalizada: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao enviar work session finalizada: {workSession.Id}");
            }
        }

        public async Task SendWorkSessionUpdatedAsync(WorkSession workSession, string updateReason)
        {
            try
            {
                _logger.LogDebug($"📤 Enviando update de work session: {workSession.FileName} - {updateReason}");

                var payload = new
                {
                    Type = "WORK_SESSION_UPDATED",
                    Data = workSession,
                    UpdateReason = updateReason,
                    Timestamp = DateTime.UtcNow
                };

                var json = JsonSerializer.Serialize(payload, GetJsonOptions());
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("/api/work-sessions/updated", content);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogDebug($"✅ Work session update enviado: {workSession.Id}");
                }
                else
                {
                    _logger.LogDebug($"⚠️ Erro ao enviar work session update: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, $"Erro ao enviar work session update: {workSession.Id}");
            }
        }

        #endregion

        #region Health Check

        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                _logger.LogDebug("Testando conexão com API...");

                var response = await _httpClient.GetAsync("/api/health");

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogDebug("✅ Conexão com API OK");
                    return true;
                }
                else
                {
                    _logger.LogWarning($"⚠️ API health check falhou: {response.StatusCode}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao testar conexão com API");
                return false;
            }
        }

        #endregion

        #region Helper Methods

        private JsonSerializerOptions GetJsonOptions()
        {
            return new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };
        }

        private string CalculateSessionQuality(WorkSession workSession)
        {
            try
            {
                var duration = workSession.Duration?.TotalMinutes ?? 0;
                var saves = workSession.SaveCount;

                if (duration < 5) return "TOO_SHORT";
                if (saves == 0) return "NO_SAVES";
                if (duration > 240) return "VERY_LONG"; // Mais de 4 horas
                
                var savesPerHour = saves / (duration / 60.0);
                
                if (savesPerHour < 1) return "LOW_ACTIVITY";
                if (savesPerHour > 20) return "HIGH_ACTIVITY";
                
                return "NORMAL";
            }
            catch
            {
                return "UNKNOWN";
            }
        }

        #endregion
    }
}