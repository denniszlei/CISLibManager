using System.IO;
using System.IO.Compression;
using CadenceCisLibraryManager.Models;

namespace CadenceCisLibraryManager.Services;

public sealed class ArchiveExtractionResult
{
    public string TempDirectory { get; init; } = string.Empty;

    public List<string> SymbolFiles { get; init; } = [];

    public List<string> FootprintFiles { get; init; } = [];

    public List<string> Model3DFiles { get; init; } = [];

    public List<string> PinFiles { get; init; } = [];

    public string? SuggestedPartName { get; init; }

    public string SummaryText =>
        $"识别完成：符号 {SymbolFiles.Count} 个，封装 {FootprintFiles.Count} 个，3D 模型 {Model3DFiles.Count} 个，焊盘 {PinFiles.Count} 个";
}

public sealed class ArchiveExtractorService
{
    private static readonly HashSet<string> SymbolExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".olb"
    };

    private static readonly HashSet<string> FootprintExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".psm", ".dra"
    };

    private static readonly HashSet<string> Model3DExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".step", ".stp"
    };

    private static readonly HashSet<string> PinExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pad", ".osm", ".bsm", ".fsm", ".ssm"
    };

    public async Task<ArchiveExtractionResult> ExtractZipAsync(string zipFilePath)
    {
        if (!File.Exists(zipFilePath))
        {
            throw new FileNotFoundException("找不到指定的压缩包。", zipFilePath);
        }

        var tempDir = Path.Combine(Path.GetTempPath(), "CadenceCisLibraryManager", "Intake_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        await Task.Run(() => ZipFile.ExtractToDirectory(zipFilePath, tempDir, overwriteFiles: true));

        var symbolFiles = new List<string>();
        var footprintFiles = new List<string>();
        var model3DFiles = new List<string>();
        var pinFiles = new List<string>();

        foreach (var file in Directory.EnumerateFiles(tempDir, "*.*", SearchOption.AllDirectories))
        {
            var ext = Path.GetExtension(file);
            if (SymbolExtensions.Contains(ext))
            {
                symbolFiles.Add(file);
            }
            else if (FootprintExtensions.Contains(ext))
            {
                footprintFiles.Add(file);
            }
            else if (Model3DExtensions.Contains(ext))
            {
                model3DFiles.Add(file);
            }
            else if (PinExtensions.Contains(ext))
            {
                pinFiles.Add(file);
            }
        }

        string? suggestedPartName = null;
        var psmFile = footprintFiles.FirstOrDefault(f => string.Equals(Path.GetExtension(f), ".psm", StringComparison.OrdinalIgnoreCase));
        if (psmFile != null)
        {
            suggestedPartName = Path.GetFileNameWithoutExtension(psmFile);
        }
        else if (symbolFiles.Count > 0)
        {
            suggestedPartName = Path.GetFileNameWithoutExtension(symbolFiles[0]);
        }

        return new ArchiveExtractionResult
        {
            TempDirectory = tempDir,
            SymbolFiles = symbolFiles,
            FootprintFiles = footprintFiles,
            Model3DFiles = model3DFiles,
            PinFiles = pinFiles,
            SuggestedPartName = suggestedPartName
        };
    }
}
