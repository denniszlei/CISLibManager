using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using CadenceCisLibraryManager.Models;
using CadenceCisLibraryManager.Services;

namespace CadenceCisLibraryManager;

public partial class SettingsWindow : Window
{
    private readonly DatabaseService _databaseService = new();
    private readonly DigiKeyService _digiKeyService = new();
    private readonly AppSettings _settings;
    private readonly LibraryCacheService _libraryCacheService = new();
    private readonly ObservableCollection<TableRuleRow> _tableRuleRows = [];
    private readonly ObservableCollection<StandardPartRule> _standardPartRuleRows = [];

    public SettingsWindow(AppSettings settings)
    {
        InitializeComponent();
        _settings = settings;
        TablePrefixesGrid.ItemsSource = _tableRuleRows;
        StandardPartRulesGrid.ItemsSource = _standardPartRuleRows;
        ApplySettingsToUi();
    }

    public AppSettings Settings => _settings;

    private async void TestConnection_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!uint.TryParse(PortBox.Text, out var port))
            {
                MessageBox.Show(this, "端口必须是数字。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var tempSettings = new AppSettings
            {
                Server = ServerBox.Text.Trim(),
                Port = port,
                Database = DatabaseBox.Text.Trim(),
                UserId = UserBox.Text.Trim(),
                Password = PasswordBox.Password
            };

            if (string.IsNullOrWhiteSpace(tempSettings.Server) || string.IsNullOrWhiteSpace(tempSettings.Database))
            {
                MessageBox.Show(this, "请先填写服务器和数据库名称。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var canConnect = await _databaseService.TestConnectionAsync(tempSettings);
            if (canConnect)
            {
                MessageBox.Show(this, "MariaDB 数据库连接成功！", "连接成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show(this, "无法连接到 MariaDB 数据库，请检查地址、端口及账号密码。", "连接失败", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "数据库连接测试异常：" + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void TestDigiKey_Click(object sender, RoutedEventArgs e)
    {
        var clientId = DigiKeyClientIdBox.Text.Trim();
        var clientSecret = DigiKeyClientSecretBox.Password.Trim();

        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            MessageBox.Show(this, "请先输入 Client ID 与 Client Secret。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var success = await _digiKeyService.TestCredentialsAsync(clientId, clientSecret);
            if (success)
            {
                MessageBox.Show(this, "DigiKey OAuth2 鉴权成功！凭据有效。", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show(this, "DigiKey 鉴权失败，请检查 Client ID 与 Secret 是否正确。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "测试发生异常：" + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ReadSettingsFromUi();
            DialogResult = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BrowseFootprintPath_Click(object sender, RoutedEventArgs e)
    {
        BrowseFolderInto(FootprintPathBox);
    }

    private void BrowseSymbolPath_Click(object sender, RoutedEventArgs e)
    {
        BrowseFolderInto(SymbolPathBox);
    }

    private void BrowseModel3DPath_Click(object sender, RoutedEventArgs e)
    {
        BrowseFolderInto(Model3DPathBox);
    }

    private void BrowsePinPath_Click(object sender, RoutedEventArgs e)
    {
        BrowseFolderInto(PinPathBox);
    }

    private void BrowseDatasheetPath_Click(object sender, RoutedEventArgs e)
    {
        BrowseFolderInto(DatasheetPathBox);
    }

    private void ResetStandardRules_Click(object sender, RoutedEventArgs e)
    {
        _standardPartRuleRows.Clear();
        foreach (var rule in StandardPartRule.GetDefaultRules())
        {
            _standardPartRuleRows.Add(rule);
        }
        MessageBox.Show(this, "已重置并加载推荐的 5 大类标准料映射规则（10电阻、11电容、09电感、07二极管晶体管、04杂项晶体）。\r\n点击右下角【保存】后生效。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async void RefreshTablePrefixes_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ReadSettingsFromUi();
            var existingRules = ReadTableRulesFromGrid();
            var tables = await _databaseService.GetTablesAsync(_settings);
            _tableRuleRows.Clear();
            foreach (var table in tables)
            {
                existingRules.TryGetValue(table, out var rule);
                var defaultPrefix = (rule != null && !string.IsNullOrWhiteSpace(rule.Prefix))
                    ? rule.Prefix
                    : (table.Length >= 2 && char.IsDigit(table[0]) && char.IsDigit(table[1]) ? table[..2] : string.Empty);

                _tableRuleRows.Add(new TableRuleRow
                {
                    TableName = table,
                    Prefix = defaultPrefix,
                    PatternTemplate = rule?.PatternTemplate ?? "{PREFIX}{SEQ:10}",
                    ValidationRegex = rule?.ValidationRegex ?? string.Empty,
                    RegexErrorMessage = rule?.RegexErrorMessage ?? string.Empty
                });
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "刷新表名失败：" + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static void BrowseFolderInto(TextBox target)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "选择库路径",
            Multiselect = false
        };

        if (dialog.ShowDialog() == true)
        {
            target.Text = dialog.FolderName;
        }
    }

    private void ApplySettingsToUi()
    {
        ServerBox.Text = _settings.Server;
        PortBox.Text = _settings.Port.ToString();
        DatabaseBox.Text = _settings.Database;
        UserBox.Text = _settings.UserId;
        PasswordBox.Password = _settings.Password;
        FootprintPathBox.Text = _settings.FootprintLibraryPath;
        SymbolPathBox.Text = _settings.SymbolLibraryPath;
        Model3DPathBox.Text = _settings.Model3DLibraryPath;
        PinPathBox.Text = _settings.PinLibraryPath;
        DatasheetPathBox.Text = _settings.DatasheetLibraryPath;
        StoreFileNameCheckBox.IsChecked = _settings.StoreRelativeLibraryFileName;
        PartNumberIdWidthBox.Text = Math.Max(1, _settings.PartNumberIdWidth).ToString();
        PartNumberColumnsBox.Text = JoinNames(_settings.PartNumberColumnNames);
        FootprintColumnsBox.Text = JoinNames(_settings.FootprintColumnNames);
        SymbolColumnsBox.Text = JoinNames(_settings.SymbolColumnNames);
        Model3DColumnsBox.Text = JoinNames(_settings.Model3DColumnNames);
        DatasheetColumnsBox.Text = JoinNames(_settings.DatasheetColumnNames);

        DigiKeyClientIdBox.Text = _settings.DigiKeyClientId;
        DigiKeyClientSecretBox.Password = _settings.DigiKeyClientSecret;
        AutoEnrichLcscCheckBox.IsChecked = _settings.AutoEnrichWithLcsc;

        _standardPartRuleRows.Clear();
        foreach (var rule in _settings.StandardPartRules)
        {
            _standardPartRuleRows.Add(rule);
        }

        _tableRuleRows.Clear();
        var allTables = new HashSet<string>(_settings.TablePartNumberPrefixes.Keys, StringComparer.OrdinalIgnoreCase);
        foreach (var key in _settings.TablePartNumberRules.Keys)
        {
            allTables.Add(key);
        }

        foreach (var tableName in allTables.OrderBy(t => t))
        {
            _settings.TablePartNumberPrefixes.TryGetValue(tableName, out var prefix);
            _settings.TablePartNumberRules.TryGetValue(tableName, out var partRule);

            _tableRuleRows.Add(new TableRuleRow
            {
                TableName = tableName,
                Prefix = prefix ?? string.Empty,
                PatternTemplate = partRule?.PatternTemplate ?? "{PREFIX}-{SEQ:5}",
                ValidationRegex = partRule?.ValidationRegex ?? string.Empty,
                RegexErrorMessage = partRule?.RegexErrorMessage ?? string.Empty
            });
        }
    }

    private void ReadSettingsFromUi()
    {
        if (!uint.TryParse(PortBox.Text, out var port))
        {
            throw new InvalidOperationException("端口必须是数字。");
        }

        if (!int.TryParse(PartNumberIdWidthBox.Text, out var width) || width < 1)
        {
            throw new InvalidOperationException("ID 位数必须是大于 0 的数字。");
        }

        _settings.Server = ServerBox.Text.Trim();
        _settings.Port = port;
        _settings.Database = DatabaseBox.Text.Trim();
        _settings.UserId = UserBox.Text.Trim();
        _settings.Password = PasswordBox.Password;
        _settings.FootprintLibraryPath = FootprintPathBox.Text.Trim();
        _settings.SymbolLibraryPath = SymbolPathBox.Text.Trim();
        _settings.Model3DLibraryPath = Model3DPathBox.Text.Trim();
        _settings.PinLibraryPath = PinPathBox.Text.Trim();
        _settings.DatasheetLibraryPath = DatasheetPathBox.Text.Trim();
        _settings.StoreRelativeLibraryFileName = StoreFileNameCheckBox.IsChecked == true;
        _settings.PartNumberIdWidth = width;
        _settings.PartNumberColumnNames = SplitNames(PartNumberColumnsBox.Text);
        _settings.FootprintColumnNames = SplitNames(FootprintColumnsBox.Text);
        _settings.SymbolColumnNames = SplitNames(SymbolColumnsBox.Text);
        _settings.Model3DColumnNames = SplitNames(Model3DColumnsBox.Text);
        _settings.DatasheetColumnNames = SplitNames(DatasheetColumnsBox.Text);

        _settings.DigiKeyClientId = DigiKeyClientIdBox.Text.Trim();
        _settings.DigiKeyClientSecret = DigiKeyClientSecretBox.Password.Trim();
        _settings.AutoEnrichWithLcsc = AutoEnrichLcscCheckBox.IsChecked == true;

        _settings.StandardPartRules = [.. _standardPartRuleRows.Where(r => !string.IsNullOrWhiteSpace(r.TableName))];

        var rulesDict = ReadTableRulesFromGrid();
        _settings.TablePartNumberPrefixes = rulesDict.ToDictionary(k => k.Key, v => v.Value.Prefix);
        _settings.TablePartNumberRules = rulesDict.ToDictionary(k => k.Key, v => new PartNumberRule
        {
            TableName = v.Key,
            PatternTemplate = v.Value.PatternTemplate,
            ValidationRegex = v.Value.ValidationRegex,
            RegexErrorMessage = v.Value.RegexErrorMessage
        });
    }

    private static string JoinNames(IEnumerable<string> names)
    {
        return string.Join(", ", names);
    }

    private static List<string> SplitNames(string value)
    {
        return value.Split([',', ';', '，', '；'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    }

    private Dictionary<string, TableRuleRow> ReadTableRulesFromGrid()
    {
        var result = new Dictionary<string, TableRuleRow>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in _tableRuleRows)
        {
            var tableName = row.TableName.Trim();
            if (tableName.Length == 0)
            {
                continue;
            }

            result[tableName] = row;
        }

        return result;
    }

    public sealed class TableRuleRow
    {
        public string TableName { get; set; } = string.Empty;

        public string Prefix { get; set; } = string.Empty;

        public string PatternTemplate { get; set; } = "{PREFIX}-{SEQ:5}";

        public string ValidationRegex { get; set; } = string.Empty;

        public string RegexErrorMessage { get; set; } = string.Empty;
    }

    private async void RefreshLibraryCache_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            RefreshCacheButton.IsEnabled = false;
            CacheStatusText.Text = "正在扫描 SMB 物理库中现有的 Allegro 封装与模型文件...";

            var tempSettings = new AppSettings
            {
                FootprintLibraryPath = FootprintPathBox.Text.Trim(),
                SymbolLibraryPath = SymbolPathBox.Text.Trim(),
                Model3DLibraryPath = Model3DPathBox.Text.Trim(),
                PinLibraryPath = PinPathBox.Text.Trim(),
                DatasheetLibraryPath = DatasheetPathBox.Text.Trim()
            };

            var summary = await _libraryCacheService.RefreshAsync(tempSettings);
            if (summary.ErrorMessage != null)
            {
                CacheStatusText.Text = "扫描失败：" + summary.ErrorMessage;
                MessageBox.Show(this, "扫描物理库出现异常：" + summary.ErrorMessage, "扫描失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            else
            {
                CacheStatusText.Text = $"扫描完成: 封装 {summary.FootprintCount} 个, 3D模型 {summary.Model3DCount} 个, 符号库 {summary.SymbolCount} 个 (耗时 {summary.Elapsed.TotalMilliseconds:F0}ms)";
                MessageBox.Show(this, $"物理库扫描成功！\n\n已成功建立物理资产索引：\n• Allegro 封装 (dra/psm): {summary.FootprintCount} 个\n• 3D STEP 模型 (step/stp): {summary.Model3DCount} 个\n• OrCAD 符号库 (olb): {summary.SymbolCount} 个\n扫描耗时: {summary.Elapsed.TotalMilliseconds:F0} 毫秒", "物理库缓存更新", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            CacheStatusText.Text = "扫描失败：" + ex.Message;
        }
        finally
        {
            RefreshCacheButton.IsEnabled = true;
        }
    }
}
