namespace TicketSales.Models.Admin;

public class MenuSecurityViewModel
{
    public List<MenuSecurityRoleRow> Roles { get; set; } = [];
    public List<MenuSecurityOption> Options { get; set; } = [];
}
public class MenuSecurityRoleRow
{
    public string RoleId { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public HashSet<string> GrantedKeys { get; set; } = [];
}
public class MenuSecurityOption
{
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
}
