using System.ComponentModel.DataAnnotations;

namespace TicketSales.Models.Account;

public class RegisterViewModel : IValidatableObject
{
    [Required, EmailAddress(ErrorMessage = "Ingresa un correo electrónico válido."), Display(Name = "Correo electrónico")]
    public string Email { get; set; } = string.Empty;
    [Required(ErrorMessage = "Ingresa tu nombre completo."), StringLength(200), Display(Name = "Nombre completo")]
    public string FullName { get; set; } = string.Empty;
    [Required(ErrorMessage = "Ingresa tu teléfono."), Phone(ErrorMessage = "Ingresa un teléfono válido."), StringLength(50), Display(Name = "Teléfono")]
    public string Phone { get; set; } = string.Empty;
    [Display(Name = "Factura")]
    public string InvoiceType { get; set; } = "CF";
    [StringLength(25), Display(Name = "Número NIT")]
    public string? InvoiceNit { get; set; }
    [StringLength(200), Display(Name = "Nombre de factura")]
    public string? InvoiceName { get; set; }
    [Required, StringLength(100, MinimumLength = 12), DataType(DataType.Password), Display(Name = "Contraseña")]
    public string Password { get; set; } = string.Empty;
    [Required, DataType(DataType.Password), Compare(nameof(Password), ErrorMessage = "Las contraseñas no coinciden."), Display(Name = "Confirmar contraseña")]
    public string ConfirmPassword { get; set; } = string.Empty;
    public string? ReturnUrl { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (InvoiceType is not "CF" and not "NIT") yield return new ValidationResult("Selecciona una opción de factura válida.", [nameof(InvoiceType)]);
        if (InvoiceType == "NIT")
        {
            if (string.IsNullOrWhiteSpace(InvoiceNit)) yield return new ValidationResult("Ingresa el número NIT.", [nameof(InvoiceNit)]);
            if (string.IsNullOrWhiteSpace(InvoiceName)) yield return new ValidationResult("Ingresa el nombre de factura.", [nameof(InvoiceName)]);
        }
    }
}
