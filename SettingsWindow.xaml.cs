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
                _tableRuleRows.Add(new TableRuleRow
                {
                    TableName = table,
                    Prefix = rule?.Prefix ?? string.Empty,
                    PatternTemplate = rule?.PatternTemplate ?? "{PREFIX}-{SEQ:5}",
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
        StoreFileNameCheckBox.IsChecked = _settings.StoreRelativeLibraryFileName;
        PartNumberIdWidthBox.Text = Math.Max(1, _settings.PartNumberIdWidth).ToString();
        PartNumberColumnsBox.Text = JoinNames(_settings.PartNumberColumnNames);
        FootprintColumnsBox.Text = JoinNames(_settings.FootprintColumnNames);
        SymbolColumnsBox.Text = JoinNames(_settings.SymbolColumnNames);
        Model3DColumnsBox.Text = JoinNames(_settings.Model3DColumnNames);

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
        _settings.StoreRelativeLibraryFileName = StoreFileNameCheckBox.IsChecked == true;
        _settings.PartNumberIdWidth = width;
        _settings.PartNumberColumnNames = SplitNames(PartNumberColumnsBox.Text);
        _settings.FootprintColumnNames = SplitNames(FootprintColumnsBox.Text);
        _settings.SymbolColumnNames = SplitNames(SymbolColumnsBox.Text);
        _settings.Model3DColumnNames = SplitNames(Model3DColumnsBox.Text);

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
}
