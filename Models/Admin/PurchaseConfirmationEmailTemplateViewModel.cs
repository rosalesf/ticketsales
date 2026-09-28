using System.ComponentModel.DataAnnotations;

namespace TicketSales.Models.Admin;

public class PurchaseConfirmationEmailTemplateViewModel
{
    [Required(ErrorMessage = "Ingresa el contenido HTML del correo.")]
    public string Html { get; set; } = string.Empty;
}
