using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using CadenceCisLibraryManager.Models;
using CadenceCisLibraryManager.Services;

namespace CadenceCisLibraryManager;

public partial class MainWindow : Window
{
    private readonly SettingsService _settingsService = new();
    private readonly DatabaseService _databaseService = new();
    private readonly FileLibraryService _fileLibraryService = new();
    private readonly ArchiveExtractorService _archiveExtractorService = new();
    private readonly DigiKeyService _digiKeyService = new();
    private readonly EasyedaApiService _easyedaApiService = new();
    private readonly PartNumberGeneratorService _partNumberGeneratorService = new();

    private readonly Dictionary<string, Control> _inputs = [];
    private readonly Dictionary<string, ComboBox> _symbolLibraryInputs = [];
    private readonly Dictionary<FileColumnKind, List<string>> _selectedFiles = [];
    private string? _sourceSymbolLibraryPath;
    private IReadOnlyList<ColumnInfo> _columns = [];
    private string? _loadedTableName;
    private AppSettings _settings = new();
    private string? _editingKeyColumn;
    private string? _editingKeyValue;

    private List<ComponentItem> _allComponents = [];
    private List<ComponentItem> _drawingsTargetComponents = [];
    private ComponentItem? _selectedDrawingComponent;

    private bool IsStandardPartMode => chkStandardPartMode.IsChecked == true;

    private sealed record RecordFileItem(string Title, string Path, bool Exists);

    public MainWindow()
    {
        InitializeComponent();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        Title = GetWindowTitleWithVersion();
        var loadResult = await _settingsService.LoadWithStatusAsync();
        _settings = loadResult.Settings;
        LoadTargetSymbolLibraries();
        if (loadResult.PasswordDecryptionFailed)
        {
            MessageBox.Show(this, "已保存的数据库密码无法在当前 Windows 用户环境下解密，可能是配置文件来自其他用户或电脑。请打开“设置”重新输入数据库密码并保存。", "密码解密失败", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        InitSampleLocations();

        try
        {
            var tables = await _databaseService.GetTablesAsync(_settings);
            TableComboBox.ItemsSource = tables;
            if (tables.Count > 0)
            {
                TableComboBox.SelectedIndex = 0;
            }
        }
        catch
        {
            // Initial connection failure handled gracefully
        }

        _ = LoadAllComponentsAsync();
        _ = LoadDrawingsTargetListAsync();
    }

    private static string GetWindowTitleWithVersion()
    {
        var asm = Assembly.GetEntryAssembly() ?? typeof(MainWindow).Assembly;
        var fvi = FileVersionInfo.GetVersionInfo(asm.Location);
        var ver = !string.IsNullOrWhiteSpace(fvi.ProductVersion)
            ? fvi.ProductVersion.Split('+')[0]
            : (asm.GetName().Version?.ToString(3) ?? "2.0.0");
        return $"OrCAD CIS 智能库与研发物料管控系统 v{ver} [Enterprise Edition]";
    }

    private async void Settings_Click(object sender, RoutedEventArgs e)
    {
        var window = new SettingsWindow(_settings) { Owner = this };
        if (window.ShowDialog() == true)
        {
            _settings = window.Settings;
            await _settingsService.SaveAsync(_settings);
            LoadTargetSymbolLibraries();
            InitSampleLocations();
            SetStatus("设置已保存并生效。");
        }
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        var asm = Assembly.GetEntryAssembly() ?? typeof(MainWindow).Assembly;
        var fvi = FileVersionInfo.GetVersionInfo(asm.Location);
        var ver = !string.IsNullOrWhiteSpace(fvi.ProductVersion) ? fvi.ProductVersion.Split('+')[0] : "2.0.0";
        MessageBox.Show(this, $"OrCAD CIS 智能库与研发物料管控系统
版本: v{ver}

功能特性:
- 🔖 料号管理：跨表全库查询、无图档先行建号、CSV 导出
- 🌐 3 模式电气参数检索：联合搜索 / DigiKey / 立创，自动下载 Datasheet
- 📐 CIS 图档维护：UltraLibrarian / SnapEDA 资产包分发、封装/符号/3D模型关联
- 📦 样品库存管理：入库、领用、借出、归还、报废全生命周期追踪", "关于系统", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    #region Tab 1: 料号管理 (Part Number Management)

    private async void ShowAllComponents_Click(object sender, RoutedEventArgs e)
    {
        ComponentSearchBox.Text = string.Empty;
        await LoadAllComponentsAsync();
    }

    private void ComponentSearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ComponentSearch_Click(sender, e);
        }
    }

    private async void ComponentSearch_Click(object sender, RoutedEventArgs e)
    {
        var kw = ComponentSearchBox.Text.Trim();
        await LoadAllComponentsAsync(string.IsNullOrWhiteSpace(kw) ? null : kw);
    }

    private async Task LoadAllComponentsAsync(string? keyword = null)
    {
        try
        {
            SetStatus("正在从 MariaDB 跨表检索全库料号...");
            _allComponents = await _databaseService.QueryAllComponentsAsync(_settings, keyword);
            AllComponentsGrid.ItemsSource = _allComponents;
            SetStatus($"已加载 {_allComponents.Count} 条料号记录。");
        }
        catch (Exception ex)
        {
            ShowError("查询全库料号失败：" + ex.Message);
        }
    }

    private void ExportComponentsCsv_Click(object sender, RoutedEventArgs e)
    {
        var items = AllComponentsGrid.ItemsSource as IEnumerable<ComponentItem> ?? _allComponents;
        var list = items.ToList();
        if (list.Count == 0)
        {
            MessageBox.Show(this, "当前列表没有数据可供导出。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "导出料号清单",
            Filter = "CSV 电子表格 (*.csv)|*.csv|所有文件 (*.*)|*.*",
            FileName = $"CIS_Part_Numbers_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
        };

        if (dialog.ShowDialog() != true) return;

        try
        {
            var sb = new StringBuilder();
            sb.AppendLine("分类/所属表,物料号(PN),原厂型号(MPN),规格/值,封装,厂商,规格书(Datasheet),渠道,图档状态,原理图符号,PCB封装,3D模型,生命周期,描述,备注");

            foreach (var item in list)
            {
                var row = string.Join(",", new[]
                {
                    EscapeCsv(item.Category),
                    EscapeCsv(item.PartNumber),
                    EscapeCsv(item.ManufacturerPartNumber),
                    EscapeCsv(item.Value),
                    EscapeCsv(item.Package),
                    EscapeCsv(item.Manufacturer),
                    EscapeCsv(item.Datasheet),
                    EscapeCsv(item.Distributor),
                    EscapeCsv(item.DrawingStatus),
                    EscapeCsv(item.SchematicPart),
                    EscapeCsv(item.PcbFootprint),
                    EscapeCsv(item.Model3D),
                    EscapeCsv(item.LifecycleStatus),
                    EscapeCsv(item.Description),
                    EscapeCsv(item.Note)
                });
                sb.AppendLine(row);
            }

            File.WriteAllText(dialog.FileName, sb.ToString(), new UTF8Encoding(true));
            MessageBox.Show(this, $"成功导出 {list.Count} 条料号记录至：
{dialog.FileName}

带 UTF-8 BOM 编码，支持 Excel 直接双击打开。", "导出成功", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            ShowError("导出 CSV 失败：" + ex.Message);
        }
    }

    private static string EscapeCsv(string? val)
    {
        if (string.IsNullOrEmpty(val)) return """";
        return $""{val.Replace(""", """")}"";
    }

    private async void NewComponent_Click(object sender, RoutedEventArgs e)
    {
        ResetEditMode();
        ClearDynamicForm();
        FormEditingHint.Text = "进入新增料号模式。填写参数后点击【保存料号至数据库】即可建号（图档可留空）。";

        var tableName = GetSelectedTableName();
        if (string.IsNullOrWhiteSpace(tableName) && TableComboBox.Items.Count > 0)
        {
            TableComboBox.SelectedIndex = 0;
            tableName = GetSelectedTableName();
        }

        if (!string.IsNullOrWhiteSpace(tableName))
        {
            await LoadSelectedTableAsync();
        }
    }

    private async void AllComponentsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var item = AllComponentsGrid.SelectedItem as ComponentItem;
        if (item == null) return;

        // Sync table combo if needed
        var targetTable = item.TableName;
        if (!string.IsNullOrWhiteSpace(targetTable) &&
            !string.Equals(GetSelectedTableName(), targetTable, StringComparison.OrdinalIgnoreCase))
        {
            var match = TableComboBox.Items.Cast<object>()
                .FirstOrDefault(o => o?.ToString()?.Equals(targetTable, StringComparison.OrdinalIgnoreCase) == true ||
                                     o?.ToString()?.Contains(targetTable, StringComparison.OrdinalIgnoreCase) == true);
            if (match != null)
            {
                TableComboBox.SelectedItem = match;
            }
        }

        await EnsureColumnsLoadedAsync();

        // Populate fields
        SetInputValue("Part_Number", item.PartNumber);
        SetInputValue("Part_Type", item.PartType);
        SetInputValue("Value", item.Value);
        SetInputValue("Package", item.Package);
        SetInputValue("Description", item.Description);
        SetInputValue("Manufacturer", item.Manufacturer);
        SetInputValue("Manufacturer_Part_Number", item.ManufacturerPartNumber);
        SetInputValue("Datasheet", item.Datasheet);
        SetInputValue("Distributor", item.Distributor);
        SetInputValue("Note", item.Note);
        SetInputValue("Lifecycle_Status", item.LifecycleStatus);
        SetInputValue("LCSC_Part_Number", item.LcscPartNumber);
        SetInputValue("Schematic_Part", item.SchematicPart);
        SetInputValue("PCB_Footprint", item.PcbFootprint);
        SetInputValue("Model_3D", item.Model3D);

        _editingKeyColumn = "Part_Number";
        _editingKeyValue = item.PartNumber;
        FormEditingHint.Text = $"当前正在编辑料号: {item.PartNumber} ({item.ManufacturerPartNumber}) | 分类: {item.Category}";
    }

    private void AllComponentsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        var item = AllComponentsGrid.SelectedItem as ComponentItem;
        if (item == null) return;

        // Switch to Tab 2 and select item
        MainTabControl.SelectedIndex = 1;
        var target = _drawingsTargetComponents.FirstOrDefault(c => c.PartNumber == item.PartNumber);
        if (target != null)
        {
            DrawingsTargetGrid.SelectedItem = target;
            DrawingsTargetGrid.ScrollIntoView(target);
        }
    }

    private void ResetForm_Click(object sender, RoutedEventArgs e)
    {
        ResetEditMode();
        ClearDynamicForm();
        FormEditingHint.Text = "提示：图档字段可留空，先建号即可在样品库中入库管理";
    }

    private async void SaveComponent_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_columns.Count == 0)
            {
                MessageBox.Show(this, "请先选择分类表并生成表单。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var tableName = GetSelectedTableName();
            await EnsurePartNumberValueAsync(tableName);

            var partNumberColumn = _databaseService.FindPartNumberColumn(_columns, _settings) ?? "Part_Number";
            var pnValue = GetInputValue(partNumberColumn);
            if (string.IsNullOrWhiteSpace(pnValue))
            {
                MessageBox.Show(this, "料号 (Part Number) 不能为空！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _settings.TablePartNumberRules.TryGetValue(tableName, out var rule);
            var (isValid, errMsg) = _partNumberGeneratorService.Validate(pnValue, rule);
            if (!isValid)
            {
                var proceed = MessageBox.Show(this, errMsg + "\r\n\r\n是否仍然强制保存？", "料号规范拦截", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (proceed != MessageBoxResult.Yes) return;
            }

            var values = new Dictionary<string, string?>();
            foreach (var column in _columns.Where(c => !c.IsAutoIncrement && !c.IsGenerated))
            {
                if (_inputs.ContainsKey(column.Name))
                {
                    values[column.Name] = GetInputValue(column.Name);
                }
            }

            if (!string.IsNullOrWhiteSpace(_editingKeyColumn) && !string.IsNullOrWhiteSpace(_editingKeyValue))
            {
                await _databaseService.UpdateRowAsync(_settings, tableName, values, _columns, _editingKeyColumn, _editingKeyValue);
                MessageBox.Show(this, $"料号 {pnValue} 记录已更新成功！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                SetStatus($"料号 {pnValue} 已成功更新。");
            }
            else
            {
                await _databaseService.InsertRowAsync(_settings, tableName, values, _columns);
                MessageBox.Show(this, $"料号 {pnValue} 已成功保存至数据库！\r\n\r\n可在【样品库存管理】中进行入库与借还调测；\r\n如需补齐图档，可在【CIS图档维护】中关联更新。", "建号成功", MessageBoxButton.OK, MessageBoxImage.Information);
                SetStatus($"料号 {pnValue} 已成功建号。");
            }

            await LoadAllComponentsAsync();
            await LoadDrawingsTargetListAsync();
            ResetEditMode();
        }
        catch (Exception ex)
        {
            ShowError("保存料号失败：" + ex.Message);
        }
    }

    #endregion

    #region Tab 1 Area B: 3-模式联网检索与参数注入

    private void OnlineSearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            OnlineSearchButton_Click(sender, e);
        }
    }

    private async void OnlineSearchButton_Click(object sender, RoutedEventArgs e)
    {
        var keyword = OnlineSearchBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(keyword))
        {
            MessageBox.Show(this, "请输入需要检索的元器件型号或规格描述（如 12402076E512A, LM358DR 或 0402 10k 1%）。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            OnlineSearchButton.IsEnabled = false;
            SetStatus($"正在联网检索：{keyword} ...");
            IReadOnlyList<ComponentSearchResult> results;

            if (rbSearchCombined.IsChecked == true)
            {
                var digiTask = SearchDigiKeySafeAsync(keyword);
                var lcscTask = _easyedaApiService.SearchAsync(keyword, limit: 15);
                await Task.WhenAll(digiTask, lcscTask);

                results = CombineSearchResults(await digiTask, await lcscTask);
            }
            else if (rbSearchDigiKey.IsChecked == true)
            {
                results = await _digiKeyService.SearchAsync(_settings, keyword, limit: 15);
            }
            else
            {
                results = await _easyedaApiService.SearchAsync(keyword, limit: 15);
            }

            SearchResultsGrid.ItemsSource = results;
            SelectedResultSummary.Text = $"检索到 {results.Count} 条记录。点击列表选中条目可注入参数并自动下载规格书。";
            SetStatus($"检索完成，共找到 {results.Count} 条候选元器件。");
        }
        catch (Exception ex)
        {
            ShowError("联网检索失败：" + ex.Message);
        }
        finally
        {
            OnlineSearchButton.IsEnabled = true;
        }
    }

    private async Task<IReadOnlyList<ComponentSearchResult>> SearchDigiKeySafeAsync(string keyword)
    {
        if (string.IsNullOrWhiteSpace(_settings.DigiKeyClientId) || string.IsNullOrWhiteSpace(_settings.DigiKeyClientSecret))
        {
            return [];
        }
        try
        {
            return await _digiKeyService.SearchAsync(_settings, keyword, limit: 15);
        }
        catch
        {
            return [];
        }
    }

    private static IReadOnlyList<ComponentSearchResult> CombineSearchResults(
        IReadOnlyList<ComponentSearchResult> digiResults,
        IReadOnlyList<ComponentSearchResult> lcscResults)
    {
        var combined = new List<ComponentSearchResult>();
        var lcscUsed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var digi in digiResults)
        {
            var cleanDigiMpn = CleanPartNumber(digi.ManufacturerProductNumber);
            var match = lcscResults.FirstOrDefault(l =>
                !string.IsNullOrWhiteSpace(cleanDigiMpn) &&
                (string.Equals(CleanPartNumber(l.ManufacturerProductNumber), cleanDigiMpn, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(CleanPartNumber(l.LcscPartNumber), cleanDigiMpn, StringComparison.OrdinalIgnoreCase)));

            if (match != null)
            {
                if (!string.IsNullOrWhiteSpace(match.LcscPartNumber)) lcscUsed.Add(match.LcscPartNumber);
                if (!string.IsNullOrWhiteSpace(match.ManufacturerProductNumber)) lcscUsed.Add(match.ManufacturerProductNumber);

                digi.Source = "DigiKey + 立创";
                digi.LcscPartNumber ??= match.LcscPartNumber;
                digi.Model3DUuid ??= match.Model3DUuid;
                if (!string.IsNullOrWhiteSpace(match.Price))
                {
                    digi.Price = !string.IsNullOrWhiteSpace(digi.Price) ? $"{digi.Price} ({match.Price})" : match.Price;
                }
                if ((digi.Stock ?? 0) == 0 && (match.Stock ?? 0) > 0)
                {
                    digi.Stock = match.Stock;
                    digi.LifecycleStatus = match.LifecycleStatus;
                }
                if (string.IsNullOrWhiteSpace(digi.DatasheetUrl) && !string.IsNullOrWhiteSpace(match.DatasheetUrl))
                {
                    digi.DatasheetUrl = match.DatasheetUrl;
                }
            }

            combined.Add(digi);
        }

        foreach (var lcsc in lcscResults)
        {
            var isUsed = (!string.IsNullOrWhiteSpace(lcsc.LcscPartNumber) && lcscUsed.Contains(lcsc.LcscPartNumber)) ||
                         (!string.IsNullOrWhiteSpace(lcsc.ManufacturerProductNumber) && lcscUsed.Contains(lcsc.ManufacturerProductNumber));

            if (!isUsed)
            {
                combined.Add(lcsc);
            }
        }

        return combined;
    }

    private static string CleanPartNumber(string? pn)
    {
        if (string.IsNullOrWhiteSpace(pn)) return string.Empty;
        return Regex.Replace(pn, @"[\s\-_\/]", string.Empty).ToUpperInvariant();
    }

    private void SearchResultsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var item = SearchResultsGrid.SelectedItem as ComponentSearchResult;
        if (item == null)
        {
            ApplyOnlineResultButton.IsEnabled = false;
            SelectedResultSummary.Text = "请在上方列表中选中条目以注入参数。";
            return;
        }

        ApplyOnlineResultButton.IsEnabled = true;
        var summary = $"已选中: {item.ManufacturerProductNumber} ({item.Manufacturer}) | 封装: {item.Package ?? "-"} | 状态: {item.DisplayStatus} | 参考价: {item.Price ?? "-"}";
        if (!string.IsNullOrWhiteSpace(item.DatasheetUrl))
        {
            summary += " | [含规格书]";
        }
        SelectedResultSummary.Text = summary;
    }

    private async void ApplyOnlineResultButton_Click(object sender, RoutedEventArgs e)
    {
        var item = SearchResultsGrid.SelectedItem as ComponentSearchResult;
        if (item == null) return;

        var suggestedTable = InferTargetTable(item);
        if (!string.IsNullOrWhiteSpace(suggestedTable) &&
            !string.Equals(GetSelectedTableName(), suggestedTable, StringComparison.OrdinalIgnoreCase))
        {
            var match = TableComboBox.Items.Cast<object>()
                .FirstOrDefault(o => o?.ToString()?.Contains(suggestedTable, StringComparison.OrdinalIgnoreCase) == true);
            if (match != null)
            {
                TableComboBox.SelectedItem = match;
                await EnsureColumnsLoadedAsync();
            }
        }

        if (item.IsObsolete)
        {
            var warningText = $"【高风险停产料预警】\r\n\r\n物料 \"{item.ManufacturerProductNumber}\" 在原厂数据库中被标记为：{item.LifecycleStatus} (停产/淘汰)！\r\n\r\n在商业量产设计中录入停产料可能导致断供风险。是否确定仍要录入该料？";
            var res = MessageBox.Show(this, warningText, "停产料审查拦截", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (res != MessageBoxResult.Yes) return;
        }

        string? storedDatasheetPath = null;
        if (chkAutoDownloadDatasheet.IsChecked == true && !string.IsNullOrWhiteSpace(item.DatasheetUrl))
        {
            try
            {
                SetStatus($"正在下载规格书至 SMB / 本地目录: {item.DatasheetUrl} ...");
                var targetName = !string.IsNullOrWhiteSpace(item.ManufacturerProductNumber)
                    ? item.ManufacturerProductNumber
                    : (GetInputValue("Part_Number") ?? "datasheet");
                storedDatasheetPath = await _fileLibraryService.DownloadAndSaveDatasheetAsync(_settings, item.DatasheetUrl, targetName);
                if (!string.IsNullOrWhiteSpace(storedDatasheetPath))
                {
                    SetStatus($"规格书已成功保存至文档目录: {storedDatasheetPath}");
                }
            }
            catch (Exception ex)
            {
                SetStatus($"规格书下载提示: {ex.Message}，暂保留原始链接。");
            }
        }

        var finalDatasheet = !string.IsNullOrWhiteSpace(storedDatasheetPath) ? storedDatasheetPath : item.DatasheetUrl;

        SetInputValueIfColumnMatches(item.Manufacturer, "Manufacturer", "厂商", "品牌", "Mfg");
        SetInputValueIfColumnMatches(item.ManufacturerProductNumber, "Manufacturer_Part_Number", "MPN", "原厂型号", "PartNumber", "Part_Number_Mfg");
        SetInputValueIfColumnMatches(item.Category, "Part_Type", "Type", "类别", "器件类型");
        SetInputValueIfColumnMatches(item.Description, "Description", "描述", "品名", "Desc");
        SetInputValueIfColumnMatches(finalDatasheet, "Datasheet", "Datasheet_URL", "规格书", "URL");
        SetInputValueIfColumnMatches(item.Package, "Package", "封装", "Package_Type");
        SetInputValueIfColumnMatches(item.LifecycleStatus, "Lifecycle_Status", "Status", "生命周期", "状态");
        SetInputValueIfColumnMatches(item.Price, "Price_RMB", "Price", "价格", "单价");
        SetInputValueIfColumnMatches(item.LcscPartNumber, "LCSC_Part_Number", "LCSC", "立创编号", "LCSC_PN");
        var defaultDistributor = string.Equals(item.Source, "LCSC", StringComparison.OrdinalIgnoreCase) || item.Source.Contains("立创")
            ? "立创商城 (LCSC)"
            : (string.Equals(item.Source, "DigiKey", StringComparison.OrdinalIgnoreCase) ? "得捷电子 (DigiKey)" : string.Empty);
        SetInputValueIfColumnMatches(defaultDistributor, "Distributor", "分销商", "渠道", "代理商");

        var hasValueParam = false;
        foreach (var (paramKey, paramVal) in item.Parameters)
        {
            if (paramKey.Contains("Resistance", StringComparison.OrdinalIgnoreCase) ||
                paramKey.Contains("Capacitance", StringComparison.OrdinalIgnoreCase) ||
                paramKey.Contains("Inductance", StringComparison.OrdinalIgnoreCase))
            {
                SetInputValueIfColumnMatches(paramVal, "Value", "阻值", "容值", "感值", "数值");
                hasValueParam = true;
            }
        }

        if (!hasValueParam && !string.IsNullOrWhiteSpace(item.ManufacturerProductNumber))
        {
            SetInputValueIfColumnMatches(item.ManufacturerProductNumber, "Value", "型号", "数值");
        }

        if (IsStandardPartMode && !string.IsNullOrWhiteSpace(item.Package))
        {
            ApplyStandardPackageDefaults(item.Package);
        }

        SetStatus($"已成功将物料 {item.ManufacturerProductNumber} 的规格参数注入动态表单。");
    }

    private static string? InferTargetTable(ComponentSearchResult item)
    {
        var text = $"{item.Category} {item.Description} {item.ManufacturerProductNumber}".ToLowerInvariant();
        if (text.Contains("resistor") || text.Contains("res ") || text.Contains(" 0402") && text.Contains("ohm")) return "10_resistor";
        if (text.Contains("capacitor") || text.Contains("cap ") || text.Contains(" uF") || text.Contains(" pF")) return "11_capacitor";
        if (text.Contains("inductor") || text.Contains(" uH") || text.Contains(" nH")) return "09_inductor";
        if (text.Contains("diode") || text.Contains("transistor") || text.Contains("mosfet") || text.Contains("tvs")) return "07_transistor_diode";
        if (text.Contains("connector") || text.Contains("header") || text.Contains("usb") || text.Contains("socket")) return "12_connector";
        if (text.Contains("crystal") || text.Contains("oscillator") || text.Contains("mhz") || text.Contains("khz")) return "04_misc";
        if (text.Contains("flash") || text.Contains("eeprom") || text.Contains("nor") || text.Contains("nand")) return "05_flash_rom";
        if (text.Contains("ddr") || text.Contains("sdram") || text.Contains("lpddr")) return "03_ddr";
        if (text.Contains("mcu") || text.Contains("cpu") || text.Contains("soc") || text.Contains("processor")) return "01_cpu_mcu_soc";
        if (text.Contains("power") || text.Contains("ldo") || text.Contains("regulator") || text.Contains("pmic")) return "06_power_ic";
        if (text.Contains("opamp") || text.Contains("amplifier") || text.Contains("interface") || text.Contains("transceiver")) return "02_common_ic";
        return null;
    }

    private void SetInputValueIfColumnMatches(string? value, params string[] candidateNames)
    {
        if (string.IsNullOrWhiteSpace(value) || candidateNames.Length == 0) return;

        for (var i = 0; i < candidateNames.Length; i++)
        {
            var candidate = candidateNames[i];
            var col = _columns.FirstOrDefault(c => string.Equals(c.Name, candidate, StringComparison.OrdinalIgnoreCase) ||
                                                   c.Name.Contains(candidate, StringComparison.OrdinalIgnoreCase));
            if (col != null && _inputs.ContainsKey(col.Name))
            {
                SetInputValue(col.Name, value);
                return;
            }
        }
    }

    #endregion

    #region Tab 2: CIS 图档维护 (CIS Drawing Maintenance)

    private void DrawingSearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            DrawingSearch_Click(sender, e);
        }
    }

    private async void DrawingSearch_Click(object sender, RoutedEventArgs e)
    {
        var kw = DrawingSearchBox.Text.Trim();
        await LoadDrawingsTargetListAsync(string.IsNullOrWhiteSpace(kw) ? null : kw);
    }

    private async void DrawingRefresh_Click(object sender, RoutedEventArgs e)
    {
        DrawingSearchBox.Text = string.Empty;
        await LoadDrawingsTargetListAsync();
    }

    private void DrawingFilter_Changed(object sender, RoutedEventArgs e)
    {
        ApplyDrawingFilter();
    }

    private void ApplyDrawingFilter()
    {
        if (chkOnlyMissingDrawings.IsChecked == true)
        {
            DrawingsTargetGrid.ItemsSource = _drawingsTargetComponents.Where(c => !c.HasDrawings).ToList();
        }
        else
        {
            DrawingsTargetGrid.ItemsSource = _drawingsTargetComponents;
        }
    }

    private async Task LoadDrawingsTargetListAsync(string? keyword = null)
    {
        try
        {
            _drawingsTargetComponents = await _databaseService.QueryAllComponentsAsync(_settings, keyword);
            ApplyDrawingFilter();
        }
        catch (Exception ex)
        {
            ShowError("加载图档物料清单失败：" + ex.Message);
        }
    }

    private void DrawingsTargetGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var item = DrawingsTargetGrid.SelectedItem as ComponentItem;
        if (item == null)
        {
            _selectedDrawingComponent = null;
            SelectedDrawingPartInfo.Text = "当前选定物料: 未选定 (请在下方列表点击选中目标物料)";
            return;
        }

        _selectedDrawingComponent = item;
        SelectedDrawingPartInfo.Text = $"当前选定物料: {item.PartNumber} ({item.ManufacturerPartNumber} {item.Value}) | 分类: {item.Category} | 图档状态: {item.DrawingStatus}";

        if (!string.IsNullOrWhiteSpace(item.PcbFootprint)) FootprintFilesBox.Text = item.PcbFootprint;
        if (!string.IsNullOrWhiteSpace(item.SchematicPart)) SourceSymbolLibraryBox.Text = item.SchematicPart;
        if (!string.IsNullOrWhiteSpace(item.Model3D)) Model3DFileBox.Text = item.Model3D;
    }

    private async void SubmitDrawings_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedDrawingComponent == null)
        {
            MessageBox.Show(this, "请先在上方目标物料列表中点击选定需要维护图档的物料！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            SetStatus($"正在向 SMB 共享库分发图档文件并更新物料 {_selectedDrawingComponent.PartNumber} ...");

            if (_selectedFiles.TryGetValue(FileColumnKind.Pin, out var pinFiles) && pinFiles.Count > 0)
            {
                await _fileLibraryService.CopyManyToLibraryAsync(_settings, FileColumnKind.Pin, pinFiles, ResolveFileConflict);
            }

            string? footprintVal = null;
            if (_selectedFiles.TryGetValue(FileColumnKind.Footprint, out var fpFiles) && fpFiles.Count > 0)
            {
                footprintVal = await CopySelectedFilesForDatabaseAsync(FileColumnKind.Footprint, fpFiles);
            }
            else if (!string.IsNullOrWhiteSpace(FootprintFilesBox.Text))
            {
                footprintVal = FootprintFilesBox.Text.Trim();
            }

            string? symbolVal = null;
            if (_selectedFiles.TryGetValue(FileColumnKind.Symbol, out var symFiles) && symFiles.Count > 0)
            {
                symbolVal = await CopySelectedFilesForDatabaseAsync(FileColumnKind.Symbol, symFiles);
            }
            else if (!string.IsNullOrWhiteSpace(SourceSymbolLibraryBox.Text))
            {
                symbolVal = SourceSymbolLibraryBox.Text.Trim();
            }

            string? model3dVal = null;
            if (_selectedFiles.TryGetValue(FileColumnKind.Model3D, out var m3dFiles) && m3dFiles.Count > 0)
            {
                model3dVal = await CopySelectedFilesForDatabaseAsync(FileColumnKind.Model3D, m3dFiles);
            }
            else if (!string.IsNullOrWhiteSpace(Model3DFileBox.Text))
            {
                model3dVal = Model3DFileBox.Text.Trim();
            }

            if (string.IsNullOrWhiteSpace(footprintVal) && string.IsNullOrWhiteSpace(symbolVal) && string.IsNullOrWhiteSpace(model3dVal))
            {
                MessageBox.Show(this, "未检测到任何需要更新的图档文件（封装、符号或 3D 模型）。请先拖拽压缩包或手动指定文件。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            await _databaseService.UpdateComponentDrawingsAsync(
                _settings,
                _selectedDrawingComponent.TableName,
                _selectedDrawingComponent.PartNumber,
                symbolVal,
                footprintVal,
                model3dVal
            );

            MessageBox.Show(this, $"物料 {_selectedDrawingComponent.PartNumber} 图档维护成功！\r\n\r\n已分发文件至 SMB 库并更新数据库：\r\n- 原理图符号: {symbolVal ?? "(未变动)"}\r\n- PCB封装: {footprintVal ?? "(未变动)"}\r\n- 3D模型: {model3dVal ?? "(未变动)"}", "图档更新成功", MessageBoxButton.OK, MessageBoxImage.Information);

            IntakeStatusBadge.Text = $"状态：物料 {_selectedDrawingComponent.PartNumber} 图档已更新完毕！";
            SetStatus($"物料 {_selectedDrawingComponent.PartNumber} 图档更新成功。");

            await LoadDrawingsTargetListAsync();
            await LoadAllComponentsAsync();
            ClearSelectedFiles();
        }
        catch (OperationCanceledException)
        {
            SetStatus("图档提交已取消。");
        }
        catch (Exception ex)
        {
            ShowError("提交图档更新失败：" + ex.Message);
        }
    }

    #endregion

    #region 文件资产拖拽解析与分发 (Asset Intake & ZIP Parsing)

    private void ArchiveDropZone_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private async void ArchiveDropZone_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        var files = (string[]?)e.Data.GetData(DataFormats.FileDrop);
        if (files == null || files.Length == 0) return;

        if (files.Length == 1 && string.Equals(Path.GetExtension(files[0]), ".zip", StringComparison.OrdinalIgnoreCase))
        {
            await ProcessZipArchiveAsync(files[0]);
        }
        else
        {
            ProcessMultipleDroppedFiles(files);
        }
    }

    private async void BrowseZipArchive_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "压缩资产包 (*.zip)|*.zip|所有文件 (*.*)|*.*",
            Title = "选择 UltraLibrarian 或 SnapEDA 导出的 ZIP 压缩包"
        };

        if (dialog.ShowDialog() == true)
        {
            await ProcessZipArchiveAsync(dialog.FileName);
        }
    }

    private async Task ProcessZipArchiveAsync(string zipPath)
    {
        try
        {
            SetStatus($"正在解压并智能识别资产包：{Path.GetFileName(zipPath)} ...");
            var intake = await _archiveExtractorService.ExtractAndClassifyAsync(zipPath);

            ClearSelectedFiles();
            SetSelectedFiles(FileColumnKind.Footprint, intake.FootprintFiles);
            SetSelectedFiles(FileColumnKind.Symbol, intake.SymbolFiles);
            SetSelectedFiles(FileColumnKind.Model3D, intake.Model3DFiles);
            SetSelectedFiles(FileColumnKind.Pin, intake.PinFiles);

            if (intake.SymbolFiles.Count > 0)
            {
                _sourceSymbolLibraryPath = intake.SymbolFiles[0];
                SourceSymbolLibraryBox.Text = _sourceSymbolLibraryPath;
            }

            FootprintFilesBox.Text = string.Join("; ", intake.FootprintFiles);
            Model3DFileBox.Text = string.Join("; ", intake.Model3DFiles);
            PinFileBox.Text = string.Join("; ", intake.PinFiles);

            ApplySelectedFilesToForm();

            var summary = new StringBuilder();
            summary.Append($"已识别资产包 [{intake.SourceFormat}]：");
            if (intake.FootprintFiles.Count > 0) summary.Append($" 封装 {intake.FootprintFiles.Count} 个;");
            if (intake.SymbolFiles.Count > 0) summary.Append($" 符号库 {intake.SymbolFiles.Count} 个;");
            if (intake.Model3DFiles.Count > 0) summary.Append($" 3D模型 {intake.Model3DFiles.Count} 个;");
            if (intake.PinFiles.Count > 0) summary.Append($" 焊盘引脚 {intake.PinFiles.Count} 个;");

            IntakeStatusBadge.Text = summary.ToString();
            SetStatus(summary.ToString());

            if (_selectedDrawingComponent != null)
            {
                MessageBox.Show(this, $"{summary}\r\n\r\n已就绪，点击右下方【提交并更新物料图档】即可完成更新！", "资产包就绪", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            ShowError("解析压缩包失败：" + ex.Message);
        }
    }

    private void ProcessMultipleDroppedFiles(string[] files)
    {
        var footprints = new List<string>();
        var symbols = new List<string>();
        var models = new List<string>();
        var pins = new List<string>();

        foreach (var file in files)
        {
            var ext = Path.GetExtension(file).ToLowerInvariant();
            if (ext is ".dra" or ".psm" or ".fsm" or ".osm" or ".ssm" or ".bsm") footprints.Add(file);
            else if (ext is ".olb" or ".edn") symbols.Add(file);
            else if (ext is ".step" or ".stp") models.Add(file);
            else if (ext is ".pad") pins.Add(file);
        }

        if (footprints.Count > 0) SetSelectedFiles(FileColumnKind.Footprint, footprints);
        if (symbols.Count > 0) SetSelectedFiles(FileColumnKind.Symbol, symbols);
        if (models.Count > 0) SetSelectedFiles(FileColumnKind.Model3D, models);
        if (pins.Count > 0) SetSelectedFiles(FileColumnKind.Pin, pins);

        FootprintFilesBox.Text = string.Join("; ", footprints);
        SourceSymbolLibraryBox.Text = string.Join("; ", symbols);
        Model3DFileBox.Text = string.Join("; ", models);
        PinFileBox.Text = string.Join("; ", pins);

        ApplySelectedFilesToForm();
        IntakeStatusBadge.Text = $"手动拖入文件已归类：封装 {footprints.Count}，符号 {symbols.Count}，3D {models.Count}，引脚 {pins.Count}";
    }

    private void BrowseFootprintFiles_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Multiselect = true,
            Filter = "封装文件 (*.dra;*.psm)|*.dra;*.psm|所有文件 (*.*)|*.*",
            Title = "选择封装文件"
        };
        if (dialog.ShowDialog() == true)
        {
            SetSelectedFiles(FileColumnKind.Footprint, [.. dialog.FileNames]);
            FootprintFilesBox.Text = string.Join("; ", dialog.FileNames);
            ApplySelectedFilesToForm();
        }
    }

    private void BrowseSourceSymbolLibrary_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "符号库文件 (*.olb)|*.olb|所有文件 (*.*)|*.*",
            Title = "选择源符号库"
        };
        if (dialog.ShowDialog() == true)
        {
            _sourceSymbolLibraryPath = dialog.FileName;
            SourceSymbolLibraryBox.Text = dialog.FileName;
            SetSelectedFiles(FileColumnKind.Symbol, [dialog.FileName]);
            ApplySelectedFilesToForm();
        }
    }

    private void OpenSymbolLibraryFolders_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_sourceSymbolLibraryPath))
        {
            var dir = Path.GetDirectoryName(_sourceSymbolLibraryPath);
            if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
            {
                OpenFolder(dir);
                return;
            }
        }
        if (!string.IsNullOrWhiteSpace(_settings.SymbolLibraryPath) && Directory.Exists(_settings.SymbolLibraryPath))
        {
            OpenFolder(_settings.SymbolLibraryPath);
            return;
        }
        MessageBox.Show(this, "暂无可打开的符号库文件夹路径，请先在“设置”中配置符号库路径或选择源符号库。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private static void OpenFolder(string folderPath)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = folderPath,
            UseShellExecute = true
        });
    }

    private void BrowseModel3DFile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "3D 模型文件 (*.step;*.stp)|*.step;*.stp|所有文件 (*.*)|*.*",
            Title = "选择 3D 模型文件"
        };
        if (dialog.ShowDialog() == true)
        {
            SetSelectedFiles(FileColumnKind.Model3D, [dialog.FileName]);
            Model3DFileBox.Text = dialog.FileName;
            ApplySelectedFilesToForm();
        }
    }

    private void BrowsePinFile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Multiselect = true,
            Filter = "引脚/焊盘文件 (*.pad)|*.pad|所有文件 (*.*)|*.*",
            Title = "选择焊盘文件"
        };
        if (dialog.ShowDialog() == true)
        {
            SetSelectedFiles(FileColumnKind.Pin, [.. dialog.FileNames]);
            PinFileBox.Text = string.Join("; ", dialog.FileNames);
        }
    }

    #endregion

    #region 核心动态表单与标准件模式 (Dynamic Form & Standard Rules)

    private async void LoadColumns_Click(object sender, RoutedEventArgs e)
    {
        await LoadSelectedTableAsync();
    }

    private async void TableComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TableComboBox.SelectedItem is not null)
        {
            await LoadSelectedTableAsync();
            UpdateStandardPartModeState();
        }
    }

    private void chkStandardPartMode_Checked(object sender, RoutedEventArgs e)
    {
        ApplyStandardPartMode(true);
    }

    private void chkStandardPartMode_Unchecked(object sender, RoutedEventArgs e)
    {
        ApplyStandardPartMode(false);
    }

    private void UpdateStandardPartModeState()
    {
        var tableName = GetSelectedTableName();
        var rule = _settings.StandardPartRules.FirstOrDefault(r => string.Equals(r.TableName, tableName, StringComparison.OrdinalIgnoreCase));
        if (rule != null)
        {
            chkStandardPartMode.IsChecked = true;
        }
        else
        {
            chkStandardPartMode.IsChecked = false;
        }
    }

    private void ApplyStandardPartMode(bool enable)
    {
        var tableName = GetSelectedTableName();
        var rule = _settings.StandardPartRules.FirstOrDefault(r => string.Equals(r.TableName, tableName, StringComparison.OrdinalIgnoreCase));

        var symCol = _databaseService.FindSymbolColumn(_columns, _settings);
        if (symCol != null)
        {
            if (enable && rule != null)
            {
                SetInputValue(symCol, rule.SymbolTemplate);
            }
        }

        var refCol = _columns.FirstOrDefault(c => string.Equals(c.Name, "Part_Reference", StringComparison.OrdinalIgnoreCase))?.Name;
        if (refCol != null && enable && rule != null && !string.IsNullOrWhiteSpace(rule.DefaultReference))
        {
            SetInputValue(refCol, rule.DefaultReference);
        }
    }

    private void ApplyStandardPackageDefaults(string packageCode)
    {
        var tableName = GetSelectedTableName();
        var rule = _settings.StandardPartRules.FirstOrDefault(r => string.Equals(r.TableName, tableName, StringComparison.OrdinalIgnoreCase));
        if (rule == null) return;

        var cleanPkg = packageCode.Trim().ToUpperInvariant();
        var footCol = _databaseService.FindFootprintColumn(_columns, _settings);
        if (footCol != null)
        {
            var fpName = (!string.IsNullOrWhiteSpace(rule.FootprintPrefix) ? rule.FootprintPrefix : "") + cleanPkg;
            SetInputValue(footCol, fpName);
        }

        var m3dCol = _databaseService.FindModel3DColumn(_columns, _settings);
        if (m3dCol != null)
        {
            var m3dName = (!string.IsNullOrWhiteSpace(rule.Model3DPrefix) ? rule.Model3DPrefix : "") + cleanPkg + ".step";
            SetInputValue(m3dCol, m3dName);
        }
    }

    private async void GenerateNumber_Click(object sender, RoutedEventArgs e)
    {
        var tableName = GetSelectedTableName();
        if (string.IsNullOrWhiteSpace(tableName))
        {
            MessageBox.Show(this, "请先选择表。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        await EnsureColumnsLoadedAsync();
        var partNumberColumn = _databaseService.FindPartNumberColumn(_columns, _settings);
        if (partNumberColumn is null || !_inputs.ContainsKey(partNumberColumn))
        {
            MessageBox.Show(this, "当前表没有识别到可用的 Part Number 列。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var nextId = await _databaseService.GetNextAutoIncrementIdAsync(_settings, tableName);
        var currentForm = GetCurrentFormValues();
        var generatedNumber = _partNumberGeneratorService.Generate(_settings, tableName, nextId, currentForm);
        SetInputValue(partNumberColumn, generatedNumber);
        SetStatus($"已生成新物料号: {generatedNumber}");
    }

    private async Task EnsurePartNumberValueAsync(string tableName)
    {
        var partNumberColumn = _databaseService.FindPartNumberColumn(_columns, _settings);
        if (partNumberColumn is not null && _inputs.ContainsKey(partNumberColumn) && string.IsNullOrWhiteSpace(GetInputValue(partNumberColumn)))
        {
            var nextId = await _databaseService.GetNextAutoIncrementIdAsync(_settings, tableName);
            var currentForm = GetCurrentFormValues();
            SetInputValue(partNumberColumn, _partNumberGeneratorService.Generate(_settings, tableName, nextId, currentForm));
        }
    }

    private void LoadTargetSymbolLibraries()
    {
        // Target symbol libraries loaded if configured
    }

    private async Task LoadSelectedTableAsync()
    {
        var tableName = GetSelectedTableName();
        if (string.IsNullOrWhiteSpace(tableName)) return;

        try
        {
            _columns = await _databaseService.GetColumnsAsync(_settings, tableName);
            _loadedTableName = tableName;
            BuildDynamicForm();
            ApplySelectedFilesToForm();
            await GenerateInitialPartNumberAsync(tableName);
            SetStatus($"已生成表 {tableName} 的输入表单（共 {_columns.Count} 列）。");
        }
        catch (Exception ex)
        {
            ShowError("加载表结构失败：" + ex.Message);
        }
    }

    private async Task EnsureColumnsLoadedAsync()
    {
        var tableName = GetSelectedTableName();
        if (_columns.Count == 0 || !string.Equals(_loadedTableName, tableName, StringComparison.OrdinalIgnoreCase))
        {
            await LoadSelectedTableAsync();
        }
    }

    private async Task GenerateInitialPartNumberAsync(string tableName)
    {
        var partNumberColumn = _databaseService.FindPartNumberColumn(_columns, _settings);
        if (partNumberColumn is not null && _inputs.ContainsKey(partNumberColumn) && string.IsNullOrWhiteSpace(GetInputValue(partNumberColumn)))
        {
            var nextId = await _databaseService.GetNextAutoIncrementIdAsync(_settings, tableName);
            var currentForm = GetCurrentFormValues();
            SetInputValue(partNumberColumn, _partNumberGeneratorService.Generate(_settings, tableName, nextId, currentForm));
        }
    }

    private void BuildDynamicForm()
    {
        FormPanel.Children.Clear();
        _inputs.Clear();
        _symbolLibraryInputs.Clear();

        foreach (var column in _columns.Where(c => !c.IsAutoIncrement && !c.IsGenerated))
        {
            var itemPanel = new DockPanel { Margin = new Thickness(0, 0, 0, 6), LastChildFill = true };
            var label = new TextBlock
            {
                Text = column.Name + (column.IsNullable ? "" : " *"),
                Width = 110,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = column.IsNullable ? new SolidColorBrush(Color.FromRgb(51, 65, 85)) : new SolidColorBrush(Color.FromRgb(220, 38, 38)),
                FontWeight = column.IsNullable ? FontWeights.Normal : FontWeights.SemiBold
            };
            DockPanel.SetDock(label, Dock.Left);
            itemPanel.Children.Add(label);

            var textBox = new TextBox
            {
                Height = 26,
                VerticalContentAlignment = VerticalAlignment.Center,
                ToolTip = $"数据库字段: {column.Name} ({column.DataType})"
            };
            _inputs[column.Name] = textBox;
            itemPanel.Children.Add(textBox);

            FormPanel.Children.Add(itemPanel);
        }
    }

    private string? GetInputValue(string columnName)
    {
        if (_inputs.TryGetValue(columnName, out var control) && control is TextBox tb)
        {
            return tb.Text;
        }
        return null;
    }

    private void SetInputValue(string columnName, string? value)
    {
        if (_inputs.TryGetValue(columnName, out var control) && control is TextBox tb)
        {
            tb.Text = value ?? string.Empty;
        }
    }

    private void ClearDynamicForm()
    {
        foreach (var control in _inputs.Values)
        {
            if (control is TextBox tb) tb.Text = string.Empty;
        }
    }

    private void ResetEditMode()
    {
        _editingKeyColumn = null;
        _editingKeyValue = null;
    }

    private void ClearSelectedFiles()
    {
        _selectedFiles.Clear();
        _sourceSymbolLibraryPath = null;
        FootprintFilesBox.Text = string.Empty;
        SourceSymbolLibraryBox.Text = string.Empty;
        Model3DFileBox.Text = string.Empty;
        PinFileBox.Text = string.Empty;
        IntakeStatusBadge.Text = "状态：等待资产包导入（未关联文件）";
    }

    private void SetSelectedFiles(FileColumnKind kind, List<string> files)
    {
        _selectedFiles[kind] = files;
    }

    private void ApplySelectedFilesToForm()
    {
        foreach (var (kind, files) in _selectedFiles)
        {
            string? targetCol = kind switch
            {
                FileColumnKind.Footprint => _databaseService.FindFootprintColumn(_columns, _settings),
                FileColumnKind.Symbol => _databaseService.FindSymbolColumn(_columns, _settings),
                FileColumnKind.Model3D => _databaseService.FindModel3DColumn(_columns, _settings),
                _ => null
            };

            if (targetCol != null && files.Count > 0)
            {
                SetInputValue(targetCol, Path.GetFileNameWithoutExtension(files[0]));
            }
        }
    }

    private async Task<string> CopySelectedFilesForDatabaseAsync(FileColumnKind kind, IReadOnlyList<string> files)
    {
        var copied = await _fileLibraryService.CopyManyToLibraryAsync(_settings, kind, files, ResolveFileConflict);
        var successful = copied.Where(r => !string.IsNullOrWhiteSpace(r.DestinationPath)).Select(r => r.DestinationPath!).ToList();

        if (kind == FileColumnKind.Footprint && files.Count > 0)
        {
            return Path.GetFileNameWithoutExtension(files[0]);
        }

        return string.Join(";", successful);
    }

    private FileConflictAction ResolveFileConflict(string targetPath)
    {
        var result = MessageBox.Show(this, $"文件已存在：{targetPath}\r\n是否覆盖已有文件？\r\n[是] 覆盖，[否] 自动重命名，[取消] 跳过此文件", "文件冲突", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
        return result switch
        {
            MessageBoxResult.Yes => FileConflictAction.Overwrite,
            MessageBoxResult.No => FileConflictAction.Rename,
            _ => FileConflictAction.Skip
        };
    }

    private string GetSelectedTableName()
    {
        return TableComboBox.SelectedItem?.ToString() ?? string.Empty;
    }

    private Dictionary<string, string?> GetCurrentFormValues()
    {
        var dict = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (col, ctl) in _inputs)
        {
            if (ctl is TextBox tb) dict[col] = tb.Text;
        }
        return dict;
    }

    private void SetStatus(string message)
    {
        StatusText.Text = message;
    }

    private void ShowError(string message)
    {
        StatusText.Text = "错误: " + message;
        MessageBox.Show(this, message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    #endregion

    #region Tab 3: 样品库存管理 (Sample Inventory Management)

    private void InitSampleLocations()
    {
        SampleLocationComboBox.ItemsSource = _settings.SampleLocations;
        if (_settings.SampleLocations.Count > 0)
        {
            SampleLocationComboBox.SelectedIndex = 0;
        }
    }

    private async Task LoadSampleInventoriesAsync(string? keyword = null)
    {
        try
        {
            var list = await _databaseService.QuerySampleInventoriesAsync(_settings, keyword);
            SampleInventoryGrid.ItemsSource = list;
            SetStatus($"已载入 {list.Count} 条样品库存记录。");
        }
        catch (Exception ex)
        {
            ShowError("加载样品库存失败：" + ex.Message);
        }
    }

    private async void SampleSearch_Click(object sender, RoutedEventArgs e)
    {
        var kw = SampleSearchBox.Text.Trim();
        await LoadSampleInventoriesAsync(string.IsNullOrWhiteSpace(kw) ? null : kw);
    }

    private async void SampleShowAll_Click(object sender, RoutedEventArgs e)
    {
        SampleSearchBox.Text = string.Empty;
        await LoadSampleInventoriesAsync();
    }

    private void SampleSearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            SampleSearch_Click(sender, e);
        }
    }

    private async void SampleInventoryGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var item = SampleInventoryGrid.SelectedItem as SampleInventoryItem;
        if (item == null)
        {
            SelectedSamplePartLabel.Text = "未选中（请先在上方列表点选物料）";
            SampleTransactionsGrid.ItemsSource = null;
            return;
        }

        SelectedSamplePartLabel.Text = $"{item.PartNumber} ({item.ManufacturerPartNumber} {item.Value}) | 在库: {item.QuantityOnHand} | 库位: {item.Location}";

        if (!string.IsNullOrWhiteSpace(item.Location))
        {
            SampleLocationComboBox.Text = item.Location;
        }

        await LoadSampleTransactionsForPartAsync(item.PartNumber);
    }

    private async Task LoadSampleTransactionsForPartAsync(string partNumber)
    {
        try
        {
            var txs = await _databaseService.QuerySampleTransactionsAsync(_settings, partNumber);
            SampleTransactionsGrid.ItemsSource = txs;
        }
        catch (Exception ex)
        {
            ShowError("加载物料出入库履历失败：" + ex.Message);
        }
    }

    private void SampleOperationType_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded || BtnExecuteSample == null) return;

        if (rbOpCheckIn.IsChecked == true)
        {
            BtnExecuteSample.Content = "📥 确认登记入库";
            BtnExecuteSample.Background = new SolidColorBrush(Color.FromRgb(37, 99, 235));
        }
        else if (rbOpConsume.IsChecked == true)
        {
            BtnExecuteSample.Content = "📤 确认领用出库";
            BtnExecuteSample.Background = new SolidColorBrush(Color.FromRgb(16, 185, 129));
        }
        else if (rbOpBorrow.IsChecked == true)
        {
            BtnExecuteSample.Content = "⏱️ 确认借出登记";
            BtnExecuteSample.Background = new SolidColorBrush(Color.FromRgb(245, 158, 11));
        }
        else if (rbOpReturn.IsChecked == true)
        {
            BtnExecuteSample.Content = "🔄 确认样品归还";
            BtnExecuteSample.Background = new SolidColorBrush(Color.FromRgb(99, 102, 241));
        }
        else if (rbOpScrap.IsChecked == true)
        {
            BtnExecuteSample.Content = "🗑️ 确认损坏报废";
            BtnExecuteSample.Background = new SolidColorBrush(Color.FromRgb(220, 38, 38));
        }
    }

    private async void ExecuteSampleOperation_Click(object sender, RoutedEventArgs e)
    {
        var selectedItem = SampleInventoryGrid.SelectedItem as SampleInventoryItem;
        if (selectedItem == null)
        {
            MessageBox.Show(this, "请先在上方库存列表中选中需要变动的元器件物料！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!int.TryParse(SampleQuantityBox.Text.Trim(), out var qty) || qty <= 0)
        {
            MessageBox.Show(this, "变动数量必须为大于 0 的有效整数。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var location = SampleLocationComboBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(location))
        {
            MessageBox.Show(this, "请指定或选择物料存放库位。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var opPerson = SampleOperatorBox.Text.Trim();
        var project = SampleProjectBox.Text.Trim();
        var remark = SampleRemarkBox.Text.Trim();

        var opType = "CheckIn";
        if (rbOpConsume.IsChecked == true) opType = "Consume";
        else if (rbOpBorrow.IsChecked == true) opType = "Borrow";
        else if (rbOpReturn.IsChecked == true) opType = "Return";
        else if (rbOpScrap.IsChecked == true) opType = "Scrap";

        if ((opType == "Consume" || opType == "Borrow") && string.IsNullOrWhiteSpace(opPerson))
        {
            MessageBox.Show(this, "领用或借出操作必须填写【经办/领用人】！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (opType != "CheckIn" && opType != "Return" && selectedItem.QuantityOnHand < qty)
        {
            MessageBox.Show(this, $"库存不足！当前库位可用库存仅为 {selectedItem.QuantityOnHand}，无法出库/消耗 {qty} 件。", "库存不足", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var tx = new SampleTransactionItem
            {
                PartNumber = selectedItem.PartNumber,
                Location = location,
                TransactionType = opType,
                Quantity = qty,
                Operator = opPerson,
                ProjectName = project,
                Remark = remark,
                CreatedAt = DateTime.Now
            };

            await _databaseService.ExecuteSampleTransactionAsync(_settings, tx);
            MessageBox.Show(this, $"样品变动业务 [{BtnExecuteSample.Content}] 执行成功！", "变动成功", MessageBoxButton.OK, MessageBoxImage.Information);

            await LoadSampleInventoriesAsync(SampleSearchBox.Text.Trim());
            await LoadSampleTransactionsForPartAsync(selectedItem.PartNumber);
        }
        catch (Exception ex)
        {
            ShowError("执行样品操作失败：" + ex.Message);
        }
    }

    #endregion
}
