using Microsoft.EntityFrameworkCore;
using TicketSales.Models;

namespace TicketSales.Data;

public class TicketDbContext(DbContextOptions<TicketDbContext> options) : DbContext(options)
{
    public DbSet<Event> Events => Set<Event>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<TicketType> TicketTypes => Set<TicketType>();
    public DbSet<PurchaseConfirmationEmailTemplate> PurchaseConfirmationEmailTemplates => Set<PurchaseConfirmationEmailTemplate>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TicketType>()
            .HasIndex(x => new { x.EventId, x.Name }).IsUnique();
        modelBuilder.Entity<Order>()
            .HasIndex(x => x.OrderNumber).IsUnique();
        modelBuilder.Entity<Order>()
            .HasIndex(x => x.CustomerUserId);
        modelBuilder.Entity<Order>().HasIndex(x => x.PaymentAccessToken).IsUnique().HasFilter("[PaymentAccessToken] IS NOT NULL");
        modelBuilder.Entity<Order>().HasIndex(x => x.RecurrenteCheckoutId).IsUnique().HasFilter("[RecurrenteCheckoutId] IS NOT NULL");
    }
}
