using System.ComponentModel.DataAnnotations;

namespace TicketSales.Models;

public class CustomerProfile
{
    public string UserId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string InvoiceType { get; set; } = "CF";
    public string? InvoiceNit { get; set; }
    public string? InvoiceName { get; set; }
    [StringLength(128)]
    public string? RecurrenteCustomerId { get; set; }
}
