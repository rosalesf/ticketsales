using System.ComponentModel.DataAnnotations;

namespace TicketSales.Models;

public class TicketType
{
    public int Id { get; set; }
    public int EventId { get; set; }
    public Event Event { get; set; } = null!;

    [Required, StringLength(60)]
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int AvailableTickets { get; set; }
}
