namespace CadenceCisLibraryManager.Models;

public sealed class StandardPartRule
{
    public string TableName { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string SymbolTemplate { get; set; } = string.Empty;

    public string FootprintPrefix { get; set; } = string.Empty;

    public string Model3DPrefix { get; set; } = string.Empty;

    public string DefaultReference { get; set; } = string.Empty;

    public List<string> SupportedPackages { get; set; } = ["0201", "0402", "0603", "0805", "1206", "1210", "2512"];

    public static List<StandardPartRule> GetDefaultRules() =>
    [
        new StandardPartRule
        {
            TableName = "resistors",
            DisplayName = "贴片电阻",
            SymbolTemplate = @"DISCRETE\RES",
            FootprintPrefix = "R",
            Model3DPrefix = "R",
            DefaultReference = "R"
        },
        new StandardPartRule
        {
            TableName = "capacitors",
            DisplayName = "贴片陶瓷电容",
            SymbolTemplate = @"DISCRETE\CAP",
            FootprintPrefix = "C",
            Model3DPrefix = "C",
            DefaultReference = "C"
        },
        new StandardPartRule
        {
            TableName = "inductors",
            DisplayName = "贴片电感",
            SymbolTemplate = @"DISCRETE\IND",
            FootprintPrefix = "L",
            Model3DPrefix = "L",
            DefaultReference = "L"
        }
    ];
}
