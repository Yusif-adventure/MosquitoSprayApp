using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SmartMosquitoControl.Models;
using SmartMosquitoControl.Services;

namespace SmartMosquitoControl.Controllers;

public sealed record HeartbeatRequest(int? InsecticideLevel = null);

public sealed record CompleteCommandRequest(bool Success = true, int? InsecticideLevel = null);

/// <summary>
/// HTTP API used by the physical sprayers (not by browsers). Each request carries
/// <c>X-Device-Id</c> and <c>X-Device-Key</c> headers; the key is shown once when the sprayer is paired.
/// See README.md for the protocol.
/// </summary>
[ApiController]
[AllowAnonymous]
[EnableRateLimiting("device")]
[Route("api/device")]
public class DeviceApiController : ControllerBase
{
    private readonly DeviceCommandService _commands;

    public DeviceApiController(DeviceCommandService commands)
    {
        _commands = commands;
    }

    private Task<LinkedDevice?> AuthenticateAsync(CancellationToken ct) =>
        _commands.AuthenticateDeviceAsync(
            Request.Headers["X-Device-Id"].ToString(),
            Request.Headers["X-Device-Key"].ToString(),
            ct);

    private IActionResult InvalidCredentials() =>
        Unauthorized(new { message = "Invalid device ID or key." });

    /// <summary>Tell the server the sprayer is alive; optionally report the insecticide level (0–100).</summary>
    [HttpPost("heartbeat")]
    public async Task<IActionResult> Heartbeat([FromBody] HeartbeatRequest? body, CancellationToken ct)
    {
        var device = await AuthenticateAsync(ct);
        if (device is null) return InvalidCredentials();

        await _commands.RecordHeartbeatAsync(device, body?.InsecticideLevel, ct);
        return NoContent();
    }

    /// <summary>Poll for work. 200 with a command, or 204 when there is nothing to do.</summary>
    [HttpGet("commands/next")]
    public async Task<IActionResult> NextCommand(CancellationToken ct)
    {
        var device = await AuthenticateAsync(ct);
        if (device is null) return InvalidCredentials();

        var command = await _commands.ClaimNextCommandAsync(device, ct);
        if (command is null) return NoContent();

        return Ok(new
        {
            id = command.Id,
            type = command.Type.ToString().ToLowerInvariant(),
            durationSeconds = command.DurationSeconds
        });
    }

    /// <summary>Report the outcome of a command previously returned by <c>commands/next</c>.</summary>
    [HttpPost("commands/{id:int}/complete")]
    public async Task<IActionResult> CompleteCommand(int id, [FromBody] CompleteCommandRequest? body, CancellationToken ct)
    {
        var device = await AuthenticateAsync(ct);
        if (device is null) return InvalidCredentials();

        var completed = await _commands.CompleteCommandAsync(
            device, id, body?.Success ?? true, body?.InsecticideLevel, ct);

        return completed ? NoContent() : NotFound(new { message = "No such command in progress." });
    }
}
