// Services/InventorConnectionService.cs - ATUALIZAÇÃO STEP 2
using Microsoft.Extensions.Logging;

namespace InventorBOMExtractor.Services
{
    public interface IInventorConnectionService
    {
        bool IsConnected { get; }
        string? InventorVersion { get; }
        Task ConnectAsync();
        Task DisconnectAsync();
        Task<bool> TestConnectionAsync();
        Task ReconnectAsync();
        
        // ✅ NOVO - Para Step 2
        dynamic? GetInventorApp();
    }

    public class InventorConnectionService : IInventorConnectionService
    {
        private readonly ILogger<InventorConnectionService> _logger;
        private readonly InventorBomExtractor _bomExtractor;
        private bool _isConnected = false;
        private string? _inventorVersion;

        public bool IsConnected => _isConnected;
        public string? InventorVersion => _inventorVersion;

        public InventorConnectionService(
            ILogger<InventorConnectionService> logger,
            InventorBomExtractor bomExtractor)
        {
            _logger = logger;
            _bomExtractor = bomExtractor;
        }

        public async Task ConnectAsync()
        {
            try
            {
                _logger.LogInformation("Tentando conectar ao Inventor...");

                await Task.Run(() =>
                {
                    _bomExtractor.ConnectToInventor();
                });

                _inventorVersion = await GetInventorVersionAsync();
                _isConnected = !string.IsNullOrEmpty(_inventorVersion);

                if (_isConnected)
                {
                    _logger.LogInformation($"✓ Conectado ao Inventor {_inventorVersion}");
                }
                else
                {
                    _logger.LogWarning("Falha ao conectar ao Inventor - versão não detectada");
                }
            }
            catch (Exception ex)
            {
                _isConnected = false;
                _logger.LogError(ex, "Erro ao conectar com Inventor");
                throw;
            }
        }

        public async Task DisconnectAsync()
        {
            try
            {
                _isConnected = false;
                _inventorVersion = null;
                _logger.LogInformation("Desconectado do Inventor");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao desconectar do Inventor");
            }
        }

        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                var version = await GetInventorVersionAsync();
                var isValid = !string.IsNullOrEmpty(version);
                
                if (isValid != _isConnected)
                {
                    _isConnected = isValid;
                    _inventorVersion = version;
                }
                
                return isValid;
            }
            catch
            {
                _isConnected = false;
                _inventorVersion = null;
                return false;
            }
        }

        public async Task ReconnectAsync()
        {
            _logger.LogInformation("Reconectando ao Inventor...");
            
            await DisconnectAsync();
            await Task.Delay(2000); // Aguarda estabilizar
            await ConnectAsync();
        }

        // ✅ NOVO - Para Step 2 (eventos)
        public dynamic? GetInventorApp()
        {
            try
            {
                if (!_isConnected)
                {
                    _logger.LogWarning("Inventor não conectado - não é possível obter aplicação");
                    return null;
                }

                return _bomExtractor.GetInventorApp();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao obter aplicação Inventor");
                return null;
            }
        }

        private async Task<string?> GetInventorVersionAsync()
        {
            try
            {
                return await Task.Run(() => _bomExtractor.GetInventorVersion());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao obter versão do Inventor");
                return null;
            }
        }
    }
}