using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using TicketSales.Data;
using TicketSales.Models;
using TicketSales.Models.Admin;
using TicketSales.Services;

namespace TicketSales.Controllers;

[Route("admin")]
public class AdminController(UserManager<IdentityUser> userManager, RoleManager<IdentityRole> roleManager, SignInManager<IdentityUser> signInManager, AdminIdentityDbContext identityDb, TicketDbContext ticketDb, IAdminMenuAuthorizationService menuAuthorization, TicketSales.Services.IPersonalDataProtector personalDataProtector) : Controller
{
    [AllowAnonymous, HttpGet("login")]
    public IActionResult Login(string? returnUrl = null) => View(new LoginViewModel { ReturnUrl = returnUrl });

    [AllowAnonymous, HttpPost("login"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var user = await userManager.FindByNameAsync(model.UserName);
        if (user is null || await userManager.IsInRoleAsync(user, AdminIdentitySeed.CustomerRole))
        {
            ModelState.AddModelError(string.Empty, "Usuario o contraseña incorrectos.");
            return View(model);
        }
        var result = await signInManager.PasswordSignInAsync(model.UserName, model.Password, model.RememberMe, lockoutOnFailure: true);
        if (result.Succeeded && Url.IsLocalUrl(model.ReturnUrl)) return LocalRedirect(model.ReturnUrl!);
        if (result.Succeeded) return RedirectToAction(nameof(Index));
        ModelState.AddModelError(string.Empty, result.IsLockedOut ? "Cuenta bloqueada temporalmente." : "Correo o contraseña incorrectos.");
        return View(model);
    }

    [Authorize, HttpPost("logout"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout() { await signInManager.SignOutAsync(); return RedirectToAction(nameof(Login)); }

    [Authorize, HttpGet("")]
    public async Task<IActionResult> Index()
    {
        if (!await menuAuthorization.HasAccessAsync(User, AdminMenuOptions.Dashboard)) return Forbid();
        ViewBag.UserCount = await userManager.Users.CountAsync();
        ViewBag.RoleCount = await roleManager.Roles.CountAsync();
        return View();
    }

    [Authorize, HttpGet("events")]
    public async Task<IActionResult> Events(int page = 1, string? search = null, DateOnly? startDate = null, DateOnly? endDate = null)
    {
        if (!await menuAuthorization.HasAccessAsync(User, AdminMenuOptions.Events)) return Forbid();
        const int pageSize = 10;
        startDate ??= DateOnly.FromDateTime(DateTime.Today);
        endDate ??= startDate.Value.AddDays(30);
        if (endDate < startDate) endDate = startDate;
        var start = startDate.Value.ToDateTime(TimeOnly.MinValue);
        var endExclusive = endDate.Value.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var normalizedSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        var events = ticketDb.Events.AsNoTracking().Where(e => e.StartsAt >= start && e.StartsAt < endExclusive);
        if (normalizedSearch is not null) events = events.Where(e => e.Name.Contains(normalizedSearch));
        var totalItems = await events.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)pageSize));
        page = Math.Clamp(page, 1, totalPages);
        var items = await events.OrderBy(e => e.StartsAt).ThenBy(e => e.Name)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(e => new Event
            {
                Id = e.Id,
                Name = e.Name,
                Venue = e.Venue,
                StartsAt = e.StartsAt,
                Status = e.Status,
                Impulsar = e.Impulsar
            })
            .ToListAsync();
        return View(new EventListViewModel { Items = items, Page = page, PageSize = pageSize, TotalItems = totalItems, Search = normalizedSearch, StartDate = startDate.Value, EndDate = endDate.Value });
    }

    [Authorize, HttpGet("events/create")]
    public async Task<IActionResult> CreateEvent()
    {
        if (!await menuAuthorization.HasAccessAsync(User, AdminMenuOptions.Events)) return Forbid();
        return View("EventForm", new EventFormViewModel());
    }

    [Authorize, HttpPost("events/create"), ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateEvent(EventFormViewModel model)
    {
        if (!await menuAuthorization.HasAccessAsync(User, AdminMenuOptions.Events)) return Forbid();
        ValidateEvent(model);
        ValidateTicketTypes(model);
        ValidateBanner(model);
        if (!ModelState.IsValid) return View("EventForm", model);

        var item = new Event
        {
            Name = model.Name.Trim(), Description = model.Description.Trim(), Venue = model.Venue.Trim(),
            StartsAt = model.StartsAt, Price = model.TicketTypes.Min(x => x.Price), AvailableTickets = model.TicketTypes.Sum(x => x.AvailableTickets),
            Status = model.Status, Impulsar = model.Impulsar
        };
        await ApplyBannerAsync(item, model.Banner);
        foreach (var type in model.TicketTypes) item.TicketTypes.Add(new TicketType { Name = type.Name.Trim(), Price = type.Price, AvailableTickets = type.AvailableTickets });
        ticketDb.Events.Add(item);
        await ticketDb.SaveChangesAsync();
        TempData["Success"] = "Evento creado correctamente.";
        return RedirectToAction(nameof(Events));
    }

    [Authorize, HttpGet("events/{id}/edit")]
    public async Task<IActionResult> EditEvent(int id)
    {
        if (!await menuAuthorization.HasAccessAsync(User, AdminMenuOptions.Events)) return Forbid();
        var model = await ticketDb.Events.AsNoTracking().Where(x => x.Id == id).Select(item => new EventFormViewModel
        {
            Id = item.Id, Name = item.Name, Description = item.Description, Venue = item.Venue, StartsAt = item.StartsAt, Status = item.Status, Impulsar = item.Impulsar,
            HasExistingBanner = item.BannerBase64 != null && item.BannerContentType != null,
            TicketTypes = item.TicketTypes.OrderBy(x => x.Name).Select(x => new EventTicketTypeForm { Id = x.Id, Name = x.Name, Price = x.Price, AvailableTickets = x.AvailableTickets }).ToList()
        }).FirstOrDefaultAsync();
        return model is null ? NotFound() : View("EventForm", model);
    }

    [Authorize, HttpGet("events/{id}/banner-preview")]
    public async Task<IActionResult> EventBannerPreview(int id)
    {
        if (!await menuAuthorization.HasAccessAsync(User, AdminMenuOptions.Events)) return Forbid();
        var banner = await ticketDb.Events.AsNoTracking().Where(e => e.Id == id).Select(e => new { e.BannerBase64, e.BannerContentType }).FirstOrDefaultAsync();
        if (banner is null || string.IsNullOrWhiteSpace(banner.BannerBase64) || string.IsNullOrWhiteSpace(banner.BannerContentType)) return NotFound();
        try { return File(ResizeBanner(Convert.FromBase64String(banner.BannerBase64), banner.BannerContentType, out var contentType), contentType); }
        catch (FormatException) { return NotFound(); }
    }

    [Authorize, HttpPost("events/{id}/edit"), ValidateAntiForgeryToken]
    public async Task<IActionResult> EditEvent(int id, EventFormViewModel model)
    {
        if (!await menuAuthorization.HasAccessAsync(User, AdminMenuOptions.Events)) return Forbid();
        var item = await ticketDb.Events.Include(x => x.TicketTypes).FirstOrDefaultAsync(x => x.Id == id);
        if (item is null) return NotFound();
        model.Id = id;
        ValidateEvent(model);
        ValidateTicketTypes(model);
        ValidateBanner(model);
        await ValidateRemovedTicketTypesAsync(model, item);
        if (!ModelState.IsValid) { SetExistingBanner(model, item); return View("EventForm", model); }

        item.Name = model.Name.Trim(); item.Description = model.Description.Trim(); item.Venue = model.Venue.Trim();
        item.StartsAt = model.StartsAt; item.Price = model.TicketTypes.Min(x => x.Price); item.AvailableTickets = model.TicketTypes.Sum(x => x.AvailableTickets);
        item.Status = model.Status; item.Impulsar = model.Impulsar;
        await ApplyTicketTypesAsync(model, item);
        if (model.Banner is not null && model.Banner.Length > 0) await ApplyBannerAsync(item, model.Banner);
        else if (model.RemoveBanner) { item.BannerBase64 = null; item.BannerContentType = null; }
        await ticketDb.SaveChangesAsync();
        TempData["Success"] = "Evento actualizado correctamente.";
        return RedirectToAction(nameof(Events));
    }

    [Authorize, HttpPost("events/{id}/delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteEvent(int id)
    {
        if (!await menuAuthorization.HasAccessAsync(User, AdminMenuOptions.Events)) return Forbid();
        var item = await ticketDb.Events.Include(x => x.TicketTypes).FirstOrDefaultAsync(x => x.Id == id);
        if (item is null) return NotFound();
        if (await ticketDb.OrderItems.AnyAsync(x => x.EventId == id))
        {
            TempData["Error"] = "El evento tiene ventas registradas y no puede eliminarse. Cámbialo a Cancelado o Finalizado.";
            return RedirectToAction(nameof(Events));
        }
        ticketDb.TicketTypes.RemoveRange(item.TicketTypes);
        ticketDb.Events.Remove(item);
        await ticketDb.SaveChangesAsync();
        TempData["Success"] = "Evento eliminado correctamente.";
        return RedirectToAction(nameof(Events));
    }

    [Authorize, HttpGet("email-template")]
    public async Task<IActionResult> PurchaseConfirmationEmailTemplate()
    {
        if (!await menuAuthorization.HasAccessAsync(User, AdminMenuOptions.EmailTemplate)) return Forbid();
        var template = await ticketDb.PurchaseConfirmationEmailTemplates.AsNoTracking().OrderBy(x => x.Id).FirstOrDefaultAsync();
        return View(new PurchaseConfirmationEmailTemplateViewModel { Html = template?.Html ?? PurchaseConfirmationEmailTemplateDefaults.Html });
    }

    [Authorize, HttpPost("email-template"), ValidateAntiForgeryToken]
    public async Task<IActionResult> PurchaseConfirmationEmailTemplate(PurchaseConfirmationEmailTemplateViewModel model)
    {
        if (!await menuAuthorization.HasAccessAsync(User, AdminMenuOptions.EmailTemplate)) return Forbid();
        if (string.IsNullOrWhiteSpace(model.Html)) ModelState.AddModelError(nameof(model.Html), "Ingresa el contenido HTML del correo.");
        if (model.Html?.Length > 200_000) ModelState.AddModelError(nameof(model.Html), "El HTML no puede superar 200,000 caracteres.");
        if (!ModelState.IsValid) return View(model);

        var template = await ticketDb.PurchaseConfirmationEmailTemplates.OrderBy(x => x.Id).FirstOrDefaultAsync();
        if (template is null) { template = new PurchaseConfirmationEmailTemplate(); ticketDb.PurchaseConfirmationEmailTemplates.Add(template); }
        template.Html = model.Html.Trim();
        template.UpdatedAt = DateTime.UtcNow;
        await ticketDb.SaveChangesAsync();
        TempData["Success"] = "Plantilla de correo actualizada. Se usará en las próximas compras confirmadas.";
        return RedirectToAction(nameof(PurchaseConfirmationEmailTemplate));
    }

    [Authorize, HttpGet("ticket-scanner")]
    public async Task<IActionResult> TicketScanner()
    {
        if (!await menuAuthorization.HasAccessAsync(User, AdminMenuOptions.TicketScanner)) return Forbid();
        return View();
    }

    [Authorize, HttpPost("ticket-scanner/lookup"), ValidateAntiForgeryToken]
    public async Task<IActionResult> LookupTicket([FromForm] string qr)
    {
        if (!await menuAuthorization.HasAccessAsync(User, AdminMenuOptions.TicketScanner)) return Forbid();
        if (!TryReadTicket(qr, out var itemId, out var number)) return BadRequest(new { message = "El código QR no es válido." });
        var item = await ticketDb.OrderItems.Include(x => x.Order).AsNoTracking().FirstOrDefaultAsync(x => x.Id == itemId);
        if (item is null || item.Order.PaymentStatus != "Approved" || number > item.Quantity) return NotFound(new { message = "La entrada no existe o no está confirmada." });
        var ticket = await ticketDb.AdmissionTickets.AsNoTracking().FirstOrDefaultAsync(x => x.OrderItemId == itemId && x.TicketNumber == number);
        return Json(new { orderItemId = itemId, number, order = item.Order.OrderNumber, item.EventName, locality = item.TicketTypeName, used = ticket?.UsedAt is not null, usedAt = ticket?.UsedAt?.ToLocalTime().ToString("g") });
    }

    [Authorize, HttpPost("ticket-scanner/use"), ValidateAntiForgeryToken]
    public async Task<IActionResult> UseTicket([FromForm] string qr, CancellationToken cancellationToken)
    {
        if (!await menuAuthorization.HasAccessAsync(User, AdminMenuOptions.TicketScanner)) return Forbid();
        if (!TryReadTicket(qr, out var itemId, out var number)) return BadRequest(new { message = "El código QR no es válido." });
        await using var transaction = await PaymentOrderService.LockAsync(ticketDb, $"admission:{itemId}:{number}", cancellationToken);
        var item = await ticketDb.OrderItems.Include(x => x.Order).SingleOrDefaultAsync(x => x.Id == itemId, cancellationToken);
        if (item is null || item.Order.PaymentStatus != "Approved" || number > item.Quantity) return NotFound(new { message = "La entrada no existe o no está confirmada." });
        var ticket = await ticketDb.AdmissionTickets.SingleOrDefaultAsync(x => x.OrderItemId == itemId && x.TicketNumber == number, cancellationToken);
        if (ticket?.UsedAt is not null) return Conflict(new { message = "Este QR ya fue utilizado.", usedAt = ticket.UsedAt.Value.ToLocalTime().ToString("g") });
        ticket ??= new AdmissionTicket { OrderItemId = itemId, TicketNumber = number };
        if (ticket.Id == 0) ticketDb.AdmissionTickets.Add(ticket);
        ticket.UsedAt = DateTime.UtcNow;
        ticket.UsedBy = User.Identity?.Name;
        await ticketDb.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Json(new { message = "Entrada utilizada correctamente.", eventName = item.EventName, locality = item.TicketTypeName });
    }


    [Authorize, HttpGet("users")]
    public async Task<IActionResult> Users(int page = 1, string? search = null, string[]? roles = null)
    {
        if (!await menuAuthorization.HasAccessAsync(User, AdminMenuOptions.Users)) return Forbid();
        const int pageSize = 10;
        var availableRoles = await roleManager.Roles
            .OrderBy(role => role.Name)
            .Select(role => role.Name!)
            .ToListAsync();
        var selectedRoles = roles is null
            ? availableRoles.Where(role => !string.Equals(role, AdminIdentitySeed.CustomerRole, StringComparison.OrdinalIgnoreCase)).ToList()
            : roles.Where(role => availableRoles.Contains(role, StringComparer.OrdinalIgnoreCase)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        var users = await userManager.Users.OrderBy(x => x.UserName).ToListAsync();
        var rows = new List<UserListRow>();
        foreach (var user in users)
        {
            rows.Add(new UserListRow { User = user, Roles = (await userManager.GetRolesAsync(user)).ToList() });
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim();
            rows = rows.Where(row => (row.User.UserName ?? string.Empty).Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)).ToList();
        }
        if (selectedRoles.Count > 0)
        {
            rows = rows.Where(row => row.Roles.Any(role => selectedRoles.Contains(role, StringComparer.OrdinalIgnoreCase))).ToList();
        }

        var totalItems = rows.Count;
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)pageSize));
        page = Math.Clamp(page, 1, totalPages);
        return View(new UserListViewModel
        {
            Items = rows.Skip((page - 1) * pageSize).Take(pageSize).ToList(),
            AvailableRoles = availableRoles,
            SelectedRoles = selectedRoles,
            Search = search?.Trim(),
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems
        });
    }

    [Authorize, HttpGet("users/create")]
    public async Task<IActionResult> CreateUser()
    {
        if (!await menuAuthorization.HasAccessAsync(User, AdminMenuOptions.Users)) return Forbid();
        return View(await UserForm());
    }

    [Authorize, HttpPost("users/create"), ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateUser(CreateUserViewModel model)
    {
        if (!await menuAuthorization.HasAccessAsync(User, AdminMenuOptions.Users)) return Forbid();
        model.Roles = await RoleNames();
        if (!ModelState.IsValid) return View(model);
        var user = new IdentityUser { UserName = model.UserName };
        var result = await userManager.CreateAsync(user, model.Password);
        if (result.Succeeded) result = await userManager.AddToRoleAsync(user, model.Role);
        if (result.Succeeded) { TempData["Success"] = "Usuario creado correctamente."; return RedirectToAction(nameof(Users)); }
        foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Description);
        return View(model);
    }

    [Authorize, HttpGet("users/{id}/edit")]
    public async Task<IActionResult> EditUser(string id)
    {
        if (!await menuAuthorization.HasAccessAsync(User, AdminMenuOptions.Users)) return Forbid();
        var user = await userManager.FindByIdAsync(id);
        if (user is null) return NotFound();
        return View(new EditUserViewModel { Id = user.Id, UserName = user.UserName ?? string.Empty, Role = (await userManager.GetRolesAsync(user)).FirstOrDefault() ?? string.Empty, Roles = await RoleNames() });
    }

    [Authorize, HttpPost("users/{id}/edit"), ValidateAntiForgeryToken]
    public async Task<IActionResult> EditUser(string id, EditUserViewModel model)
    {
        if (!await menuAuthorization.HasAccessAsync(User, AdminMenuOptions.Users)) return Forbid();
        var user = await userManager.FindByIdAsync(id);
        if (user is null) return NotFound();
        model.Id = id; model.Roles = await RoleNames();
        if (user.Id == userManager.GetUserId(User) && model.Role != AdminIdentitySeed.SuperAdminRole) ModelState.AddModelError(nameof(model.Role), "No puedes quitarte el rol SuperAdmin.");
        if (!ModelState.IsValid) return View(model);
        user.UserName = model.UserName.Trim();
        var result = await userManager.UpdateAsync(user);
        if (result.Succeeded && !string.IsNullOrWhiteSpace(model.Password)) result = await userManager.RemovePasswordAsync(user);
        if (result.Succeeded && !string.IsNullOrWhiteSpace(model.Password)) result = await userManager.AddPasswordAsync(user, model.Password);
        if (result.Succeeded)
        {
            var currentRoles = await userManager.GetRolesAsync(user);
            result = await userManager.RemoveFromRolesAsync(user, currentRoles);
            if (result.Succeeded) result = await userManager.AddToRoleAsync(user, model.Role);
        }
        if (result.Succeeded) { TempData["Success"] = "Usuario actualizado correctamente."; return RedirectToAction(nameof(Users)); }
        foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Description);
        return View(model);
    }

    [Authorize, HttpPost("users/{id}/delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUser(string id)
    {
        if (!await menuAuthorization.HasAccessAsync(User, AdminMenuOptions.Users)) return Forbid();
        var user = await userManager.FindByIdAsync(id);
        if (user is null) return NotFound();
        if (user.Id == userManager.GetUserId(User)) { TempData["Error"] = "No puedes eliminar tu propia cuenta."; return RedirectToAction(nameof(Users)); }
        var result = await userManager.DeleteAsync(user);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? "Usuario eliminado correctamente." : string.Join(" ", result.Errors.Select(x => x.Description));
        return RedirectToAction(nameof(Users));
    }

    [Authorize, HttpGet("roles")]
    public async Task<IActionResult> Roles()
    {
        if (!await menuAuthorization.HasAccessAsync(User, AdminMenuOptions.Roles)) return Forbid();
        return View(await roleManager.Roles.OrderBy(r => r.Name).ToListAsync());
    }

    [Authorize, HttpPost("roles/create"), ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateRole(CreateRoleViewModel model)
    {
        if (!await menuAuthorization.HasAccessAsync(User, AdminMenuOptions.Roles)) return Forbid();
        if (!ModelState.IsValid)
        {
            TempData["Error"] = ModelState.Values.SelectMany(value => value.Errors).Select(error => error.ErrorMessage).FirstOrDefault() ?? "Ingresa un nombre de rol válido.";
            return RedirectToAction(nameof(Roles));
        }
        var result = await roleManager.CreateAsync(new IdentityRole(model.Name.Trim()));
        if (result.Succeeded) TempData["Success"] = "Rol creado correctamente.";
        else foreach (var error in result.Errors) TempData["Error"] = error.Description;
        return RedirectToAction(nameof(Roles));
    }

    [Authorize, HttpGet("roles/{id}/edit")]
    public async Task<IActionResult> EditRole(string id)
    {
        if (!await menuAuthorization.HasAccessAsync(User, AdminMenuOptions.Roles)) return Forbid();
        var role = await roleManager.FindByIdAsync(id);
        if (role is null) return NotFound();
        if (IsProtectedRole(role.Name)) return RedirectToAction(nameof(Roles));
        return View(new EditRoleViewModel { Id = role.Id, Name = role.Name ?? string.Empty });
    }

    [Authorize, HttpPost("roles/{id}/edit"), ValidateAntiForgeryToken]
    public async Task<IActionResult> EditRole(string id, EditRoleViewModel model)
    {
        if (!await menuAuthorization.HasAccessAsync(User, AdminMenuOptions.Roles)) return Forbid();
        var role = await roleManager.FindByIdAsync(id);
        if (role is null) return NotFound();
        if (IsProtectedRole(role.Name)) { TempData["Error"] = "Los roles Customer y SuperAdmin no pueden modificarse."; return RedirectToAction(nameof(Roles)); }
        if (!ModelState.IsValid) return View(model);
        role.Name = model.Name.Trim();
        var result = await roleManager.UpdateAsync(role);
        if (result.Succeeded) { TempData["Success"] = "Rol actualizado correctamente."; return RedirectToAction(nameof(Roles)); }
        foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Description);
        return View(model);
    }

    [Authorize, HttpPost("roles/{id}/delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteRole(string id)
    {
        if (!await menuAuthorization.HasAccessAsync(User, AdminMenuOptions.Roles)) return Forbid();
        var role = await roleManager.FindByIdAsync(id);
        if (role is null) return NotFound();
        if (IsProtectedRole(role.Name)) { TempData["Error"] = "Los roles Customer y SuperAdmin no pueden eliminarse."; return RedirectToAction(nameof(Roles)); }
        if ((await userManager.GetUsersInRoleAsync(role.Name!)).Any()) { TempData["Error"] = "No puedes eliminar un rol asignado a usuarios."; return RedirectToAction(nameof(Roles)); }
        var result = await roleManager.DeleteAsync(role);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? "Rol eliminado correctamente." : string.Join(" ", result.Errors.Select(x => x.Description));
        return RedirectToAction(nameof(Roles));
    }

    [Authorize(Roles = AdminIdentitySeed.SuperAdminRole), HttpGet("menu-security")]
    public async Task<IActionResult> MenuSecurity()
    {
        var roles = await roleManager.Roles.OrderBy(x => x.Name).ToListAsync();
        var roleIds = roles.Select(x => x.Id).ToList();
        var permissions = await identityDb.AdminMenuPermissions.Where(x => roleIds.Contains(x.RoleId)).ToListAsync();
        return View(new MenuSecurityViewModel
        {
            Options = AdminMenuOptions.All.Select(x => new MenuSecurityOption { Key = x.Key, Title = x.Title }).ToList(),
            Roles = roles.Select(role => new MenuSecurityRoleRow { RoleId = role.Id, RoleName = role.Name ?? string.Empty, GrantedKeys = permissions.Where(x => x.RoleId == role.Id).Select(x => x.MenuKey).ToHashSet() }).ToList()
        });
    }

    [Authorize(Roles = AdminIdentitySeed.SuperAdminRole), HttpPost("menu-security/{roleId}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveMenuSecurity(string roleId, List<string>? menuKeys)
    {
        var role = await roleManager.FindByIdAsync(roleId);
        if (role is null) return NotFound();
        if (role.Name == AdminIdentitySeed.SuperAdminRole) { TempData["Error"] = "SuperAdmin siempre tiene acceso total."; return RedirectToAction(nameof(MenuSecurity)); }
        var allowedKeys = AdminMenuOptions.All.Select(x => x.Key).ToHashSet();
        var requestedKeys = (menuKeys ?? []).Where(allowedKeys.Contains).Distinct().ToList();
        var existing = await identityDb.AdminMenuPermissions.Where(x => x.RoleId == roleId).ToListAsync();
        identityDb.AdminMenuPermissions.RemoveRange(existing);
        identityDb.AdminMenuPermissions.AddRange(requestedKeys.Select(key => new TicketSales.Models.AdminMenuPermission { RoleId = roleId, MenuKey = key }));
        await identityDb.SaveChangesAsync();
        TempData["Success"] = $"Permisos de {role.Name} actualizados.";
        return RedirectToAction(nameof(MenuSecurity));
    }

    [AllowAnonymous, HttpGet("access-denied")]
    public IActionResult AccessDenied() => View();

    private async Task<CreateUserViewModel> UserForm() => new() { Roles = await RoleNames() };
    private async Task<List<string>> RoleNames() => await roleManager.Roles.OrderBy(r => r.Name).Select(r => r.Name!).ToListAsync();
    private void ValidateEvent(EventFormViewModel model)
    {
        if (!EventStatuses.All.Contains(model.Status)) ModelState.AddModelError(nameof(model.Status), "Selecciona un estado válido.");
        if (model.Status == EventStatuses.Published && model.StartsAt <= DateTime.Now) ModelState.AddModelError(nameof(model.StartsAt), "Un evento publicado debe tener una fecha futura.");
        if (model.Impulsar && model.Status != EventStatuses.Published) ModelState.AddModelError(nameof(model.Impulsar), "Solo los eventos publicados pueden ser destacados.");
    }
    private void ValidateTicketTypes(EventFormViewModel model)
    {
        if (model.TicketTypes.Count == 0) { ModelState.AddModelError(nameof(model.TicketTypes), "Agrega al menos una localidad."); return; }
        for (var index = 0; index < model.TicketTypes.Count; index++)
        {
            var type = model.TicketTypes[index];
            if (string.IsNullOrWhiteSpace(type.Name)) ModelState.AddModelError($"TicketTypes[{index}].Name", "Indica el nombre de la localidad.");
            if (type.Price <= 0) ModelState.AddModelError($"TicketTypes[{index}].Price", "El precio debe ser mayor que cero.");
            if (type.AvailableTickets < 0) ModelState.AddModelError($"TicketTypes[{index}].AvailableTickets", "La disponibilidad no puede ser negativa.");
        }
        if (model.TicketTypes.GroupBy(x => x.Name.Trim(), StringComparer.OrdinalIgnoreCase).Any(x => x.Key.Length > 0 && x.Count() > 1)) ModelState.AddModelError(nameof(model.TicketTypes), "No repitas nombres de localidad.");
    }
    private async Task ValidateRemovedTicketTypesAsync(EventFormViewModel model, Event item)
    {
        var submittedIds = model.TicketTypes.Where(x => x.Id != 0).Select(x => x.Id).ToHashSet();
        foreach (var removed in item.TicketTypes.Where(x => !submittedIds.Contains(x.Id)))
            if (await ticketDb.OrderItems.AnyAsync(x => x.TicketTypeId == removed.Id)) ModelState.AddModelError(nameof(model.TicketTypes), $"No puedes quitar la localidad {removed.Name} porque tiene ventas registradas.");
    }
    private async Task ApplyTicketTypesAsync(EventFormViewModel model, Event item)
    {
        var submittedIds = model.TicketTypes.Where(x => x.Id != 0).Select(x => x.Id).ToHashSet();
        foreach (var removed in item.TicketTypes.Where(x => !submittedIds.Contains(x.Id)).ToList()) ticketDb.TicketTypes.Remove(removed);
        foreach (var formType in model.TicketTypes)
        {
            var type = formType.Id == 0 ? null : item.TicketTypes.FirstOrDefault(x => x.Id == formType.Id);
            if (type is null)
            {
                item.TicketTypes.Add(new TicketType { Name = formType.Name.Trim(), Price = formType.Price, AvailableTickets = formType.AvailableTickets });
            }
            else
            {
                type.Name = formType.Name.Trim(); type.Price = formType.Price; type.AvailableTickets = formType.AvailableTickets;
            }
        }
    }
    private void ValidateBanner(EventFormViewModel model)
    {
        if (model.Banner is null || model.Banner.Length == 0) return;
        var allowedTypes = new[] { "image/jpeg", "image/png", "image/webp", "image/gif" };
        if (model.Banner.Length > 5 * 1024 * 1024) ModelState.AddModelError(nameof(model.Banner), "El banner no puede superar 5 MB.");
        else if (!allowedTypes.Contains(model.Banner.ContentType, StringComparer.OrdinalIgnoreCase)) ModelState.AddModelError(nameof(model.Banner), "Usa una imagen JPG, PNG, WEBP o GIF.");
    }
    private static async Task ApplyBannerAsync(Event item, IFormFile? banner)
    {
        if (banner is null || banner.Length == 0) return;
        await using var stream = new MemoryStream();
        await banner.CopyToAsync(stream);
        item.BannerBase64 = Convert.ToBase64String(ResizeBanner(stream.ToArray(), banner.ContentType, out var contentType));
        item.BannerContentType = contentType;
    }
    private static void SetExistingBanner(EventFormViewModel model, Event item)
    {
        model.HasExistingBanner = !string.IsNullOrWhiteSpace(item.BannerBase64) && !string.IsNullOrWhiteSpace(item.BannerContentType);
    }
    private static byte[] ResizeBanner(byte[] source, string contentType, out string outputContentType)
    {
#pragma warning disable CA1416 // El portal se ejecuta en Windows y System.Drawing se usa solo para imágenes administrativas.
        outputContentType = contentType;
        if (contentType is not "image/jpeg" and not "image/png") return source;
        try
        {
            using var sourceStream = new MemoryStream(source);
            using var image = Image.FromStream(sourceStream);
            var scale = Math.Min(1d, Math.Min(1280d / image.Width, 720d / image.Height));
            var width = Math.Max(1, (int)Math.Round(image.Width * scale));
            var height = Math.Max(1, (int)Math.Round(image.Height * scale));
            using var resized = new Bitmap(width, height);
            using (var graphics = Graphics.FromImage(resized))
            {
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.DrawImage(image, 0, 0, width, height);
            }
            using var output = new MemoryStream();
            var encoder = ImageCodecInfo.GetImageEncoders().First(codec => codec.FormatID == ImageFormat.Jpeg.Guid);
            using var parameters = new EncoderParameters(1);
            parameters.Param[0] = new EncoderParameter(Encoder.Quality, 82L);
            resized.Save(output, encoder, parameters);
            outputContentType = "image/jpeg";
            return output.ToArray();
        }
        catch (ArgumentException) { return source; }
        catch (ExternalException) { return source; }
#pragma warning restore CA1416
    }
    private bool TryReadTicket(string qr, out int itemId, out int number)
    {
        itemId = number = 0;
        try
        {
            var parts = personalDataProtector.Unprotect(qr.Trim()).Split(':');
            return parts.Length == 3 && int.TryParse(parts[1], out itemId) && int.TryParse(parts[2], out number) && itemId > 0 && number > 0;
        }
        catch (System.Security.Cryptography.CryptographicException) { return false; }
    }

    private static bool IsProtectedRole(string? role) => role is AdminIdentitySeed.SuperAdminRole or AdminIdentitySeed.CustomerRole;
}
