using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AARCHIZZ.Models;
using AARCHIZZ.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AARCHIZZ.Controllers;

[Route("Admin")]
[Authorize(Policy = "AdminOnly")]
public class AdminController : Controller
{
  private readonly AdminCredentialValidator _credentials;

  public AdminController(AdminCredentialValidator credentials)
  {
    _credentials = credentials;
  }

  [HttpGet("Login")]
  [AllowAnonymous]
  public IActionResult Login(string? returnUrl = null)
  {
    return View(new AdminLoginViewModel { ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null });
  }

  [HttpPost("Login")]
  [AllowAnonymous]
  [ValidateAntiForgeryToken]
  public async Task<IActionResult> Login(AdminLoginViewModel model)
  {
    if (!ModelState.IsValid || !await _credentials.ValidateDatabaseAsync(model.Username, model.Password, HttpContext.RequestAborted))
    {
      ModelState.Clear();
      ModelState.AddModelError(string.Empty, "The username or password is not valid.");
      return View(new AdminLoginViewModel { ReturnUrl = Url.IsLocalUrl(model.ReturnUrl) ? model.ReturnUrl : null });
    }

    var claims = new[]
    {
            new Claim(ClaimTypes.NameIdentifier, (await _credentials.GetOwnerAsync(HttpContext.RequestAborted))!.Id.ToString()),
            new Claim(ClaimTypes.Name, model.Username.Trim()),
            new Claim("owner_security_stamp", (await _credentials.GetOwnerAsync(HttpContext.RequestAborted))!.SecurityStamp),
            new Claim(ClaimTypes.Role, "Administrator")
        };
    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
    await HttpContext.SignInAsync(
        CookieAuthenticationDefaults.AuthenticationScheme,
        new ClaimsPrincipal(identity),
        new AuthenticationProperties { IsPersistent = false, AllowRefresh = true });

    return Url.IsLocalUrl(model.ReturnUrl)
        ? LocalRedirect(model.ReturnUrl!)
        : RedirectToAction(nameof(Dashboard));
  }

  [HttpPost("Logout")]
  [ValidateAntiForgeryToken]
  public async Task<IActionResult> Logout()
  {
    await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return RedirectToAction(nameof(Login));
  }

  [HttpGet("")]
  public IActionResult Dashboard()
  {
    return View();
  }

  [HttpGet("Account")]
  public async Task<IActionResult> Account()
  {
    var owner = await _credentials.GetOwnerAsync(HttpContext.RequestAborted);
    if (owner is null)
    {
      return NotFound();
    }

    return View(new AdminAccountViewModel
    {
      CurrentUsername = owner.Username,
      NewUsername = owner.Username
    });
  }

  [HttpPost("Account")]
  [ValidateAntiForgeryToken]
  public async Task<IActionResult> Account(AdminAccountViewModel model)
  {
    var owner = await _credentials.GetOwnerAsync(HttpContext.RequestAborted);
    if (owner is null)
    {
      return NotFound();
    }

    model.CurrentUsername = owner.Username;
    if (!ModelState.IsValid)
    {
      return View(model);
    }

    if (string.IsNullOrEmpty(model.NewPassword) && !model.NewUsername.Trim().Equals(owner.Username, StringComparison.Ordinal))
    {
      ModelState.AddModelError(nameof(model.NewUsername), "Enter a new username or a new password.");
      return View(model);
    }

    if (!string.IsNullOrEmpty(model.NewPassword) && model.NewPassword.Length < 12)
    {
      ModelState.AddModelError(nameof(model.NewPassword), "The new password must be at least 12 characters.");
      return View(model);
    }

    var result = await _credentials.UpdateOwnerAsync(
        model.CurrentPassword,
        model.NewUsername,
        model.NewPassword,
        HttpContext.RequestAborted);
    if (!result.Succeeded)
    {
      ModelState.AddModelError(string.Empty, result.Error ?? "The account could not be updated.");
      return View(model);
    }

    await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    TempData["AdminAccountMessage"] = "Your account was updated. Please sign in again.";
    return RedirectToAction(nameof(Login));
  }
}