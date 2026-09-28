namespace TicketSales.Models;

public class PurchaseConfirmationEmailTemplate
{
    public int Id { get; set; }
    public string Html { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
