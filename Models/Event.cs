namespace TicketSales.Models;

public class Event
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Venue { get; set; } = string.Empty;
    public DateTime StartsAt { get; set; }
    public decimal Price { get; set; }
    public int AvailableTickets { get; set; }
    public string Status { get; set; } = EventStatuses.Published;
    public bool Impulsar { get; set; }
    public string? BannerBase64 { get; set; }
    public string? BannerContentType { get; set; }
    public List<TicketType> TicketTypes { get; set; } = [];
}

public static class EventStatuses
{
    public const string Draft = "Borrador";
    public const string Published = "Publicado";
    public const string Paused = "Pausado";
    public const string Cancelled = "Cancelado";
    public const string Finished = "Finalizado";

    public static readonly IReadOnlyList<string> All = [Draft, Published, Paused, Cancelled, Finished];
}
