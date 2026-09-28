using System.ComponentModel.DataAnnotations;

namespace TicketSales.Models.Account;

public class ResetPasswordViewModel
{
    [Required, EmailAddress, Display(Name = "Correo electrónico")]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Code { get; set; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 12), DataType(DataType.Password), Display(Name = "Nueva contraseña")]
    public string Password { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), Compare(nameof(Password), ErrorMessage = "Las contraseñas no coinciden."), Display(Name = "Confirmar nueva contraseña")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
