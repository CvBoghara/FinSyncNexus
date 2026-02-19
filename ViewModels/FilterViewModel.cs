namespace FinSyncNexus.ViewModels;

public class FilterViewModel
{
    public string DatePreset { get; set; } = "ThisMonth";
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string Provider { get; set; } = "All";
    public string CustomerVendor { get; set; } = string.Empty;
    public string AccountType { get; set; } = "All";
    public decimal? AmountMin { get; set; }
    public decimal? AmountMax { get; set; }
    public string Currency { get; set; } = "All";
    public string TransactionStatus { get; set; } = "All";

    public string DateRangeLabel { get; set; } = string.Empty;
    public List<string> ActiveBadges { get; set; } = new();
}
