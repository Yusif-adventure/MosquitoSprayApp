using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using SmartMosquitoControl.Models;
using SmartMosquitoControl.Services;
using System.Security.Claims;

namespace SmartMosquitoControl.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly MosquitoDataService _data;
    private readonly AuthenticationService _authService;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;

    public HomeController(
        ILogger<HomeController> logger,
        MosquitoDataService data,
        AuthenticationService authService,
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager)
    {
        _logger = logger;
        _data = data;
        _authService = authService;
        _signInManager = signInManager;
        _userManager = userManager;
    }

    public IActionResult Index()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction(nameof(Dashboard));
        }
        return View();
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction(nameof(Dashboard));
        }
        return View(new LoginViewModel());
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            ModelState.AddModelError(string.Empty, "Email and password are required.");
            return View(model);
        }

        var (success, message) = await _authService.LoginAsync(model.EmailOrPhone, model.Password);

        if (success)
        {
            _logger.LogInformation("User {Email} logged in successfully", model.EmailOrPhone);
            return RedirectToAction(nameof(Dashboard));
        }

        ModelState.AddModelError(string.Empty, message);
        return View(model);
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction(nameof(Dashboard));
        }
        return View(new RegisterViewModel());
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var (success, message) = await _authService.RegisterAsync(model.Email, model.Password, model.FirstName, model.LastName, autoConfirmEmail: true);

        if (success)
        {
            _logger.LogInformation("New user registered: {Email}", model.Email);
            var (loginOk, loginMsg) = await _authService.LoginAsync(model.Email, model.Password);
            if (!loginOk)
            {
                _logger.LogWarning("Auto-login after registration failed for {Email}: {Message}", model.Email, loginMsg);
                ModelState.AddModelError(string.Empty, "Registration succeeded but sign-in failed. Please log in manually.");
                return RedirectToAction(nameof(Login));
            }
            return RedirectToAction(nameof(Dashboard));
        }

        ModelState.AddModelError(string.Empty, message);
        return View(model);
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public IActionResult ExternalLogin(string provider, string? returnUrl = null)
    {
        var redirectUrl = Url.Action(nameof(ExternalLoginCallback), "Home", new { returnUrl });
        var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
        return Challenge(properties, provider);
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> ExternalLoginCallback(string? returnUrl = null, string? remoteError = null)
    {
        returnUrl ??= Url.Content("~/");
        if (remoteError != null)
        {
            ModelState.AddModelError(string.Empty, $"Error from external provider: {remoteError}");
            return View(nameof(Login));
        }

        var info = await _signInManager.GetExternalLoginInfoAsync();
        if (info == null)
        {
            return RedirectToAction(nameof(Login));
        }

        var result = await _signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor: true);
        if (result.Succeeded)
        {
            return LocalRedirect(returnUrl);
        }

        // New user provisioning or link existing account
        var email = info.Principal.FindFirstValue(ClaimTypes.Email);
        var firstName = info.Principal.FindFirstValue(ClaimTypes.GivenName);
        var lastName = info.Principal.FindFirstValue(ClaimTypes.Surname);

        if (email != null)
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                var (success, _) = await _authService.RegisterAsync(email, "GoogleAuth$" + Guid.NewGuid().ToString("N") + "!", firstName, lastName, autoConfirmEmail: true);
                if (success)
                {
                    user = await _userManager.FindByEmailAsync(email);
                }
            }

            if (user != null)
            {
                await _userManager.AddLoginAsync(user, info);
                await _signInManager.SignInAsync(user, isPersistent: false);
                return LocalRedirect(returnUrl);
            }
        }
        
        TempData["Error"] = "Error creating or linking account from Google login.";
        return RedirectToAction(nameof(Login));
    }

    [Authorize]
    public async Task<IActionResult> Dashboard()
    {
        try
        {
            var user = await _data.GetCurrentUserAsync();
            var schedules = await _data.GetUpcomingSchedulesAsync();
            var notifications = await _data.GetNotificationsAsync();
            var device = await _data.GetDeviceAsync();

            var hour = DateTime.Now.Hour;
            var greeting = hour < 12 ? "Good morning" : hour < 18 ? "Good afternoon" : "Good evening";
            var firstName = user?.FirstName ?? user?.Email?.Split('@')[0] ?? "there";
            var avatarLetter = (firstName.Length > 0 ? firstName[0] : '?').ToString().ToUpperInvariant();

            var model = new DashboardViewModel
            {
                Device = device,
                Schedules = schedules.Take(2).ToList(),
                Notifications = notifications.Take(2).ToList(),
                UserFirstName = firstName,
                UserAvatarLetter = avatarLetter,
                Greeting = greeting,
                NextSchedule = schedules.FirstOrDefault(),
                UnreadNotificationCount = notifications.Count(n => !n.IsRead)
            };
            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading dashboard");
            TempData["Error"] = "An error occurred while loading the dashboard.";
            return RedirectToAction(nameof(Index));
        }
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> ManualSpray()
    {
        try
        {
            var user = await _data.GetCurrentUserAsync();
            ViewBag.SprayDurationSeconds = user?.SprayDurationSeconds ?? 30;
            return View();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading manual spray page");
            TempData["Error"] = "An error occurred while loading the manual spray page.";
            return RedirectToAction(nameof(Dashboard));
        }
    }

    [Authorize]
    public async Task<IActionResult> Schedule(string tab = "scheduled")
    {
        try
        {
            var isHistory = string.Equals(tab, "history", StringComparison.OrdinalIgnoreCase);
            List<ScheduleItem> schedules;
            if (isHistory)
            {
                schedules = await _data.GetScheduleHistoryAsync();
            }
            else
            {
                schedules = await _data.GetUpcomingSchedulesAsync();
            }

            ViewBag.ActiveScheduleTab = isHistory ? "history" : "scheduled";
            return View(schedules);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading schedule page");
            TempData["Error"] = "An error occurred while loading schedules.";
            return RedirectToAction(nameof(Dashboard));
        }
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleSchedule(int id, bool isEnabled)
    {
        try
        {
            await _data.SetScheduleEnabledAsync(id, isEnabled);
            return RedirectToAction(nameof(Schedule));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling schedule {ScheduleId}", id);
            TempData["Error"] = "Failed to update schedule.";
            return RedirectToAction(nameof(Schedule));
        }
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteSchedule(int id)
    {
        try
        {
            await _data.RemoveScheduleAsync(id);
            return RedirectToAction(nameof(Schedule));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting schedule {ScheduleId}", id);
            TempData["Error"] = "Failed to delete schedule.";
            return RedirectToAction(nameof(Schedule));
        }
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddSchedule(string name, DateTime scheduledFor)
    {
        try
        {
            // scheduledFor comes from datetime-local (user local time) — compare against local now
            if (string.IsNullOrWhiteSpace(name) || scheduledFor <= DateTime.Now)
            {
                TempData["ScheduleError"] = "Enter a name and a future date and time.";
                return RedirectToAction(nameof(Schedule));
            }

            await _data.AddScheduleAsync(new ScheduleItem
            {
                Name = name.Trim(),
                Description = scheduledFor.ToString("ddd, MMM d \u2022 h:mm tt"),
                ScheduledFor = scheduledFor.ToUniversalTime(),
                IsEnabled = true,
                IsCustom = true
            });

            return RedirectToAction(nameof(Schedule));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding schedule");
            TempData["ScheduleError"] = "An error occurred while adding the schedule.";
            return RedirectToAction(nameof(Schedule));
        }
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SprayNow()
    {
        try
        {
            await _data.TriggerManualSprayAsync();
            TempData["SprayStarted"] = true;
            return RedirectToAction(nameof(ManualSpray));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error triggering manual spray");
            TempData["Error"] = "Failed to trigger spray.";
            return RedirectToAction(nameof(ManualSpray));
        }
    }

    [Authorize]
    public async Task<IActionResult> Monitoring()
    {
        try
        {
            var model = new MonitoringViewModel
            {
                Device = await _data.GetDeviceAsync(),
                History = await _data.GetSprayHistoryAsync()
            };
            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading monitoring page");
            TempData["Error"] = "An error occurred while loading monitoring data.";
            return RedirectToAction(nameof(Dashboard));
        }
    }

    [Authorize]
    public async Task<IActionResult> Notifications()
    {
        try
        {
            return View(await _data.GetNotificationsAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading notifications");
            TempData["Error"] = "An error occurred while loading notifications.";
            return RedirectToAction(nameof(Dashboard));
        }
    }

    [Authorize]
    public async Task<IActionResult> Profile(bool saved = false)
    {
        try
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading profile page");
            TempData["Error"] = "An error occurred while loading your profile.";
            return RedirectToAction(nameof(Dashboard));
        }
    }

    [Authorize]
    public async Task<IActionResult> LinkedDevices()
    {
        try
        {
            return View(await _data.GetLinkedDevicesAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading linked devices");
            TempData["Error"] = "An error occurred while loading linked devices.";
            return RedirectToAction(nameof(Dashboard));
        }
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LinkDevice(string deviceId, string? deviceName)
    {
        try
        {
            var normalizedDeviceId = deviceId?.Trim().ToUpperInvariant() ?? string.Empty;
            var validDeviceId = normalizedDeviceId.Length is >= 4 and <= 64
                && char.IsLetterOrDigit(normalizedDeviceId[0])
                && normalizedDeviceId.All(character => char.IsLetterOrDigit(character) || character == '-');

            if (!validDeviceId || (deviceName?.Trim().Length ?? 0) > 40)
            {
                TempData["LinkedDeviceError"] = "Enter a valid sprayer ID (4–64 letters, numbers, or hyphens).";
                return RedirectToAction(nameof(LinkedDevices));
            }

            var linkedDevice = await _data.AddLinkedDeviceAsync(normalizedDeviceId, deviceName ?? string.Empty);
            if (linkedDevice is null)
            {
                TempData["LinkedDeviceError"] = "This sprayer ID is already linked.";
                return RedirectToAction(nameof(LinkedDevices));
            }

            TempData["LinkedDeviceMessage"] = $"Sprayer {linkedDevice.DeviceId} linked successfully.";
            return RedirectToAction(nameof(LinkedDevices));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error linking device");
            TempData["LinkedDeviceError"] = "An error occurred while linking the device.";
            return RedirectToAction(nameof(LinkedDevices));
        }
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UnlinkDevice(int id)
    {
        try
        {
            if (!await _data.RemoveLinkedDeviceAsync(id))
            {
                TempData["LinkedDeviceError"] = "This device cannot be removed.";
            }
            else
            {
                TempData["LinkedDeviceMessage"] = "Device unlinked.";
            }

            return RedirectToAction(nameof(LinkedDevices));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unlinking device {DeviceId}", id);
            TempData["LinkedDeviceError"] = "An error occurred while unlinking the device.";
            return RedirectToAction(nameof(LinkedDevices));
        }
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveSettings(SettingsViewModel model)
    {
        try
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving settings");
            TempData["Error"] = "Failed to save settings.";
            return RedirectToAction(nameof(Profile));
        }
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        try
        {
            await _authService.LogoutAsync();
            _logger.LogInformation("User logged out");
            TempData["SignedOut"] = true;
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during logout");
            return RedirectToAction(nameof(Index));
        }
    }

    [AllowAnonymous]
    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
