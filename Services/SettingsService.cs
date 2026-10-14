using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CadenceCisLibraryManager.Models;

namespace CadenceCisLibraryManager.Services;

public sealed record SettingsLoadResult(AppSettings Settings, bool PasswordDecryptionFailed);

public sealed class SettingsService
{
    private readonly string _settingsPath;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public SettingsService()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var directory = Path.Combine(appDataPath, "CadenceCisLibraryManager");
        Directory.CreateDirectory(directory);
        _settingsPath = Path.Combine(directory, "settings.json");
    }

    public async Task<AppSettings> LoadAsync()
    {
        return (await LoadWithStatusAsync()).Settings;
    }

    public async Task<SettingsLoadResult> LoadWithStatusAsync()
    {
        if (!File.Exists(_settingsPath))
        {
            return new SettingsLoadResult(new AppSettings(), false);
        }

        await using var stream = File.OpenRead(_settingsPath);
        var persisted = await JsonSerializer.DeserializeAsync<PersistedAppSettings>(stream) ?? new PersistedAppSettings();
        var (settings, passwordDecryptionFailed) = ToAppSettings(persisted);
        return new SettingsLoadResult(settings, passwordDecryptionFailed);
    }

    public async Task SaveAsync(AppSettings settings)
    {
        var persisted = FromAppSettings(settings);
        await using var stream = File.Create(_settingsPath);
        await JsonSerializer.SerializeAsync(stream, persisted, JsonOptions);
    }

    private static (AppSettings Settings, bool PasswordDecryptionFailed) ToAppSettings(PersistedAppSettings persisted)
    {
        var decryptedPassword = DecryptSecret(persisted.EncryptedPassword);
        var passwordDecryptionFailed = !string.IsNullOrWhiteSpace(persisted.EncryptedPassword) && decryptedPassword is null;
        var password = !string.IsNullOrWhiteSpace(persisted.EncryptedPassword)
            ? decryptedPassword ?? string.Empty
            : persisted.Password;

        var decryptedDigiKeySecret = DecryptSecret(persisted.EncryptedDigiKeyClientSecret);
        var digiKeySecret = !string.IsNullOrWhiteSpace(persisted.EncryptedDigiKeyClientSecret)
            ? decryptedDigiKeySecret ?? string.Empty
            : persisted.DigiKeyClientSecret;

        return (new AppSettings
        {
            Server = persisted.Server,
            Port = persisted.Port,
            Database = persisted.Database,
            UserId = persisted.UserId,
            Password = password,
            FootprintLibraryPath = persisted.FootprintLibraryPath,
            SymbolLibraryPath = persisted.SymbolLibraryPath,
            Model3DLibraryPath = persisted.Model3DLibraryPath,
            PinLibraryPath = persisted.PinLibraryPath,
            StoreRelativeLibraryFileName = persisted.StoreRelativeLibraryFileName,
            PartNumberIdWidth = persisted.PartNumberIdWidth > 0 ? persisted.PartNumberIdWidth : 10,
            PartNumberColumnNames = persisted.PartNumberColumnNames,
            FootprintColumnNames = persisted.FootprintColumnNames,
            SymbolColumnNames = persisted.SymbolColumnNames,
            Model3DColumnNames = persisted.Model3DColumnNames,
            TablePartNumberPrefixes = SanitizeOrMigrateTablePrefixes(persisted.TablePartNumberPrefixes),
            DigiKeyClientId = persisted.DigiKeyClientId,
            DigiKeyClientSecret = digiKeySecret,
            AutoEnrichWithLcsc = persisted.AutoEnrichWithLcsc,
            StandardPartRules = SanitizeOrMigrateStandardRules(persisted.StandardPartRules),
            TablePartNumberRules = persisted.TablePartNumberRules
        }, passwordDecryptionFailed);
    }

    private static List<StandardPartRule> SanitizeOrMigrateStandardRules(List<StandardPartRule>? loadedRules)
    {
        if (loadedRules == null || loadedRules.Count == 0)
        {
            return StandardPartRule.GetDefaultRules();
        }

        // 检查旧版配置：若包含旧表名(cis_前缀)或者未覆盖07(晶体管二极管)/04(晶振)，则自动更新为最新5大类标准件规范
        var hasLegacyCis = loadedRules.Any(r => r.TableName.StartsWith("cis_", StringComparison.OrdinalIgnoreCase));
        var hasTransistor = loadedRules.Any(r => r.TableName.Contains("07") || r.TableName.Contains("transistor") || r.TableName.Contains("diode"));
        var hasMisc = loadedRules.Any(r => r.TableName.Contains("04") || r.TableName.Contains("misc"));

        if (hasLegacyCis || !hasTransistor || !hasMisc)
        {
            return StandardPartRule.GetDefaultRules();
        }

        return loadedRules;
    }

    private static Dictionary<string, string> SanitizeOrMigrateTablePrefixes(Dictionary<string, string>? loadedPrefixes)
    {
        var defaultPrefixes = new AppSettings().TablePartNumberPrefixes;
        if (loadedPrefixes == null || loadedPrefixes.Count == 0)
        {
            return defaultPrefixes;
        }

        // 若发现旧配置中全是 cis_ 前缀，或缺少 01~13 规范，则迁移并采用新前缀体系
        if (loadedPrefixes.Keys.Any(k => k.StartsWith("cis_", StringComparison.OrdinalIgnoreCase)) ||
            !loadedPrefixes.Keys.Any(k => k.StartsWith("01_") || k.StartsWith("10_")))
        {
            return defaultPrefixes;
        }

        return loadedPrefixes;
    }

    private static PersistedAppSettings FromAppSettings(AppSettings settings)
    {
        return new PersistedAppSettings
        {
            Server = settings.Server,
            Port = settings.Port,
            Database = settings.Database,
            UserId = settings.UserId,
            Password = string.Empty,
            EncryptedPassword = EncryptSecret(settings.Password),
            FootprintLibraryPath = settings.FootprintLibraryPath,
            SymbolLibraryPath = settings.SymbolLibraryPath,
            Model3DLibraryPath = settings.Model3DLibraryPath,
            PinLibraryPath = settings.PinLibraryPath,
            StoreRelativeLibraryFileName = settings.StoreRelativeLibraryFileName,
            PartNumberIdWidth = settings.PartNumberIdWidth,
            PartNumberColumnNames = [.. settings.PartNumberColumnNames],
            FootprintColumnNames = [.. settings.FootprintColumnNames],
            SymbolColumnNames = [.. settings.SymbolColumnNames],
            Model3DColumnNames = [.. settings.Model3DColumnNames],
            TablePartNumberPrefixes = settings.TablePartNumberPrefixes.ToDictionary(),
            DigiKeyClientId = settings.DigiKeyClientId,
            DigiKeyClientSecret = string.Empty,
            EncryptedDigiKeyClientSecret = EncryptSecret(settings.DigiKeyClientSecret),
            AutoEnrichWithLcsc = settings.AutoEnrichWithLcsc,
            StandardPartRules = [.. settings.StandardPartRules],
            TablePartNumberRules = settings.TablePartNumberRules.ToDictionary()
        };
    }

    private static string EncryptSecret(string? plainText)
    {
        if (string.IsNullOrEmpty(plainText))
        {
            return string.Empty;
        }

        try
        {
            var plainBytes = Encoding.UTF8.GetBytes(plainText);
            var cipherBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(cipherBytes);
        }
        catch
        {
            return plainText;
        }
    }

    private static string? DecryptSecret(string? encryptedText)
    {
        if (string.IsNullOrWhiteSpace(encryptedText))
        {
            return null;
        }

        try
        {
            var cipherBytes = Convert.FromBase64String(encryptedText);
            var plainBytes = ProtectedData.Unprotect(cipherBytes, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch
        {
            return encryptedText;
        }
    }

    private sealed class PersistedAppSettings
    {
        public string Server { get; set; } = "localhost";

        public uint Port { get; set; } = 3306;

        public string Database { get; set; } = string.Empty;

        public string UserId { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;

        public string EncryptedPassword { get; set; } = string.Empty;

        public string FootprintLibraryPath { get; set; } = string.Empty;

        public string SymbolLibraryPath { get; set; } = string.Empty;

        public string Model3DLibraryPath { get; set; } = string.Empty;

        public string PinLibraryPath { get; set; } = string.Empty;

        public bool StoreRelativeLibraryFileName { get; set; } = true;

        public int PartNumberIdWidth { get; set; } = 10;

        public List<string> PartNumberColumnNames { get; set; } = ["Part_Number", "Part Number", "PartNumber", "Part_No", "PN", "编号", "料号"];

        public List<string> FootprintColumnNames { get; set; } = ["PCB_Footprint", "PCB Footprint", "Footprint", "Package", "封装"];

        public List<string> SymbolColumnNames { get; set; } = ["Schematic_Part", "Schematic Symbol", "Symbol", "SchSymbol", "符号", "原理图符号"];

        public List<string> Model3DColumnNames { get; set; } = ["Model_3D", "3D Model", "Model3D", "StepModel", "模型", "三维模型"];

        public Dictionary<string, string> TablePartNumberPrefixes { get; set; } = [];

        public string DigiKeyClientId { get; set; } = string.Empty;

        public string DigiKeyClientSecret { get; set; } = string.Empty;

        public string EncryptedDigiKeyClientSecret { get; set; } = string.Empty;

        public bool AutoEnrichWithLcsc { get; set; } = true;

        public List<StandardPartRule> StandardPartRules { get; set; } = [];

        public Dictionary<string, PartNumberRule> TablePartNumberRules { get; set; } = [];
    }
}
