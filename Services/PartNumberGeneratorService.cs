using System.Text.RegularExpressions;
using CadenceCisLibraryManager.Models;

namespace CadenceCisLibraryManager.Services;

public sealed class PartNumberGeneratorService
{
    public string Generate(
        AppSettings settings,
        string tableName,
        long nextId,
        IReadOnlyDictionary<string, string?>? currentFormValues = null)
    {
        var rule = GetRuleForTable(settings, tableName);
        var template = rule?.PatternTemplate;

        // 若未配置高级模板，降级为默认的前缀+自增ID
        if (string.IsNullOrWhiteSpace(template))
        {
            var prefix = GetTablePrefix(settings, tableName);
            var width = Math.Max(1, settings.PartNumberIdWidth);
            return prefix + nextId.ToString(new string('0', width));
        }

        var result = template;
        var tablePrefix = GetTablePrefix(settings, tableName);

        // 替换 {PREFIX}
        result = Regex.Replace(result, @"\{PREFIX\}", tablePrefix, RegexOptions.IgnoreCase);

        // 替换 {SEP}
        result = Regex.Replace(result, @"\{SEP\}", "-", RegexOptions.IgnoreCase);

        // 替换 {TABLE_NAME}
        result = Regex.Replace(result, @"\{TABLE_NAME\}", tableName, RegexOptions.IgnoreCase);

        // 替换 {PKG}
        var pkgValue = ExtractValue(currentFormValues, "Package", "封装", "PCB Footprint", "Footprint") ?? string.Empty;
        result = Regex.Replace(result, @"\{PKG\}", CleanTokenValue(pkgValue), RegexOptions.IgnoreCase);

        // 替换 {VAL}
        var valValue = ExtractValue(currentFormValues, "Value", "阻值", "容值", "数值") ?? string.Empty;
        result = Regex.Replace(result, @"\{VAL\}", CleanTokenValue(valValue), RegexOptions.IgnoreCase);

        // 替换 {DATE:...}
        result = Regex.Replace(result, @"\{DATE(?::([^}]+))?\}", match =>
        {
            var format = match.Groups[1].Success ? match.Groups[1].Value : "yyMM";
            return DateTime.Now.ToString(format);
        }, RegexOptions.IgnoreCase);

        // 替换 {SEQ:...}
        result = Regex.Replace(result, @"\{SEQ(?::(\d+))?\}", match =>
        {
            var width = match.Groups[1].Success && int.TryParse(match.Groups[1].Value, out var w)
                ? Math.Max(1, w)
                : Math.Max(1, settings.PartNumberIdWidth);
            return nextId.ToString(new string('0', width));
        }, RegexOptions.IgnoreCase);

        return result.Trim();
    }

    public (bool IsValid, string? ErrorMessage) Validate(string partNumber, PartNumberRule? rule)
    {
        if (string.IsNullOrWhiteSpace(partNumber))
        {
            return (false, "Part Number 不能为空。");
        }

        if (rule == null || string.IsNullOrWhiteSpace(rule.ValidationRegex))
        {
            return (true, null);
        }

        try
        {
            if (!Regex.IsMatch(partNumber, rule.ValidationRegex))
            {
                var msg = string.IsNullOrWhiteSpace(rule.RegexErrorMessage)
                    ? $"Part Number '{partNumber}' 不符合企业编码规范正则：{rule.ValidationRegex}"
                    : rule.RegexErrorMessage;
                return (false, msg);
            }

            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, $"正则表达式校验异常：{ex.Message}");
        }
    }

    private static PartNumberRule? GetRuleForTable(AppSettings settings, string tableName)
    {
        if (settings.TablePartNumberRules.TryGetValue(tableName, out var rule))
        {
            return rule;
        }

        return settings.TablePartNumberRules.Values.FirstOrDefault(r => string.Equals(r.TableName, tableName, StringComparison.OrdinalIgnoreCase));
    }

    private static string GetTablePrefix(AppSettings settings, string tableName)
    {
        var configured = settings.TablePartNumberPrefixes.FirstOrDefault(pair => string.Equals(pair.Key, tableName, StringComparison.OrdinalIgnoreCase));
        return configured.Value ?? string.Empty;
    }

    private static string? ExtractValue(IReadOnlyDictionary<string, string?>? dict, params string[] candidateKeys)
    {
        if (dict == null || dict.Count == 0) return null;

        foreach (var key in candidateKeys)
        {
            var match = dict.FirstOrDefault(kv => string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(match.Value))
            {
                return match.Value;
            }
        }

        return null;
    }

    private static string CleanTokenValue(string value)
    {
        return value.Replace(" ", string.Empty).Replace("/", "_").Replace("\\", "_");
    }
}
