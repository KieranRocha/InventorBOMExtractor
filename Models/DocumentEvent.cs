// Models/DocumentEvent.cs - CORRIGIDO
using System;

namespace InventorBOMExtractor.Models
{
    public enum DocumentEventType
    {
        Opened,
        Closed,
        Saved,
        Modified
    }

    public enum DocumentType
    {
        Assembly,      // .iam
        Part,          // .ipt  
        Drawing,       // .idw
        Presentation,  // .ipn
        Unknown
    }

    public class DocumentEvent
    {
        public string FilePath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public DocumentEventType EventType { get; set; }
        public DocumentType DocumentType { get; set; }
        public DateTime Timestamp { get; set; }
        public string? ProjectId { get; set; }
        public string? ProjectName { get; set; }
        public string? Engineer { get; set; }
        public long FileSizeBytes { get; set; }
        public string? InventorVersion { get; set; }
    }

    public class ProjectInfo
    {
        public string ProjectId { get; set; } = string.Empty;
        public string DetectedName { get; set; } = string.Empty;
        public string FolderPath { get; set; } = string.Empty;
        public string Phase { get; set; } = string.Empty;
        public bool IsValidProject { get; set; }
        public string? Client { get; set; }
        public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
    }

    public class WorkSession
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string FilePath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string? ProjectId { get; set; }
        public string? ProjectName { get; set; }
        public string? Engineer { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public TimeSpan? Duration { get; set; }
        public int SaveCount { get; set; }
        public DateTime? LastSave { get; set; }
        public bool IsActive { get; set; } = true;
        public string CompanionId { get; set; } = Environment.MachineName;
    }

    public class DocumentWatcher : IDisposable
    {
        public string FilePath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public ProjectInfo? ProjectInfo { get; set; }
        public FileSystemWatcher? FileWatcher { get; set; }
        public DateTime OpenedAt { get; set; }
        public DateTime LastActivity { get; set; }
        public int SaveCount { get; set; }
        public string WorkSessionId { get; set; } = string.Empty;

        public void Dispose()
        {
            if (FileWatcher != null)
            {
                FileWatcher.EnableRaisingEvents = false;
                FileWatcher.Dispose();
                FileWatcher = null;
            }
        }
    }

    public class BOMDataWithContext
    {
        public string ProjectId { get; set; } = string.Empty;
        public string ProjectName { get; set; } = string.Empty;
        public string AssemblyFileName { get; set; } = string.Empty;
        public string AssemblyFilePath { get; set; } = string.Empty;
        public DateTime ExtractedAt { get; set; }
        public string ExtractedBy { get; set; } = Environment.MachineName;
        public string? WorkSessionId { get; set; }
        public string? Engineer { get; set; }
        public List<BomItem> BOMItems { get; set; } = new();
        
        // Metadata agregado - ✅ FIX: Corrigido tipos double
        public int TotalItems => BOMItems.Count;
        public double TotalMass => BOMItems.Sum(b => 
        {
            // Converte Quantity para double e Mass para double
            var quantity = Convert.ToDouble(b.Quantity);
            var mass = b.Mass;
            return quantity * mass;
        });
        
        public double TotalVolume => BOMItems.Sum(b => 
        {
            // Converte Quantity para double e Volume para double
            var quantity = Convert.ToDouble(b.Quantity);
            var volume = b.Volume;
            return quantity * volume;
        });
        
        public string InventorVersion { get; set; } = string.Empty;
    }
}