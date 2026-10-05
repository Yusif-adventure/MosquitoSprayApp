using System.Threading.Tasks;
using Moq;
using Xunit;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using SmartMosquitoControl.Services;
using SmartMosquitoControl.Models;

namespace SmartMosquitoControl.Tests;

public class AuthenticationServiceTests
{
    [Fact]
    public async Task RegisterAsync_ReturnsSuccess_OnValidCreate()
    {
        var userStore = new Mock<IUserStore<ApplicationUser>>();
        var userManager = new Mock<UserManager<ApplicationUser>>(
            userStore.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        var signInManager = new Mock<SignInManager<ApplicationUser>>(
            userManager.Object,
            new Mock<Microsoft.AspNetCore.Http.IHttpContextAccessor>().Object,
            new Mock<IUserClaimsPrincipalFactory<ApplicationUser>>().Object,
            null!, null!, null!, null!);

        var logger = new Mock<ILogger<AuthenticationService>>().Object;

        userManager.Setup(u => u.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);

        var svc = new AuthenticationService(userManager.Object, signInManager.Object, logger);

        var (success, message) = await svc.RegisterAsync("test@example.com", "P@ssw0rd!", "John", "Doe");

        Assert.True(success);
        Assert.Contains("successful", message.ToLowerInvariant());
    }

    [Fact]
    public async Task LoginAsync_ReturnsFailure_OnUnknownUser()
    {
        var userStore = new Mock<IUserStore<ApplicationUser>>();
        var userManager = new Mock<UserManager<ApplicationUser>>(
            userStore.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        var signInManager = new Mock<SignInManager<ApplicationUser>>(
            userManager.Object,
            new Mock<Microsoft.AspNetCore.Http.IHttpContextAccessor>().Object,
            new Mock<IUserClaimsPrincipalFactory<ApplicationUser>>().Object,
            null!, null!, null!, null!);

        var logger = new Mock<ILogger<AuthenticationService>>().Object;

        userManager.Setup(u => u.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync((ApplicationUser?)null);
        userManager.Setup(u => u.FindByNameAsync(It.IsAny<string>())).ReturnsAsync((ApplicationUser?)null);

        var svc = new AuthenticationService(userManager.Object, signInManager.Object, logger);

        var (success, message) = await svc.LoginAsync("noone@example.com", "nope");

        Assert.False(success);
        Assert.Contains("invalid", message.ToLowerInvariant());
    }
}