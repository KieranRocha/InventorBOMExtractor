// Models/BOMModels.cs
using System.Text.Json.Serialization;

namespace CADCompanion.Models
{
    public class BOMItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string PartNumber { get; set; } = "";
        public string Description { get; set; } = "";
        public int Quantity { get; set; }
        public string Unit { get; set; } = "UN";
        public string? Material { get; set; }
        public double? Weight { get; set; }
        public decimal? Cost { get; set; }
        public string? Supplier { get; set; }
        public int? LeadTime { get; set; }
        public int Level { get; set; }
        public string? ParentId { get; set; }
        public List<BOMItem> Children { get; set; } = new();
        public string? FilePath { get; set; }
        public bool IsAssembly { get; set; }
        public string? Thumbnail { get; set; }
    }

    public class ExtractBOMRequest
    {
        public string FilePath { get; set; } = "";
    }

    public class BOMExtractionResult
    {
        public bool Success { get; set; }
        public List<BOMItem>? BomData { get; set; }
        public string? Error { get; set; }
        public double? ProcessingTime { get; set; }
    }

    public class OpenFileRequest
    {
        public string FilePath { get; set; } = "";
    }
}