// Services/InventorDocumentEventService.cs
using Microsoft.Extensions.Logging;
using InventorBOMExtractor.Events;
using InventorBOMExtractor.Models;
using System;
using System.IO;
using System.Threading.Tasks;

namespace InventorBOMExtractor.Services
{
    public interface IInventorDocumentEventService
    {
        event EventHandler<DocumentOpenedEventArgs>? DocumentOpened;
        event EventHandler<DocumentClosedEventArgs>? DocumentClosed;
        event EventHandler<DocumentSavedEventArgs>? DocumentSaved;
        
        Task<bool> SubscribeToDocumentEventsAsync();
        Task UnsubscribeFromDocumentEventsAsync();
        bool IsSubscribed { get; }
    }

    public class InventorDocumentEventService : IInventorDocumentEventService
    {
        private readonly ILogger<InventorDocumentEventService> _logger;
        private readonly IInventorConnectionService _inventorConnection;
        private dynamic? _inventorApp;
        private bool _isSubscribed = false;

        // Eventos públicos
        public event EventHandler<DocumentOpenedEventArgs>? DocumentOpened;
        public event EventHandler<DocumentClosedEventArgs>? DocumentClosed;
        public event EventHandler<DocumentSavedEventArgs>? DocumentSaved;

        public bool IsSubscribed => _isSubscribed;

        public InventorDocumentEventService(
            ILogger<InventorDocumentEventService> logger,
            IInventorConnectionService inventorConnection)
        {
            _logger = logger;
            _inventorConnection = inventorConnection;
        }

        public async Task<bool> SubscribeToDocumentEventsAsync()
        {
            try
            {
                if (_isSubscribed)
                {
                    _logger.LogWarning("Já subscrito aos eventos do Inventor");
                    return true;
                }

                // Obtém aplicação Inventor
                if (!_inventorConnection.IsConnected)
                {
                    _logger.LogError("Inventor não conectado - não é possível subscrever eventos");
                    return false;
                }

                _inventorApp = await GetInventorApplicationAsync();
                if (_inventorApp == null)
                {
                    _logger.LogError("Não foi possível obter aplicação Inventor");
                    return false;
                }

                // 🎯 SUBSCRIÇÃO AOS EVENTOS NATIVOS DO INVENTOR
                _inventorApp.ApplicationEvents.OnDocumentOpen += OnInventorDocumentOpen;
                _inventorApp.ApplicationEvents.OnDocumentClose += OnInventorDocumentClose;
                _inventorApp.ApplicationEvents.OnDocumentSave += OnInventorDocumentSave;

                _isSubscribed = true;
                _logger.LogInformation("✅ Subscrito aos eventos do Inventor com sucesso");

                // Detecta documentos já abertos
                await DetectAlreadyOpenDocumentsAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao subscrever eventos do Inventor");
                return false;
            }
        }

        public async Task UnsubscribeFromDocumentEventsAsync()
        {
            try
            {
                if (!_isSubscribed || _inventorApp == null)
                    return;

                _inventorApp.ApplicationEvents.OnDocumentOpen -= OnInventorDocumentOpen;
                _inventorApp.ApplicationEvents.OnDocumentClose -= OnInventorDocumentClose;
                _inventorApp.ApplicationEvents.OnDocumentSave -= OnInventorDocumentSave;

                _isSubscribed = false;
                _logger.LogInformation("🔌 Desconectado dos eventos do Inventor");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao desconectar eventos do Inventor");
            }
        }

        private async Task<dynamic?> GetInventorApplicationAsync()
        {
            try
            {
                // Usa o serviço de conexão existente
                return await Task.Run(() => _inventorConnection.GetInventorApp());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao obter aplicação Inventor");
                return null;
            }
        }

        #region Inventor Event Handlers

        private void OnInventorDocumentOpen(dynamic document, dynamic timing, dynamic context, out dynamic handling)
        {
            handling = 0; // kEventHandled

            try
            {
                // Só processa After (timing = 1)
                if ((int)timing != 1) return;

                var filePath = document.FullFileName?.ToString() ?? string.Empty;
                var fileName = document.DisplayName?.ToString() ?? string.Empty;

                if (string.IsNullOrEmpty(filePath))
                {
                    _logger.LogWarning("Documento aberto sem path válido");
                    return;
                }

                var eventArgs = new DocumentOpenedEventArgs
                {
                    FilePath = filePath,
                    FileName = fileName,
                    DocumentType = DetermineDocumentType(filePath),
                    Timestamp = DateTime.UtcNow,
                    FileSizeBytes = GetFileSize(filePath)
                };

                _logger.LogInformation($"📂 ABERTO: {fileName}");
                DocumentOpened?.Invoke(this, eventArgs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro no evento OnDocumentOpen");
            }
        }

        private void OnInventorDocumentClose(dynamic document, string fullFileName, dynamic timing, dynamic context, out dynamic handling)
        {
            handling = 0; // kEventHandled

            try
            {
                // Só processa Before (timing = 0)
                if ((int)timing != 0) return;

                var filePath = fullFileName ?? string.Empty;
                var fileName = document?.DisplayName?.ToString() ?? Path.GetFileName(filePath);

                if (string.IsNullOrEmpty(filePath))
                {
                    _logger.LogWarning("Documento fechado sem path válido");
                    return;
                }

                var eventArgs = new DocumentClosedEventArgs
                {
                    FilePath = filePath,
                    FileName = fileName,
                    DocumentType = DetermineDocumentType(filePath),
                    Timestamp = DateTime.UtcNow
                };

                _logger.LogInformation($"📂 FECHADO: {fileName}");
                DocumentClosed?.Invoke(this, eventArgs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro no evento OnDocumentClose");
            }
        }

        private void OnInventorDocumentSave(dynamic document, dynamic timing, dynamic context, out dynamic handling)
        {
            handling = 0; // kEventHandled

            try
            {
                // Só processa After (timing = 1)
                if ((int)timing != 1) return;

                var filePath = document.FullFileName?.ToString() ?? string.Empty;
                var fileName = document.DisplayName?.ToString() ?? string.Empty;

                if (string.IsNullOrEmpty(filePath))
                {
                    _logger.LogWarning("Documento salvo sem path válido");
                    return;
                }

                var eventArgs = new DocumentSavedEventArgs
                {
                    FilePath = filePath,
                    FileName = fileName,
                    DocumentType = DetermineDocumentType(filePath),
                    Timestamp = DateTime.UtcNow,
                    IsAutoSave = false // Inventor não diferencia auto-save facilmente
                };

                _logger.LogDebug($"💾 SALVO: {fileName}");
                DocumentSaved?.Invoke(this, eventArgs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro no evento OnDocumentSave");
            }
        }

        #endregion

        #region Helper Methods

        private DocumentType DetermineDocumentType(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            return extension switch
            {
                ".iam" => DocumentType.Assembly,
                ".ipt" => DocumentType.Part,
                ".idw" => DocumentType.Drawing,
                ".ipn" => DocumentType.Presentation,
                _ => DocumentType.Unknown
            };
        }

        private long GetFileSize(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    return new FileInfo(filePath).Length;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, $"Erro ao obter tamanho do arquivo: {filePath}");
            }
            return 0;
        }

        private async Task DetectAlreadyOpenDocumentsAsync()
        {
            try
            {
                if (_inventorApp == null) return;

                await Task.Run(() =>
                {
                    var documents = _inventorApp.Documents;
                    var count = documents.Count;

                    _logger.LogInformation($"🔍 Detectando {count} documentos já abertos");

                    for (int i = 1; i <= count; i++)
                    {
                        try
                        {
                            var doc = documents[i];
                            var filePath = doc.FullFileName?.ToString() ?? string.Empty;
                            var fileName = doc.DisplayName?.ToString() ?? string.Empty;

                            if (!string.IsNullOrEmpty(filePath))
                            {
                                var eventArgs = new DocumentOpenedEventArgs
                                {
                                    FilePath = filePath,
                                    FileName = fileName,
                                    DocumentType = DetermineDocumentType(filePath),
                                    Timestamp = DateTime.UtcNow,
                                    FileSizeBytes = GetFileSize(filePath)
                                };

                                _logger.LogInformation($"📂 DETECTADO ABERTO: {fileName}");
                                DocumentOpened?.Invoke(this, eventArgs);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, $"Erro ao processar documento {i}");
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao detectar documentos já abertos");
            }
        }

        #endregion
    }
}