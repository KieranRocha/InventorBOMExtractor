// Events/DocumentEventArgs.cs
using System;
using InventorBOMExtractor.Models;

namespace InventorBOMExtractor.Events
{
    public class DocumentOpenedEventArgs : EventArgs
    {
        public string FilePath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public DocumentType DocumentType { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public long FileSizeBytes { get; set; }
    }

    public class DocumentClosedEventArgs : EventArgs
    {
        public string FilePath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public DocumentType DocumentType { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public TimeSpan SessionDuration { get; set; }
    }

    public class DocumentSavedEventArgs : EventArgs
    {
        public string FilePath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public DocumentType DocumentType { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public bool IsAutoSave { get; set; }
    }

    public class WorkSessionStartedEventArgs : EventArgs
    {
        public WorkSession WorkSession { get; set; } = new();
        public ProjectInfo? ProjectInfo { get; set; }
    }

    public class WorkSessionEndedEventArgs : EventArgs
    {
        public WorkSession WorkSession { get; set; } = new();
        public ProjectInfo? ProjectInfo { get; set; }
    }

    public class WorkSessionUpdatedEventArgs : EventArgs
    {
        public WorkSession WorkSession { get; set; } = new();
        public string UpdateReason { get; set; } = string.Empty; // "SAVE", "ACTIVITY", etc.
    }
}