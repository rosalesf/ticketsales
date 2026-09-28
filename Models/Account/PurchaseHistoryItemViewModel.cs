namespace TicketSales.Models.Account;

public sealed class PurchaseHistoryItemViewModel
{
    public int Id { get; init; }
    public string OrderNumber { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public string PaymentStatus { get; init; } = string.Empty;
    public decimal Total { get; init; }
    public string ItemSummary { get; init; } = string.Empty;
}
