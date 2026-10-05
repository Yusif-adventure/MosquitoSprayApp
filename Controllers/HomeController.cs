using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartMosquitoControl.Models;
using SmartMosquitoControl.Services;

namespace SmartMosquitoControl.Controllers;

public class HomeController : Controller
{
    private readonly MosquitoDataService _data;

    public HomeController(MosquitoDataService data)
    {
        _data = data;
    }

    [AllowAnonymous]
    public IActionResult Index()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction(nameof(Dashboard));
        }
        return View();
    }

    [Authorize]
    public async Task<IActionResult> Dashboard()
    {
        var user = await _data.GetCurrentUserAsync();
        var upcoming = await _data.GetUpcomingSchedulesAsync(take: 2);
        var notifications = await _data.GetNotificationsAsync(take: 2);

        var hour = DateTime.Now.Hour;
        var greeting = hour < 12 ? "Good morning" : hour < 18 ? "Good afternoon" : "Good evening";
        var firstName = user?.FirstName ?? user?.Email?.Split('@')[0] ?? "there";
        var avatarLetter = (firstName.Length > 0 ? firstName[0] : '?').ToString().ToUpperInvariant();

        return View(new DashboardViewModel
        {
            Device = await _data.GetDeviceAsync(),
            Schedules = upcoming,
            Notifications = notifications,
            UserFirstName = firstName,
            UserAvatarLetter = avatarLetter,
            Greeting = greeting,
            NextSchedule = await _data.GetNextScheduleAsync(),
            UnreadNotificationCount = await _data.CountUnreadNotificationsAsync()
        });
    }

    [Authorize]
    public async Task<IActionResult> Notifications()
    {
        return View(await _data.GetNotificationsAsync());
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllRead()
    {
        await _data.MarkAllNotificationsReadAsync();
        return RedirectToAction(nameof(Notifications));
    }

    [AllowAnonymous]
    public IActionResult Privacy()
    {
        return View();
    }

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
