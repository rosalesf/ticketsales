using System.ComponentModel.DataAnnotations;

namespace TicketSales.Models;

public class CheckoutViewModel : IValidatableObject
{
    [Required(ErrorMessage = "Ingresa tu nombre.")]
    [Display(Name = "Nombre completo")]
    public string CustomerName { get; set; } = string.Empty;

    [Required, EmailAddress(ErrorMessage = "Ingresa un correo válido.")]
    [Display(Name = "Correo electrónico")]
    public string CustomerEmail { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ingresa tu teléfono.")]
    [Phone(ErrorMessage = "Ingresa un teléfono válido.")]
    [Display(Name = "Teléfono")]
    public string CustomerPhone { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Factura")]
    public string InvoiceType { get; set; } = "CF";

    [StringLength(25)]
    [Display(Name = "Número NIT")]
    public string? InvoiceNit { get; set; }

    [StringLength(200)]
    [Display(Name = "Nombre de factura")]
    public string? InvoiceName { get; set; }

    [Display(Name = "Acepto los términos y condiciones")]
    public bool AcceptedTerms { get; set; }

    [Required]
    public string CheckoutToken { get; set; } = string.Empty;

    public List<CartItem> Items { get; set; } = [];
    public decimal Total => Items.Sum(x => x.Subtotal);

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (InvoiceType is not "NIT" and not "CF") yield return new ValidationResult("Selecciona una opción de factura válida.", [nameof(InvoiceType)]);
        if (InvoiceType == "NIT")
        {
            if (string.IsNullOrWhiteSpace(InvoiceNit)) yield return new ValidationResult("Ingresa el número NIT.", [nameof(InvoiceNit)]);
            if (string.IsNullOrWhiteSpace(InvoiceName)) yield return new ValidationResult("Ingresa el nombre de factura.", [nameof(InvoiceName)]);
        }
    }
}
