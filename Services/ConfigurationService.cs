using Microsoft.Extensions.Logging;
using InventorBOMExtractor.Configuration;

namespace InventorBOMExtractor.Services
{
    public interface IConfigurationService
    {
        Task RefreshProjectConfigurationsAsync();
        List<string> GetActiveProjectIds();
        List<ProjectConfiguration> GetActiveProjects();
    }

    public class ConfigurationService : IConfigurationService
    {
        private readonly ILogger<ConfigurationService> _logger;
        private List<ProjectConfiguration> _activeProjects = new();

        public ConfigurationService(ILogger<ConfigurationService> logger)
        {
            _logger = logger;
        }

        public async Task RefreshProjectConfigurationsAsync()
        {
            // TODO: Implementar no Step 2
            _logger.LogDebug("RefreshProjectConfigurations - TODO Step 2");
            await Task.CompletedTask;
        }

        public List<string> GetActiveProjectIds()
        {
            return _activeProjects.Select(p => p.Id).ToList();
        }

        public List<ProjectConfiguration> GetActiveProjects()
        {
            return _activeProjects.ToList();
        }
    }
}