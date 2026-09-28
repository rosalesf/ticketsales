using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TicketSales.Data;

namespace TicketSales.Services;

public class AdminMenuAuthorizationService(AdminIdentityDbContext db) : IAdminMenuAuthorizationService
{
    public async Task<bool> HasAccessAsync(ClaimsPrincipal user, string menuKey) => (await GetAccessibleKeysAsync(user)).Contains(menuKey);

    public async Task<HashSet<string>> GetAccessibleKeysAsync(ClaimsPrincipal user)
    {
        if (user.IsInRole(AdminIdentitySeed.SuperAdminRole)) return AdminMenuOptions.All.Select(x => x.Key).ToHashSet();
        var roles = user.FindAll(ClaimTypes.Role).Select(x => x.Value).ToList();
        if (roles.Count == 0) return [];
        var roleIds = await db.Roles.Where(x => roles.Contains(x.Name!)).Select(x => x.Id).ToListAsync();
        return (await db.AdminMenuPermissions.Where(x => roleIds.Contains(x.RoleId)).Select(x => x.MenuKey).ToListAsync()).ToHashSet();
    }
}
