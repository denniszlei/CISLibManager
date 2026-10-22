using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CadenceCisLibraryManager.Models;

namespace CadenceCisLibraryManager.Services;

public sealed class EasyedaApiService
{
    private static readonly HttpClient HttpClient = new(new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        ConnectTimeout = TimeSpan.FromSeconds(10)
    })
    {
        Timeout = TimeSpan.FromSeconds(20)
    };

    public async Task<IReadOnlyList<ComponentSearchResult>> SearchAsync(string keyword, int limit = 15)
    {
        var trimmed = keyword.Trim();
        if (string.IsNullOrWhiteSpace(trimmed)) return [];

        // 优先使用国内高可用 lceda.cn 官方接口（国内用户直连，无地域拦截与 Cloudflare 拦截）
        try
        {
            var cnResults = await SearchLcedaAsync("lceda.cn", trimmed, limit);
            if (cnResults.Count > 0) return cnResults;
        }
        catch
        {
            // 继续尝试备用通道
        }

        // 备用通道 1: EasyEDA 全球站接口
        try
        {
            var globalResults = await SearchLcedaAsync("easyeda.com", trimmed, limit);
            if (globalResults.Count > 0) return globalResults;
        }
        catch
        {
            // 继续尝试备用通道
        }

        // 备用通道 2: JLCPCB SMT 检索接口
        try
        {
            var jlcResults = await SearchJlcpcbLegacyAsync(trimmed, limit);
            if (jlcResults.Count > 0) return jlcResults;
        }
        catch
        {
            // 全部通道均未返回
        }

        return [];
    }

    private static async Task<IReadOnlyList<ComponentSearchResult>> SearchLcedaAsync(string host, string keyword, int limit)
    {
        var url = $"https://{host}/api/eda/product/search?keyword={Uri.EscapeDataString(keyword)}&page=1";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0.0.0 Safari/537.36");
        request.Headers.Add("Referer", $"https://{host}/editor");
        request.Headers.Add("Accept", "application/json, text/plain, */*");

        using var response = await HttpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode) return [];

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var doc = await JsonDocument.ParseAsync(stream);

        var results = new List<ComponentSearchResult>();
        if (!doc.RootElement.TryGetProperty("result", out var resultElem) ||
            !resultElem.TryGetProperty("productList", out var listElem) ||
            listElem.ValueKind != JsonValueKind.Array)
        {
            return results;
        }

        foreach (var item in listElem.EnumerateArray())
        {
            if (results.Count >= limit) break;

            var code = item.TryGetProperty("code", out var cd) ? cd.GetString() : null;
            var model = item.TryGetProperty("model", out var md) ? md.GetString() : null;
            var brand = item.TryGetProperty("brandName", out var bd) ? bd.GetString() : null;
            var name = item.TryGetProperty("name", out var nm) ? nm.GetString() : null;
            var standard = item.TryGetProperty("standard", out var sd) ? sd.GetString() : null;
            
            long stock = 0;
            if (item.TryGetProperty("stockNumber", out var sc))
            {
                if (sc.ValueKind == JsonValueKind.Number) stock = sc.GetInt64();
                else if (sc.ValueKind == JsonValueKind.String && long.TryParse(sc.GetString(), out var sVal)) stock = sVal;
            }

            string? price = null;
            if (item.TryGetProperty("priceList", out var pList) && pList.ValueKind == JsonValueKind.Array)
            {
                foreach (var p in pList.EnumerateArray())
                {
                    if (p.TryGetProperty("price", out var prc))
                    {
                        price = "￥" + prc.ToString();
                        break;
                    }
                }
            }

            var pdfUrl = item.TryGetProperty("pdfFileUrl", out var pdf) ? pdf.GetString() : null;
            if (!string.IsNullOrWhiteSpace(pdfUrl) && pdfUrl.StartsWith("/"))
            {
                pdfUrl = "https://atta.szlcsc.com" + pdfUrl;
            }

            var imgUrl = item.TryGetProperty("image", out var img) ? img.GetString() : null;
            if (!string.IsNullOrWhiteSpace(imgUrl) && imgUrl.Contains("<$>"))
            {
                imgUrl = imgUrl.Split("<$>")[0];
            }

            var result = new ComponentSearchResult
            {
                Source = "立创 / JLCPCB",
                ManufacturerProductNumber = model ?? code ?? string.Empty,
                Manufacturer = brand ?? string.Empty,
                Description = name ?? string.Empty,
                Package = standard,
                LcscPartNumber = code,
                Stock = stock,
                Price = price,
                DatasheetUrl = string.IsNullOrWhiteSpace(pdfUrl) ? null : pdfUrl,
                PhotoUrl = string.IsNullOrWhiteSpace(imgUrl) ? null : imgUrl,
                LifecycleStatus = stock > 0 ? "Active (现货)" : "无库存"
            };

            if (!string.IsNullOrWhiteSpace(standard)) result.Parameters["Package"] = standard;
            if (!string.IsNullOrWhiteSpace(brand)) result.Parameters["Manufacturer"] = brand;
            if (!string.IsNullOrWhiteSpace(model)) result.Parameters["Manufacturer Part Number"] = model;
            if (!string.IsNullOrWhiteSpace(code)) result.Parameters["LCSC Part Number"] = code;

            results.Add(result);
        }

        return results;
    }

    private static async Task<IReadOnlyList<ComponentSearchResult>> SearchJlcpcbLegacyAsync(string keyword, int limit)
    {
        const string searchUrl = "https://jlcpcb.com/api/overseas-pcb-order/v1/shoppingCart/smtGood/selectSmtComponentList";
        var payload = JsonSerializer.Serialize(new
        {
            currentPage = 1,
            pageSize = Math.Clamp(limit, 1, 30),
            keyword,
            searchKeyword = keyword
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, searchUrl);
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
        request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0.0.0 Safari/537.36");
        request.Headers.Add("Origin", "https://jlcpcb.com");
        request.Headers.Add("Referer", "https://jlcpcb.com/parts");

        using var response = await HttpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode) return [];

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var doc = await JsonDocument.ParseAsync(stream);

        var results = new List<ComponentSearchResult>();
        if (!doc.RootElement.TryGetProperty("data", out var dataElem)) return results;

        JsonElement listElem = default;
        bool hasList = false;

        if (dataElem.TryGetProperty("componentPageInfo", out var pageInfo) &&
            pageInfo.TryGetProperty("list", out listElem) &&
            listElem.ValueKind == JsonValueKind.Array)
        {
            hasList = true;
        }
        else if (dataElem.TryGetProperty("list", out listElem) &&
                 listElem.ValueKind == JsonValueKind.Array)
        {
            hasList = true;
        }

        if (!hasList) return results;

        foreach (var item in listElem.EnumerateArray())
        {
            var lcscCode = item.TryGetProperty("componentCode", out var cc) ? cc.GetString() : null;
            var mfrPart = item.TryGetProperty("componentModelEn", out var cme) ? cme.GetString() : null;
            var brand = item.TryGetProperty("componentBrandEn", out var cbe) ? cbe.GetString() : null;
            var desc = item.TryGetProperty("describe", out var ds) ? ds.GetString() : null;
            var stock = item.TryGetProperty("stockCount", out var sc) && sc.ValueKind == JsonValueKind.Number ? sc.GetInt64() : 0;
            var pkg = item.TryGetProperty("componentSpecificationEn", out var cs) ? cs.GetString() : null;
            var datasheetUrl = item.TryGetProperty("dataManualUrl", out var dm) ? dm.GetString() : null;
            var imageUrl = item.TryGetProperty("componentImageUrl", out var ci) ? ci.GetString() : null;

            string? price = null;
            if (item.TryGetProperty("componentPrices", out var prices) && prices.ValueKind == JsonValueKind.Array)
            {
                foreach (var p in prices.EnumerateArray())
                {
                    if (p.TryGetProperty("productPrice", out var pp))
                    {
                        price = "￥" + pp.ToString();
                        break;
                    }
                }
            }

            var result = new ComponentSearchResult
            {
                Source = "立创 / JLCPCB",
                ManufacturerProductNumber = mfrPart ?? lcscCode ?? string.Empty,
                Manufacturer = brand ?? string.Empty,
                Description = desc ?? string.Empty,
                Package = pkg,
                LcscPartNumber = lcscCode,
                Stock = stock,
                Price = price,
                DatasheetUrl = string.IsNullOrWhiteSpace(datasheetUrl) ? null : datasheetUrl,
                PhotoUrl = string.IsNullOrWhiteSpace(imageUrl) ? null : imageUrl,
                LifecycleStatus = stock > 0 ? "Active (现货)" : "无库存"
            };

            if (item.TryGetProperty("attributes", out var attrs) && attrs.ValueKind == JsonValueKind.Array)
            {
                foreach (var attr in attrs.EnumerateArray())
                {
                    var aName = attr.TryGetProperty("attribute_name_en", out var ane) ? ane.GetString() : null;
                    var aVal = attr.TryGetProperty("attribute_value_name", out var ave) ? ave.GetString() : null;
                    if (!string.IsNullOrWhiteSpace(aName) && !string.IsNullOrWhiteSpace(aVal) && aVal != "-")
                    {
                        result.Parameters[aName] = aVal;
                    }
                }
            }

            results.Add(result);
        }

        return results;
    }

    public async Task<ComponentSearchResult?> TryEnrichWithLcscAsync(string mpn)
    {
        try
        {
            var results = await SearchAsync(mpn, limit: 5);
            if (results.Count == 0) return null;

            var match = results.FirstOrDefault(r => string.Equals(r.ManufacturerProductNumber, mpn, StringComparison.OrdinalIgnoreCase))
                     ?? results.FirstOrDefault(r => string.Equals(r.LcscPartNumber, mpn, StringComparison.OrdinalIgnoreCase))
                     ?? results[0];

            if (!string.IsNullOrWhiteSpace(match.LcscPartNumber))
            {
                match.Model3DUuid = await TryGet3DModelUuidAsync(match.LcscPartNumber);
            }

            return match;
        }
        catch
        {
            return null;
        }
    }

    public async Task<string?> TryGet3DModelUuidAsync(string lcscPartNumber)
    {
        try
        {
            var url = $"https://easyeda.com/api/products/{Uri.EscapeDataString(lcscPartNumber)}/components";
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Add("User-Agent", "Mozilla/5.0 CadenceCisLibraryManager");

            using var resp = await HttpClient.SendAsync(req);
            if (!resp.IsSuccessStatusCode) return null;

            var json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("result", out var resultElem) ||
                !resultElem.TryGetProperty("packageDetail", out var pkgDetail) ||
                !pkgDetail.TryGetProperty("dataStr", out var dataStrElem))
            {
                return null;
            }

            var dataStr = dataStrElem.GetString();
            if (string.IsNullOrWhiteSpace(dataStr)) return null;

            var match = Regex.Match(dataStr, @"[0-9a-fA-F]{32}");
            return match.Success ? match.Value : null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<string?> Download3DStepModelAsync(string uuid, string destinationDirectory, string partName)
    {
        try
        {
            var url = $"https://modules.easyeda.com/qAxj6KHrDKw4blvCG8QJPs7Y/{uuid}";
            using var resp = await HttpClient.GetAsync(url);
            if (!resp.IsSuccessStatusCode) return null;

            Directory.CreateDirectory(destinationDirectory);
            var filePath = Path.Combine(destinationDirectory, $"{partName}.step");
            await using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
            await resp.Content.CopyToAsync(fs);
            return filePath;
        }
        catch
        {
            return null;
        }
    }
}
