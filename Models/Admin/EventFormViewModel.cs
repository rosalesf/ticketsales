using System.ComponentModel.DataAnnotations;

namespace TicketSales.Models.Admin;

public class EventFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "El nombre es obligatorio."), StringLength(160)]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "La descripción es obligatoria."), StringLength(2000)]
    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "El lugar es obligatorio."), StringLength(200)]
    public string Venue { get; set; } = string.Empty;

    [Required(ErrorMessage = "La fecha es obligatoria.")]
    public DateTime StartsAt { get; set; } = DateTime.Now.AddDays(7);

    public List<EventTicketTypeForm> TicketTypes { get; set; } =
    [
        new() { Name = "General", Price = 100m, AvailableTickets = 100 },
        new() { Name = "Preferencial", Price = 150m, AvailableTickets = 50 },
        new() { Name = "VIP", Price = 200m, AvailableTickets = 25 }
    ];

    [Required]
    public string Status { get; set; } = EventStatuses.Draft;
    public bool Impulsar { get; set; }
    [Display(Name = "Banner del evento")]
    public IFormFile? Banner { get; set; }
    [Display(Name = "Quitar banner actual")]
    public bool RemoveBanner { get; set; }
    public bool HasExistingBanner { get; set; }
    public string? ExistingBannerBase64 { get; set; }
    public string? ExistingBannerContentType { get; set; }
}
