using System.ComponentModel.DataAnnotations;

namespace TicketSales.Models.Admin;

public class EventTicketTypeForm
{
    public int Id { get; set; }
    [Required(ErrorMessage = "Indica el nombre de la localidad."), StringLength(60)]
    public string Name { get; set; } = string.Empty;
    [Range(typeof(decimal), "0.01", "999999.99", ErrorMessage = "El precio debe ser mayor que cero.")]
    public decimal Price { get; set; }
    [Range(0, 1000000, ErrorMessage = "La disponibilidad no puede ser negativa.")]
    public int AvailableTickets { get; set; }
}
