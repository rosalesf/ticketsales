using System.ComponentModel.DataAnnotations;

namespace TicketSales.Models;

public class Order
{
    public int Id { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    [StringLength(450)]
    public string? CustomerUserId { get; set; }
    [StringLength(512)]
    public string CustomerPhone { get; set; } = string.Empty;
    public string InvoiceType { get; set; } = "CF";
    public string? InvoiceNit { get; set; }
    public string? InvoiceName { get; set; }
    public bool AcceptedTerms { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public decimal Total { get; set; }
    public string PaymentStatus { get; set; } = "Pending";
    public string? PaymentReference { get; set; }
    public string OrderStatus { get; set; } = "PendingPayment";
    [StringLength(64)]
    public string? PaymentAccessToken { get; set; }
    [StringLength(128)]
    public string? RecurrenteCheckoutId { get; set; }
    [StringLength(128)]
    public string? RecurrenteCustomerId { get; set; }
    public string? RecurrenteCheckoutUrl { get; set; }
    public string? CheckoutRequestJson { get; set; }
    public DateTime? ReservationExpiresAt { get; set; }
    public bool InventoryReserved { get; set; }
    public DateTime? ConfirmationEmailSentAt { get; set; }
    public List<OrderItem> Items { get; set; } = [];
}
