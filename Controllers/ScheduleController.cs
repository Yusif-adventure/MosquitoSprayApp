using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartMosquitoControl.Models;
using SmartMosquitoControl.Services;

namespace SmartMosquitoControl.Controllers;

[Authorize]
public class ScheduleController : Controller
{
    private const int MaxNameLength = 40;

    private readonly MosquitoDataService _data;

    public ScheduleController(MosquitoDataService data)
    {
        _data = data;
    }

    [HttpGet]
    public async Task<IActionResult> Schedule(string tab = "scheduled")
    {
        var isHistory = string.Equals(tab, "history", StringComparison.OrdinalIgnoreCase);
        var schedules = isHistory
            ? await _data.GetScheduleHistoryAsync()
            : await _data.GetUpcomingSchedulesAsync();

        ViewBag.ActiveScheduleTab = isHistory ? "history" : "scheduled";
        return View(schedules);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleSchedule(int id, bool isEnabled)
    {
        if (!await _data.SetScheduleEnabledAsync(id, isEnabled))
        {
            TempData["Error"] = "That schedule no longer exists.";
        }
        return RedirectToAction(nameof(Schedule));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteSchedule(int id)
    {
        if (!await _data.RemoveScheduleAsync(id))
        {
            TempData["Error"] = "That schedule no longer exists.";
        }
        return RedirectToAction(nameof(Schedule));
    }

    /// <param name="scheduledForUtc">
    /// ISO-8601 instant. The browser converts the picked local date/time to UTC before submitting,
    /// so the server never has to guess the user's time zone.
    /// </param>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddSchedule(string? name, string? scheduledForUtc)
    {
        var trimmedName = name?.Trim();
        if (string.IsNullOrWhiteSpace(trimmedName) || trimmedName.Length > MaxNameLength)
        {
            TempData["ScheduleError"] = $"Enter a name of up to {MaxNameLength} characters.";
            return RedirectToAction(nameof(Schedule));
        }

        if (!DateTimeOffset.TryParse(scheduledForUtc, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal, out var scheduledFor))
        {
            TempData["ScheduleError"] = "Pick a valid date and time (JavaScript must be enabled).";
            return RedirectToAction(nameof(Schedule));
        }

        if (scheduledFor.UtcDateTime <= DateTime.UtcNow)
        {
            TempData["ScheduleError"] = "Pick a time in the future.";
            return RedirectToAction(nameof(Schedule));
        }

        await _data.AddScheduleAsync(new ScheduleItem
        {
            Name = trimmedName,
            ScheduledFor = scheduledFor.UtcDateTime,
            IsEnabled = true,
            IsCustom = true
        });

        return RedirectToAction(nameof(Schedule));
    }
}
