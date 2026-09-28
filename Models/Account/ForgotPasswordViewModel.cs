using System.ComponentModel.DataAnnotations;

namespace TicketSales.Models.Account;

public class ForgotPasswordViewModel
{
    [Required, EmailAddress(ErrorMessage = "Ingresa un correo electrónico válido."), Display(Name = "Correo electrónico")]
    public string Email { get; set; } = string.Empty;
}
