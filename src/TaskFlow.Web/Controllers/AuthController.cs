using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TaskFlow.Application;
using TaskFlow.Application.Models;

namespace TaskFlow.Web.Controllers;

[ApiController, Route("api/auth")]
public sealed class AuthController(AuthService service) : ControllerBase
{
    [AllowAnonymous, HttpGet("csrf")]
    public IActionResult Csrf([FromServices] IAntiforgery antiforgery) => Ok(new { token = antiforgery.GetAndStoreTokens(HttpContext).RequestToken });

    [AllowAnonymous, EnableRateLimiting("auth"), HttpPost("register")]
    public async Task<IActionResult> Register(RegisterInput input, CancellationToken ct)
    {
        var user = await service.Register(input, ct);
        await SignIn(user);
        return Created("/api/auth/me", user);
    }

    [AllowAnonymous, EnableRateLimiting("auth"), HttpPost("login")]
    public async Task<IActionResult> Login(LoginInput input, CancellationToken ct)
    {
        var user = await service.Login(input, ct);
        if (user is null) return Problem(statusCode: 401, title: "Invalid email or password.");
        await SignIn(user);
        return Ok(user);
    }

    [Authorize, HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var user = await service.GetUser(Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!), ct);
        return user is null ? Unauthorized() : Ok(user);
    }

    [Authorize, HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }

    [AllowAnonymous, HttpGet("public")]
    public IActionResult Public() => Ok(new { application = "TaskFlow", status = "ok" });

    private Task SignIn(UserInfo user) => HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
        new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()), new Claim(ClaimTypes.Name, user.Name)
        ], CookieAuthenticationDefaults.AuthenticationScheme)), new AuthenticationProperties { IsPersistent = false });
}
