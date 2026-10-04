using Microsoft.AspNetCore.Identity;
using SmartMosquitoControl.Models;

namespace SmartMosquitoControl.Services;

public class AuthenticationService
{
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

    public async Task<(bool Success, string Message)> LoginAsync(string emailOrPhone, string password)
    {
        try
        {
            var user = await _userManager.FindByEmailAsync(emailOrPhone) 
                ?? await _userManager.FindByNameAsync(emailOrPhone);

            if (user == null)
            {
                _logger.LogWarning("Login attempt for non-existent user: {EmailOrPhone}", emailOrPhone);
                return (false, "Invalid email or password");
            }

            var result = await _signInManager.PasswordSignInAsync(user, password, isPersistent: false, lockoutOnFailure: true);

            if (result.Succeeded)
            {
                _logger.LogInformation("User {Email} logged in successfully", user.Email);
                return (true, "Login successful");
            }

            if (result.IsLockedOut)
            {
                _logger.LogWarning("User {Email} account is locked out", user.Email);
                return (false, "Account is locked. Please try again later");
            }

            _logger.LogWarning("Failed login attempt for user {Email}", user.Email);
            return (false, "Invalid email or password");
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
