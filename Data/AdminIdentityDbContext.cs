using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TicketSales.Models;

namespace TicketSales.Data;

public class AdminIdentityDbContext(DbContextOptions<AdminIdentityDbContext> options)
    : IdentityDbContext<IdentityUser, IdentityRole, string>(options)
{
    public DbSet<AdminMenuPermission> AdminMenuPermissions => Set<AdminMenuPermission>();
    public DbSet<CustomerProfile> CustomerProfiles => Set<CustomerProfile>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<AdminMenuPermission>().HasIndex(x => new { x.RoleId, x.MenuKey }).IsUnique();
        builder.Entity<CustomerProfile>().HasKey(x => x.UserId);
        builder.Entity<CustomerProfile>().HasIndex(x => x.RecurrenteCustomerId).IsUnique().HasFilter("[RecurrenteCustomerId] IS NOT NULL");
    }
}
