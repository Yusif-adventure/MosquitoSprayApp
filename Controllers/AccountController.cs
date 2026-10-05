using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using SmartMosquitoControl.Models;
using SmartMosquitoControl.Services;

namespace SmartMosquitoControl.Controllers;

[Authorize]
public class AccountController : Controller
{
    private readonly ILogger<AccountController> _logger;
    private readonly MosquitoDataService _data;
    private readonly AuthenticationService _authService;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailService _email;
    private readonly ExternalAuthInfo _externalAuth;

    public AccountController(
        ILogger<AccountController> logger,
        MosquitoDataService data,
        AuthenticationService authService,
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        IEmailService email,
        ExternalAuthInfo externalAuth)
    {
        _logger = logger;
        _data = data;
        _authService = authService;
        _signInManager = signInManager;
        _userManager = userManager;
        _email = email;
        _externalAuth = externalAuth;
    }

    private bool RequireConfirmedEmail => _signInManager.Options.SignIn.RequireConfirmedEmail;

    private IActionResult RedirectToLocal(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? LocalRedirect(returnUrl)
            : RedirectToAction("Dashboard", "Home");

    // ── Sign in ───────────────────────────────────────────────────────────────

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Dashboard", "Home");
        }

        ViewBag.ReturnUrl = returnUrl;
        return View(new LoginViewModel());
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewBag.ReturnUrl = returnUrl;

        if (!ModelState.IsValid)
        {
            ModelState.AddModelError(string.Empty, "Email and password are required.");
            return View(model);
        }

        var (success, message) = await _authService.LoginAsync(model.EmailOrPhone, model.Password);
        if (success)
        {
            return RedirectToLocal(returnUrl);
        }

        ViewBag.ShowResend = message == AuthenticationService.EmailNotConfirmedMessage;
        ViewBag.ResendEmail = model.EmailOrPhone;
        ModelState.AddModelError(string.Empty, message);
        return View(model);
    }

    // ── Registration & email confirmation ─────────────────────────────────────

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Dashboard", "Home");
        }
        return View(new RegisterViewModel());
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // Never auto-confirm: the address has to be proven by clicking the emailed link.
        var (success, message) = await _authService.RegisterAsync(
            model.Email, model.Password, model.FirstName, model.LastName, autoConfirmEmail: false);

        if (!success)
        {
            ModelState.AddModelError(string.Empty, message);
            return View(model);
        }

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user is not null)
        {
            await SendConfirmationEmailAsync(user);
        }

        if (RequireConfirmedEmail)
        {
            TempData["Info"] = "Account created. Check your email and click the confirmation link, then sign in.";
            return RedirectToAction(nameof(Login));
        }

        var (loginOk, _) = await _authService.LoginAsync(model.Email, model.Password);
        if (!loginOk)
        {
            TempData["Info"] = "Account created. Please sign in.";
            return RedirectToAction(nameof(Login));
        }

        return RedirectToAction("Dashboard", "Home");
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> ConfirmEmail(string? userId, string? code)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(code))
        {
            return RedirectToAction(nameof(Login));
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            TempData["Error"] = "That confirmation link is not valid.";
            return RedirectToAction(nameof(Login));
        }

        try
        {
            var token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
            var result = await _userManager.ConfirmEmailAsync(user, token);
            if (result.Succeeded)
            {
                TempData["Info"] = "Email confirmed. You can sign in now.";
            }
            else
            {
                TempData["Error"] = "That confirmation link is invalid or has expired. Request a new one below.";
                TempData["ShowResend"] = true;
            }
        }
        catch (FormatException)
        {
            TempData["Error"] = "That confirmation link is not valid.";
        }

        return RedirectToAction(nameof(Login));
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ResendConfirmation(string? email)
    {
        if (!string.IsNullOrWhiteSpace(email))
        {
            var user = await _userManager.FindByEmailAsync(email) ?? await _userManager.FindByNameAsync(email);
            if (user is not null && !user.EmailConfirmed)
            {
                await SendConfirmationEmailAsync(user);
            }
        }

        // Same answer whether or not the account exists.
        TempData["Info"] = "If that account exists and is unconfirmed, a new confirmation email is on its way.";
        return RedirectToAction(nameof(Login));
    }

    private async Task SendConfirmationEmailAsync(ApplicationUser user)
    {
        try
        {
            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
            var link = Url.Action(nameof(ConfirmEmail), "Account", new { userId = user.Id, code }, Request.Scheme);

            await _email.SendAsync(
                user.Email!,
                "Confirm your Smart Mosquito Control account",
                $"<p>Welcome to Smart Mosquito Control.</p><p><a href=\"{System.Net.WebUtility.HtmlEncode(link)}\">Confirm your email address</a></p>");
        }
        catch (Exception ex)
        {
            // Registration itself succeeded; the user can request another email from the sign-in page.
            _logger.LogError(ex, "Failed to send confirmation email to {Email}", user.Email);
        }
    }

    // ── External (Google) sign in ─────────────────────────────────────────────

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public IActionResult ExternalLogin(string provider, string? returnUrl = null)
    {
        if (!string.Equals(provider, "Google", StringComparison.Ordinal) || !_externalAuth.GoogleEnabled)
        {
            TempData["Error"] = "That sign-in method is not available.";
            return RedirectToAction(nameof(Login));
        }

        var redirectUrl = Url.Action(nameof(ExternalLoginCallback), "Account", new { returnUrl });
        var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
        return Challenge(properties, provider);
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> ExternalLoginCallback(string? returnUrl = null, string? remoteError = null)
    {
        if (remoteError != null)
        {
            _logger.LogWarning("External login provider returned an error: {Error}", remoteError);
            TempData["Error"] = "Google sign-in failed. Please try again.";
            return RedirectToAction(nameof(Login));
        }

        var info = await _signInManager.GetExternalLoginInfoAsync();
        if (info == null)
        {
            return RedirectToAction(nameof(Login));
        }

        var signIn = await _signInManager.ExternalLoginSignInAsync(
            info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor: true);
        if (signIn.Succeeded)
        {
            return RedirectToLocal(returnUrl);
        }
        if (signIn.IsLockedOut)
        {
            TempData["Error"] = "This account is temporarily locked. Please try again later.";
            return RedirectToAction(nameof(Login));
        }

        // First time with this Google account. Only trust the email if Google says it verified it;
        // otherwise anyone could claim an address they don't own.
        var email = info.Principal.FindFirstValue(ClaimTypes.Email);
        var emailVerified = string.Equals(
            info.Principal.FindFirstValue("email_verified"), "true", StringComparison.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(email) || !emailVerified)
        {
            TempData["Error"] = "Google did not provide a verified email address for this account.";
            return RedirectToAction(nameof(Login));
        }

        var user = await _userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = await _authService.RegisterExternalAsync(
                email,
                info.Principal.FindFirstValue(ClaimTypes.GivenName),
                info.Principal.FindFirstValue(ClaimTypes.Surname));
        }
        else if (!user.EmailConfirmed)
        {
            // Linking here would let whoever pre-registered this address with their own password
            // keep access to the Google user's account. Make them confirm the local account first.
            TempData["Error"] = "An account with this email exists but was never confirmed. " +
                                "Confirm it from the email we sent (or request a new one), then try Google again.";
            return RedirectToAction(nameof(Login));
        }

        if (user is null)
        {
            TempData["Error"] = "Error creating your account from Google sign-in.";
            return RedirectToAction(nameof(Login));
        }

        var link = await _userManager.AddLoginAsync(user, info);
        if (!link.Succeeded)
        {
            _logger.LogWarning("Could not link Google login for {Email}: {Errors}",
                email, string.Join(", ", link.Errors.Select(e => e.Description)));
            TempData["Error"] = "Error linking your Google account.";
            return RedirectToAction(nameof(Login));
        }

        await _signInManager.SignInAsync(user, isPersistent: false);
        return RedirectToLocal(returnUrl);
    }

    // ── Settings / profile ────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Profile(bool saved = false)
    {
        var user = await _data.GetCurrentUserAsync();
        ViewBag.SettingsSaved = saved;
        return View(new SettingsViewModel
        {
            SprayDurationSeconds = user?.SprayDurationSeconds ?? 30,
            LowInsecticideThreshold = user?.LowInsecticideThreshold ?? 20,
            LowInsecticideAlertsEnabled = user?.LowInsecticideAlertsEnabled ?? true,
            SprayNotificationsEnabled = user?.SprayNotificationsEnabled ?? true
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveSettings(SettingsViewModel model)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.SettingsSaved = false;
            return View("Profile", model);
        }

        await _data.UpdateSettingsAsync(
            model.SprayDurationSeconds,
            model.LowInsecticideThreshold,
            model.LowInsecticideAlertsEnabled,
            model.SprayNotificationsEnabled);

        return RedirectToAction(nameof(Profile), new { saved = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _authService.LogoutAsync();
        TempData["SignedOut"] = true;
        return RedirectToAction("Index", "Home");
    }
}
