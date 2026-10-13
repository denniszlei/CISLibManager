namespace CadenceCisLibraryManager.Models;

public sealed class PartNumberRule
{
    public string TableName { get; set; } = string.Empty;

    public string PatternTemplate { get; set; } = "{PREFIX}-{SEQ:5}";

    public string? ValidationRegex { get; set; }

    public string? RegexErrorMessage { get; set; }
}
