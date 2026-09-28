using System.ComponentModel.DataAnnotations;

namespace TicketSales.Models.Admin;

public class EditRoleViewModel
{
    public string Id { get; set; } = string.Empty;
    [Required, StringLength(100), RegularExpression("^[a-zA-Z0-9_-]+$", ErrorMessage = "Usa sólo letras, números, guiones o guiones bajos.")]
    [Display(Name = "Nombre del rol")]
    public string Name { get; set; } = string.Empty;
}
