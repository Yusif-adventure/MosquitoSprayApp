using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using SmartMosquitoControl.Models;
using SmartMosquitoControl.Services;

namespace SmartMosquitoControl.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly MosquitoDataService _data;

    public HomeController(ILogger<HomeController> logger, MosquitoDataService data)
    {
        _logger = logger;
        _data = data;
    }

    public IActionResult Index()
    {
        return View();
    }

    [HttpGet]
    public IActionResult Login()
    {
        return View(new LoginViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        return RedirectToAction(nameof(Dashboard));
    }

    public IActionResult Dashboard()
    {
        var model = new DashboardViewModel
        {
            Device = _data.Device,
            Schedules = _data.Schedules.Take(2).ToList(),
            Notifications = _data.Notifications.Take(2).ToList()
        };

        return View(model);
    }

    [HttpGet]
    public IActionResult ManualSpray()
    {
        ViewBag.SprayDurationSeconds = _data.SprayDurationSeconds;
        return View();
    }

    public IActionResult Schedule(string tab = "scheduled")
    {
        var isHistory = string.Equals(tab, "history", StringComparison.OrdinalIgnoreCase);
        var schedules = isHistory
            ? _data.ScheduleHistory.Concat(_data.Schedules.Where(item => item.ScheduledFor <= DateTime.Now)).OrderByDescending(item => item.ScheduledFor).ToList()
            : _data.Schedules.Where(item => item.ScheduledFor > DateTime.Now).ToList();

        ViewBag.ActiveScheduleTab = isHistory ? "history" : "scheduled";
        return View(schedules);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ToggleSchedule(int id, bool isEnabled)
    {
        _data.SetScheduleEnabled(id, isEnabled);
        return RedirectToAction(nameof(Schedule));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult DeleteSchedule(int id)
    {
        _data.RemoveSchedule(id);
        return RedirectToAction(nameof(Schedule));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult AddSchedule(string name, DateTime scheduledFor)
    {
        if (string.IsNullOrWhiteSpace(name) || scheduledFor <= DateTime.Now)
        {
            TempData["ScheduleError"] = "Enter a name and a future date and time.";
            return RedirectToAction(nameof(Schedule));
        }

        _data.AddSchedule(new ScheduleItem
        {
            Name = name.Trim(),
            Description = scheduledFor.ToString("ddd, MMM d • h:mm tt"),
            ScheduledFor = scheduledFor,
            IsEnabled = true,
            IsCustom = true
        });

        return RedirectToAction(nameof(Schedule));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult SprayNow()
    {
        _data.TriggerManualSpray();
        TempData["SprayStarted"] = true;
        return RedirectToAction(nameof(ManualSpray));
    }

    public IActionResult Monitoring()
    {
        var model = new MonitoringViewModel
        {
            Device = _data.Device,
            History = _data.SprayHistory.OrderByDescending(item => item.Time)
        };

        return View(model);
    }

    public IActionResult Notifications()
    {
        return View(_data.Notifications);
    }

    public IActionResult Profile(bool saved = false)
    {
        ViewBag.SettingsSaved = saved;
        return View(new SettingsViewModel
        {
            SprayDurationSeconds = _data.SprayDurationSeconds,
            LowInsecticideThreshold = _data.LowInsecticideThreshold,
            LowInsecticideAlertsEnabled = _data.LowInsecticideAlertsEnabled,
            SprayNotificationsEnabled = _data.SprayNotificationsEnabled
        });
    }

    public IActionResult LinkedDevices()
    {
        return View(_data.LinkedDevices.OrderByDescending(device => device.IsCurrent).ThenByDescending(device => device.LastActiveAt));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult LinkDevice(string deviceId, string? deviceName)
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

        var linkedDevice = _data.AddLinkedDevice(normalizedDeviceId, deviceName ?? string.Empty);
        if (linkedDevice is null)
        {
            TempData["LinkedDeviceError"] = "This sprayer ID is already linked.";
            return RedirectToAction(nameof(LinkedDevices));
        }

        TempData["LinkedDeviceMessage"] = $"Sprayer {linkedDevice.DeviceId} linked successfully.";
        return RedirectToAction(nameof(LinkedDevices));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult UnlinkDevice(int id)
    {
        if (!_data.RemoveLinkedDevice(id))
        {
            TempData["LinkedDeviceError"] = "This device cannot be removed.";
        }
        else
        {
            TempData["LinkedDeviceMessage"] = "Device unlinked.";
        }

        return RedirectToAction(nameof(LinkedDevices));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult SaveSettings(SettingsViewModel model)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.SettingsSaved = false;
            return View("Profile", model);
        }

        _data.UpdateSettings(
            model.SprayDurationSeconds,
            model.LowInsecticideThreshold,
            model.LowInsecticideAlertsEnabled,
            model.SprayNotificationsEnabled);

        return RedirectToAction(nameof(Profile), new { saved = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Logout()
    {
        TempData["SignedOut"] = true;
        return RedirectToAction(nameof(Login));
    }

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

public class DashboardViewModel
{
    public DeviceState Device { get; set; } = new();
    public List<ScheduleItem> Schedules { get; set; } = new();
    public List<NotificationItem> Notifications { get; set; } = new();
}

public class MonitoringViewModel
{
    public DeviceState Device { get; set; } = new();
    public IEnumerable<SprayHistoryItem> History { get; set; } = Array.Empty<SprayHistoryItem>();
}
