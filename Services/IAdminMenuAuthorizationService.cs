using System.Security.Claims;

namespace TicketSales.Services;

public interface IAdminMenuAuthorizationService
{
    Task<bool> HasAccessAsync(ClaimsPrincipal user, string menuKey);
    Task<HashSet<string>> GetAccessibleKeysAsync(ClaimsPrincipal user);
}
