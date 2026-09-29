namespace TicketSales.Models;

public class AdmissionTicket
{
    public int Id { get; set; }
    public int OrderItemId { get; set; }
    public int TicketNumber { get; set; }
    public DateTime? UsedAt { get; set; }
    public string? UsedBy { get; set; }
}
