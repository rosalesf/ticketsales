using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using System.Text;
using TicketSales.Data;
using TicketSales.Models.Account;
using TicketSales.Models;
using TicketSales.Services;

namespace TicketSales.Controllers;

[Route("account")]
public class AccountController(UserManager<IdentityUser> userManager, SignInManager<IdentityUser> signInManager, AdminIdentityDbContext identityDb, TicketDbContext ticketDb, TicketSales.Services.IPersonalDataProtector personalDataProtector, IAccountEmailSender accountEmailSender, IConfiguration configuration, ILogger<AccountController> logger) : Controller
{
    [HttpGet("register")]
    public IActionResult Register(string? returnUrl = null) => View(new RegisterViewModel { ReturnUrl = returnUrl });

    [HttpPost("register"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var email = model.Email.Trim();
        if (await userManager.FindByEmailAsync(email) is not null) { ModelState.AddModelError(nameof(model.Email), "Ya existe una cuenta con este correo."); return View(model); }
        var user = new IdentityUser { UserName = email, Email = email };
        var result = await userManager.CreateAsync(user, model.Password);
        if (result.Succeeded) result = await userManager.AddToRoleAsync(user, AdminIdentitySeed.CustomerRole);
        if (result.Succeeded)
        {
            identityDb.CustomerProfiles.Add(new CustomerProfile
            {
                UserId = user.Id,
                FullName = personalDataProtector.Protect(model.FullName.Trim()),
                Phone = personalDataProtector.Protect(model.Phone.Trim()),
                InvoiceType = model.InvoiceType,
                InvoiceNit = model.InvoiceType == "NIT" ? personalDataProtector.Protect(model.InvoiceNit!.Trim()) : null,
                InvoiceName = model.InvoiceType == "NIT" ? personalDataProtector.Protect(model.InvoiceName!.Trim()) : null
            });
            await identityDb.SaveChangesAsync();
        }
        if (result.Succeeded)
        {
            await signInManager.SignInAsync(user, isPersistent: false);
            return Url.IsLocalUrl(model.ReturnUrl) ? LocalRedirect(model.ReturnUrl!) : RedirectToAction("Index", "Home");
        }
        foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Description);
        return View(model);
    }

    [HttpGet("login")]
    public IActionResult Login(string? returnUrl = null) => View(new CustomerLoginViewModel { ReturnUrl = returnUrl });

    [HttpPost("login"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(CustomerLoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var user = await userManager.FindByEmailAsync(model.Email.Trim());
        if (user is null || !await userManager.IsInRoleAsync(user, AdminIdentitySeed.CustomerRole)) { ModelState.AddModelError(string.Empty, "Correo o contraseña incorrectos."); return View(model); }
        var result = await signInManager.PasswordSignInAsync(user.UserName!, model.Password, model.RememberMe, lockoutOnFailure: true);
        if (result.Succeeded) return Url.IsLocalUrl(model.ReturnUrl) ? LocalRedirect(model.ReturnUrl!) : RedirectToAction("Index", "Home");
        ModelState.AddModelError(string.Empty, result.IsLockedOut ? "Cuenta bloqueada temporalmente." : "Correo o contraseña incorrectos.");
        return View(model);
    }

    [HttpGet("forgot-password")]
    public IActionResult ForgotPassword() => View(new ForgotPasswordViewModel());

    [HttpPost("forgot-password"), ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await userManager.FindByEmailAsync(model.Email.Trim());
        if (user is not null && await userManager.IsInRoleAsync(user, AdminIdentitySeed.CustomerRole))
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            var code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
            var resetUrl = Url.Action(nameof(ResetPassword), "Account", new { email = user.Email, code }, Request.Scheme);
            var expirationMinutes = configuration.GetValue<int?>("Account:PasswordResetTokenMinutes") ?? 30;
            var emailTimeoutSeconds = configuration.GetValue<int?>("Account:PasswordResetEmailTimeoutSeconds") ?? 15;
            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(emailTimeoutSeconds));
                await accountEmailSender.SendPasswordResetAsync(user.Email!, resetUrl!, expirationMinutes, timeout.Token);
            }
            catch (OperationCanceledException)
            {
                logger.LogWarning("El envío del correo de restablecimiento excedió {TimeoutSeconds} segundos para la cuenta {UserId}.", emailTimeoutSeconds, user.Id);
                ModelState.AddModelError(string.Empty, "El servicio de correo tardó demasiado en responder. Intenta nuevamente en unos minutos.");
                return View(model);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "No se pudo enviar el correo de restablecimiento para la cuenta {UserId}.", user.Id);
                ModelState.AddModelError(string.Empty, "No pudimos enviar el correo de restablecimiento. Intenta nuevamente más tarde.");
                return View(model);
            }
        }

        return RedirectToAction(nameof(ForgotPasswordConfirmation));
    }

    [HttpGet("forgot-password-confirmation")]
    public IActionResult ForgotPasswordConfirmation() => View();

    [HttpGet("reset-password")]
    public IActionResult ResetPassword(string? email, string? code)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(code))
            return RedirectToAction(nameof(ForgotPassword));
        return View(new ResetPasswordViewModel { Email = email, Code = code });
    }

    [HttpPost("reset-password"), ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var user = await userManager.FindByEmailAsync(model.Email.Trim());
        if (user is null || !await userManager.IsInRoleAsync(user, AdminIdentitySeed.CustomerRole))
        {
            ModelState.AddModelError(string.Empty, "El enlace o código no es válido. Solicita uno nuevo.");
            return View(model);
        }

        string token;
        try { token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(model.Code)); }
        catch (FormatException)
        {
            ModelState.AddModelError(string.Empty, "El enlace o código no es válido. Solicita uno nuevo.");
            return View(model);
        }

        var result = await userManager.ResetPasswordAsync(user, token, model.Password);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, "El enlace o código venció, ya fue utilizado o no es válido. Solicita uno nuevo.");
            return View(model);
        }

        TempData["PasswordReset"] = "Tu contraseña fue actualizada. Ya puedes iniciar sesión.";
        return RedirectToAction(nameof(Login));
    }

    [Authorize(Roles = AdminIdentitySeed.CustomerRole)]
    [HttpGet("profile")]
    public async Task<IActionResult> Profile()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return Challenge();
        var profile = await identityDb.CustomerProfiles.FindAsync(user.Id);
        if (profile is null) return NotFound();

        return View(new EditProfileViewModel
        {
            Email = user.Email ?? string.Empty,
            FullName = personalDataProtector.Unprotect(profile.FullName),
            Phone = personalDataProtector.Unprotect(profile.Phone),
            InvoiceType = profile.InvoiceType == "NIT" ? "NIT" : "CF",
            InvoiceNit = profile.InvoiceNit is null ? null : personalDataProtector.Unprotect(profile.InvoiceNit),
            InvoiceName = profile.InvoiceName is null ? null : personalDataProtector.Unprotect(profile.InvoiceName)
        });
    }

    [Authorize(Roles = AdminIdentitySeed.CustomerRole)]
    [HttpPost("profile"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(EditProfileViewModel model)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return Challenge();
        var profile = await identityDb.CustomerProfiles.FindAsync(user.Id);
        if (profile is null) return NotFound();

        var email = model.Email.Trim();
        var existingUser = await userManager.FindByEmailAsync(email);
        if (existingUser is not null && existingUser.Id != user.Id)
            ModelState.AddModelError(nameof(model.Email), "Ya existe una cuenta con este correo.");
        if (!ModelState.IsValid) return View(model);

        user.Email = email;
        user.UserName = email;
        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            foreach (var error in updateResult.Errors) ModelState.AddModelError(string.Empty, error.Description);
            return View(model);
        }

        profile.FullName = personalDataProtector.Protect(model.FullName.Trim());
        profile.Phone = personalDataProtector.Protect(model.Phone.Trim());
        profile.InvoiceType = model.InvoiceType;
        profile.InvoiceNit = model.InvoiceType == "NIT" ? personalDataProtector.Protect(model.InvoiceNit!.Trim()) : null;
        profile.InvoiceName = model.InvoiceType == "NIT" ? personalDataProtector.Protect(model.InvoiceName!.Trim()) : null;
        await identityDb.SaveChangesAsync();
        await signInManager.RefreshSignInAsync(user);
        TempData["Success"] = "Los datos de tu cuenta fueron actualizados.";
        return RedirectToAction(nameof(Profile));
    }

    [Authorize(Roles = AdminIdentitySeed.CustomerRole)]
    [HttpGet("purchases")]
    public async Task<IActionResult> Purchases()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return Challenge();

        var purchases = await ticketDb.Orders.AsNoTracking()
            .Where(order => order.CustomerUserId == user.Id)
            .OrderByDescending(order => order.CreatedAt)
            .Select(order => new PurchaseHistoryItemViewModel
            {
                Id = order.Id,
                OrderNumber = order.OrderNumber,
                CreatedAt = order.CreatedAt,
                PaymentStatus = order.PaymentStatus,
                Total = order.Total,
                ItemSummary = string.Join(", ", order.Items.OrderBy(item => item.Id)
                    .Select(item => item.EventName + " · " + item.TicketTypeName + " × " + item.Quantity))
            })
            .ToListAsync();

        return View(purchases);
    }

    [HttpPost("logout"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout() { await signInManager.SignOutAsync(); return RedirectToAction("Index", "Home"); }
}
