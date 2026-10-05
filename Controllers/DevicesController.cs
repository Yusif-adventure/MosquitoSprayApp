using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartMosquitoControl.Models;
using SmartMosquitoControl.Services;

namespace SmartMosquitoControl.Controllers;

[Authorize]
public class DevicesController : Controller
{
    private readonly ILogger<DevicesController> _logger;
    private readonly MosquitoDataService _data;

    public DevicesController(ILogger<DevicesController> logger, MosquitoDataService data)
    {
        _logger = logger;
        _data = data;
    }

    private bool IsAjax => Request.Headers["X-Requested-With"] == "XMLHttpRequest";

    // ── Manual spray ──────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> ManualSpray()
    {
        var user = await _data.GetCurrentUserAsync();
        ViewBag.SprayDurationSeconds = user?.SprayDurationSeconds ?? 30;
        return View();
    }

    /// <summary>
    /// Queues the spray as soon as the user taps (not when the on-screen countdown ends), so the
    /// request can't be lost by closing the tab. The page's JS calls this with fetch and then runs the countdown.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SprayNow()
    {
        var result = await _data.TriggerManualSprayAsync();

        switch (result.Outcome)
        {
            case QueueSprayOutcome.Queued:
                _logger.LogInformation("Manual spray queued as command {CommandId}", result.Command!.Id);
                if (IsAjax)
                {
                    return Json(new { success = true, durationSeconds = result.Command.DurationSeconds });
                }
                TempData["SprayStarted"] = true;
                return RedirectToAction(nameof(ManualSpray));

            case QueueSprayOutcome.Busy:
                return Failure("Your sprayer is already running a spray cycle. Wait for it to finish.");

            default:
                return Failure("Link a sprayer under Settings → Linked Devices before spraying.");
        }
    }

    private IActionResult Failure(string message)
    {
        if (IsAjax)
        {
            return Conflict(new { success = false, message });
        }
        TempData["Error"] = message;
        return RedirectToAction(nameof(ManualSpray));
    }

    // ── Monitoring ────────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Monitoring()
    {
        return View(new MonitoringViewModel
        {
            Device = await _data.GetDeviceAsync(),
            History = await _data.GetSprayHistoryAsync()
        });
    }

    // ── Linked devices ────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> LinkedDevices()
    {
        return View(await _data.GetLinkedDevicesAsync());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LinkDevice(string? deviceId, string? deviceName)
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

        var result = await _data.AddLinkedDeviceAsync(normalizedDeviceId, deviceName ?? string.Empty);
        if (result.Outcome != LinkDeviceOutcome.Linked || result.Device is null)
        {
            TempData["LinkedDeviceError"] = "This sprayer ID is already linked.";
            return RedirectToAction(nameof(LinkedDevices));
        }

        TempData["LinkedDeviceMessage"] = $"Sprayer {result.Device.DeviceId} linked successfully.";
        // Shown once on the next page load; only its hash is stored.
        TempData["LinkedDeviceId"] = result.Device.DeviceId;
        TempData["LinkedDeviceKey"] = result.ApiKey;
        return RedirectToAction(nameof(LinkedDevices));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetCurrentDevice(int id)
    {
        if (await _data.SetCurrentDeviceAsync(id))
        {
            TempData["LinkedDeviceMessage"] = "Primary sprayer updated.";
        }
        else
        {
            TempData["LinkedDeviceError"] = "That sprayer could not be found.";
        }
        return RedirectToAction(nameof(LinkedDevices));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UnlinkDevice(int id)
    {
        if (await _data.RemoveLinkedDeviceAsync(id))
        {
            TempData["LinkedDeviceMessage"] = "Device unlinked.";
        }
        else
        {
            TempData["LinkedDeviceError"] = "That sprayer could not be found.";
        }
        return RedirectToAction(nameof(LinkedDevices));
    }
}
