using Microsoft.EntityFrameworkCore;
using TicketSales.Models;

namespace TicketSales.Data;

public static class SeedData
{
    public static void Initialize(TicketDbContext db)
    {
        if (!db.Events.Any())
        {
            db.Events.AddRange(
            new Event { Name = "Noche de Jazz", Description = "Una velada íntima con músicos locales.", Venue = "Teatro Nacional", StartsAt = DateTime.Today.AddDays(10).AddHours(20), Price = 225m, AvailableTickets = 80, Impulsar = true },
            new Event { Name = "Festival Sabores", Description = "Gastronomía, música y experiencias para toda la familia.", Venue = "Parque Central", StartsAt = DateTime.Today.AddDays(18).AddHours(11), Price = 150m, AvailableTickets = 300, Impulsar = true },
            new Event { Name = "Tech Summit 2026", Description = "Conferencias prácticas sobre innovación y tecnología.", Venue = "Centro de Convenciones", StartsAt = DateTime.Today.AddDays(28).AddHours(8), Price = 475m, AvailableTickets = 120 });
            db.SaveChanges();
        }

        // Conserva los eventos existentes: los tipos se crean sólo si aún no existen.
        foreach (var ticketEvent in db.Events.Include(e => e.TicketTypes))
        {
            if (ticketEvent.TicketTypes.Any()) continue;
            db.TicketTypes.AddRange(
                new TicketType { EventId = ticketEvent.Id, Name = "General", Price = ticketEvent.Price, AvailableTickets = ticketEvent.AvailableTickets },
                new TicketType { EventId = ticketEvent.Id, Name = "Preferencial", Price = Math.Round(ticketEvent.Price * 1.35m, 2), AvailableTickets = Math.Max(0, ticketEvent.AvailableTickets / 3) },
                new TicketType { EventId = ticketEvent.Id, Name = "VIP", Price = Math.Round(ticketEvent.Price * 1.75m, 2), AvailableTickets = Math.Max(0, ticketEvent.AvailableTickets / 5) });
        }
        db.SaveChanges();
    }
}
