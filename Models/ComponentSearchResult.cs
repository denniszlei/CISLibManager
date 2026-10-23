namespace CadenceCisLibraryManager.Models;

public sealed class ComponentSearchResult
{
    public string Source { get; set; } = "DigiKey";

    public string ManufacturerProductNumber { get; set; } = string.Empty;

    public string Manufacturer { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string? DetailedDescription { get; set; }

    public string? Category { get; set; }

    public string? Package { get; set; }

    public string? DatasheetUrl { get; set; }

    public string? AlternativeDatasheetUrl { get; set; }

    public string? PhotoUrl { get; set; }

    public string LifecycleStatus { get; set; } = "Active";

    public bool IsObsolete => string.Equals(LifecycleStatus, "Obsolete", StringComparison.OrdinalIgnoreCase)
                           || string.Equals(LifecycleStatus, "Discontinued", StringComparison.OrdinalIgnoreCase);

    public long? Stock { get; set; }

    public string? Price { get; set; }

    public string? LcscPartNumber { get; set; }

    public string? Model3DUuid { get; set; }

    public Dictionary<string, string> Parameters { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public string DisplayStatus
    {
        get
        {
            if (string.IsNullOrWhiteSpace(LifecycleStatus)) return "Unknown";
            return LifecycleStatus;
        }
    }
}
