using Microsoft.AspNetCore.Identity;
using SmartMosquitoControl.Models;

namespace SmartMosquitoControl.Services;

public class AuthenticationService
{
    public const string EmailNotConfirmedMessage = "Please confirm your email address before signing in.";

    // Deliberately identical for "no such user", "wrong password" and "locked out" so the
    // login form cannot be used to discover which accounts exist.
    private const string InvalidCredentialsMessage = "Invalid email or password, or the account is temporarily locked.";

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ILogger<AuthenticationService> _logger;

    public AuthenticationService(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ILogger<AuthenticationService> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _logger = logger;
    }

    public async Task<(bool Success, string Message)> RegisterAsync(string email, string password, string? firstName = null, string? lastName = null, bool autoConfirmEmail = false)
    {
        try
        {
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FirstName = firstName,
                LastName = lastName,
                EmailConfirmed = autoConfirmEmail
            };

            var result = await _userManager.CreateAsync(user, password);

            if (result.Succeeded)
            {
                _logger.LogInformation("User {Email} registered successfully", email);
                return (true, "Registration successful");
            }

            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            _logger.LogWarning("Registration failed for {Email}: {Errors}", email, errors);
            return (false, errors);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during registration for {Email}", email);
            return (false, "An error occurred during registration");
        }
    }

    /// <summary>
    /// Creates an account that signs in only through an external provider (no local password).
    /// The caller must already have a provider-verified email address.
    /// </summary>
    public async Task<ApplicationUser?> RegisterExternalAsync(string email, string? firstName, string? lastName)
    {
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            EmailConfirmed = true
        };

        var result = await _userManager.CreateAsync(user);
        if (!result.Succeeded)
        {
            _logger.LogWarning("External registration failed for {Email}: {Errors}",
                email, string.Join(", ", result.Errors.Select(e => e.Description)));
            return null;
        }

        return user;
    }

    public async Task<(bool Success, string Message)> LoginAsync(string emailOrPhone, string password)
    {
        try
        {
            var user = await _userManager.FindByEmailAsync(emailOrPhone)
                ?? await _userManager.FindByNameAsync(emailOrPhone);

            if (user == null)
            {
                _logger.LogWarning("Login attempt for non-existent user: {EmailOrPhone}", emailOrPhone);
                return (false, InvalidCredentialsMessage);
            }

            var result = await _signInManager.PasswordSignInAsync(user, password, isPersistent: false, lockoutOnFailure: true);

            if (result.Succeeded)
            {
                _logger.LogInformation("User {Email} logged in successfully", user.Email);
                return (true, "Login successful");
            }

            if (result.IsNotAllowed)
            {
                _logger.LogInformation("User {Email} tried to sign in before confirming their email", user.Email);
                return (false, EmailNotConfirmedMessage);
            }

            if (result.IsLockedOut)
            {
                _logger.LogWarning("User {Email} account is locked out", user.Email);
            }
            else
            {
                _logger.LogWarning("Failed login attempt for user {Email}", user.Email);
            }

            return (false, InvalidCredentialsMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login for {EmailOrPhone}", emailOrPhone);
            return (false, "An error occurred during login");
        }
    }

    public async Task LogoutAsync()
    {
        try
        {
            await _signInManager.SignOutAsync();
            _logger.LogInformation("User logged out");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during logout");
        }
    }
}
