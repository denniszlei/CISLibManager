using System.Diagnostics;
using System.IO;
using System.Reflection;
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

        SetStatus("设置已加载。请先读取表并生成表单，或拖入资产包。");
    }

    private static string GetWindowTitleWithVersion()
    {
        const string baseTitle = "OrCAD CIS 智能库管理器";
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        return version is null ? baseTitle : $"{baseTitle} v{version.Major}.{version.Minor}.{version.Build}";
    }

    private async void Settings_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SettingsWindow(_settings) { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            _settings = dialog.Settings;
            await _settingsService.SaveAsync(_settings);
            LoadTargetSymbolLibraries();
            ApplySelectedFilesToForm();
            SetStatus("设置已保存。");
        }
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new AboutWindow { Owner = this };
        dialog.ShowDialog();
    }

    private async void TestConnection_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await _databaseService.TestConnectionAsync(_settings);
            SetStatus("MariaDB 连接成功。");
        }
        catch (Exception ex)
        {
            ShowError("连接失败：" + ex.Message);
        }
    }

    #region 区域 A: 资产包导入与拖拽 (Asset Intake Zone)

    private void ArchiveDropZone_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }
    }

    private async void ArchiveDropZone_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        var files = (string[]?)e.Data.GetData(DataFormats.FileDrop);
        if (files == null || files.Length == 0) return;

        var zipFile = files.FirstOrDefault(f => string.Equals(Path.GetExtension(f), ".zip", StringComparison.OrdinalIgnoreCase));
        if (zipFile != null)
        {
            await ProcessZipArchiveAsync(zipFile);
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
            Title = "选择 Cadence 库 ZIP 压缩包 (UltraLibrarian / SnapEDA)",
            Filter = "Cadence 压缩包 (*.zip)|*.zip|所有文件 (*.*)|*.*",
            Multiselect = false
        };

        if (dialog.ShowDialog(this) == true)
        {
            await ProcessZipArchiveAsync(dialog.FileName);
        }
    }

    private async Task ProcessZipArchiveAsync(string zipPath)
    {
        try
        {
            SetStatus($"正在解包并提纯资产：{Path.GetFileName(zipPath)} ...");
            var result = await _archiveExtractorService.ExtractZipAsync(zipPath);

            ClearSelectedFiles();

            if (result.FootprintFiles.Count > 0)
            {
                SetSelectedFiles(FileColumnKind.Footprint, result.FootprintFiles);
                FootprintFilesBox.Text = string.Join("; ", result.FootprintFiles.Select(Path.GetFileName));
            }

            if (result.SymbolFiles.Count > 0)
            {
                _sourceSymbolLibraryPath = result.SymbolFiles[0];
                SourceSymbolLibraryBox.Text = _sourceSymbolLibraryPath;
                PopulateSymbolNamesFromLibrary(_sourceSymbolLibraryPath);
                SetSelectedFiles(FileColumnKind.Symbol, result.SymbolFiles);
            }

            if (result.Model3DFiles.Count > 0)
            {
                SetSelectedFiles(FileColumnKind.Model3D, result.Model3DFiles);
                Model3DFileBox.Text = string.Join("; ", result.Model3DFiles.Select(Path.GetFileName));
            }

            if (result.PinFiles.Count > 0)
            {
                SetSelectedFiles(FileColumnKind.Pin, result.PinFiles);
                PinFileBox.Text = string.Join("; ", result.PinFiles.Select(Path.GetFileName));
            }

            IntakeStatusBadge.Text = $"已解包：{Path.GetFileName(zipPath)} ({result.SummaryText})";
            IntakeStatusBadge.Foreground = new SolidColorBrush(Color.FromRgb(37, 99, 235));

            if (!string.IsNullOrWhiteSpace(result.SuggestedPartName) && string.IsNullOrWhiteSpace(OnlineSearchBox.Text))
            {
                OnlineSearchBox.Text = result.SuggestedPartName;
            }

            ApplySelectedFilesToForm();
            SetStatus($"资产包 {Path.GetFileName(zipPath)} 解析成功，文件已自动关联。");
        }
        catch (Exception ex)
        {
            ShowError("解压提纯失败：" + ex.Message);
        }
    }

    private void ProcessMultipleDroppedFiles(string[] files)
    {
        var footprints = files.Where(f => string.Equals(Path.GetExtension(f), ".psm", StringComparison.OrdinalIgnoreCase) || string.Equals(Path.GetExtension(f), ".dra", StringComparison.OrdinalIgnoreCase)).ToList();
        var symbols = files.Where(f => string.Equals(Path.GetExtension(f), ".olb", StringComparison.OrdinalIgnoreCase)).ToList();
        var models = files.Where(f => string.Equals(Path.GetExtension(f), ".step", StringComparison.OrdinalIgnoreCase) || string.Equals(Path.GetExtension(f), ".stp", StringComparison.OrdinalIgnoreCase)).ToList();
        var pins = files.Where(f => string.Equals(Path.GetExtension(f), ".pad", StringComparison.OrdinalIgnoreCase) || string.Equals(Path.GetExtension(f), ".ssm", StringComparison.OrdinalIgnoreCase)).ToList();

        if (footprints.Count > 0) { SetSelectedFiles(FileColumnKind.Footprint, footprints); FootprintFilesBox.Text = string.Join("; ", footprints.Select(Path.GetFileName)); }
        if (symbols.Count > 0) { _sourceSymbolLibraryPath = symbols[0]; SourceSymbolLibraryBox.Text = _sourceSymbolLibraryPath; PopulateSymbolNamesFromLibrary(_sourceSymbolLibraryPath); SetSelectedFiles(FileColumnKind.Symbol, symbols); }
        if (models.Count > 0) { SetSelectedFiles(FileColumnKind.Model3D, models); Model3DFileBox.Text = string.Join("; ", models.Select(Path.GetFileName)); }
        if (pins.Count > 0) { SetSelectedFiles(FileColumnKind.Pin, pins); PinFileBox.Text = string.Join("; ", pins.Select(Path.GetFileName)); }

        IntakeStatusBadge.Text = $"手动拖入：符号 {symbols.Count} 个，封装 {footprints.Count} 个，3D {models.Count} 个，焊盘 {pins.Count} 个";
        ApplySelectedFilesToForm();
        SetStatus("已接收拖入的离散 CAD 文件。");
    }

    #endregion

    #region 区域 B: 联网搜索与参数注入器 (Online Search & Spec Enricher)

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
            MessageBox.Show(this, "请输入需要检索的元器件型号或规格描述（如 LM358DR 或 0402 10k 1%）。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            OnlineSearchButton.IsEnabled = false;
            SetStatus($"正在联网检索：{keyword} ...");
            IReadOnlyList<ComponentSearchResult> results;

            if (rbDigiKey.IsChecked == true)
            {
                results = await _digiKeyService.SearchAsync(_settings, keyword, limit: 15);
            }
            else
            {
                results = await _easyedaApiService.SearchAsync(keyword, limit: 15);
            }

            SearchResultsGrid.ItemsSource = results;
            SelectedResultSummary.Text = $"检索到 {results.Count} 条记录。点击列表选中条目可查看详情并应用。";
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

    private void SearchResultsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var item = SearchResultsGrid.SelectedItem as ComponentSearchResult;
        if (item == null)
        {
            ApplyOnlineResultButton.IsEnabled = false;
            return;
        }

        ApplyOnlineResultButton.IsEnabled = true;
        var statusAlert = item.IsObsolete ? "【⚠️ 停产淘汰】" : $"【{item.DisplayStatus}】";
        SelectedResultSummary.Text = $"已选中: {item.ManufacturerProductNumber} ({item.Manufacturer}) {statusAlert} | 库存: {item.Stock ?? 0} | 价格: {item.Price ?? "--"}";
        if (item.IsObsolete)
        {
            SelectedResultSummary.Foreground = new SolidColorBrush(Color.FromRgb(220, 38, 38));
        }
        else
        {
            SelectedResultSummary.Foreground = new SolidColorBrush(Color.FromRgb(22, 101, 52));
        }
    }

    private async void ApplyOnlineResultButton_Click(object sender, RoutedEventArgs e)
    {
        var item = SearchResultsGrid.SelectedItem as ComponentSearchResult;
        if (item == null) return;

        if (item.IsObsolete)
        {
            var warningText = $"【高风险停产料预警】\r\n\r\n物料 \"{item.ManufacturerProductNumber}\" 在原厂数据库中被标记为：{item.LifecycleStatus} (停产/淘汰)！\r\n\r\n在商业量产设计中录入停产料可能导致量产断供风险。是否确定仍要录入该料？";
            var res = MessageBox.Show(this, warningText, "停产料审查拦截", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (res != MessageBoxResult.Yes) return;
        }

        if (chkAutoEnrichLcsc.IsChecked == true && string.Equals(item.Source, "DigiKey", StringComparison.OrdinalIgnoreCase))
        {
            SetStatus($"正在级联立创补充物料 {item.ManufacturerProductNumber} 供应链数据...");
            var lcscResult = await _easyedaApiService.TryEnrichWithLcscAsync(item.ManufacturerProductNumber);
            if (lcscResult != null)
            {
                item.LcscPartNumber ??= lcscResult.LcscPartNumber;
                item.Price ??= lcscResult.Price;
                item.Model3DUuid ??= lcscResult.Model3DUuid;
            }
        }

        SetInputValueIfColumnMatches(item.Manufacturer, "Manufacturer", "厂商", "品牌", "Mfg");
        SetInputValueIfColumnMatches(item.ManufacturerProductNumber, "Manufacturer_Part_Number", "MPN", "原厂型号", "PartNumber", "Part_Number_Mfg");
        SetInputValueIfColumnMatches(item.Category, "Part_Type", "Type", "类别", "器件类型");
        SetInputValueIfColumnMatches(item.Description, "Description", "描述", "品名", "Desc");
        SetInputValueIfColumnMatches(item.DatasheetUrl, "Datasheet_URL", "Datasheet", "规格书", "URL");
        SetInputValueIfColumnMatches(item.Package, "Package", "封装", "Package_Type");
        SetInputValueIfColumnMatches(item.LifecycleStatus, "Lifecycle_Status", "Status", "生命周期", "状态");
        SetInputValueIfColumnMatches(item.Price, "Price_RMB", "Price", "价格", "单价");
        SetInputValueIfColumnMatches(item.LcscPartNumber, "LCSC_Part_Number", "LCSC", "立创编号", "LCSC_PN");

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

        // 若不是阻容感等参数件，默认将芯片/器件的 Value 设置为主型号 (Cadence CIS 惯例)
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

    #region 区域 C: 核心入库工作台与标准料模式 (Core Workstation)

    private async void RefreshTables_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var tables = await _databaseService.GetTablesAsync(_settings);
            TableComboBox.ItemsSource = tables;
            if (tables.Count > 0)
            {
                TableComboBox.SelectedIndex = 0;
            }
            else
            {
                ClearDynamicForm();
            }

            SetStatus($"已读取 {tables.Count} 张表。");
        }
        catch (Exception ex)
        {
            ShowError("读取表失败：" + ex.Message);
        }
    }

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
        var rule = GetStandardPartRuleForTable(tableName);
        if (rule != null)
        {
            chkStandardPartMode.IsEnabled = true;
            chkStandardPartMode.IsChecked = true;
        }
        else
        {
            chkStandardPartMode.IsChecked = false;
        }
    }

    private StandardPartRule? GetStandardPartRuleForTable(string tableName)
    {
        return _settings.StandardPartRules.FirstOrDefault(r => string.Equals(r.TableName, tableName, StringComparison.OrdinalIgnoreCase));
    }

    private void ApplyStandardPartMode(bool enable)
    {
        var tableName = GetSelectedTableName();
        var rule = GetStandardPartRuleForTable(tableName);

        if (enable)
        {
            if (rule != null)
            {
                var symbolCol = _databaseService.FindSymbolColumn(_columns, _settings);
                if (symbolCol != null && _inputs.ContainsKey(symbolCol))
                {
                    SetInputValue(symbolCol, rule.SymbolTemplate);
                    _inputs[symbolCol].IsEnabled = false;
                }

                var refCol = _columns.FirstOrDefault(c => string.Equals(c.Name, "Part_Reference", StringComparison.OrdinalIgnoreCase) || string.Equals(c.Name, "Reference", StringComparison.OrdinalIgnoreCase));
                if (refCol != null && _inputs.ContainsKey(refCol.Name))
                {
                    SetInputValue(refCol.Name, rule.DefaultReference);
                }
            }

            SetStatus("已开启标准料录入模式：共享 DISCRETE 符号与标准封装，入库时仅插入数据行，不复制物理文件。");
        }
        else
        {
            var symbolCol = _databaseService.FindSymbolColumn(_columns, _settings);
            if (symbolCol != null && _inputs.ContainsKey(symbolCol))
            {
                _inputs[symbolCol].IsEnabled = true;
            }
            SetStatus("标准料模式已关闭。");
        }
    }

    private void ApplyStandardPackageDefaults(string packageCode)
    {
        var tableName = GetSelectedTableName();
        var rule = GetStandardPartRuleForTable(tableName);
        if (rule == null) return;

        var cleanPkg = packageCode.Trim();
        foreach (var p in rule.SupportedPackages)
        {
            if (cleanPkg.Contains(p, StringComparison.OrdinalIgnoreCase))
            {
                cleanPkg = p;
                break;
            }
        }

        var footprintName = rule.FootprintPrefix + cleanPkg;
        var modelName = rule.Model3DPrefix + cleanPkg;

        var footprintCol = _databaseService.FindFootprintColumn(_columns, _settings);
        if (footprintCol != null && _inputs.ContainsKey(footprintCol))
        {
            SetInputValue(footprintCol, footprintName);
        }

        var modelCol = _databaseService.FindModel3DColumn(_columns, _settings);
        if (modelCol != null && _inputs.ContainsKey(modelCol))
        {
            SetInputValue(modelCol, modelName);
        }
    }

    private async void GenerateNumber_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await EnsureColumnsLoadedAsync();
            var tableName = GetSelectedTableName();
            var partNumberColumn = _databaseService.FindPartNumberColumn(_columns, _settings);
            if (partNumberColumn is null || !_inputs.ContainsKey(partNumberColumn))
            {
                MessageBox.Show(this, "当前表未发现可自动编号的列，请在设置中配置 Part Number 字段名。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var currentForm = GetCurrentFormValues();
            var nextId = await _databaseService.GetNextAutoIncrementIdAsync(_settings, tableName);
            var generatedPn = _partNumberGeneratorService.Generate(_settings, tableName, nextId, currentForm);

            _settings.TablePartNumberRules.TryGetValue(tableName, out var rule);
            var (isValid, errMsg) = _partNumberGeneratorService.Validate(generatedPn, rule);
            if (!isValid)
            {
                MessageBox.Show(this, errMsg, "料号格式提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            SetInputValue(partNumberColumn, generatedPn);
            SetStatus($"已生成 Part Number：{generatedPn}");
        }
        catch (Exception ex)
        {
            ShowError("生成编号失败：" + ex.Message);
        }
    }

    private Dictionary<string, string?> GetCurrentFormValues()
    {
        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (col, ctrl) in _inputs)
        {
            result[col] = GetInputValue(col);
        }
        return result;
    }

    private async void FindRecord_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await EnsureColumnsLoadedAsync();
            var partNumber = PartNumberSearchBox.Text.Trim();
            if (partNumber.Length == 0)
            {
                MessageBox.Show(this, "请输入需要查找的 Part Number。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var partNumberColumn = _databaseService.FindPartNumberColumn(_columns, _settings);
            if (partNumberColumn is null)
            {
                MessageBox.Show(this, "当前表未配置 Part Number 对应字段，无法按编号查找。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var tableName = GetSelectedTableName();
            var record = await _databaseService.FindRowByPartNumberAsync(_settings, tableName, partNumberColumn, partNumber, _columns);
            if (record is null)
            {
                MessageBox.Show(this, $"未找到 Part Number 为 {partNumber} 的记录。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _editingKeyColumn = partNumberColumn;
            _editingKeyValue = partNumber;

            foreach (var column in _columns)
            {
                if (_inputs.TryGetValue(column.Name, out var control) && record.TryGetValue(column.Name, out var value))
                {
                    if (control is TextBox textBox) textBox.Text = value ?? string.Empty;
                    else if (control is ComboBox comboBox) comboBox.Text = value ?? string.Empty;
                }
            }

            ShowRecordFiles(record);
            SetStatus($"已加载器件记录：{partNumber}（当前处于编辑模式，提交时将更新此记录）");
        }
        catch (Exception ex)
        {
            ShowError("查找记录失败：" + ex.Message);
        }
    }

    private async void Submit_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_columns.Count == 0)
            {
                MessageBox.Show(this, "请先选择表并生成表单。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var tableName = GetSelectedTableName();
            await EnsurePartNumberValueAsync(tableName);

            var partNumberColumn = _databaseService.FindPartNumberColumn(_columns, _settings);
            if (partNumberColumn is not null)
            {
                var pnValue = GetInputValue(partNumberColumn);
                _settings.TablePartNumberRules.TryGetValue(tableName, out var rule);
                var (isValid, errMsg) = _partNumberGeneratorService.Validate(pnValue ?? string.Empty, rule);
                if (!isValid)
                {
                    var proceed = MessageBox.Show(this, errMsg + "\r\n\r\n是否仍然强制提交入库？", "料号规范拦截", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (proceed != MessageBoxResult.Yes) return;
                }
            }

            if (!IsStandardPartMode && _selectedFiles.TryGetValue(FileColumnKind.Pin, out var pinFiles))
            {
                await _fileLibraryService.CopyManyToLibraryAsync(_settings, FileColumnKind.Pin, pinFiles, ResolveFileConflict);
            }

            var values = new Dictionary<string, string?>();
            foreach (var column in _columns.Where(c => !c.IsAutoIncrement && !c.IsGenerated))
            {
                var kind = GetFileColumnKind(column.Name);
                if (kind == FileColumnKind.Symbol)
                {
                    values[column.Name] = IsStandardPartMode
                        ? (GetInputValue(column.Name) ?? GetStandardSymbolValue(tableName))
                        : GetSymbolDatabaseValue(column.Name);
                }
                else if (kind != FileColumnKind.None && _selectedFiles.TryGetValue(kind, out var files) && files.Count > 0)
                {
                    values[column.Name] = IsStandardPartMode
                        ? GetInputValue(column.Name)
                        : await CopySelectedFilesForDatabaseAsync(kind, files);
                }
                else if (_inputs.ContainsKey(column.Name))
                {
                    values[column.Name] = GetInputValue(column.Name);
                }
            }

            if (!string.IsNullOrWhiteSpace(_editingKeyColumn) && !string.IsNullOrWhiteSpace(_editingKeyValue))
            {
                await _databaseService.UpdateRowAsync(_settings, tableName, values, _columns, _editingKeyColumn, _editingKeyValue);
                MessageBox.Show(this, "更新完成。", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                SetStatus("器件记录已更新。表单已清空，请重新生成表单后继续操作。");
            }
            else
            {
                await _databaseService.InsertRowAsync(_settings, tableName, values, _columns);
                MessageBox.Show(this, "提交完成。", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                SetStatus("器件已写入数据库，相关文件已入库。表单已清空，请重新生成表单后继续提交。");
            }

            ClearDynamicForm();
            ClearSelectedFiles();
            ResetEditMode();
        }
        catch (OperationCanceledException)
        {
            SetStatus("提交已取消。");
        }
        catch (Exception ex)
        {
            ShowError("提交失败：" + ex.Message);
        }
    }

    private string GetStandardSymbolValue(string tableName)
    {
        var rule = GetStandardPartRuleForTable(tableName);
        return rule?.SymbolTemplate ?? @"DISCRETE\RES";
    }

    private async Task EnsurePartNumberValueAsync(string tableName)
    {
        var partNumberColumn = _databaseService.FindPartNumberColumn(_columns, _settings);
        if (partNumberColumn is not null && string.IsNullOrWhiteSpace(GetInputValue(partNumberColumn)))
        {
            var nextId = await _databaseService.GetNextAutoIncrementIdAsync(_settings, tableName);
            var currentForm = GetCurrentFormValues();
            SetInputValue(partNumberColumn, _partNumberGeneratorService.Generate(_settings, tableName, nextId, currentForm));
        }
    }

    private async Task<string?> CopySelectedFilesForDatabaseAsync(FileColumnKind kind, IReadOnlyList<string> files)
    {
        var databaseSourceFiles = GetDatabaseSourceFiles(files, kind).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var copiedNames = new List<string>();

        foreach (var file in files)
        {
            var copyResult = await _fileLibraryService.CopyToLibraryAsync(_settings, kind, file, ResolveFileConflict);
            if (databaseSourceFiles.Contains(file) && copyResult.StoredValue is not null)
            {
                copiedNames.Add(copyResult.StoredValue);
            }
        }

        if (copiedNames.Count == 0)
        {
            return null;
        }

        return string.Join(",", copiedNames.Select(RemoveFileExtension));
    }

    private void BrowseFootprintFiles_Click(object sender, RoutedEventArgs e)
    {
        var files = BrowseFiles("选择 Allegro 封装与图元文件", "Allegro 封装文件 (*.psm;*.dra)|*.psm;*.dra|所有文件 (*.*)|*.*", multiselect: true);
        if (files.Count == 0) return;
        SetSelectedFiles(FileColumnKind.Footprint, files);
        FootprintFilesBox.Text = string.Join("; ", files.Select(Path.GetFileName));
        ApplySelectedFilesToForm();
    }

    private void BrowseSourceSymbolLibrary_Click(object sender, RoutedEventArgs e)
    {
        var files = BrowseFiles("选择 OrCAD 符号库", "OrCAD 符号库 (*.olb)|*.olb|所有文件 (*.*)|*.*", multiselect: false);
        if (files.Count == 0) return;
        _sourceSymbolLibraryPath = files[0];
        SourceSymbolLibraryBox.Text = _sourceSymbolLibraryPath;
        PopulateSymbolNamesFromLibrary(_sourceSymbolLibraryPath);
        SetSelectedFiles(FileColumnKind.Symbol, files);
        ApplySelectedFilesToForm();
    }

    private void PopulateSymbolNamesFromLibrary(string libraryPath)
    {
        var defaultSymbol = Path.GetFileNameWithoutExtension(libraryPath);
        var targetSymbolLibraryBox = FindName("TargetSymbolLibraryBox") as ComboBox;
        var targetLibraryName = targetSymbolLibraryBox?.SelectedItem?.ToString() ?? defaultSymbol;

        foreach (var comboBox in _symbolLibraryInputs.Values)
        {
            comboBox.ItemsSource = new[] { defaultSymbol };
            comboBox.SelectedIndex = 0;
            ApplySymbolValueToForm(comboBox, targetLibraryName, defaultSymbol);
        }
    }

    private void OpenSymbolLibraryFolders_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_sourceSymbolLibraryPath) && File.Exists(_sourceSymbolLibraryPath))
        {
            var dir = Path.GetDirectoryName(_sourceSymbolLibraryPath);
            if (!string.IsNullOrWhiteSpace(dir)) OpenFolder(dir);
        }
        else if (!string.IsNullOrWhiteSpace(_settings.SymbolLibraryPath) && Directory.Exists(_settings.SymbolLibraryPath))
        {
            OpenFolder(_settings.SymbolLibraryPath);
        }
        else
        {
            MessageBox.Show(this, "符号库文件夹不存在，请检查设置。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void LoadTargetSymbolLibraries()
    {
        // 保持空实现或刷新
    }

    private static void OpenFolder(string folderPath)
    {
        Process.Start(new ProcessStartInfo { FileName = folderPath, UseShellExecute = true });
    }

    private void BrowseModel3DFile_Click(object sender, RoutedEventArgs e)
    {
        var files = BrowseFiles("选择 3D 模型文件", "3D STEP 模型 (*.step;*.stp)|*.step;*.stp|所有文件 (*.*)|*.*", multiselect: false);
        if (files.Count == 0) return;
        SetSelectedFiles(FileColumnKind.Model3D, files);
        Model3DFileBox.Text = Path.GetFileName(files[0]);
        ApplySelectedFilesToForm();
    }

    private void BrowsePinFile_Click(object sender, RoutedEventArgs e)
    {
        var files = BrowseFiles("选择焊盘与引脚文件", "Allegro 焊盘文件 (*.pad;*.osm;*.bsm;*.fsm;*.ssm)|*.pad;*.osm;*.bsm;*.fsm;*.ssm|所有文件 (*.*)|*.*", multiselect: true);
        if (files.Count == 0) return;
        SetSelectedFiles(FileColumnKind.Pin, files);
        PinFileBox.Text = string.Join("; ", files.Select(Path.GetFileName));
    }

    private static List<string> BrowseFiles(string title, string filter, bool multiselect)
    {
        var dialog = new OpenFileDialog { Title = title, Filter = filter, Multiselect = multiselect };
        return dialog.ShowDialog() == true ? dialog.FileNames.ToList() : [];
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
            var itemPanel = new DockPanel { Margin = new Thickness(0, 0, 0, 8), LastChildFill = true };
            var label = new TextBlock
            {
                Text = column.Name,
                Width = 140,
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = column.Comment
            };
            DockPanel.SetDock(label, Dock.Left);
            itemPanel.Children.Add(label);

            var kind = GetFileColumnKind(column.Name);
            Control control;

            if (kind == FileColumnKind.Symbol)
            {
                var comboBox = new ComboBox { IsEditable = true };
                control = comboBox;
                _symbolLibraryInputs[column.Name] = comboBox;
            }
            else
            {
                var textBox = new TextBox();
                control = textBox;
            }

            _inputs[column.Name] = control;
            itemPanel.Children.Add(control);
            FormPanel.Children.Add(itemPanel);
        }
    }

    private string? GetInputValue(string columnName)
    {
        if (!_inputs.TryGetValue(columnName, out var control)) return null;
        if (control is TextBox textBox) return textBox.Text.Trim();
        if (control is ComboBox comboBox) return comboBox.Text.Trim();
        return null;
    }

    private void SetInputValue(string columnName, string? value)
    {
        if (!_inputs.TryGetValue(columnName, out var control)) return;
        if (control is TextBox textBox) textBox.Text = value ?? string.Empty;
        else if (control is ComboBox comboBox) comboBox.Text = value ?? string.Empty;
    }

    private void ClearDynamicForm()
    {
        foreach (var control in _inputs.Values)
        {
            if (control is TextBox textBox) textBox.Text = string.Empty;
            else if (control is ComboBox comboBox) comboBox.Text = string.Empty;
        }
    }

    private void ResetEditMode()
    {
        _editingKeyColumn = null;
        _editingKeyValue = null;
        ClearRecordFiles();
    }

    private void ShowRecordFiles(IReadOnlyDictionary<string, string?> record)
    {
        // 保持空实现
    }

    private void ClearSelectedFiles()
    {
        _selectedFiles.Clear();
        FootprintFilesBox.Text = string.Empty;
        SourceSymbolLibraryBox.Text = string.Empty;
        Model3DFileBox.Text = string.Empty;
        PinFileBox.Text = string.Empty;
    }

    private void ClearRecordFiles()
    {
        SymbolFilesGroupBox.Visibility = Visibility.Collapsed;
        FootprintFilesGroupBox.Visibility = Visibility.Collapsed;
        Model3DFilesGroupBox.Visibility = Visibility.Collapsed;
    }

    private void SetSelectedFiles(FileColumnKind kind, List<string> files)
    {
        _selectedFiles[kind] = files;
    }

    private void ApplySelectedFilesToForm()
    {
        if (IsStandardPartMode) return;

        foreach (var column in _columns)
        {
            var kind = GetFileColumnKind(column.Name);
            if (kind != FileColumnKind.None && _selectedFiles.TryGetValue(kind, out var files) && files.Count > 0)
            {
                ApplySelectedFileToForm(column.Name, kind, files);
            }
        }
    }

    private void ApplySelectedFileToForm(string columnName, FileColumnKind kind, IReadOnlyList<string> files)
    {
        var previewFiles = GetDatabaseSourceFiles(files, kind).ToList();
        if (previewFiles.Count == 0) return;

        if (kind == FileColumnKind.Symbol)
        {
            var libName = Path.GetFileNameWithoutExtension(previewFiles[0]);
            var symbolName = libName;
            SetInputValue(columnName, $"{libName}\\{symbolName}");
        }
        else
        {
            var value = string.Join(",", previewFiles.Select(Path.GetFileNameWithoutExtension));
            SetInputValue(columnName, value);
        }
    }

    private void ApplySymbolValueToForm(ComboBox comboBox, string targetLibraryName, string symbolName)
    {
        var cleanLibraryName = RemoveFileExtension(targetLibraryName);
        var cleanSymbolName = RemoveFileExtension(symbolName);
        comboBox.Text = $"{cleanLibraryName}\\{cleanSymbolName}";
    }

    private string? GetSymbolDatabaseValue(string columnName)
    {
        var text = GetInputValue(columnName);
        if (string.IsNullOrWhiteSpace(text)) return null;
        return text;
    }

    private static IEnumerable<string> GetDatabaseSourceFiles(IEnumerable<string> files, FileColumnKind kind)
    {
        if (kind == FileColumnKind.Footprint)
        {
            return files.Where(file => string.Equals(Path.GetExtension(file), ".psm", StringComparison.OrdinalIgnoreCase));
        }
        return files;
    }

    private static string RemoveFileExtension(string fileName)
    {
        return Path.GetFileNameWithoutExtension(fileName);
    }

    private FileColumnKind GetFileColumnKind(string columnName)
    {
        if (MatchesConfiguredColumn(columnName, _settings.FootprintColumnNames)) return FileColumnKind.Footprint;
        if (MatchesConfiguredColumn(columnName, _settings.SymbolColumnNames)) return FileColumnKind.Symbol;
        if (MatchesConfiguredColumn(columnName, _settings.Model3DColumnNames)) return FileColumnKind.Model3D;
        return FileColumnKind.None;
    }

    private static bool MatchesConfiguredColumn(string columnName, IEnumerable<string> configuredNames)
    {
        var normalized = NormalizeColumnName(columnName);
        return configuredNames.Any(c => string.Equals(NormalizeColumnName(c), normalized, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeColumnName(string name)
    {
        return name.Replace(" ", string.Empty).Replace("_", string.Empty);
    }

    private string GetSelectedTableName()
    {
        return TableComboBox.SelectedItem?.ToString() ?? string.Empty;
    }

    private FileConflictAction ResolveFileConflict(string destinationPath)
    {
        var fileName = Path.GetFileName(destinationPath);
        var conflictMsg = $"文件已存在：{fileName}\r\n\r\n是否覆盖？\r\n- 点击“是”覆盖当前文件\r\n- 点击“否”保留已有文件跳过拷贝\r\n- 点击“取消”终止本次入库";
        var result = MessageBox.Show(this, conflictMsg, "文件冲突处理", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
        return result switch
        {
            MessageBoxResult.Yes => FileConflictAction.Overwrite,
            MessageBoxResult.No => FileConflictAction.Skip,
            _ => FileConflictAction.Cancel
        };
    }

    private void SetStatus(string message)
    {
        StatusText.Text = message;
    }

    private void ShowError(string message)
    {
        SetStatus("错误：" + message);
        MessageBox.Show(this, message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
    }
    #endregion
}
