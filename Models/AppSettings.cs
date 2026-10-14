namespace CadenceCisLibraryManager.Models;

public sealed class AppSettings
{
    public string Server { get; set; } = "localhost";

    public uint Port { get; set; } = 3306;

    public string Database { get; set; } = "cadence_cis_db";

    public string UserId { get; set; } = "root";

    public string Password { get; set; } = string.Empty;

    public string FootprintLibraryPath { get; set; } = string.Empty;

    public string SymbolLibraryPath { get; set; } = string.Empty;

    public string Model3DLibraryPath { get; set; } = string.Empty;

    public string PinLibraryPath { get; set; } = string.Empty;

    public bool StoreRelativeLibraryFileName { get; set; } = true;

    // 默认流水号宽度 (与两位前缀组合，如 10 + 0000000001 -> 100000000001 构成长达12位的标准大厂料号)
    public int PartNumberIdWidth { get; set; } = 10;

    public List<string> PartNumberColumnNames { get; set; } = ["Part_Number", "Part Number", "PartNumber", "Part_No", "PN", "编号", "料号"];

    public List<string> FootprintColumnNames { get; set; } = ["PCB_Footprint", "PCB Footprint", "Footprint", "Package", "封装"];

    public List<string> SymbolColumnNames { get; set; } = ["Schematic_Part", "Schematic Symbol", "Symbol", "SchSymbol", "符号", "原理图符号"];

    public List<string> Model3DColumnNames { get; set; } = ["Model_3D", "3D Model", "Model3D", "StepModel", "模型", "三维模型"];

    // 预置 01~13 经典大厂料号前缀
    public Dictionary<string, string> TablePartNumberPrefixes { get; set; } = new()
    {
        ["cis_01_cpu_mcu_soc"] = "01",
        ["cis_02_common_ic"] = "02",
        ["cis_03_ddr"] = "03",
        ["cis_04_misc"] = "04",
        ["cis_05_flash_rom"] = "05",
        ["cis_06_power_ic"] = "06",
        ["cis_07_transistor_diode"] = "07",
        ["cis_08_pcb"] = "08",
        ["cis_09_inductor"] = "09",
        ["cis_10_resistor"] = "10",
        ["cis_11_capacitor"] = "11",
        ["cis_12_connector"] = "12",
        ["cis_13_mechanical"] = "13"
    };

    // DigiKey API 凭据
    public string DigiKeyClientId { get; set; } = string.Empty;

    public string DigiKeyClientSecret { get; set; } = string.Empty;

    public bool AutoEnrichWithLcsc { get; set; } = true;

    // 标准料映射规则 (10电阻, 11电容, 09电感)
    public List<StandardPartRule> StandardPartRules { get; set; } = StandardPartRule.GetDefaultRules();

    // 高级料号生成与校验规则字典 (Key: 表名)
    public Dictionary<string, PartNumberRule> TablePartNumberRules { get; set; } = [];
}
