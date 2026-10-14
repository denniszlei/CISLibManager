namespace CadenceCisLibraryManager.Models;

public sealed class StandardPartRule
{
    public string TableName { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string SymbolTemplate { get; set; } = string.Empty;

    public string FootprintPrefix { get; set; } = string.Empty;

    public string Model3DPrefix { get; set; } = string.Empty;

    public string DefaultReference { get; set; } = string.Empty;

    public List<string> SupportedPackages { get; set; } = [];

    public static List<StandardPartRule> GetDefaultRules() =>
    [
        new StandardPartRule
        {
            TableName = "10_resistor",
            DisplayName = "10 Resistor (电阻)",
            SymbolTemplate = @"DISCRETE\RES",
            FootprintPrefix = "R",
            Model3DPrefix = "R",
            DefaultReference = "R",
            SupportedPackages = ["0201", "0402", "0603", "0805", "1206", "1210", "2010", "2512", "0402X4", "0603X4"]
        },
        new StandardPartRule
        {
            TableName = "11_capacitor",
            DisplayName = "11 Capacitor (电容)",
            SymbolTemplate = @"DISCRETE\CAP",
            FootprintPrefix = "C",
            Model3DPrefix = "C",
            DefaultReference = "C",
            SupportedPackages = ["0201", "0402", "0603", "0805", "1206", "1210", "1812", "2220", "3216", "3528", "6032", "7343"]
        },
        new StandardPartRule
        {
            TableName = "09_inductor",
            DisplayName = "09 Inductor (电感/磁珠)",
            SymbolTemplate = @"DISCRETE\IND",
            FootprintPrefix = "L",
            Model3DPrefix = "L",
            DefaultReference = "L",
            SupportedPackages = ["0201", "0402", "0603", "0805", "1206", "4040", "5050", "6060"]
        },
        new StandardPartRule
        {
            TableName = "07_transistor_diode",
            DisplayName = "07 Transistor/Diode (二极管/晶体管)",
            SymbolTemplate = @"DISCRETE\DIODE",
            FootprintPrefix = "",
            Model3DPrefix = "",
            DefaultReference = "D",
            SupportedPackages = ["SOD123", "SOD323", "SOD523", "SMA", "SMB", "SMC", "SOT23", "LED0402", "LED0603", "LED0805"]
        },
        new StandardPartRule
        {
            TableName = "04_misc",
            DisplayName = "04 Misc (无源晶体等)",
            SymbolTemplate = @"DISCRETE\XTAL",
            FootprintPrefix = "XTAL_",
            Model3DPrefix = "XTAL_",
            DefaultReference = "Y",
            SupportedPackages = ["3215", "3225", "2016", "5032"]
        }
    ];
}
