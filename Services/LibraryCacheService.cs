using System.IO;
using CadenceCisLibraryManager.Models;

namespace CadenceCisLibraryManager.Services;

public sealed record LibraryScanSummary(
    int FootprintCount,
    int Model3DCount,
    int SymbolCount,
    TimeSpan Elapsed,
    string? ErrorMessage = null
);

public sealed class LibraryCacheService
{
    private readonly List<string> _footprints = [];
    private readonly HashSet<string> _footprintSet = new(StringComparer.OrdinalIgnoreCase);

    private readonly List<string> _model3ds = [];
    private readonly HashSet<string> _model3dSet = new(StringComparer.OrdinalIgnoreCase);

    private readonly List<string> _symbols = [];
    private readonly HashSet<string> _symbolSet = new(StringComparer.OrdinalIgnoreCase);

    public event EventHandler? CacheRefreshed;

    public IReadOnlyList<string> Footprints
    {
        get
        {
            lock (_footprints) return [.. _footprints];
        }
    }

    public IReadOnlyList<string> Model3Ds
    {
        get
        {
            lock (_model3ds) return [.. _model3ds];
        }
    }

    public IReadOnlyList<string> Symbols
    {
        get
        {
            lock (_symbols) return [.. _symbols];
        }
    }

    public async Task<LibraryScanSummary> RefreshAsync(AppSettings settings)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        return await Task.Run(() =>
        {
            try
            {
                var fpList = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var m3dList = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var symList = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                // 1. 扫描封装库 (dra, psm)
                if (!string.IsNullOrWhiteSpace(settings.FootprintLibraryPath) && Directory.Exists(settings.FootprintLibraryPath))
                {
                    foreach (var file in Directory.EnumerateFiles(settings.FootprintLibraryPath, "*.*", SearchOption.TopDirectoryOnly))
                    {
                        var ext = Path.GetExtension(file);
                        if (string.Equals(ext, ".dra", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(ext, ".psm", StringComparison.OrdinalIgnoreCase))
                        {
                            var name = Path.GetFileNameWithoutExtension(file);
                            if (!string.IsNullOrWhiteSpace(name))
                            {
                                fpList.Add(name);
                            }
                        }
                    }
                }

                // 2. 扫描 3D 模型库 (step, stp)
                if (!string.IsNullOrWhiteSpace(settings.Model3DLibraryPath) && Directory.Exists(settings.Model3DLibraryPath))
                {
                    foreach (var file in Directory.EnumerateFiles(settings.Model3DLibraryPath, "*.*", SearchOption.TopDirectoryOnly))
                    {
                        var ext = Path.GetExtension(file);
                        if (string.Equals(ext, ".step", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(ext, ".stp", StringComparison.OrdinalIgnoreCase))
                        {
                            var name = Path.GetFileName(file);
                            if (!string.IsNullOrWhiteSpace(name))
                            {
                                m3dList.Add(name);
                            }
                        }
                    }
                }

                // 3. 扫描符号库 (olb)
                if (!string.IsNullOrWhiteSpace(settings.SymbolLibraryPath) && Directory.Exists(settings.SymbolLibraryPath))
                {
                    foreach (var file in Directory.EnumerateFiles(settings.SymbolLibraryPath, "*.olb", SearchOption.TopDirectoryOnly))
                    {
                        var name = Path.GetFileNameWithoutExtension(file);
                        if (!string.IsNullOrWhiteSpace(name))
                        {
                            symList.Add(name);
                        }
                    }
                }

                lock (_footprints)
                {
                    _footprints.Clear();
                    _footprintSet.Clear();
                    foreach (var item in fpList.OrderBy(x => x))
                    {
                        _footprints.Add(item);
                        _footprintSet.Add(item);
                    }
                }

                lock (_model3ds)
                {
                    _model3ds.Clear();
                    _model3dSet.Clear();
                    foreach (var item in m3dList.OrderBy(x => x))
                    {
                        _model3ds.Add(item);
                        _model3dSet.Add(item);
                    }
                }

                lock (_symbols)
                {
                    _symbols.Clear();
                    _symbolSet.Clear();
                    foreach (var item in symList.OrderBy(x => x))
                    {
                        _symbols.Add(item);
                        _symbolSet.Add(item);
                    }
                }

                sw.Stop();
                var summary = new LibraryScanSummary(fpList.Count, m3dList.Count, symList.Count, sw.Elapsed);
                CacheRefreshed?.Invoke(this, EventArgs.Empty);
                return summary;
            }
            catch (Exception ex)
            {
                sw.Stop();
                return new LibraryScanSummary(0, 0, 0, sw.Elapsed, ex.Message);
            }
        });
    }

    public bool IsFootprintExists(string? footprintName)
    {
        if (string.IsNullOrWhiteSpace(footprintName)) return false;
        var clean = CleanName(footprintName);
        lock (_footprints)
        {
            return _footprintSet.Contains(clean);
        }
    }

    public bool IsModel3DExists(string? model3dName)
    {
        if (string.IsNullOrWhiteSpace(model3dName)) return false;
        var clean = model3dName.Trim();
        lock (_model3ds)
        {
            if (_model3dSet.Contains(clean)) return true;
            // 如果传入没带 .step，自动补全比对
            if (!clean.EndsWith(".step", StringComparison.OrdinalIgnoreCase) &&
                !clean.EndsWith(".stp", StringComparison.OrdinalIgnoreCase))
            {
                return _model3dSet.Contains(clean + ".step") || _model3dSet.Contains(clean + ".stp");
            }
            return false;
        }
    }

    public string FindBestFootprintMatch(string? prefix, string packageCode)
    {
        var cleanPkg = packageCode.Trim();
        var cleanPrefix = prefix?.Trim() ?? string.Empty;

        // 候选 1: prefix + packageCode (如 R0402)
        var c1 = cleanPrefix + cleanPkg;
        // 候选 2: prefix + "_" + packageCode (如 R_0402)
        var c2 = cleanPrefix + "_" + cleanPkg;
        // 候选 3: packageCode (如 0402)
        var c3 = cleanPkg;

        lock (_footprints)
        {
            var match1 = _footprints.FirstOrDefault(f => string.Equals(f, c1, StringComparison.OrdinalIgnoreCase));
            if (match1 != null) return match1;

            if (!string.IsNullOrWhiteSpace(cleanPrefix))
            {
                var match2 = _footprints.FirstOrDefault(f => string.Equals(f, c2, StringComparison.OrdinalIgnoreCase));
                if (match2 != null) return match2;
            }

            var match3 = _footprints.FirstOrDefault(f => string.Equals(f, c3, StringComparison.OrdinalIgnoreCase));
            if (match3 != null) return match3;

            // 模糊前缀匹配 (如以 c1 开头)
            var matchPrefix = _footprints.FirstOrDefault(f => f.StartsWith(c1, StringComparison.OrdinalIgnoreCase));
            if (matchPrefix != null) return matchPrefix;
        }

        // 未在物理库找到，默认拼接（不卡死）
        return c1.ToUpperInvariant();
    }

    public IReadOnlyList<string> SearchFootprints(string? query, int limit = 50)
    {
        lock (_footprints)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return _footprints.Take(limit).ToList();
            }

            var q = query.Trim();
            var exact = _footprints.Where(f => string.Equals(f, q, StringComparison.OrdinalIgnoreCase));
            var starts = _footprints.Where(f => f.StartsWith(q, StringComparison.OrdinalIgnoreCase) && !string.Equals(f, q, StringComparison.OrdinalIgnoreCase));
            var contains = _footprints.Where(f => f.Contains(q, StringComparison.OrdinalIgnoreCase) && !f.StartsWith(q, StringComparison.OrdinalIgnoreCase));

            return exact.Concat(starts).Concat(contains).Take(limit).ToList();
        }
    }

    private static string CleanName(string raw)
    {
        var name = raw.Trim();
        if (name.EndsWith(".dra", StringComparison.OrdinalIgnoreCase) ||
            name.EndsWith(".psm", StringComparison.OrdinalIgnoreCase))
        {
            name = Path.GetFileNameWithoutExtension(name);
        }
        return name;
    }
}
