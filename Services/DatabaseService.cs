using CadenceCisLibraryManager.Models;
using MySqlConnector;

namespace CadenceCisLibraryManager.Services;

public sealed class DatabaseService
{
    public async Task<IReadOnlyList<string>> GetTablesAsync(AppSettings settings)
    {
        await using var connection = await OpenConnectionAsync(settings);
        const string sql = "select table_name from information_schema.tables where table_schema = database() and table_type = 'BASE TABLE' order by table_name";
        await using var command = new MySqlCommand(sql, connection);
        var tables = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            tables.Add(reader.GetString(0));
        }

        return tables;
    }

    public async Task<IReadOnlyList<ColumnInfo>> GetColumnsAsync(AppSettings settings, string tableName)
    {
        await using var connection = await OpenConnectionAsync(settings);
        const string sql = @"select c.column_name, c.data_type, c.is_nullable, c.column_default, c.extra, case when k.column_name is null then 0 else 1 end as is_primary_key from information_schema.columns c left join information_schema.key_column_usage k on k.table_schema = c.table_schema and k.table_name = c.table_name and k.column_name = c.column_name and k.constraint_name = 'PRIMARY' where c.table_schema = database() and c.table_name = @tableName order by c.ordinal_position";
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@tableName", tableName);
        var columns = new List<ColumnInfo>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var extra = reader.GetString(4);
            columns.Add(new ColumnInfo
            {
                Name = reader.GetString(0),
                DataType = reader.GetString(1),
                IsNullable = string.Equals(reader.GetString(2), "YES", StringComparison.OrdinalIgnoreCase),
                DefaultValue = reader.IsDBNull(3) ? null : reader.GetString(3),
                IsAutoIncrement = extra.Contains("auto_increment", StringComparison.OrdinalIgnoreCase),
                IsGenerated = extra.Contains("generated", StringComparison.OrdinalIgnoreCase),
                IsPrimaryKey = reader.GetInt32(5) == 1
            });
        }

        return columns;
    }

    public async Task<bool> TestConnectionAsync(AppSettings settings)
    {
        await using var connection = await OpenConnectionAsync(settings);
        return connection.State == System.Data.ConnectionState.Open;
    }

    public async Task<IReadOnlyList<string>> GetDistinctColumnValuesAsync(AppSettings settings, string tableName, string columnName, int maxCount = 200)
    {
        await using var connection = await OpenConnectionAsync(settings);
        var sql = $"select distinct {EscapeIdentifier(columnName)} from {EscapeIdentifier(tableName)} where {EscapeIdentifier(columnName)} is not null and trim({EscapeIdentifier(columnName)}) <> '' order by {EscapeIdentifier(columnName)} limit @maxCount";
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@maxCount", maxCount);

        var values = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (!reader.IsDBNull(0))
            {
                values.Add(reader.GetValue(0).ToString() ?? string.Empty);
            }
        }

        return values;
    }

    public string? FindPartNumberColumn(IEnumerable<ColumnInfo> columns, AppSettings settings)
    {
        return columns.Select(c => c.Name).FirstOrDefault(name => MatchesConfiguredColumn(name, settings.PartNumberColumnNames));
    }

    public string? FindSymbolColumn(IEnumerable<ColumnInfo> columns, AppSettings settings)
    {
        return columns.Select(c => c.Name).FirstOrDefault(name => MatchesConfiguredColumn(name, settings.SymbolColumnNames));
    }

    public string? FindFootprintColumn(IEnumerable<ColumnInfo> columns, AppSettings settings)
    {
        return columns.Select(c => c.Name).FirstOrDefault(name => MatchesConfiguredColumn(name, settings.FootprintColumnNames));
    }

    public string? FindModel3DColumn(IEnumerable<ColumnInfo> columns, AppSettings settings)
    {
        return columns.Select(c => c.Name).FirstOrDefault(name => MatchesConfiguredColumn(name, settings.Model3DColumnNames));
    }

    public async Task<IReadOnlyDictionary<string, string?>?> FindRowByPartNumberAsync(AppSettings settings, string tableName, string partNumberColumn, string partNumberValue, IReadOnlyList<ColumnInfo>? columns = null)
    {
        columns ??= await GetColumnsAsync(settings, tableName);
        return await GetSingleRowByColumnAsync(settings, tableName, partNumberColumn, partNumberValue, columns);
    }

    public async Task<long> GetNextAutoIncrementIdAsync(AppSettings settings, string tableName)
    {
        await using var connection = await OpenConnectionAsync(settings);
        const string sql = "select auto_increment from information_schema.tables where table_schema = database() and table_name = @tableName";
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@tableName", tableName);
        var value = await command.ExecuteScalarAsync();
        if (value is null || value == DBNull.Value)
        {
            throw new InvalidOperationException($"表 {tableName} 没有可用的 AUTO_INCREMENT ID，无法生成 Part Number。");
        }

        return Convert.ToInt64(value);
    }

    public async Task<string> GenerateNextPartNumberAsync(AppSettings settings, string tableName)

    {
        await using var connection = await OpenConnectionAsync(settings);
        const string sql = "select auto_increment from information_schema.tables where table_schema = database() and table_name = @tableName";
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@tableName", tableName);
        var value = await command.ExecuteScalarAsync();
        if (value is null || value == DBNull.Value)
        {
            throw new InvalidOperationException($"表 {tableName} 没有可用的 AUTO_INCREMENT ID，无法生成 Part Number。");
        }

        var nextId = Convert.ToInt64(value);
        var width = Math.Max(1, settings.PartNumberIdWidth);
        return GetTablePrefix(settings, tableName) + nextId.ToString(new string('0', width));
    }

    private static bool MatchesConfiguredColumn(string columnName, IEnumerable<string>? candidates)
    {
        return (candidates ?? []).Any(candidate => string.Equals(NormalizeName(candidate), NormalizeName(columnName), StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeName(string value)
    {
        return value.Replace(" ", string.Empty).Replace("_", string.Empty).Replace("-", string.Empty);
    }

    private static string GetTablePrefix(AppSettings settings, string tableName)
    {
        var configured = settings.TablePartNumberPrefixes.FirstOrDefault(pair => string.Equals(pair.Key, tableName, StringComparison.OrdinalIgnoreCase));
        return configured.Value ?? string.Empty;
    }

    public async Task InsertRowAsync(AppSettings settings, string tableName, IReadOnlyDictionary<string, string?> values, IReadOnlyList<ColumnInfo> columns)
    {
        var writableColumns = columns.Where(c => !c.IsAutoIncrement && !c.IsGenerated && values.ContainsKey(c.Name)).ToList();
        if (writableColumns.Count == 0)
        {
            throw new InvalidOperationException("没有可写入的列。");
        }

        await using var connection = await OpenConnectionAsync(settings);
        var columnList = string.Join(", ", writableColumns.Select(c => EscapeIdentifier(c.Name)));
        var parameterList = string.Join(", ", writableColumns.Select((_, index) => "@p" + index));
        var sql = $"insert into {EscapeIdentifier(tableName)} ({columnList}) values ({parameterList})";
        await using var command = new MySqlCommand(sql, connection);
        for (var i = 0; i < writableColumns.Count; i++)
        {
            var column = writableColumns[i];
            var value = values[column.Name];
            command.Parameters.AddWithValue("@p" + i, string.IsNullOrWhiteSpace(value) ? DBNull.Value : value);
        }

        await command.ExecuteNonQueryAsync();
    }

    public async Task<IReadOnlyDictionary<string, string?>?> GetSingleRowByColumnAsync(AppSettings settings, string tableName, string filterColumn, string filterValue, IReadOnlyList<ColumnInfo> columns)
    {
        await using var connection = await OpenConnectionAsync(settings);
        var selectColumns = string.Join(", ", columns.Select(c => EscapeIdentifier(c.Name)));
        var sql = $"select {selectColumns} from {EscapeIdentifier(tableName)} where {EscapeIdentifier(filterColumn)} = @filterValue limit 1";
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@filterValue", filterValue);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < columns.Count; i++)
        {
            values[columns[i].Name] = reader.IsDBNull(i) ? null : reader.GetValue(i).ToString();
        }

        return values;
    }

    public async Task UpdateRowAsync(AppSettings settings, string tableName, IReadOnlyDictionary<string, string?> values, IReadOnlyList<ColumnInfo> columns, string keyColumnName, string? keyValue)
    {
        if (string.IsNullOrWhiteSpace(keyValue))
        {
            throw new InvalidOperationException("主键值为空，无法更新记录。");
        }

        var writableColumns = columns.Where(c => !c.IsAutoIncrement && !c.IsGenerated && !c.IsPrimaryKey && values.ContainsKey(c.Name)).ToList();
        if (writableColumns.Count == 0)
        {
            throw new InvalidOperationException("没有可更新的列。");
        }

        await using var connection = await OpenConnectionAsync(settings);
        var setClause = string.Join(", ", writableColumns.Select((c, index) => $"{EscapeIdentifier(c.Name)} = @p{index}"));
        var sql = $"update {EscapeIdentifier(tableName)} set {setClause} where {EscapeIdentifier(keyColumnName)} = @keyValue";
        await using var command = new MySqlCommand(sql, connection);
        for (var i = 0; i < writableColumns.Count; i++)
        {
            var column = writableColumns[i];
            var value = values[column.Name];
            command.Parameters.AddWithValue("@p" + i, string.IsNullOrWhiteSpace(value) ? DBNull.Value : value);
        }

        command.Parameters.AddWithValue("@keyValue", keyValue);
        var affected = await command.ExecuteNonQueryAsync();
        if (affected == 0)
        {
            throw new InvalidOperationException("未找到需要更新的记录，可能已被删除。");
        }
    }

    private static async Task<MySqlConnection> OpenConnectionAsync(AppSettings settings)
    {
        var builder = new MySqlConnectionStringBuilder
        {
            Server = settings.Server,
            Port = settings.Port,
            Database = settings.Database,
            UserID = settings.UserId,
            Password = settings.Password,
            AllowUserVariables = true
        };

        var connection = new MySqlConnection(builder.ConnectionString);
        await connection.OpenAsync();
        return connection;
    }

    public async Task EnsureSampleTablesExistAsync(AppSettings settings)
    {
        await using var connection = await OpenConnectionAsync(settings);

        var ddl1 = """
        CREATE TABLE IF NOT EXISTS `sample_inventory` (
            `id` BIGINT AUTO_INCREMENT PRIMARY KEY,
            `Part_Number` VARCHAR(64) NOT NULL,
            `Location` VARCHAR(64) NOT NULL DEFAULT '默认样品柜',
            `Quantity_On_Hand` INT NOT NULL DEFAULT 0,
            `Quantity_Lent` INT NOT NULL DEFAULT 0,
            `Quantity_Total_In` INT NOT NULL DEFAULT 0,
            `Min_Alert_Qty` INT NOT NULL DEFAULT 0,
            `Distributor` VARCHAR(128) DEFAULT NULL,
            `Remark` VARCHAR(512) DEFAULT NULL,
            `Created_At` TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
            `Updated_At` TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
            UNIQUE KEY `uk_pn_loc` (`Part_Number`, `Location`),
            INDEX `idx_part_number` (`Part_Number`),
            INDEX `idx_location` (`Location`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
        """;

        var ddl2 = """
        CREATE TABLE IF NOT EXISTS `sample_transactions` (
            `id` BIGINT AUTO_INCREMENT PRIMARY KEY,
            `Part_Number` VARCHAR(64) NOT NULL,
            `Transaction_Type` VARCHAR(32) NOT NULL,
            `Quantity` INT NOT NULL,
            `Operator` VARCHAR(64) NOT NULL,
            `Project_Name` VARCHAR(128) DEFAULT NULL,
            `Location` VARCHAR(64) NOT NULL DEFAULT '默认样品柜',
            `Remark` VARCHAR(512) DEFAULT NULL,
            `Created_At` TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
            INDEX `idx_tx_pn` (`Part_Number`),
            INDEX `idx_tx_type` (`Transaction_Type`),
            INDEX `idx_tx_operator` (`Operator`),
            INDEX `idx_tx_created` (`Created_At`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
        """;

        await using (var cmd = new MySqlCommand(ddl1, connection))
        {
            await cmd.ExecuteNonQueryAsync();
        }
        await using (var cmd = new MySqlCommand(ddl2, connection))
        {
            await cmd.ExecuteNonQueryAsync();
        }
    }

    public async Task<List<SampleInventoryItem>> QuerySampleInventoriesAsync(AppSettings settings, string? keyword = null)
    {
        await EnsureSampleTablesExistAsync(settings);
        var results = new List<SampleInventoryItem>();
        await using var connection = await OpenConnectionAsync(settings);

        var sql = @"
            SELECT si.id, si.Part_Number, si.Location, si.Quantity_On_Hand, si.Quantity_Lent,
                   si.Quantity_Total_In, si.Min_Alert_Qty, si.Distributor, si.Remark, si.Updated_At,
                   v.Part_Type, v.Value, v.Package, v.Description, v.Manufacturer, v.Manufacturer_Part_Number
            FROM `sample_inventory` si
            LEFT JOIN `v_all_components` v ON si.Part_Number = v.Part_Number
        ";

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            sql += @" WHERE si.Part_Number LIKE @kw OR si.Location LIKE @kw OR si.Distributor LIKE @kw
                      OR v.Manufacturer_Part_Number LIKE @kw OR v.Description LIKE @kw OR v.Value LIKE @kw";
        }
        sql += " ORDER BY si.Updated_At DESC";

        await using var cmd = new MySqlCommand(sql, connection);
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            cmd.Parameters.AddWithValue("@kw", $"%{keyword.Trim()}%");
        }

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(new SampleInventoryItem
            {
                Id = reader.GetInt64(0),
                PartNumber = reader.GetString(1),
                Location = reader.GetString(2),
                QuantityOnHand = reader.GetInt32(3),
                QuantityLent = reader.GetInt32(4),
                QuantityTotalIn = reader.GetInt32(5),
                MinAlertQty = reader.GetInt32(6),
                Distributor = reader.IsDBNull(7) ? null : reader.GetString(7),
                Remark = reader.IsDBNull(8) ? null : reader.GetString(8),
                UpdatedAt = reader.IsDBNull(9) ? null : reader.GetDateTime(9),
                PartType = reader.IsDBNull(10) ? null : reader.GetString(10),
                Value = reader.IsDBNull(11) ? null : reader.GetString(11),
                Package = reader.IsDBNull(12) ? null : reader.GetString(12),
                Description = reader.IsDBNull(13) ? null : reader.GetString(13),
                Manufacturer = reader.IsDBNull(14) ? null : reader.GetString(14),
                ManufacturerPartNumber = reader.IsDBNull(15) ? null : reader.GetString(15)
            });
        }
        return results;
    }

    public async Task<List<SampleTransactionRecord>> QuerySampleTransactionsAsync(AppSettings settings, string? partNumber = null, int limit = 100)
    {
        await EnsureSampleTablesExistAsync(settings);
        var results = new List<SampleTransactionRecord>();
        await using var connection = await OpenConnectionAsync(settings);

        var sql = "SELECT id, Part_Number, Transaction_Type, Quantity, Operator, Project_Name, Location, Remark, Created_At FROM `sample_transactions`";
        if (!string.IsNullOrWhiteSpace(partNumber))
        {
            sql += " WHERE Part_Number = @pn";
        }
        sql += $" ORDER BY Created_At DESC LIMIT {limit}";

        await using var cmd = new MySqlCommand(sql, connection);
        if (!string.IsNullOrWhiteSpace(partNumber))
        {
            cmd.Parameters.AddWithValue("@pn", partNumber.Trim());
        }

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(new SampleTransactionRecord
            {
                Id = reader.GetInt64(0),
                PartNumber = reader.GetString(1),
                TransactionType = reader.GetString(2),
                Quantity = reader.GetInt32(3),
                Operator = reader.GetString(4),
                ProjectName = reader.IsDBNull(5) ? null : reader.GetString(5),
                Location = reader.GetString(6),
                Remark = reader.IsDBNull(7) ? null : reader.GetString(7),
                CreatedAt = reader.GetDateTime(8)
            });
        }
        return results;
    }

    public async Task ExecuteSampleTransactionAsync(AppSettings settings, SampleTransactionRecord tx, string? distributor = null)
    {
        await EnsureSampleTablesExistAsync(settings);
        await using var connection = await OpenConnectionAsync(settings);
        await using var dbTransaction = await connection.BeginTransactionAsync();

        try
        {
            int currentOnHand = 0;
            int currentLent = 0;
            int currentTotalIn = 0;
            bool exists = false;

            var selectSql = "SELECT Quantity_On_Hand, Quantity_Lent, Quantity_Total_In FROM `sample_inventory` WHERE Part_Number = @pn AND Location = @loc FOR UPDATE";
            await using (var selCmd = new MySqlCommand(selectSql, connection, dbTransaction))
            {
                selCmd.Parameters.AddWithValue("@pn", tx.PartNumber.Trim());
                selCmd.Parameters.AddWithValue("@loc", tx.Location.Trim());
                await using var reader = await selCmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    exists = true;
                    currentOnHand = reader.GetInt32(0);
                    currentLent = reader.GetInt32(1);
                    currentTotalIn = reader.GetInt32(2);
                }
            }

            switch (tx.TransactionType)
            {
                case SampleTransactionTypes.CheckIn:
                    currentOnHand += tx.Quantity;
                    currentTotalIn += tx.Quantity;
                    break;
                case SampleTransactionTypes.Consume:
                    if (currentOnHand < tx.Quantity)
                        throw new InvalidOperationException($"在手库存不足！当前库存: {currentOnHand}，申请领用: {tx.Quantity}");
                    currentOnHand -= tx.Quantity;
                    break;
                case SampleTransactionTypes.Borrow:
                    if (currentOnHand < tx.Quantity)
                        throw new InvalidOperationException($"在手库存不足！当前库存: {currentOnHand}，申请借出: {tx.Quantity}");
                    currentOnHand -= tx.Quantity;
                    currentLent += tx.Quantity;
                    break;
                case SampleTransactionTypes.Return:
                    if (currentLent < tx.Quantity)
                        throw new InvalidOperationException($"在借数量不足以归还！当前在借: {currentLent}，尝试归还: {tx.Quantity}");
                    currentLent -= tx.Quantity;
                    currentOnHand += tx.Quantity;
                    break;
                case SampleTransactionTypes.Scrap:
                    if (currentOnHand < tx.Quantity)
                        throw new InvalidOperationException($"在手库存不足以报废！当前库存: {currentOnHand}，申请报废: {tx.Quantity}");
                    currentOnHand -= tx.Quantity;
                    break;
                default:
                    throw new InvalidOperationException($"未知样品业务类型: {tx.TransactionType}");
            }

            if (exists)
            {
                var updateSql = @"UPDATE `sample_inventory` 
                                  SET Quantity_On_Hand = @onHand, Quantity_Lent = @lent, Quantity_Total_In = @totalIn,
                                      Distributor = COALESCE(@distributor, Distributor), Remark = COALESCE(@remark, Remark)
                                  WHERE Part_Number = @pn AND Location = @loc";
                await using var upCmd = new MySqlCommand(updateSql, connection, dbTransaction);
                upCmd.Parameters.AddWithValue("@onHand", currentOnHand);
                upCmd.Parameters.AddWithValue("@lent", currentLent);
                upCmd.Parameters.AddWithValue("@totalIn", currentTotalIn);
                upCmd.Parameters.AddWithValue("@distributor", (object?)distributor ?? DBNull.Value);
                upCmd.Parameters.AddWithValue("@remark", (object?)tx.Remark ?? DBNull.Value);
                upCmd.Parameters.AddWithValue("@pn", tx.PartNumber.Trim());
                upCmd.Parameters.AddWithValue("@loc", tx.Location.Trim());
                await upCmd.ExecuteNonQueryAsync();
            }
            else
            {
                var insertSql = @"INSERT INTO `sample_inventory` 
                                  (Part_Number, Location, Quantity_On_Hand, Quantity_Lent, Quantity_Total_In, Min_Alert_Qty, Distributor, Remark)
                                  VALUES (@pn, @loc, @onHand, @lent, @totalIn, 0, @distributor, @remark)";
                await using var inCmd = new MySqlCommand(insertSql, connection, dbTransaction);
                inCmd.Parameters.AddWithValue("@pn", tx.PartNumber.Trim());
                inCmd.Parameters.AddWithValue("@loc", tx.Location.Trim());
                inCmd.Parameters.AddWithValue("@onHand", currentOnHand);
                inCmd.Parameters.AddWithValue("@lent", currentLent);
                inCmd.Parameters.AddWithValue("@totalIn", currentTotalIn);
                inCmd.Parameters.AddWithValue("@distributor", (object?)distributor ?? DBNull.Value);
                inCmd.Parameters.AddWithValue("@remark", (object?)tx.Remark ?? DBNull.Value);
                await inCmd.ExecuteNonQueryAsync();
            }

            var logSql = @"INSERT INTO `sample_transactions` 
                           (Part_Number, Transaction_Type, Quantity, Operator, Project_Name, Location, Remark)
                           VALUES (@pn, @type, @qty, @op, @proj, @loc, @rem)";
            await using (var logCmd = new MySqlCommand(logSql, connection, dbTransaction))
            {
                logCmd.Parameters.AddWithValue("@pn", tx.PartNumber.Trim());
                logCmd.Parameters.AddWithValue("@type", tx.TransactionType);
                logCmd.Parameters.AddWithValue("@qty", tx.Quantity);
                logCmd.Parameters.AddWithValue("@op", tx.Operator.Trim());
                logCmd.Parameters.AddWithValue("@proj", (object?)tx.ProjectName?.Trim() ?? DBNull.Value);
                logCmd.Parameters.AddWithValue("@loc", tx.Location.Trim());
                logCmd.Parameters.AddWithValue("@rem", (object?)tx.Remark?.Trim() ?? DBNull.Value);
                await logCmd.ExecuteNonQueryAsync();
            }

            await dbTransaction.CommitAsync();
        }
        catch
        {
            await dbTransaction.RollbackAsync();
            throw;
        }
    }

    private static string EscapeIdentifier(string identifier)
    {
        return "`" + identifier.Replace("`", "``") + "`";
    }

    public async Task<List<ComponentItem>> QueryAllComponentsAsync(AppSettings settings, string? keyword = null)
    {
        var results = new List<ComponentItem>();
        await using var connection = await OpenConnectionAsync(settings);

        var sql = @"
            SELECT `Category`, `Part_Number`, `Part_Type`, `Value`, `Package`, `Description`, 
                   `Schematic_Part`, `PCB_Footprint`, `Model_3D`, `Manufacturer`, `Manufacturer_Part_Number`, 
                   `Datasheet`, `Lifecycle_Status`, `LCSC_Part_Number`, `Distributor`, `Note` 
            FROM `v_all_components`
        ";

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            sql += @" WHERE `Part_Number` LIKE @kw OR `Manufacturer_Part_Number` LIKE @kw 
                      OR `Description` LIKE @kw OR `Value` LIKE @kw OR `Manufacturer` LIKE @kw 
                      OR `Package` LIKE @kw OR `Distributor` LIKE @kw OR `Note` LIKE @kw";
        }
        sql += " ORDER BY `Part_Number` ASC";

        try
        {
            await using var cmd = new MySqlCommand(sql, connection);
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                cmd.Parameters.AddWithValue("@kw", $"%{keyword.Trim()}%");
            }

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var cat = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
                var pn = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                var item = new ComponentItem
                {
                    Category = cat,
                    PartNumber = pn,
                    PartType = reader.IsDBNull(2) ? null : reader.GetString(2),
                    Value = reader.IsDBNull(3) ? null : reader.GetString(3),
                    Package = reader.IsDBNull(4) ? null : reader.GetString(4),
                    Description = reader.IsDBNull(5) ? null : reader.GetString(5),
                    SchematicPart = reader.IsDBNull(6) ? null : reader.GetString(6),
                    PcbFootprint = reader.IsDBNull(7) ? null : reader.GetString(7),
                    Model3D = reader.IsDBNull(8) ? null : reader.GetString(8),
                    Manufacturer = reader.IsDBNull(9) ? null : reader.GetString(9),
                    ManufacturerPartNumber = reader.IsDBNull(10) ? null : reader.GetString(10),
                    Datasheet = reader.IsDBNull(11) ? null : reader.GetString(11),
                    LifecycleStatus = reader.IsDBNull(12) ? "Active" : reader.GetString(12),
                    LcscPartNumber = reader.IsDBNull(13) ? null : reader.GetString(13),
                    Distributor = reader.IsDBNull(14) ? null : reader.GetString(14),
                    Note = reader.IsDBNull(15) ? null : reader.GetString(15)
                };

                // Infer TableName from PartNumber or Category
                item.TableName = InferTableName(settings, pn, cat);
                results.Add(item);
            }
        }
        catch (MySqlException)
        {
            // If v_all_components query fails (e.g. view outdated or missing Datasheet column), fallback query without Datasheet or recreate view
            return await QueryAllComponentsFallbackAsync(settings, connection, keyword);
        }

        return results;
    }

    private static string InferTableName(AppSettings settings, string partNumber, string category)
    {
        foreach (var (tbl, pfx) in settings.TablePartNumberPrefixes)
        {
            if (!string.IsNullOrWhiteSpace(pfx) && partNumber.StartsWith(pfx, StringComparison.OrdinalIgnoreCase))
            {
                return tbl;
            }
        }

        var prefixFromCat = category.Split(' ').FirstOrDefault()?.Trim();
        if (!string.IsNullOrWhiteSpace(prefixFromCat))
        {
            foreach (var tbl in settings.TablePartNumberPrefixes.Keys)
            {
                if (tbl.StartsWith(prefixFromCat, StringComparison.OrdinalIgnoreCase))
                {
                    return tbl;
                }
            }
        }

        return "10_resistor";
    }

    private async Task<List<ComponentItem>> QueryAllComponentsFallbackAsync(AppSettings settings, MySqlConnection connection, string? keyword)
    {
        var results = new List<ComponentItem>();
        var tables = await GetTablesAsync(settings);
        foreach (var tbl in tables.Where(t => !t.StartsWith("sample_", StringComparison.OrdinalIgnoreCase) && !t.StartsWith("v_", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                var sql = $"SELECT * FROM {EscapeIdentifier(tbl)}";
                if (!string.IsNullOrWhiteSpace(keyword))
                {
                    sql += " WHERE `Part_Number` LIKE @kw OR `Description` LIKE @kw";
                }
                sql += " LIMIT 200";

                await using var cmd = new MySqlCommand(sql, connection);
                if (!string.IsNullOrWhiteSpace(keyword))
                {
                    cmd.Parameters.AddWithValue("@kw", $"%{keyword.Trim()}%");
                }

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var item = new ComponentItem
                    {
                        Category = tbl,
                        TableName = tbl,
                        PartNumber = GetSafeString(reader, "Part_Number"),
                        PartType = GetSafeString(reader, "Part_Type"),
                        Value = GetSafeString(reader, "Value"),
                        Package = GetSafeString(reader, "Package"),
                        Description = GetSafeString(reader, "Description"),
                        SchematicPart = GetSafeString(reader, "Schematic_Part"),
                        PcbFootprint = GetSafeString(reader, "PCB_Footprint"),
                        Model3D = GetSafeString(reader, "Model_3D"),
                        Manufacturer = GetSafeString(reader, "Manufacturer"),
                        ManufacturerPartNumber = GetSafeString(reader, "Manufacturer_Part_Number"),
                        Datasheet = GetSafeString(reader, "Datasheet"),
                        LifecycleStatus = GetSafeString(reader, "Lifecycle_Status") ?? "Active",
                        LcscPartNumber = GetSafeString(reader, "LCSC_Part_Number"),
                        Distributor = GetSafeString(reader, "Distributor"),
                        Note = GetSafeString(reader, "Note")
                    };
                    results.Add(item);
                }
            }
            catch
            {
                // Continue other tables
            }
        }
        return results;
    }

    private static string? GetSafeString(MySqlDataReader reader, string colName)
    {
        try
        {
            var ord = reader.GetOrdinal(colName);
            return reader.IsDBNull(ord) ? null : reader.GetString(ord);
        }
        catch
        {
            return null;
        }
    }

    public async Task UpdateComponentDrawingsAsync(AppSettings settings, string tableName, string partNumber, string? schematicPart, string? pcbFootprint, string? model3D)
    {
        await using var connection = await OpenConnectionAsync(settings);
        var cols = await GetColumnsAsync(settings, tableName);
        var symCol = FindSymbolColumn(cols, settings) ?? "Schematic_Part";
        var footCol = FindFootprintColumn(cols, settings) ?? "PCB_Footprint";
        var m3dCol = FindModel3DColumn(cols, settings) ?? "Model_3D";
        var pnCol = FindPartNumberColumn(cols, settings) ?? "Part_Number";

        var setClauses = new List<string>();
        if (!string.IsNullOrWhiteSpace(schematicPart)) setClauses.Add($"{EscapeIdentifier(symCol)} = @sym");
        if (!string.IsNullOrWhiteSpace(pcbFootprint)) setClauses.Add($"{EscapeIdentifier(footCol)} = @foot");
        if (!string.IsNullOrWhiteSpace(model3D)) setClauses.Add($"{EscapeIdentifier(m3dCol)} = @m3d");

        if (setClauses.Count == 0) return;

        var sql = $"UPDATE {EscapeIdentifier(tableName)} SET {string.Join(", ", setClauses)} WHERE {EscapeIdentifier(pnCol)} = @pn";
        await using var cmd = new MySqlCommand(sql, connection);
        if (!string.IsNullOrWhiteSpace(schematicPart)) cmd.Parameters.AddWithValue("@sym", schematicPart);
        if (!string.IsNullOrWhiteSpace(pcbFootprint)) cmd.Parameters.AddWithValue("@foot", pcbFootprint);
        if (!string.IsNullOrWhiteSpace(model3D)) cmd.Parameters.AddWithValue("@m3d", model3D);
        cmd.Parameters.AddWithValue("@pn", partNumber);

        await cmd.ExecuteNonQueryAsync();
    }

}
