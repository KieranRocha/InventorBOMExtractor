// Controllers/BOMController.cs - USA SEUS SERVICES EXISTENTES
using Microsoft.AspNetCore.Mvc;
using InventorBOMExtractor.Services;
using InventorBOMExtractor.Models;

namespace InventorBOMExtractor.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BOMController : ControllerBase
    {
        // 🔄 USANDO SEUS SERVICES EXISTENTES
        private readonly InventorBomExtractor _bomExtractor;
        private readonly IInventorConnectionService _connectionService;
        private readonly IDocumentProcessingService _documentService;
        private readonly ILogger<BOMController> _logger;

        public BOMController(
            InventorBomExtractor bomExtractor,
            IInventorConnectionService connectionService,
            IDocumentProcessingService documentService,
            ILogger<BOMController> logger)
        {
            _bomExtractor = bomExtractor;
            _connectionService = connectionService;
            _documentService = documentService;
            _logger = logger;
        }

        /// <summary>
        /// Verifica status da conexão com Inventor
        /// </summary>
        [HttpGet("status")]
        public ActionResult GetStatus()
        {
            try
            {
                // 🔄 USAR SEU SERVICE EXISTENTE
                var isConnected = _connectionService.IsConnected;
                
                return Ok(new 
                {
                    inventorRunning = isConnected,
                    inventorVersion = _connectionService.InventorVersion ?? "Unknown",
                    companionId = Environment.MachineName,
                    timestamp = DateTime.Now,
                    version = "2.0.0-webapi"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao verificar status do Inventor");
                return Ok(new 
                {
                    inventorRunning = false,
                    error = ex.Message,
                    timestamp = DateTime.Now
                });
            }
        }

        /// <summary>
        /// Extrai BOM de um arquivo CAD específico
        /// </summary>
        [HttpPost("extract")]
        public async Task<ActionResult> ExtractBOM([FromBody] ExtractBOMRequest request)
        {
            try
            {
                _logger.LogInformation($"📋 Extraindo BOM do arquivo: {request.FilePath}");
                
                if (string.IsNullOrEmpty(request.FilePath))
                {
                    return BadRequest(new { error = "FilePath é obrigatório" });
                }

                if (!System.IO.File.Exists(request.FilePath))
                {
                    return BadRequest(new { error = "Arquivo não encontrado" });
                }

                // 🔄 USAR SEU MÉTODO EXISTENTE
                var bomItems = await Task.Run(() => _bomExtractor.GetBOMFromFile(request.FilePath));
                
                return Ok(new 
                {
                    success = true,
                    filePath = request.FilePath,
                    fileName = Path.GetFileName(request.FilePath),
                    bomData = bomItems,
                    totalItems = bomItems.Count,
                    extractedAt = DateTime.Now,
                    extractedBy = Environment.MachineName
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao extrair BOM do arquivo: {FilePath}", request.FilePath);
                return BadRequest(new 
                {
                    success = false,
                    error = ex.Message,
                    filePath = request.FilePath
                });
            }
        }



        /// <summary>
        /// Lista assemblies abertos no Inventor
        /// </summary>
        [HttpGet("open-assemblies")]
        public ActionResult GetOpenAssemblies()
        {
            try
            {
                _logger.LogInformation("📂 Listando assemblies abertos no Inventor");
                
                // 🔄 USAR NOVO MÉTODO IMPLEMENTADO
                var openAssemblies = _bomExtractor.ListOpenAssemblies();
                
                return Ok(new 
                {
                    success = true,
                    assemblies = openAssemblies.Select(assembly => new
                    {
                        fileName = assembly.FileName,
                        filePath = assembly.FilePath,
                        isActive = assembly.IsActive,
                        isSaved = assembly.IsSaved,
                        documentType = assembly.DocumentType
                    }).ToList(),
                    totalCount = openAssemblies.Count,
                    timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao listar assemblies abertos");
                return BadRequest(new 
                {
                    success = false,
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Extrai BOM de um assembly atualmente aberto no Inventor
        /// </summary>
        [HttpPost("extract-from-open")]
        public async Task<ActionResult> ExtractBOMFromOpenAssembly([FromBody] ExtractFromOpenRequest request)
        {
            try
            {
                _logger.LogInformation($"📋 Extraindo BOM de assembly aberto: {request.FileName}");
                
                if (string.IsNullOrEmpty(request.FileName))
                {
                    return BadRequest(new { error = "FileName é obrigatório" });
                }

                // 🔄 USAR NOVO MÉTODO IMPLEMENTADO
                var bomItems = await Task.Run(() => _bomExtractor.GetBOMFromOpenAssembly(request.FileName));
                
                return Ok(new 
                {
                    success = true,
                    fileName = request.FileName,
                    bomData = bomItems,
                    totalItems = bomItems.Count,
                    extractedAt = DateTime.Now,
                    extractedBy = Environment.MachineName
                });
            }
            catch (FileNotFoundException ex)
            {
                _logger.LogWarning(ex, "Assembly não encontrado: {FileName}", request.FileName);
                
                // Listar assemblies disponíveis para ajudar o usuário
                var availableAssemblies = _bomExtractor.ListOpenAssemblies()
                    .Select(a => a.FileName).ToList();
                
                return BadRequest(new 
                {
                    success = false,
                    error = $"Assembly '{request.FileName}' não está aberto no Inventor",
                    availableAssemblies = availableAssemblies,
                    hint = availableAssemblies.Count > 0 ? 
                           "Assemblies disponíveis listados acima" : 
                           "Nenhum assembly está aberto no Inventor"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao extrair BOM de assembly aberto: {FileName}", request.FileName);
                return BadRequest(new 
                {
                    success = false,
                    error = ex.Message,
                    fileName = request.FileName
                });
            }
        }

        /// <summary>
        /// Obtém informações do documento ativo no Inventor
        /// </summary>
        [HttpGet("active-document")]
        public ActionResult GetActiveDocument()
        {
            try
            {
                _logger.LogInformation("📄 Obtendo informações do documento ativo");
                
                var activeDoc = _bomExtractor.GetActiveDocumentInfo();
                
                if (activeDoc == null)
                {
                    return Ok(new 
                    {
                        success = true,
                        hasActiveDocument = false,
                        message = "Nenhum documento ativo no Inventor"
                    });
                }
                
                return Ok(new 
                {
                    success = true,
                    hasActiveDocument = true,
                    activeDocument = new
                    {
                        fileName = activeDoc.FileName,
                        filePath = activeDoc.FilePath,
                        documentType = activeDoc.DocumentType,
                        isSaved = activeDoc.IsSaved,
                        isAssembly = activeDoc.IsAssembly,
                        lastSaved = activeDoc.LastSaved
                    },
                    timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao obter documento ativo");
                return BadRequest(new 
                {
                    success = false,
                    error = ex.Message
                });
            }
        }
        [HttpPost("reconnect")]
        public async Task<ActionResult> ReconnectToInventor()
        {
            try
            {
                _logger.LogInformation("🔌 Forçando reconexão com Inventor");
                
                // 🔄 USAR SEU SERVICE EXISTENTE
                await _connectionService.ConnectAsync();
                
                return Ok(new 
                {
                    success = true,
                    message = "Reconexão com Inventor realizada",
                    isConnected = _connectionService.IsConnected,
                    timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao reconectar com Inventor");
                return BadRequest(new 
                {
                    success = false,
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Abre um arquivo no Inventor
        /// </summary>
        [HttpPost("open-file")]
        public async Task<ActionResult> OpenFile([FromBody] OpenFileRequest request)
        {
            try
            {
                _logger.LogInformation($"📂 Abrindo arquivo no Inventor: {request.FilePath}");
                
                if (string.IsNullOrEmpty(request.FilePath))
                {
                    return BadRequest(new { error = "FilePath é obrigatório" });
                }

                if (!System.IO.File.Exists(request.FilePath))
                {
                    return BadRequest(new { error = "Arquivo não encontrado" });
                }

                // 🔄 USAR NOVO MÉTODO IMPLEMENTADO
                var success = await Task.Run(() => _bomExtractor.OpenDocument(request.FilePath));
                
                if (success)
                {
                    return Ok(new 
                    {
                        success = true,
                        message = $"Arquivo aberto com sucesso: {Path.GetFileName(request.FilePath)}",
                        filePath = request.FilePath,
                        fileName = Path.GetFileName(request.FilePath),
                        timestamp = DateTime.Now
                    });
                }
                else
                {
                    return BadRequest(new 
                    {
                        success = false,
                        error = "Falha ao abrir arquivo no Inventor",
                        filePath = request.FilePath
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao abrir arquivo: {FilePath}", request.FilePath);
                return BadRequest(new 
                {
                    success = false,
                    error = ex.Message,
                    filePath = request.FilePath
                });
            }
        }

        /// <summary>
        /// Ativa um documento específico que já está aberto
        /// </summary>
        [HttpPost("activate-document")]
        public async Task<ActionResult> ActivateDocument([FromBody] ActivateDocumentRequest request)
        {
            try
            {
                _logger.LogInformation($"🎯 Ativando documento: {request.FileName}");
                
                if (string.IsNullOrEmpty(request.FileName))
                {
                    return BadRequest(new { error = "FileName é obrigatório" });
                }

                var success = await Task.Run(() => _bomExtractor.ActivateDocument(request.FileName));
                
                if (success)
                {
                    return Ok(new 
                    {
                        success = true,
                        message = $"Documento ativado: {request.FileName}",
                        fileName = request.FileName,
                        timestamp = DateTime.Now
                    });
                }
                else
                {
                    return BadRequest(new 
                    {
                        success = false,
                        error = "Falha ao ativar documento",
                        fileName = request.FileName
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao ativar documento: {FileName}", request.FileName);
                return BadRequest(new 
                {
                    success = false,
                    error = ex.Message,
                    fileName = request.FileName
                });
            }
        }
    }

    // 🆕 MODELS PARA REQUESTS
    public class ExtractBOMRequest
    {
        public string FilePath { get; set; } = "";
    }

    public class ExtractFromOpenRequest
    {
        public string FileName { get; set; } = "";
    }

    public class OpenFileRequest
    {
        public string FilePath { get; set; } = "";
    }

    public class ActivateDocumentRequest
    {
        public string FileName { get; set; } = "";
    }
}