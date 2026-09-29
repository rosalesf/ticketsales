using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TicketSales.Models;
using TicketSales.Services;

namespace TicketSales.Data;

public static class AdminIdentitySeed
{
    public const string SuperAdminRole = "SuperAdmin";
    public const string AdministratorRole = "Administrator";
    public const string CustomerRole = "Customer";

    public static async Task InitializeAsync(IServiceProvider services, IConfiguration configuration)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<IdentityUser>>();
        foreach (var role in new[] { SuperAdminRole, AdministratorRole, CustomerRole })
            if (!await roleManager.RoleExistsAsync(role)) await roleManager.CreateAsync(new IdentityRole(role));

        var db = services.GetRequiredService<AdminIdentityDbContext>();
        var administrator = await roleManager.FindByNameAsync(AdministratorRole);
        if (administrator is not null)
        {
            var existingPermissions = await db.AdminMenuPermissions.Where(x => x.RoleId == administrator.Id).Select(x => x.MenuKey).ToListAsync();
            if (!existingPermissions.Contains(AdminMenuOptions.Dashboard)) db.AdminMenuPermissions.Add(new AdminMenuPermission { RoleId = administrator.Id, MenuKey = AdminMenuOptions.Dashboard });
            if (!existingPermissions.Contains(AdminMenuOptions.Events)) db.AdminMenuPermissions.Add(new AdminMenuPermission { RoleId = administrator.Id, MenuKey = AdminMenuOptions.Events });
            if (!existingPermissions.Contains(AdminMenuOptions.EmailTemplate)) db.AdminMenuPermissions.Add(new AdminMenuPermission { RoleId = administrator.Id, MenuKey = AdminMenuOptions.EmailTemplate });
            if (!existingPermissions.Contains(AdminMenuOptions.TicketScanner)) db.AdminMenuPermissions.Add(new AdminMenuPermission { RoleId = administrator.Id, MenuKey = AdminMenuOptions.TicketScanner });
            if (db.ChangeTracker.HasChanges()) await db.SaveChangesAsync();
        }

        var userName = configuration["AdminSeed:UserName"] ?? throw new InvalidOperationException("Configura AdminSeed:UserName.");
        var password = configuration["AdminSeed:Password"] ?? throw new InvalidOperationException("Configura AdminSeed:Password.");
        var user = await userManager.FindByNameAsync(userName);
        // Actualiza el superadministrador creado por la versión inicial basada en correo.
        user ??= await userManager.FindByNameAsync("superadmin@ventatickets.local");
        if (user is null)
        {
            user = new IdentityUser { UserName = userName };
            var result = await userManager.CreateAsync(user, password);
            if (!result.Succeeded) throw new InvalidOperationException(string.Join(" ", result.Errors.Select(x => x.Description)));
        }
        else if (!string.Equals(user.UserName, userName, StringComparison.Ordinal))
        {
            user.UserName = userName;
            user.Email = null;
            user.EmailConfirmed = false;
            var result = await userManager.UpdateAsync(user);
            if (!result.Succeeded) throw new InvalidOperationException(string.Join(" ", result.Errors.Select(x => x.Description)));
        }
        if (!await userManager.IsInRoleAsync(user, SuperAdminRole)) await userManager.AddToRoleAsync(user, SuperAdminRole);
    }
}
