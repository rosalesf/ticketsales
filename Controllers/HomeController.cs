using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using TicketSales.Data;
using TicketSales.Models;
using TicketSales.Services;

namespace TicketSales.Controllers;

public class HomeController(TicketDbContext db, AdminIdentityDbContext identityDb, PaymentOrderService payments, IPersonalDataProtector personalDataProtector, IOrderQrCodeGenerator orderQrCodeGenerator, ILogger<HomeController> logger) : Controller
{
    private const string CartKey = "cart";
    public async Task<IActionResult> Index()
    {
        var checkoutToken = HttpContext.Session.GetString("checkout-token");
        if (checkoutToken is not null) await SynchronizeReservationAsync(checkoutToken, default);
        var events = await db.Events.Include(e => e.TicketTypes)
            .Where(e => e.Status == EventStatuses.Published && e.StartsAt > DateTime.Now)
            .OrderBy(e => e.StartsAt).ToListAsync();
        return View(new HomeIndexViewModel
        {
            Events = events,
            FeaturedEvents = events.Where(e => e.Impulsar && e.TicketTypes.Any(t => t.AvailableTickets > 0)).ToList()
        });
    }
    public async Task<IActionResult> Details(int id)
    {
        var item = await db.Events.Include(e => e.TicketTypes)
            .FirstOrDefaultAsync(e => e.Id == id && e.Status == EventStatuses.Published && e.StartsAt > DateTime.Now);
        return item is null ? NotFound() : View(item);
    }
    public async Task<IActionResult> Cart()
    {
        var reserved = new Dictionary<int, int>();
        var token = HttpContext.Session.GetString("checkout-token");
        if (token is not null)
        {
            await SynchronizeReservationAsync(token, default);
            var order = await db.Orders.AsNoTracking().Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.PaymentAccessToken == token && o.InventoryReserved);
            if (order is not null) reserved = order.Items.ToDictionary(item => item.TicketTypeId, item => item.Quantity);
        }
        ViewData["ReservedQuantities"] = reserved;
        return View(GetCart());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddToCart(int eventId, int ticketTypeId, int quantity = 1)
    {
        var ticketType = await db.TicketTypes.Include(t => t.Event).FirstOrDefaultAsync(t => t.Id == ticketTypeId && t.EventId == eventId);
        if (ticketType is null || ticketType.Event.Status != EventStatuses.Published || ticketType.Event.StartsAt <= DateTime.Now) return NotFound();
        var cart = GetCart(); var item = cart.FirstOrDefault(x => x.TicketTypeId == ticketTypeId);
        var desired = (item?.Quantity ?? 0) + Math.Max(1, quantity);
        if (desired > ticketType.AvailableTickets) { TempData["Error"] = "No hay suficientes entradas disponibles de este tipo."; return RedirectToAction(nameof(Details), new { id = eventId }); }
        if (item is null) cart.Add(new CartItem { EventId = eventId, EventName = ticketType.Event.Name, TicketTypeId = ticketType.Id, TicketTypeName = ticketType.Name, UnitPrice = ticketType.Price, Quantity = Math.Max(1, quantity) });
        else { item.UnitPrice = ticketType.Price; item.Quantity = desired; }
        try
        {
            var token = HttpContext.Session.GetString("checkout-token");
            if (token is not null) await payments.UpdateReservationAsync(token, cart, default);
        }
        catch (InvalidOperationException exception)
        {
            TempData["Error"] = exception.Message;
            return RedirectToAction(nameof(Cart));
        }
        SaveCart(cart); TempData["Success"] = "Entradas agregadas al carrito."; return RedirectToAction(nameof(Cart));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateCart(int ticketTypeId, int quantity)
    {
        var cart = GetCart(); var item = cart.FirstOrDefault(x => x.TicketTypeId == ticketTypeId);
        if (item is null) return RedirectToAction(nameof(Cart));
        var ticketType = await db.TicketTypes.Include(t => t.Event).FirstOrDefaultAsync(t => t.Id == ticketTypeId);
        if (ticketType is null) { TempData["Error"] = "La entrada ya no está disponible."; return RedirectToAction(nameof(Cart)); }

        var updatedCart = quantity <= 0
            ? cart.Where(x => x.TicketTypeId != ticketTypeId).ToList()
            : cart.Select(x => x.TicketTypeId == ticketTypeId
                ? new CartItem { EventId = ticketType.EventId, EventName = ticketType.Event.Name, TicketTypeId = ticketType.Id, TicketTypeName = ticketType.Name, UnitPrice = ticketType.Price, Quantity = quantity }
                : x).ToList();
        try
        {
            var token = HttpContext.Session.GetString("checkout-token");
            if (token is not null) await payments.UpdateReservationAsync(token, updatedCart, default);
            else if (quantity > 0 && quantity > ticketType.AvailableTickets) throw new InvalidOperationException("No hay más entradas disponibles de este tipo.");
            SaveCart(updatedCart);
        }
        catch (InvalidOperationException exception) { TempData["Error"] = exception.Message; }
        return RedirectToAction(nameof(Cart));
    }

    public async Task<IActionResult> Checkout(string? token = null, bool fromPayment = false)
    {
        token ??= HttpContext.Session.GetString("checkout-token");
        if (token is not null)
        {
            await SynchronizeReservationAsync(token, default);
            var pending = await db.Orders.AsNoTracking().Include(o => o.Items).FirstOrDefaultAsync(o => o.PaymentAccessToken == token);
            if (pending is not null && pending.PaymentStatus == "Approved")
                return RedirectToAction(nameof(Confirmation), new { id = pending.Id, token });
            if (pending is not null && pending.InventoryReserved)
            {
                HttpContext.Session.SetString("checkout-token", token);
                ViewData["PaymentsConfigured"] = payments.IsConfigured;
                ViewData["PendingReservation"] = true;
                return View(new CheckoutViewModel
                {
                    CheckoutToken = token,
                    CustomerName = personalDataProtector.Unprotect(pending.CustomerName),
                    CustomerEmail = personalDataProtector.Unprotect(pending.CustomerEmail),
                    CustomerPhone = personalDataProtector.Unprotect(pending.CustomerPhone),
                    InvoiceType = pending.InvoiceType,
                    InvoiceNit = pending.InvoiceNit is null ? null : personalDataProtector.Unprotect(pending.InvoiceNit),
                    InvoiceName = pending.InvoiceName is null ? null : personalDataProtector.Unprotect(pending.InvoiceName),
                    AcceptedTerms = pending.AcceptedTerms,
                    Items = pending.Items.Select(item => new CartItem
                    {
                        EventId = item.EventId,
                        EventName = item.EventName,
                        TicketTypeId = item.TicketTypeId,
                        TicketTypeName = item.TicketTypeName,
                        UnitPrice = item.UnitPrice,
                        Quantity = item.Quantity
                    }).ToList()
                });
            }
        }
        token = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        HttpContext.Session.SetString("checkout-token", token);
        var cart = await RefreshCartFromDatabase(GetCart()); SaveCart(cart);
        if (cart.Count == 0) return RedirectToAction(nameof(Index));
        var model = new CheckoutViewModel { Items = cart, CheckoutToken = token };
        ViewData["PaymentsConfigured"] = payments.IsConfigured;
        await ApplyCustomerProfileAsync(model);
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Checkout(CheckoutViewModel model, CancellationToken cancellationToken)
    {
        ViewData["PaymentsConfigured"] = payments.IsConfigured;
        if (string.IsNullOrEmpty(model.CheckoutToken) || model.CheckoutToken != HttpContext.Session.GetString("checkout-token")) return BadRequest();
        var existing = await db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.PaymentAccessToken == model.CheckoutToken, cancellationToken);
        if (existing is not null) return RedirectToAction(nameof(Pay), new { id = existing.Id, token = existing.PaymentAccessToken });
        var cart = GetCart(); model.Items = cart;
        if (await ApplyCustomerProfileAsync(model))
        {
            foreach (var field in new[] { nameof(model.CustomerName), nameof(model.CustomerEmail), nameof(model.CustomerPhone), nameof(model.InvoiceType), nameof(model.InvoiceNit), nameof(model.InvoiceName) }) ModelState.Remove(field);
        }
        if (cart.Count == 0) ModelState.AddModelError(string.Empty, "Tu carrito está vacío.");
        if (!model.AcceptedTerms) ModelState.AddModelError(nameof(model.AcceptedTerms), "Debes aceptar los términos y condiciones.");
        if (!ModelState.IsValid) return View(model);
        var typeIds = cart.Select(c => c.TicketTypeId).ToList();
        var types = await db.TicketTypes.Include(t => t.Event).Where(t => typeIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, cancellationToken);
        if (cart.Any(c => !types.TryGetValue(c.TicketTypeId, out var t) || t.EventId != c.EventId || t.Event.Status != EventStatuses.Published || t.Event.StartsAt <= DateTime.Now || c.Quantity > t.AvailableTickets)) { ModelState.AddModelError(string.Empty, "Una o más entradas ya no están disponibles para compra."); return View(model); }
        foreach (var item in cart) { var type = types[item.TicketTypeId]; item.UnitPrice = type.Price; item.EventName = type.Event.Name; item.TicketTypeName = type.Name; }
        model.Items = cart; var total = cart.Sum(x => x.Subtotal);

        var order = new Order { OrderNumber = CreateOrderNumber(), CustomerName = personalDataProtector.Protect(model.CustomerName.Trim()), CustomerEmail = personalDataProtector.Protect(model.CustomerEmail.Trim()), CustomerUserId = IsCustomerAuthenticated() ? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value : null, CustomerPhone = personalDataProtector.Protect(model.CustomerPhone.Trim()), InvoiceType = model.InvoiceType, InvoiceNit = model.InvoiceType == "NIT" ? personalDataProtector.Protect(model.InvoiceNit!.Trim()) : null, InvoiceName = model.InvoiceType == "NIT" ? personalDataProtector.Protect(model.InvoiceName!.Trim()) : null, AcceptedTerms = model.AcceptedTerms, Total = total, PaymentAccessToken = model.CheckoutToken };
        foreach (var item in cart) order.Items.Add(new OrderItem { EventId = item.EventId, EventName = item.EventName, TicketTypeId = item.TicketTypeId, TicketTypeName = item.TicketTypeName, UnitPrice = item.UnitPrice, Quantity = item.Quantity });
        try
        {
            order = await payments.ReserveAsync(order, cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return View(model);
        }
        return RedirectToAction(nameof(Pay), new { id = order.Id, token = order.PaymentAccessToken });
    }
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Pay(int id, string? token, CancellationToken cancellationToken)
    {
        var order = await db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
        if (order is null || !CanAccess(order, token)) return NotFound();
        Response.Headers["Referrer-Policy"] = "no-referrer";
        if (order.PaymentStatus is "Approved" or "Expired" or "Review")
            return RedirectToAction(nameof(Confirmation), new { id, token });
        await payments.SynchronizeAsync(id, cancellationToken);
        order = await db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
        if (order is null || order.PaymentStatus is "Approved" or "Expired" or "Review")
            return RedirectToAction(nameof(Confirmation), new { id, token });
        try { await payments.EnsureCheckoutAsync(id, cancellationToken); }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("No se pudo iniciar checkout para orden {OrderId}. Tipo: {ErrorType}.", id, exception.GetType().Name);
            ViewData["PaymentError"] = "No pudimos cargar el formulario de pago ("
                + exception.GetType().Name + "): " + exception.Message;
        }
        order = await db.Orders.AsNoTracking().Include(o => o.Items).SingleAsync(o => o.Id == id, cancellationToken);
        if (order.PaymentStatus is "Approved" or "Expired" or "Review")
            return RedirectToAction(nameof(Confirmation), new { id, token = order.PaymentAccessToken });
        return View(order);
    }

    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> PaymentReturn(string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(token)) return NotFound();
        var id = await db.Orders.Where(o => o.PaymentAccessToken == token).Select(o => (int?)o.Id).SingleOrDefaultAsync();
        if (id is null) return NotFound();

        // The browser return is a useful fallback when the webhook is delayed or unavailable.
        await payments.SynchronizeAsync(id.Value, cancellationToken);
        return RedirectToAction(nameof(Confirmation), new { id, token });
    }

    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> PaymentCancelled(string token)
    {
        if (string.IsNullOrEmpty(token)) return NotFound();
        var exists = await db.Orders.AsNoTracking().AnyAsync(o => o.PaymentAccessToken == token);
        if (!exists) return NotFound();

        var checkoutUrl = Url.Action(nameof(Checkout), new { token, fromPayment = true })!;
        var encodedUrl = JsonSerializer.Serialize(checkoutUrl);
        return Content($"<!doctype html><html><body><script>window.top.location.replace({encodedUrl});</script><a href=\"{checkoutUrl}\" target=\"_top\">Volver al checkout</a></body></html>", "text/html");
    }

    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> PaymentStatus(int id, string? token, CancellationToken cancellationToken)
    {
        var order = await db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == id);
        if (order is null || !CanAccess(order, token)) return NotFound();

        // Do not rely exclusively on the asynchronous webhook: the payment page polls this
        // endpoint after Recurrente reports success, so reconcile the provider's current state.
        await payments.SynchronizeAsync(id, cancellationToken);
        order = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == id, cancellationToken);
        return Json(new { status = order.PaymentStatus });
    }

    private bool CanAccess(Order order, string? token) =>
        (order.PaymentAccessToken is not null && (order.PaymentAccessToken == token || order.PaymentAccessToken == HttpContext.Session.GetString("checkout-token")))
        || (order.CustomerUserId is not null && order.CustomerUserId == User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value);

    private async Task SynchronizeReservationAsync(string token, CancellationToken cancellationToken)
    {
        var orderId = await db.Orders.AsNoTracking().Where(order => order.PaymentAccessToken == token)
            .Select(order => (int?)order.Id).SingleOrDefaultAsync(cancellationToken);
        if (orderId is not null) await payments.SynchronizeAsync(orderId.Value, cancellationToken);
    }

    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Confirmation(int id, string? token)
    {
        var order = await db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id);
        if (order is null || !CanAccess(order, token)) return NotFound();
        Response.Headers["Referrer-Policy"] = "no-referrer";
        await payments.SynchronizeAsync(id, default);
        order = await db.Orders.Include(o => o.Items).SingleAsync(o => o.Id == id);
        if (order.PaymentStatus == "Approved" && HttpContext.Session.GetString("checkout-token") == order.PaymentAccessToken)
        {
            var cart = GetCart();
            foreach (var item in order.Items)
            {
                var purchased = cart.FirstOrDefault(c => c.TicketTypeId == item.TicketTypeId && c.Quantity == item.Quantity && c.UnitPrice == item.UnitPrice);
                if (purchased is not null) cart.Remove(purchased);
            }
            SaveCart(cart);
            HttpContext.Session.Remove("checkout-token");
        }
        order.CustomerName = personalDataProtector.Unprotect(order.CustomerName);
        order.CustomerEmail = personalDataProtector.Unprotect(order.CustomerEmail);
        order.CustomerPhone = personalDataProtector.Unprotect(order.CustomerPhone);
        if (order.PaymentStatus == "Approved")
        {
            ViewData["TicketQrCodes"] = order.Items.OrderBy(item => item.Id)
                .SelectMany(item => Enumerable.Range(1, item.Quantity)
                    .Select(number => new TicketQrCode(item, number, orderQrCodeGenerator.GenerateDataUri(order, item, number))))
                .ToArray();
        }
        return View(order);
    }

    private async Task<List<CartItem>> RefreshCartFromDatabase(List<CartItem> cart)
    {
        var types = await db.TicketTypes.Include(t => t.Event).Where(t => cart.Select(x => x.TicketTypeId).Contains(t.Id)).ToListAsync(); var lookup = types.ToDictionary(t => t.Id);
        return cart.Where(x => lookup.ContainsKey(x.TicketTypeId) && x.Quantity > 0).Select(x => { var type = lookup[x.TicketTypeId]; x.EventId = type.EventId; x.EventName = type.Event.Name; x.TicketTypeName = type.Name; x.UnitPrice = type.Price; return x; }).ToList();
    }
    private async Task<bool> ApplyCustomerProfileAsync(CheckoutViewModel model)
    {
        if (!IsCustomerAuthenticated()) return false;
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return false;
        var profile = await identityDb.CustomerProfiles.FindAsync(userId);
        if (profile is null) return false;
        model.CustomerName = personalDataProtector.Unprotect(profile.FullName);
        model.CustomerEmail = User.Identity?.Name ?? string.Empty;
        model.CustomerPhone = personalDataProtector.Unprotect(profile.Phone);
        model.InvoiceType = profile.InvoiceType == "NIT" ? "NIT" : "CF";
        model.InvoiceNit = profile.InvoiceNit is null ? null : personalDataProtector.Unprotect(profile.InvoiceNit);
        model.InvoiceName = profile.InvoiceName is null ? null : personalDataProtector.Unprotect(profile.InvoiceName);
        ViewData["ProfileLocked"] = true;
        return true;
    }
    private bool IsCustomerAuthenticated() => User.Identity?.IsAuthenticated == true && User.IsInRole(AdminIdentitySeed.CustomerRole);
    private static string CreateOrderNumber() => $"ORD-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
    private List<CartItem> GetCart() => JsonSerializer.Deserialize<List<CartItem>>(HttpContext.Session.GetString(CartKey) ?? "[]") ?? [];
    private void SaveCart(List<CartItem> cart) => HttpContext.Session.SetString(CartKey, JsonSerializer.Serialize(cart));

}


