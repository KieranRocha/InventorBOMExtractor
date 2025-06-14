// Services/DocumentProcessingService.cs
using Microsoft.Extensions.Logging;
using InventorBOMExtractor.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace InventorBOMExtractor.Services
{
    public interface IDocumentProcessingService
    {
        Task ProcessDocumentSaveAsync(DocumentEvent documentEvent);
        Task ProcessDocumentChangeAsync(DocumentEvent documentEvent);
        Task<BOMDataWithContext?> ExtractBOMWithContextAsync(string filePath, ProjectInfo? projectInfo, string? workSessionId = null);
    }

    public class DocumentProcessingService : IDocumentProcessingService
    {
        private readonly ILogger<DocumentProcessingService> _logger;
        private readonly IInventorConnectionService _inventorConnection;
        private readonly IApiCommunicationService _apiCommunication;
        private readonly InventorBomExtractor _bomExtractor;

        public DocumentProcessingService(
            ILogger<DocumentProcessingService> logger,
            IInventorConnectionService inventorConnection,
            IApiCommunicationService apiCommunication,
            InventorBomExtractor bomExtractor)
        {
            _logger = logger;
            _inventorConnection = inventorConnection;
            _apiCommunication = apiCommunication;
            _bomExtractor = bomExtractor;
        }

        public async Task ProcessDocumentSaveAsync(DocumentEvent documentEvent)
        {
            try
            {
                _logger.LogInformation($"📄 Processando save: {documentEvent.FileName} (Tipo: {documentEvent.DocumentType})");

                // Verifica se arquivo ainda existe (pode ter sido movido/deletado)
                if (!File.Exists(documentEvent.FilePath))
                {
                    _logger.LogWarning($"⚠️ Arquivo não encontrado: {documentEvent.FilePath}");
                    return;
                }

                // Aguarda arquivo estabilizar (pode ainda estar sendo escrito)
                await WaitForFileStabilityAsync(documentEvent.FilePath);

                // Processa baseado no tipo de documento
                switch (documentEvent.DocumentType)
                {
                    case DocumentType.Assembly:
                        await ProcessAssemblyDocumentAsync(documentEvent);
                        break;
                        
                    case DocumentType.Part:
                        await ProcessPartDocumentAsync(documentEvent);
                        break;
                        
                    case DocumentType.Drawing:
                        await ProcessDrawingDocumentAsync(documentEvent);
                        break;
                        
                    default:
                        _logger.LogDebug($"Tipo de documento não processado: {documentEvent.DocumentType}");
                        break;
                }

                // Sempre envia atividade para API (auditoria)
                await SendDocumentActivityAsync(documentEvent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao processar save do documento: {documentEvent.FileName}");
            }
        }

        public async Task ProcessDocumentChangeAsync(DocumentEvent documentEvent)
        {
            try
            {
                _logger.LogDebug($"📝 Processando mudança: {documentEvent.FileName}");

                // Para mudanças simples, só registra atividade
                await SendDocumentActivityAsync(documentEvent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao processar mudança do documento: {documentEvent.FileName}");
            }
        }

        #region Assembly Processing

        private async Task ProcessAssemblyDocumentAsync(DocumentEvent documentEvent)
        {
            try
            {
                _logger.LogInformation($"🔧 Extraindo BOM de assembly: {documentEvent.FileName}");

                // Extrai BOM com contexto completo
                var bomData = await ExtractBOMWithContextAsync(
                    documentEvent.FilePath, 
                    CreateProjectInfoFromEvent(documentEvent),
                    null // workSessionId será obtido via documentEvent se necessário
                );

                if (bomData != null && bomData.BOMItems.Count > 0)
                {
                    // Envia BOM para API
                    await _apiCommunication.SendBOMDataAsync(bomData);
                    
                    _logger.LogInformation($"✅ BOM enviado: {bomData.TotalItems} itens (Projeto: {bomData.ProjectName})");
                }
                else
                {
                    _logger.LogWarning($"⚠️ BOM vazio ou erro na extração: {documentEvent.FileName}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao processar assembly: {documentEvent.FileName}");
            }
        }

        public async Task<BOMDataWithContext?> ExtractBOMWithContextAsync(string filePath, ProjectInfo? projectInfo, string? workSessionId = null)
        {
            try
            {
                if (!_inventorConnection.IsConnected)
                {
                    _logger.LogError("Inventor não conectado - não é possível extrair BOM");
                    return null;
                }

                // Extrai BOM usando código existente (em background thread)
                var bomItems = await Task.Run(() => _bomExtractor.GetBOMFromFile(filePath));

                if (bomItems == null || bomItems.Count == 0)
                {
                    _logger.LogWarning($"BOM vazio para arquivo: {Path.GetFileName(filePath)}");
                    return null;
                }

                // Cria objeto com contexto rico
                var bomData = new BOMDataWithContext
                {
                    ProjectId = projectInfo?.ProjectId ?? "UNKNOWN",
                    ProjectName = projectInfo?.DetectedName ?? "Projeto Desconhecido",
                    AssemblyFileName = Path.GetFileName(filePath),
                    AssemblyFilePath = filePath,
                    ExtractedAt = DateTime.UtcNow,
                    ExtractedBy = Environment.MachineName,
                    WorkSessionId = workSessionId,
                    Engineer = Environment.UserName,
                    BOMItems = bomItems,
                    InventorVersion = _inventorConnection.InventorVersion ?? "Unknown"
                };

                return bomData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao extrair BOM: {filePath}");
                return null;
            }
        }

        #endregion

        #region Part Processing

        private async Task ProcessPartDocumentAsync(DocumentEvent documentEvent)
        {
            try
            {
                _logger.LogDebug($"🔩 Processando part: {documentEvent.FileName}");

                // Para parts, podemos extrair propriedades básicas
                var partProperties = await ExtractPartPropertiesAsync(documentEvent.FilePath);

                if (partProperties != null)
                {
                    await _apiCommunication.SendPartDataAsync(new PartDataWithContext
                    {
                        ProjectId = documentEvent.ProjectId ?? "UNKNOWN",
                        ProjectName = documentEvent.ProjectName ?? "Projeto Desconhecido",
                        PartFileName = documentEvent.FileName,
                        PartFilePath = documentEvent.FilePath,
                        ExtractedAt = DateTime.UtcNow,
                        ExtractedBy = Environment.MachineName,
                        Engineer = documentEvent.Engineer,
                        Properties = partProperties
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao processar part: {documentEvent.FileName}");
            }
        }

        private async Task<Dictionary<string, object>?> ExtractPartPropertiesAsync(string filePath)
        {
            try
            {
                return await Task.Run(() =>
                {
                    // Aqui poderia usar Inventor API para extrair propriedades da part
                    // Por simplicity, retorna properties básicas
                    var fileInfo = new FileInfo(filePath);
                    
                    return new Dictionary<string, object>
                    {
                        ["FileName"] = fileInfo.Name,
                        ["FileSizeBytes"] = fileInfo.Length,
                        ["LastModified"] = fileInfo.LastWriteTime,
                        ["Extension"] = fileInfo.Extension
                        // TODO: Adicionar properties específicas do Inventor (Material, Mass, Volume, etc.)
                    };
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao extrair propriedades da part: {filePath}");
                return null;
            }
        }

        #endregion

        #region Drawing Processing

        private async Task ProcessDrawingDocumentAsync(DocumentEvent documentEvent)
        {
            try
            {
                _logger.LogDebug($"📐 Processando drawing: {documentEvent.FileName}");

                // Para drawings, registra atividade mas não extrai dados complexos
                // Futuro: poderia extrair lista de views, dimensões, etc.
                
                await Task.CompletedTask; // Placeholder
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao processar drawing: {documentEvent.FileName}");
            }
        }

        #endregion

        #region Helper Methods

        private async Task WaitForFileStabilityAsync(string filePath)
        {
            const int maxAttempts = 10;
            const int delayMs = 500;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                try
                {
                    // Tenta abrir arquivo exclusivo para verificar se está livre
                    using var fileStream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.None);
                    
                    // Se chegou aqui, arquivo está livre
                    _logger.LogDebug($"Arquivo estável após {attempt + 1} tentativas: {Path.GetFileName(filePath)}");
                    return;
                }
                catch (IOException)
                {
                    // Arquivo ainda sendo escrito
                    _logger.LogDebug($"Arquivo ainda sendo escrito, tentativa {attempt + 1}: {Path.GetFileName(filePath)}");
                    await Task.Delay(delayMs);
                }
            }

            _logger.LogWarning($"Arquivo pode ainda estar sendo escrito: {Path.GetFileName(filePath)}");
        }

        private ProjectInfo? CreateProjectInfoFromEvent(DocumentEvent documentEvent)
        {
            if (string.IsNullOrEmpty(documentEvent.ProjectId))
                return null;

            return new ProjectInfo
            {
                ProjectId = documentEvent.ProjectId,
                DetectedName = documentEvent.ProjectName ?? "Projeto Desconhecido",
                FolderPath = Path.GetDirectoryName(documentEvent.FilePath) ?? string.Empty,
                IsValidProject = true
            };
        }

        private async Task SendDocumentActivityAsync(DocumentEvent documentEvent)
        {
            try
            {
                var activity = new DocumentActivity
                {
                    FilePath = documentEvent.FilePath,
                    FileName = documentEvent.FileName,
                    EventType = documentEvent.EventType.ToString(),
                    DocumentType = documentEvent.DocumentType.ToString(),
                    ProjectId = documentEvent.ProjectId,
                    ProjectName = documentEvent.ProjectName,
                    Engineer = documentEvent.Engineer,
                    Timestamp = documentEvent.Timestamp,
                    CompanionId = Environment.MachineName,
                    InventorVersion = documentEvent.InventorVersion
                };

                await _apiCommunication.SendDocumentActivityAsync(activity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao enviar atividade do documento para API");
            }
        }

        #endregion
    }

    #region Supporting Models

    public class PartDataWithContext
    {
        public string ProjectId { get; set; } = string.Empty;
        public string ProjectName { get; set; } = string.Empty;
        public string PartFileName { get; set; } = string.Empty;
        public string PartFilePath { get; set; } = string.Empty;
        public DateTime ExtractedAt { get; set; }
        public string ExtractedBy { get; set; } = string.Empty;
        public string? Engineer { get; set; }
        public Dictionary<string, object> Properties { get; set; } = new();
    }

    public class DocumentActivity
    {
        public string FilePath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string EventType { get; set; } = string.Empty;
        public string DocumentType { get; set; } = string.Empty;
        public string? ProjectId { get; set; }
        public string? ProjectName { get; set; }
        public string? Engineer { get; set; }
        public DateTime Timestamp { get; set; }
        public string CompanionId { get; set; } = string.Empty;
        public string? InventorVersion { get; set; }
    }

    #endregion
}