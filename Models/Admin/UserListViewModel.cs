using Microsoft.AspNetCore.Identity;

namespace TicketSales.Models.Admin;

public class UserListViewModel
{
    public List<UserListRow> Items { get; set; } = [];
    public List<string> AvailableRoles { get; set; } = [];
    public List<string> SelectedRoles { get; set; } = [];
    public string? Search { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalItems { get; set; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalItems / (double)PageSize));
    public int FirstItem => TotalItems == 0 ? 0 : ((Page - 1) * PageSize) + 1;
    public int LastItem => Math.Min(Page * PageSize, TotalItems);
}

public class UserListRow
{
    public IdentityUser User { get; set; } = null!;
    public List<string> Roles { get; set; } = [];
}
