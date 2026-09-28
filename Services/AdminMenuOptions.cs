namespace TicketSales.Services;

public record AdminMenuOption(string Key, string Title, string Action);

public static class AdminMenuOptions
{
    public const string Dashboard = "dashboard";
    public const string Users = "users";
    public const string Roles = "roles";
    public const string Events = "events";
    public const string MenuSecurity = "menu-security";
    public const string EmailTemplate = "email-template";

    public static readonly IReadOnlyList<AdminMenuOption> All =
    [
        new(Dashboard, "Panel", "Index"),
        new(Users, "Usuarios", "Users"),
        new(Roles, "Roles", "Roles"),
        new(Events, "Eventos", "Events"),
        new(EmailTemplate, "Correo de confirmación", "PurchaseConfirmationEmailTemplate"),
        new(MenuSecurity, "Seguridad de menú", "MenuSecurity")
    ];
}
