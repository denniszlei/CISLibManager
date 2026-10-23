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

    public string DatasheetLibraryPath { get; set; } = string.Empty;

    public bool StoreRelativeLibraryFileName { get; set; } = true;

    // 默认流水号宽度 (与两位前缀组合，如 10 + 0000000001 -> 100000000001 构成长达12位的标准大厂料号)
    public int PartNumberIdWidth { get; set; } = 10;

    public List<string> PartNumberColumnNames { get; set; } = ["Part_Number", "Part Number", "PartNumber", "Part_No", "PN", "编号", "料号"];

    public List<string> FootprintColumnNames { get; set; } = ["PCB_Footprint", "PCB Footprint", "Footprint", "PCBFootprint", "封装"];

    public List<string> SymbolColumnNames { get; set; } = ["Schematic_Part", "Schematic Symbol", "Symbol", "SchSymbol", "符号", "原理图符号"];

    public List<string> Model3DColumnNames { get; set; } = ["Model_3D", "3D Model", "Model3D", "StepModel", "模型", "三维模型"];

    public List<string> DatasheetColumnNames { get; set; } = ["Datasheet", "DataSheet", "DatasheetUrl", "规格书", "器件手册", "手册"];

    // 预置 01~13 经典大厂料号前缀 (无 cis_ 前缀)
    public Dictionary<string, string> TablePartNumberPrefixes { get; set; } = new()
    {
        ["01_cpu_mcu_soc"] = "01",
        ["02_common_ic"] = "02",
        ["03_ddr"] = "03",
        ["04_misc"] = "04",
        ["05_flash_rom"] = "05",
        ["06_power_ic"] = "06",
        ["07_transistor_diode"] = "07",
        ["08_pcb"] = "08",
        ["09_inductor"] = "09",
        ["10_resistor"] = "10",
        ["11_capacitor"] = "11",
        ["12_connector"] = "12",
        ["13_mechanical"] = "13"
    };

    // DigiKey API 凭据
    public string DigiKeyClientId { get; set; } = string.Empty;

    public string DigiKeyClientSecret { get; set; } = string.Empty;

    public bool AutoEnrichWithLcsc { get; set; } = true;

    // 标准料映射规则 (涵盖 10电阻, 11电容, 09电感, 07二极管晶体管, 04杂项晶体)
    public List<StandardPartRule> StandardPartRules { get; set; } = StandardPartRule.GetDefaultRules();

    // 样品库存位置预置字典 (可在配置页维护)
    public List<string> SampleLocations { get; set; } = ["研发样品柜-A1", "研发样品柜-A2", "研发样品柜-B1", "0402专用样品盒", "0603专用样品盒", "高频/射频专柜", "屏蔽房备件盒", "默认样品柜"];

    // 高级料号生成与校验规则字典 (Key: 表名)
    public Dictionary<string, PartNumberRule> TablePartNumberRules { get; set; } = [];
}
