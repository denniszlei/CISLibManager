namespace CadenceCisLibraryManager.Models;

public sealed class SampleInventoryItem
{
    public long Id { get; set; }
    public string PartNumber { get; set; } = string.Empty;
    public string Location { get; set; } = "默认样品柜";
    public int QuantityOnHand { get; set; }
    public int QuantityLent { get; set; }
    public int QuantityTotalIn { get; set; }
    public int MinAlertQty { get; set; }
    public string? Distributor { get; set; }
    public string? Remark { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // 联动 CIS 库字段用于界面显示
    public string? PartType { get; set; }
    public string? Value { get; set; }
    public string? Package { get; set; }
    public string? Description { get; set; }
    public string? Manufacturer { get; set; }
    public string? ManufacturerPartNumber { get; set; }
}

public sealed class SampleTransactionRecord
{
    public long Id { get; set; }
    public string PartNumber { get; set; } = string.Empty;
    public string TransactionType { get; set; } = string.Empty; // 入库, 领用消耗, 调测借出, 归还, 报废
    public int Quantity { get; set; }
    public string Operator { get; set; } = string.Empty;
    public string? ProjectName { get; set; }
    public string Location { get; set; } = "默认样品柜";
    public string? Remark { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

public static class SampleTransactionTypes
{
    public const string CheckIn = "入库";
    public const string Consume = "领用消耗";
    public const string Borrow = "调测借出";
    public const string Return = "归还";
    public const string Scrap = "报废";
}
