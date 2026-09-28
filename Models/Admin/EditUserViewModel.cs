using System.ComponentModel.DataAnnotations;

namespace TicketSales.Models.Admin;

public class EditUserViewModel
{
    public string Id { get; set; } = string.Empty;
    [Required, StringLength(100), RegularExpression("^[a-zA-Z0-9._-]+$", ErrorMessage = "Usa sólo letras, números, punto, guion o guion bajo."), Display(Name = "Nombre de usuario")]
    public string UserName { get; set; } = string.Empty;
    [StringLength(100, MinimumLength = 12), DataType(DataType.Password), Display(Name = "Nueva contraseña")]
    public string? Password { get; set; }
    [Required, Display(Name = "Rol")]
    public string Role { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = [];
}
