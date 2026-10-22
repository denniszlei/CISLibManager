namespace CadenceCisLibraryManager.Models;

public sealed class ComponentItem
{
    public string Category { get; set; } = string.Empty;

    public string TableName { get; set; } = string.Empty;

    public string PartNumber { get; set; } = string.Empty;

    public string? PartType { get; set; }

    public string? Value { get; set; }

    public string? Package { get; set; }

    public string? Description { get; set; }

    public string? SchematicPart { get; set; }

    public string? PcbFootprint { get; set; }

    public string? Model3D { get; set; }

    public string? Manufacturer { get; set; }

    public string? ManufacturerPartNumber { get; set; }

    public string? Datasheet { get; set; }

    public string? LifecycleStatus { get; set; } = "Active";

    public string? LcscPartNumber { get; set; }

    public string? Distributor { get; set; }

    public string? Note { get; set; }

    public bool HasDrawings => !string.IsNullOrWhiteSpace(SchematicPart) || !string.IsNullOrWhiteSpace(PcbFootprint);

    public string DrawingStatus => HasDrawings ? "✅ 完备" : "⚠️ 待补图档";
}
