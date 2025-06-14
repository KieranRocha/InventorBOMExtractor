// Services/WorkSessionService.cs - CORRIGIDO
using Microsoft.Extensions.Logging;
using InventorBOMExtractor.Models;
using InventorBOMExtractor.Events;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace InventorBOMExtractor.Services
{
    public interface IWorkSessionService
    {
        Task<WorkSession> StartWorkSessionAsync(WorkSession workSession);
        Task<WorkSession?> EndWorkSessionAsync(string sessionId, DateTime endTime);
        Task<WorkSession?> UpdateWorkSessionAsync(string sessionId, string updateReason);
        Task<WorkSession?> GetWorkSessionAsync(string sessionId);
        Task<List<WorkSession>> GetActiveWorkSessionsAsync();
        Task<WorkSessionStatistics> GetDailyStatisticsAsync(DateTime date);
        
        event EventHandler<WorkSessionStartedEventArgs>? WorkSessionStarted;
        event EventHandler<WorkSessionEndedEventArgs>? WorkSessionEnded;
        event EventHandler<WorkSessionUpdatedEventArgs>? WorkSessionUpdated;
    }

    public class WorkSessionService : IWorkSessionService
    {
        private readonly ILogger<WorkSessionService> _logger;
        private readonly IApiCommunicationService _apiCommunication;

        // Sessões ativas em memória - thread-safe
        private readonly ConcurrentDictionary<string, WorkSession> _activeSessions = new();
        
        // Cache de estatísticas diárias
        private readonly ConcurrentDictionary<string, WorkSessionStatistics> _dailyStatsCache = new();

        // Eventos públicos
        public event EventHandler<WorkSessionStartedEventArgs>? WorkSessionStarted;
        public event EventHandler<WorkSessionEndedEventArgs>? WorkSessionEnded;
        public event EventHandler<WorkSessionUpdatedEventArgs>? WorkSessionUpdated;

        public WorkSessionService(
            ILogger<WorkSessionService> logger,
            IApiCommunicationService apiCommunication)
        {
            _logger = logger;
            _apiCommunication = apiCommunication;
        }

        public async Task<WorkSession> StartWorkSessionAsync(WorkSession workSession)
        {
            try
            {
                // Gera ID único se não fornecido
                if (string.IsNullOrEmpty(workSession.Id))
                {
                    workSession.Id = GenerateSessionId();
                }

                // Configura estado inicial
                workSession.StartTime = DateTime.UtcNow;
                workSession.IsActive = true;
                workSession.SaveCount = 0;

                // Adiciona à coleção de sessões ativas
                _activeSessions[workSession.Id] = workSession;

                _logger.LogInformation($"🚀 Work Session iniciada: {workSession.FileName} (ID: {workSession.Id})");

                // Envia para API
                //await _apiCommunication.SendWorkSessionStartedAsync(workSession);

                // Dispara evento
                WorkSessionStarted?.Invoke(this, new WorkSessionStartedEventArgs
                {
                    WorkSession = workSession
                });

                return workSession;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao iniciar work session: {workSession.FileName}");
                throw;
            }
        }

        public async Task<WorkSession?> EndWorkSessionAsync(string sessionId, DateTime endTime)
        {
            try
            {
                if (_activeSessions.TryRemove(sessionId, out var workSession))
                {
                    // Finaliza sessão
                    workSession.EndTime = endTime;
                    workSession.Duration = endTime - workSession.StartTime;
                    workSession.IsActive = false;

                    _logger.LogInformation($"🏁 Work Session finalizada: {workSession.FileName} - Duração: {workSession.Duration:hh\\:mm\\:ss} - {workSession.SaveCount} saves");

                    // Envia para API
                    await _apiCommunication.SendWorkSessionEndedAsync(workSession);

                    // Atualiza estatísticas diárias
                    await UpdateDailyStatisticsAsync(workSession);

                    // Dispara evento
                    WorkSessionEnded?.Invoke(this, new WorkSessionEndedEventArgs
                    {
                        WorkSession = workSession
                    });

                    return workSession;
                }
                else
                {
                    _logger.LogWarning($"Work session não encontrada para finalizar: {sessionId}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao finalizar work session: {sessionId}");
                return null;
            }
        }

        public async Task<WorkSession?> UpdateWorkSessionAsync(string sessionId, string updateReason)
        {
            try
            {
                if (_activeSessions.TryGetValue(sessionId, out var workSession))
                {
                    // Atualiza timestamp da última atividade
                    workSession.LastSave = DateTime.UtcNow;

                    // Incrementa contador se for save
                    if (updateReason == "SAVE")
                    {
                        workSession.SaveCount++;
                        _logger.LogDebug($"Save #{workSession.SaveCount}: {workSession.FileName}");
                    }

                    // Envia update para API
                    await _apiCommunication.SendWorkSessionUpdatedAsync(workSession, updateReason);

                    // Dispara evento
                    WorkSessionUpdated?.Invoke(this, new WorkSessionUpdatedEventArgs
                    {
                        WorkSession = workSession,
                        UpdateReason = updateReason
                    });

                    return workSession;
                }
                else
                {
                    _logger.LogWarning($"Work session não encontrada para update: {sessionId}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao atualizar work session: {sessionId}");
                return null;
            }
        }

        public Task<WorkSession?> GetWorkSessionAsync(string sessionId)
        {
            _activeSessions.TryGetValue(sessionId, out var workSession);
            return Task.FromResult(workSession);
        }

        public Task<List<WorkSession>> GetActiveWorkSessionsAsync()
        {
            return Task.FromResult(_activeSessions.Values.ToList());
        }

        public async Task<WorkSessionStatistics> GetDailyStatisticsAsync(DateTime date)
        {
            var cacheKey = date.ToString("yyyy-MM-dd");
            
            if (_dailyStatsCache.TryGetValue(cacheKey, out var cachedStats))
            {
                return cachedStats;
            }

            // Se não está no cache, calcula estatísticas
            var stats = await CalculateDailyStatisticsAsync(date);
            _dailyStatsCache[cacheKey] = stats;
            
            return stats;
        }

        #region Statistics and Analytics

        private Task UpdateDailyStatisticsAsync(WorkSession completedSession)
        {
            try
            {
                var dateKey = completedSession.StartTime.ToString("yyyy-MM-dd");
                
                // Remove do cache para forçar recálculo
                _dailyStatsCache.TryRemove(dateKey, out _);
                
                // ✅ FIX: Não precisa ser async aqui - operação simples
                _logger.LogDebug($"Estatísticas diárias atualizadas para {dateKey}");
                
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao atualizar estatísticas diárias");
                return Task.CompletedTask;
            }
        }

        private Task<WorkSessionStatistics> CalculateDailyStatisticsAsync(DateTime date)
        {
            try
            {
                // TODO: Em implementação real, buscaria do banco de dados
                // Aqui simula com sessões ativas do dia
                var sessionsToday = _activeSessions.Values
                    .Where(s => s.StartTime.Date == date.Date)
                    .ToList();

                var stats = new WorkSessionStatistics
                {
                    Date = date.Date,
                    TotalSessions = sessionsToday.Count,
                    TotalWorkTime = TimeSpan.FromTicks(sessionsToday.Sum(s => s.Duration?.Ticks ?? 0)),
                    TotalSaves = sessionsToday.Sum(s => s.SaveCount),
                    ActiveEngineers = sessionsToday.Select(s => s.Engineer).Distinct().Count(),
                    ActiveProjects = sessionsToday.Where(s => !string.IsNullOrEmpty(s.ProjectId))
                                                .Select(s => s.ProjectId).Distinct().Count(),
                    AverageSessionDuration = sessionsToday.Count > 0 
                        ? TimeSpan.FromTicks((long)sessionsToday.Average(s => s.Duration?.Ticks ?? 0))
                        : TimeSpan.Zero,
                    MostActiveProject = sessionsToday
                        .Where(s => !string.IsNullOrEmpty(s.ProjectId))
                        .GroupBy(s => s.ProjectId)
                        .OrderByDescending(g => g.Sum(s => s.Duration?.TotalMinutes ?? 0))
                        .FirstOrDefault()?.Key,
                    TopEngineers = sessionsToday
                        .Where(s => !string.IsNullOrEmpty(s.Engineer))
                        .GroupBy(s => s.Engineer)
                        .Select(g => new EngineerStatistics
                        {
                            Engineer = g.Key!,
                            SessionCount = g.Count(),
                            TotalWorkTime = TimeSpan.FromTicks(g.Sum(s => s.Duration?.Ticks ?? 0)),
                            TotalSaves = g.Sum(s => s.SaveCount)
                        })
                        .OrderByDescending(e => e.TotalWorkTime)
                        .Take(5)
                        .ToList()
                };

                return Task.FromResult(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao calcular estatísticas para {date:yyyy-MM-dd}");
                return Task.FromResult(new WorkSessionStatistics { Date = date.Date });
            }
        }

        #endregion

        #region Helper Methods

        private static string GenerateSessionId()
        {
            // Gera ID único baseado em timestamp + random
            var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            var random = Random.Shared.Next(1000, 9999);
            return $"WS_{timestamp}_{random}";
        }

        #endregion
    }

    #region Supporting Models

    public class WorkSessionStatistics
    {
        public DateTime Date { get; set; }
        public int TotalSessions { get; set; }
        public TimeSpan TotalWorkTime { get; set; }
        public int TotalSaves { get; set; }
        public int ActiveEngineers { get; set; }
        public int ActiveProjects { get; set; }
        public TimeSpan AverageSessionDuration { get; set; }
        public string? MostActiveProject { get; set; }
        public List<EngineerStatistics> TopEngineers { get; set; } = new();

        // Calculated properties
        public double AverageSavesPerSession => TotalSessions > 0 ? (double)TotalSaves / TotalSessions : 0;
        public double AverageWorkTimePerEngineer => ActiveEngineers > 0 ? TotalWorkTime.TotalHours / ActiveEngineers : 0;
    }

    public class EngineerStatistics
    {
        public string Engineer { get; set; } = string.Empty;
        public int SessionCount { get; set; }
        public TimeSpan TotalWorkTime { get; set; }
        public int TotalSaves { get; set; }
        public double AverageSavesPerSession => SessionCount > 0 ? (double)TotalSaves / SessionCount : 0;
    }

    #endregion
}