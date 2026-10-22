using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CadenceCisLibraryManager.Models;

namespace CadenceCisLibraryManager.Services;

public sealed class FileLibraryService
{
    public async Task<IReadOnlyList<FileCopyResult>> CopyManyToLibraryAsync(AppSettings settings, FileColumnKind kind, IEnumerable<string> sourcePaths, Func<string, FileConflictAction> resolveConflict)
    {
        var results = new List<FileCopyResult>();
        foreach (var sourcePath in sourcePaths.Where(path => !string.IsNullOrWhiteSpace(path)))
        {
            results.Add(await CopyToLibraryAsync(settings, kind, sourcePath, resolveConflict));
        }

        return results;
    }

    public async Task<FileCopyResult> CopyToLibraryAsync(AppSettings settings, FileColumnKind kind, string sourcePath, Func<string, FileConflictAction> resolveConflict)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            return new FileCopyResult(null, true);
        }

        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException("找不到要入库的文件。", sourcePath);
        }

        var libraryPath = GetLibraryPath(settings, kind);
        if (string.IsNullOrWhiteSpace(libraryPath))
        {
            throw new InvalidOperationException($"请先设置 {GetDisplayName(kind)} 的库路径。");
        }

        Directory.CreateDirectory(libraryPath);
        var destinationPath = Path.Combine(libraryPath, Path.GetFileName(sourcePath));
        if (File.Exists(destinationPath))
        {
            var action = resolveConflict(destinationPath);
            if (action == FileConflictAction.Cancel)
            {
                throw new OperationCanceledException("用户取消了文件入库。");
            }

            if (action == FileConflictAction.Skip)
            {
                return new FileCopyResult(ToStoredValue(settings, destinationPath), true);
            }

            if (action == FileConflictAction.Rename)
            {
                destinationPath = GetAvailablePath(destinationPath);
            }

            await CopyFileAsync(sourcePath, destinationPath, action == FileConflictAction.Overwrite);
        }
        else
        {
            await CopyFileAsync(sourcePath, destinationPath, overwrite: false);
        }

        return new FileCopyResult(ToStoredValue(settings, destinationPath), false);
    }

    public async Task<string?> DownloadAndSaveDatasheetAsync(AppSettings settings, string datasheetUrl, string partNumberOrMpn)
    {
        if (string.IsNullOrWhiteSpace(datasheetUrl)) return null;

        var libraryPath = settings.DatasheetLibraryPath;
        if (string.IsNullOrWhiteSpace(libraryPath))
        {
            libraryPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "datasheets");
        }

        Directory.CreateDirectory(libraryPath);

        var safeName = Regex.Replace(partNumberOrMpn, @"[^\w\-.]", "_").Trim('_');
        if (string.IsNullOrWhiteSpace(safeName)) safeName = "datasheet_" + DateTime.Now.Ticks;
        var destinationPath = Path.Combine(libraryPath, $"{safeName}.pdf");

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

        using var resp = await client.GetAsync(datasheetUrl);
        if (!resp.IsSuccessStatusCode)
        {
            return null;
        }

        await using var fs = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await resp.Content.CopyToAsync(fs);

        return ToStoredValue(settings, destinationPath);
    }

    private static async Task CopyFileAsync(string sourcePath, string destinationPath, bool overwrite)
    {
        await using var source = File.OpenRead(sourcePath);
        await using var destination = new FileStream(destinationPath, overwrite ? FileMode.Create : FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await source.CopyToAsync(destination);
    }

    private static string GetLibraryPath(AppSettings settings, FileColumnKind kind)
    {
        return kind switch
        {
            FileColumnKind.Footprint => settings.FootprintLibraryPath,
            FileColumnKind.Symbol => settings.SymbolLibraryPath,
            FileColumnKind.Model3D => settings.Model3DLibraryPath,
            FileColumnKind.Pin => settings.PinLibraryPath,
            FileColumnKind.Datasheet => settings.DatasheetLibraryPath,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };
    }

    private static string GetDisplayName(FileColumnKind kind)
    {
        return kind switch
        {
            FileColumnKind.Footprint => "封装",
            FileColumnKind.Symbol => "符号",
            FileColumnKind.Model3D => "3D 模型",
            FileColumnKind.Pin => "焊盘/引脚",
            FileColumnKind.Datasheet => "规格书/Datasheet",
            _ => "文件"
        };
    }

    private static string ToStoredValue(AppSettings settings, string destinationPath)
    {
        return settings.StoreRelativeLibraryFileName ? Path.GetFileName(destinationPath) : destinationPath;
    }

    private static string GetAvailablePath(string path)
    {
        var directory = Path.GetDirectoryName(path) ?? string.Empty;
        var fileName = Path.GetFileNameWithoutExtension(path);
        var extension = Path.GetExtension(path);
        var index = 1;
        string candidate;
        do
        {
            candidate = Path.Combine(directory, $"{fileName}_{index}{extension}");
            index++;
        }
        while (File.Exists(candidate));

        return candidate;
    }
}
