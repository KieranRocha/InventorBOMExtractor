// Services/WorkDrivenMonitoringService.cs
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using InventorBOMExtractor.Configuration;
using InventorBOMExtractor.Events;
using InventorBOMExtractor.Models;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace InventorBOMExtractor.Services
{
    public interface IWorkDrivenMonitoringService
    {
        Task StartAsync(CancellationToken cancellationToken);
        Task StopAsync(CancellationToken cancellationToken);
        int ActiveWatchersCount { get; }
        IReadOnlyDictionary<string, DocumentWatcher> ActiveWatchers { get; }
    }

    public class WorkDrivenMonitoringService : IWorkDrivenMonitoringService
    {
        private readonly ILogger<WorkDrivenMonitoringService> _logger;
        private readonly IInventorDocumentEventService _documentEventService;
        private readonly IDocumentProcessingService _documentProcessor;
        private readonly IWorkSessionService _workSessionService;
        private readonly CompanionSettings _settings;

        // Watchers ativos - thread-safe
        private readonly ConcurrentDictionary<string, DocumentWatcher> _activeWatchers = new();
        
        // Debouncing para evitar múltiplos eventos
        private readonly ConcurrentDictionary<string, DateTime> _lastEventTimes = new();
        private readonly int _debounceDelayMs = 2000;

        public int ActiveWatchersCount => _activeWatchers.Count;
        public IReadOnlyDictionary<string, DocumentWatcher> ActiveWatchers => _activeWatchers;

        public WorkDrivenMonitoringService(
            ILogger<WorkDrivenMonitoringService> logger,
            IInventorDocumentEventService documentEventService,
            IDocumentProcessingService documentProcessor,
            IWorkSessionService workSessionService,
            IOptions<CompanionSettings> settings)
        {
            _logger = logger;
            _documentEventService = documentEventService;
            _documentProcessor = documentProcessor;
            _workSessionService = workSessionService;
            _settings = settings.Value;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("🔥 Work-Driven Monitoring iniciando...");

                // Subscreve aos eventos de documento
                _documentEventService.DocumentOpened += OnDocumentOpened;
                _documentEventService.DocumentClosed += OnDocumentClosed;
                _documentEventService.DocumentSaved += OnDocumentSaved;

                // Garante que eventos estão subscritos
                if (!_documentEventService.IsSubscribed)
                {
                    var subscribed = await _documentEventService.SubscribeToDocumentEventsAsync();
                    if (!subscribed)
                    {
                        _logger.LogError("❌ Falha ao subscrever eventos do Inventor");
                        return;
                    }
                }

                _logger.LogInformation("✅ Work-Driven Monitoring ativo - aguardando atividade...");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao iniciar Work-Driven Monitoring");
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("🔴 Work-Driven Monitoring parando...");

                // Desconecta eventos
                _documentEventService.DocumentOpened -= OnDocumentOpened;
                _documentEventService.DocumentClosed -= OnDocumentClosed;
                _documentEventService.DocumentSaved -= OnDocumentSaved;

                // Finaliza todas as sessões ativas
                await FinalizeAllActiveSessionsAsync();

                // Dispose de todos os watchers
                foreach (var watcher in _activeWatchers.Values)
                {
                    watcher.Dispose();
                }
                _activeWatchers.Clear();

                _logger.LogInformation("✅ Work-Driven Monitoring parado");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao parar Work-Driven Monitoring");
            }
        }

        #region Document Event Handlers

        private async void OnDocumentOpened(object? sender, DocumentOpenedEventArgs e)
        {
            try
            {
                _logger.LogInformation($"🔥 INICIANDO monitoring: {e.FileName}");

                // 1. Auto-detecta projeto pelo path
                var projectInfo = DetermineProjectFromPath(e.FilePath);
                if (!projectInfo.IsValidProject)
                {
                    _logger.LogWarning($"⚠️ Projeto não detectado para: {e.FilePath}");
                    // Continua mesmo assim - pode ser arquivo avulso
                    projectInfo.ProjectId = "UNKNOWN";
                    projectInfo.DetectedName = "Arquivo Avulso";
                }

                // 2. Cria FileSystemWatcher específico para este arquivo
                var watcher = CreateFileWatcher(e.FilePath);
                if (watcher == null)
                {
                    _logger.LogError($"❌ Erro ao criar watcher para: {e.FilePath}");
                    return;
                }

                // 3. Inicia Work Session
                var workSession = await _workSessionService.StartWorkSessionAsync(new WorkSession
                {
                    FilePath = e.FilePath,
                    FileName = e.FileName,
                    ProjectId = projectInfo.ProjectId,
                    ProjectName = projectInfo.DetectedName,
                    Engineer = Environment.UserName,
                    StartTime = e.Timestamp
                });

                // 4. Cria DocumentWatcher
                var documentWatcher = new DocumentWatcher
                {
                    FilePath = e.FilePath,
                    FileName = e.FileName,
                    ProjectInfo = projectInfo,
                    FileWatcher = watcher,
                    OpenedAt = e.Timestamp,
                    LastActivity = e.Timestamp,
                    SaveCount = 0,
                    WorkSessionId = workSession.Id
                };

                // 5. Armazena na coleção thread-safe
                _activeWatchers[e.FilePath] = documentWatcher;

                _logger.LogInformation($"✅ Monitoring ativo: {e.FileName} (Projeto: {projectInfo.ProjectId}) - Total: {ActiveWatchersCount} watchers");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao processar documento aberto: {e.FileName}");
            }
        }

        private async void OnDocumentClosed(object? sender, DocumentClosedEventArgs e)
        {
            try
            {
                if (_activeWatchers.TryRemove(e.FilePath, out var documentWatcher))
                {
                    _logger.LogInformation($"🔴 PARANDO monitoring: {e.FileName}");

                    // 1. Para e disposa FileSystemWatcher
                    documentWatcher.Dispose();

                    // 2. Finaliza Work Session
                    await _workSessionService.EndWorkSessionAsync(documentWatcher.WorkSessionId, e.Timestamp);

                    _logger.LogInformation($"✅ Monitoring parado: {e.FileName} - Restam: {ActiveWatchersCount} watchers");
                }
                else
                {
                    _logger.LogWarning($"⚠️ Documento fechado não estava sendo monitorado: {e.FileName}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao processar documento fechado: {e.FileName}");
            }
        }

        private async void OnDocumentSaved(object? sender, DocumentSavedEventArgs e)
        {
            try
            {
                if (_activeWatchers.TryGetValue(e.FilePath, out var documentWatcher))
                {
                    // Atualiza estatísticas
                    documentWatcher.LastActivity = e.Timestamp;
                    documentWatcher.SaveCount++;

                    _logger.LogInformation($"💾 SAVE #{documentWatcher.SaveCount}: {e.FileName}");

                    // Atualiza Work Session
                    await _workSessionService.UpdateWorkSessionAsync(documentWatcher.WorkSessionId, "SAVE");

                    // Processa save se for assembly (BOM extraction)
                    if (e.DocumentType == DocumentType.Assembly)
                    {
                        await ProcessAssemblySave(e, documentWatcher);
                    }
                }
                else
                {
                    _logger.LogDebug($"Save ignorado - arquivo não monitorado: {e.FileName}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao processar save: {e.FileName}");
            }
        }

        #endregion

        #region FileSystemWatcher Management

        private FileSystemWatcher? CreateFileWatcher(string filePath)
        {
            try
            {
                var directory = Path.GetDirectoryName(filePath);
                var fileName = Path.GetFileName(filePath);

                if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                {
                    _logger.LogError($"Diretório não existe: {directory}");
                    return null;
                }

                var watcher = new FileSystemWatcher(directory)
                {
                    Filter = fileName,  // ⭐ MONITORA APENAS ESTE ARQUIVO
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                    EnableRaisingEvents = false
                };

                // Event handler para mudanças no arquivo
                watcher.Changed += async (sender, args) => await OnFileChanged(args);

                // Ativa o watcher
                watcher.EnableRaisingEvents = true;

                return watcher;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao criar FileSystemWatcher para: {filePath}");
                return null;
            }
        }

        private async Task OnFileChanged(FileSystemEventArgs e)
        {
            try
            {
                // Debouncing - evita múltiplos eventos do mesmo arquivo
                var eventKey = e.FullPath;
                var now = DateTime.UtcNow;

                if (_lastEventTimes.ContainsKey(eventKey))
                {
                    var timeSinceLastEvent = now - _lastEventTimes[eventKey];
                    if (timeSinceLastEvent.TotalMilliseconds < _debounceDelayMs)
                    {
                        _logger.LogDebug($"Evento ignorado (debounce): {e.Name}");
                        return;
                    }
                }

                _lastEventTimes[eventKey] = now;

                // Processa mudança se arquivo está sendo monitorado
                if (_activeWatchers.TryGetValue(e.FullPath, out var documentWatcher))
                {
                    _logger.LogDebug($"📝 MODIFICADO: {e.Name}");
                    documentWatcher.LastActivity = now;

                    // Processa mudança
                    await _documentProcessor.ProcessDocumentChangeAsync(new DocumentEvent
                    {
                        FilePath = e.FullPath,
                        FileName = e.Name ?? string.Empty,
                        EventType = DocumentEventType.Modified,
                        Timestamp = now,
                        ProjectId = documentWatcher.ProjectInfo?.ProjectId,
                        ProjectName = documentWatcher.ProjectInfo?.DetectedName,
                        Engineer = Environment.UserName
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao processar mudança de arquivo: {e.Name}");
            }
        }

        #endregion

        #region Project Auto-Detection

        private ProjectInfo DetermineProjectFromPath(string filePath)
        {
            try
            {
                // Normaliza path
                var normalizedPath = Path.GetFullPath(filePath);
                
                // Padrões comuns de estrutura de projetos brasileiros
                var patterns = new[]
                {
                    // Padrão: \\projetos\2025_PROJ_466_Bomba_Hidraulica\...
                    new { Pattern = @".*[\\\/](\d{4}_PROJ_(\d+)_([^\\\/]+))[\\\/]", ProjectIndex = 1, IdIndex = 2, NameIndex = 3 },
                    
                    // Padrão: \\projetos\C-466_Bomba_Hidraulica\...  
                    new { Pattern = @".*[\\\/]((C-\d+)_([^\\\/]+))[\\\/]", ProjectIndex = 1, IdIndex = 2, NameIndex = 3 },
                    
                    // Padrão: \\projetos\BMW-123_Motor_Eletrico\...
                    new { Pattern = @".*[\\\/](([A-Z]+-\d+)_([^\\\/]+))[\\\/]", ProjectIndex = 1, IdIndex = 2, NameIndex = 3 },
                    
                    // Padrão genérico: \\projetos\NomeProjeto\...
                    new { Pattern = @".*[\\\/]projetos?[\\\/]([^\\\/]+)[\\\/]", ProjectIndex = 1, IdIndex = -1, NameIndex = 1 }
                };

                foreach (var pattern in patterns)
                {
                    var match = Regex.Match(normalizedPath, pattern.Pattern, RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        var projectFolder = match.Groups[pattern.ProjectIndex].Value;
                        var projectId = pattern.IdIndex > 0 ? match.Groups[pattern.IdIndex].Value : ExtractProjectIdFromFolder(projectFolder);
                        var projectName = pattern.NameIndex > 0 ? match.Groups[pattern.NameIndex].Value.Replace("_", " ") : projectFolder;
                        
                        return new ProjectInfo
                        {
                            ProjectId = projectId,
                            DetectedName = CleanProjectName(projectName),
                            FolderPath = Path.GetDirectoryName(Path.GetDirectoryName(filePath)) ?? string.Empty,
                            Phase = ExtractPhaseFromPath(filePath),
                            IsValidProject = true
                        };
                    }
                }

                return new ProjectInfo { IsValidProject = false };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao detectar projeto: {filePath}");
                return new ProjectInfo { IsValidProject = false };
            }
        }

        private string ExtractProjectIdFromFolder(string folderName)
        {
            // Extrai ID do nome da pasta se não estiver explícito
            var numberMatch = Regex.Match(folderName, @"(\d+)");
            return numberMatch.Success ? $"C-{numberMatch.Groups[1].Value}" : folderName;
        }

        private string CleanProjectName(string name)
        {
            return name.Replace("_", " ").Replace("-", " ").Trim();
        }

        private string ExtractPhaseFromPath(string filePath)
        {
            var pathParts = filePath.Split(Path.DirectorySeparatorChar);
            
            // Procura por padrões de fase
            foreach (var part in pathParts)
            {
                if (Regex.IsMatch(part, @"^\d+[._-]", RegexOptions.IgnoreCase))
                {
                    return Regex.Replace(part, @"^\d+[._-]", "").Replace("_", " ");
                }
                
                if (part.ToLowerInvariant().Contains("montagem")) return "Montagens";
                if (part.ToLowerInvariant().Contains("desenho")) return "Desenhos";
                if (part.ToLowerInvariant().Contains("conceitual")) return "Conceitual";
                if (part.ToLowerInvariant().Contains("detalhe")) return "Detalhamento";
            }

            return "Geral";
        }

        #endregion

        #region Helper Methods

        private async Task ProcessAssemblySave(DocumentSavedEventArgs e, DocumentWatcher documentWatcher)
        {
            try
            {
                _logger.LogInformation($"🔧 Processando assembly salvo: {e.FileName}");

                await _documentProcessor.ProcessDocumentSaveAsync(new DocumentEvent
                {
                    FilePath = e.FilePath,
                    FileName = e.FileName,
                    EventType = DocumentEventType.Saved,
                    DocumentType = e.DocumentType,
                    Timestamp = e.Timestamp,
                    ProjectId = documentWatcher.ProjectInfo?.ProjectId,
                    ProjectName = documentWatcher.ProjectInfo?.DetectedName,
                    Engineer = Environment.UserName
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao processar assembly save: {e.FileName}");
            }
        }

        private async Task FinalizeAllActiveSessionsAsync()
        {
            var activeSessions = new List<string>();
            
            foreach (var watcher in _activeWatchers.Values)
            {
                activeSessions.Add(watcher.WorkSessionId);
            }

            foreach (var sessionId in activeSessions)
            {
                try
                {
                    await _workSessionService.EndWorkSessionAsync(sessionId, DateTime.UtcNow);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Erro ao finalizar sessão: {sessionId}");
                }
            }
        }

        #endregion
    }
}