using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using JewelryMS.Domain.DTOs.Auth;
using JewelryMS.Domain.Interfaces.Services;
using System.Security.Claims;

namespace JewelryMS.API.Controllers;

[ApiController]
[Route("api/[controller]")] 
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthService authService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var response = await _authService.LoginAsync(request);
        if (response == null) return Unauthorized(new { message = "Invalid credentials" });
        return Ok(response);
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var success = await _authService.RegisterAsync(request);
        if (!success) return Conflict(new { message = "Registration failed" });
        return StatusCode(201, new { message = "Registered successfully" });
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var success = await _authService.ChangePasswordAsync(Guid.Parse(userId), request);
        return success ? Ok(new { message = "Password updated" }) : BadRequest("Update failed");
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        var token = await _authService.RequestPasswordResetAsync(request.Email);
        _logger.LogInformation("DEBUG: Reset token for {Email} is {Token}", request.Email, token);
        return Ok(new { message = "If the account exists, a reset link has been sent to the console/email." });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        var success = await _authService.ResetPasswordWithTokenAsync(request);
        return success ? Ok(new { message = "Password reset successfully" }) : BadRequest("Invalid or expired token");
    }
}