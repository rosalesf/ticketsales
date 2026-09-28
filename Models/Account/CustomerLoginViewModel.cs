using System.ComponentModel.DataAnnotations;

namespace TicketSales.Models.Account;

public class CustomerLoginViewModel
{
    [Required, EmailAddress(ErrorMessage = "Ingresa un correo electrónico válido."), Display(Name = "Correo electrónico")]
    public string Email { get; set; } = string.Empty;
    [Required, DataType(DataType.Password), Display(Name = "Contraseña")]
    public string Password { get; set; } = string.Empty;
    [Display(Name = "Recordarme")]
    public bool RememberMe { get; set; }
    public string? ReturnUrl { get; set; }
}
