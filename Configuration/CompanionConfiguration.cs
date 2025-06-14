// Configuration/CompanionConfiguration.cs - STEP 2 ATUALIZADO
namespace InventorBOMExtractor.Configuration
{
    public class CompanionSettings
    {
        public string ApiBaseUrl { get; set; } = "http://localhost:5001";
        public int CycleIntervalMs { get; set; } = 30000; // 30 segundos
        public int ErrorRetryDelayMs { get; set; } = 10000; // 10 segundos
        public bool EnableDetailedLogging { get; set; } = true;
        
        // ✅ STEP 2 - Configurações de Work-Driven Monitoring
        public WorkDrivenMonitoringSettings WorkDrivenMonitoring { get; set; } = new();
        public ProjectDetectionSettings ProjectDetection { get; set; } = new();
        public DocumentProcessingSettings DocumentProcessing { get; set; } = new();
        public WorkSessionTrackingSettings WorkSessionTracking { get; set; } = new();
        public PerformanceSettings Performance { get; set; } = new();
    }

    public class WorkDrivenMonitoringSettings
    {
        public int DebounceDelayMs { get; set; } = 2000;
        public int MaxActiveWatchers { get; set; } = 50;
        public bool EnableFileStabilityCheck { get; set; } = true;
        public int FileStabilityMaxAttempts { get; set; } = 10;
        public int FileStabilityDelayMs { get; set; } = 500;
    }

    public class ProjectDetectionSettings
    {
        public bool EnableAutoDetection { get; set; } = true;
        public List<string> ProjectIdPatterns { get; set; } = new()
        {
            @".*_PROJ_(\d+)_.*",
            @"(C-\d+)_.*",
            @"([A-Z]+-\d+)_.*"
        };
        public string DefaultProjectPrefix { get; set; } = "C-";
        public string UnknownProjectHandling { get; set; } = "CREATE_UNKNOWN"; // CREATE_UNKNOWN, IGNORE, LOG_ONLY
    }

    public class DocumentProcessingSettings
    {
        public bool EnableBOMExtraction { get; set; } = true;
        public bool EnablePartDataExtraction { get; set; } = true;
        public bool EnableDrawingMonitoring { get; set; } = false;
        public bool ProcessOnlyActiveFiles { get; set; } = true;
        public bool SendActivityToAPI { get; set; } = true;
    }

    public class WorkSessionTrackingSettings
    {
        public bool EnableSessionTracking { get; set; } = true;
        public int MinSessionDurationMinutes { get; set; } = 2;
        public bool EnableProductivityAnalytics { get; set; } = true;
        public bool SendSessionUpdates { get; set; } = true;
    }

    public class PerformanceSettings
    {
        public int MaxConcurrentBOMExtractions { get; set; } = 3;
        public int BOMExtractionTimeoutSeconds { get; set; } = 120;
        public bool EnableMemoryMonitoring { get; set; } = true;
        public int MaxMemoryUsageMB { get; set; } = 1024;
    }

    // ✅ STEP 1 - Mantém compatibilidade
    public class CompanionHeartbeat
    {
        public string CompanionId { get; set; } = Environment.MachineName;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string Status { get; set; } = "RUNNING";
        public bool InventorConnected { get; set; }
        public string? InventorVersion { get; set; }
        public string? Message { get; set; }
    }
}