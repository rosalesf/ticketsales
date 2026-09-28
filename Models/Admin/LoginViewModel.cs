using System.ComponentModel.DataAnnotations;

namespace TicketSales.Models.Admin;

public class LoginViewModel
{
    [Required, StringLength(100), Display(Name = "Nombre de usuario")]
    public string UserName { get; set; } = string.Empty;
    [Required, DataType(DataType.Password), Display(Name = "Contraseña")]
    public string Password { get; set; } = string.Empty;
    [Display(Name = "Recordarme")]
    public bool RememberMe { get; set; }
    public string? ReturnUrl { get; set; }
}
