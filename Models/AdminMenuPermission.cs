namespace TicketSales.Models;

public class AdminMenuPermission
{
    public int Id { get; set; }
    public string RoleId { get; set; } = string.Empty;
    public string MenuKey { get; set; } = string.Empty;
}
