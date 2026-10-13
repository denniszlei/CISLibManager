using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CadenceCisLibraryManager.Models;

namespace CadenceCisLibraryManager.Services;

public sealed class DigiKeyService
{
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(20) };
    private string? _cachedToken;
    private DateTime _tokenExpiry = DateTime.MinValue;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);

    public async Task<bool> TestCredentialsAsync(string clientId, string clientSecret)
    {
        try
        {
            var token = await AcquireTokenAsync(clientId, clientSecret, forceRefresh: true);
            return !string.IsNullOrWhiteSpace(token);
        }
        catch
        {
            return false;
        }
    }

    public async Task<IReadOnlyList<ComponentSearchResult>> SearchAsync(AppSettings settings, string keyword, int limit = 10)
    {
        if (string.IsNullOrWhiteSpace(settings.DigiKeyClientId) || string.IsNullOrWhiteSpace(settings.DigiKeyClientSecret))
        {
            throw new InvalidOperationException("请先在设置中填写 DigiKey 的 Client ID 与 Client Secret。");
        }

        var token = await GetValidTokenAsync(settings.DigiKeyClientId, settings.DigiKeyClientSecret);

        const string searchUrl = "https://api.digikey.com/products/v4/search/keyword";
        var payload = JsonSerializer.Serialize(new
        {
            Keywords = keyword.Trim(),
            Limit = Math.Clamp(limit, 1, 50)
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, searchUrl);
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("X-DIGIKEY-Client-Id", settings.DigiKeyClientId);
        request.Headers.Add("X-DIGIKEY-Locale-Site", "US");
        request.Headers.Add("X-DIGIKEY-Locale-Language", "en");
        request.Headers.Add("X-DIGIKEY-Locale-Currency", "USD");

        using var response = await HttpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            var errBody = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"DigiKey API 请求失败 ({(int)response.StatusCode}): {errBody}");
        }

        await using var responseStream = await response.Content.ReadAsStreamAsync();
        using var doc = await JsonDocument.ParseAsync(responseStream);

        var results = new List<ComponentSearchResult>();
        if (!doc.RootElement.TryGetProperty("Products", out var productsElem) || productsElem.ValueKind != JsonValueKind.Array)
        {
            return results;
        }

        foreach (var prod in productsElem.EnumerateArray())
        {
            var item = new ComponentSearchResult
            {
                Source = "DigiKey",
                ManufacturerProductNumber = prod.TryGetProperty("ManufacturerProductNumber", out var mpn) ? mpn.GetString() ?? string.Empty : string.Empty,
                Manufacturer = prod.TryGetProperty("Manufacturer", out var mfr) && mfr.TryGetProperty("Name", out var mfrName) ? mfrName.GetString() ?? string.Empty : string.Empty,
                Description = prod.TryGetProperty("Description", out var desc) && desc.TryGetProperty("ProductDescription", out var pd) ? pd.GetString() ?? string.Empty : string.Empty,
                DetailedDescription = prod.TryGetProperty("Description", out var d2) && d2.TryGetProperty("DetailedDescription", out var dd) ? dd.GetString() : null,
                DatasheetUrl = prod.TryGetProperty("DatasheetUrl", out var ds) ? ds.GetString() : null,
                PhotoUrl = prod.TryGetProperty("PhotoUrl", out var photo) ? photo.GetString() : null,
                Stock = prod.TryGetProperty("QuantityAvailable", out var qty) && qty.ValueKind == JsonValueKind.Number ? qty.GetInt64() : null
            };

            if (prod.TryGetProperty("ProductStatus", out var statusElem) && statusElem.TryGetProperty("Status", out var st))
            {
                item.LifecycleStatus = st.GetString() ?? "Unknown";
            }

            if (prod.TryGetProperty("Category", out var catElem) && catElem.TryGetProperty("Name", out var catName))
            {
                item.Category = catName.GetString();
            }

            // 提取价格
            if (prod.TryGetProperty("StandardPricing", out var pricingElem) && pricingElem.ValueKind == JsonValueKind.Array)
            {
                foreach (var price in pricingElem.EnumerateArray())
                {
                    if (price.TryGetProperty("UnitPrice", out var up))
                    {
                        item.Price = "$" + up.ToString();
                        break;
                    }
                }
            }

            // 提取参数
            if (prod.TryGetProperty("Parameters", out var paramsElem) && paramsElem.ValueKind == JsonValueKind.Array)
            {
                foreach (var p in paramsElem.EnumerateArray())
                {
                    var pText = p.TryGetProperty("ParameterText", out var pt) ? pt.GetString() : null;
                    var vText = p.TryGetProperty("ValueText", out var vt) ? vt.GetString() : null;
                    if (!string.IsNullOrWhiteSpace(pText) && !string.IsNullOrWhiteSpace(vText))
                    {
                        item.Parameters[pText] = vText;
                        if (string.Equals(pText, "Package / Case", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(pText, "Supplier Device Package", StringComparison.OrdinalIgnoreCase))
                        {
                            item.Package ??= vText;
                        }
                    }
                }
            }

            results.Add(item);
        }

        return results;
    }

    private async Task<string> GetValidTokenAsync(string clientId, string clientSecret)
    {
        if (!string.IsNullOrWhiteSpace(_cachedToken) && DateTime.UtcNow < _tokenExpiry)
        {
            return _cachedToken;
        }

        await _tokenLock.WaitAsync();
        try
        {
            if (!string.IsNullOrWhiteSpace(_cachedToken) && DateTime.UtcNow < _tokenExpiry)
            {
                return _cachedToken;
            }

            return await AcquireTokenAsync(clientId, clientSecret, forceRefresh: false);
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private async Task<string> AcquireTokenAsync(string clientId, string clientSecret, bool forceRefresh)
    {
        const string tokenUrl = "https://api.digikey.com/v1/oauth2/token";
        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("client_id", clientId),
            new KeyValuePair<string, string>("client_secret", clientSecret),
            new KeyValuePair<string, string>("grant_type", "client_credentials")
        });

        using var response = await HttpClient.PostAsync(tokenUrl, content);
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"DigiKey OAuth2 鉴权失败: {err}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var doc = await JsonDocument.ParseAsync(stream);
        var token = doc.RootElement.GetProperty("access_token").GetString() ?? throw new InvalidOperationException("未获取到 access_token。");
        var expiresIn = doc.RootElement.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 590;

        _cachedToken = token;
        _tokenExpiry = DateTime.UtcNow.AddSeconds(Math.Max(60, expiresIn - 30));
        return token;
    }
}
