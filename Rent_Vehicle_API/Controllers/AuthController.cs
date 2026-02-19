using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Rent_Vehicle.Data;
using Rent_Vehicle.Models;
using Rent_Vehicle.Models.DTOs;
using Rent_Vehicle.Services;
using System.Security.Claims;

namespace Rent_Vehicle.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly VehicleRentalDbContext _context;
    private readonly IAuthService _authService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IWebHostEnvironment _environment;
    
    // Cookie names for refresh tokens
    private const string RefreshTokenCookieName = ".RV.RefreshToken";

    public AuthController(
        VehicleRentalDbContext context, 
        IAuthService authService, 
        IHttpContextAccessor httpContextAccessor,
        IWebHostEnvironment environment)
    {
        _context = context;
        _authService = authService;
        _httpContextAccessor = httpContextAccessor;
        _environment = environment;
    }

    private string? GetClientIpAddress()
    {
        return _httpContextAccessor?.HttpContext?.Connection?.RemoteIpAddress?.ToString();
    }

    private string? GetDeviceInfo()
    {
        var userAgent = _httpContextAccessor?.HttpContext?.Request?.Headers["User-Agent"].ToString();
        return userAgent;
    }

    /// <summary>
    /// Sets the refresh token as an HttpOnly cookie
    /// </summary>
    private void SetRefreshTokenCookie(string refreshToken, DateTime expiryDate)
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            // In production, require HTTPS (Secure=true). In development, allow HTTP (Secure=false)
            Secure = !_environment.IsDevelopment(),
            SameSite = SameSiteMode.Lax,
            Expires = expiryDate,
            Path = "/"
        };
        
        Response.Cookies.Append(RefreshTokenCookieName, refreshToken, cookieOptions);
    }

    /// <summary>
    /// Clears the refresh token cookie
    /// </summary>
    private void ClearRefreshTokenCookie()
    {
        Response.Cookies.Delete(RefreshTokenCookieName, new CookieOptions
        {
            Path = "/",
            Secure = !_environment.IsDevelopment(),
            SameSite = SameSiteMode.Lax
        });
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new AuthResponse { Success = false, Message = "Email and password are required" });
        }

        // First, try to register the user (this handles duplicate email check and saves to DB)
        var (success, message, userDto) = await _authService.RegisterAsync(
            request.Name, request.Email, request.Password, request.Phone);

        if (!success)
        {
            return BadRequest(new AuthResponse { Success = false, Message = message });
        }

        // User is now saved in DB, retrieve it to get the ID
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
        if (user == null)
        {
            return BadRequest(new AuthResponse { Success = false, Message = "Failed to retrieve registered user" });
        }

        // Generate tokens
        var token = _authService.GenerateJwtToken(user);
        var (refreshTokenString, refreshToken) = await _authService.GenerateRefreshTokenAsync(
            user.Id,
            GetDeviceInfo(),
            GetClientIpAddress());

        // Set refresh token as HttpOnly cookie
        SetRefreshTokenCookie(refreshTokenString, refreshToken.ExpiryDate);

        return Ok(new AuthResponse
        {
            Success = true,
            Message = "Registration successful",
            User = userDto,
            Token = token,
            RefreshToken = refreshTokenString
        });
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new AuthResponse { Success = false, Message = "Email and password are required" });
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            return Unauthorized(new AuthResponse { Success = false, Message = "Invalid email or password" });
        }

        if (!user.IsActive)
        {
            return Unauthorized(new AuthResponse { Success = false, Message = "User account is inactive" });
        }

        var token = _authService.GenerateJwtToken(user);
        var (refreshTokenString, refreshToken) = await _authService.GenerateRefreshTokenAsync(
            user.Id,
            GetDeviceInfo(),
            GetClientIpAddress());

        var userDto = new UserDto
        {
            Id = user.Id,
            Name = user.Name,
            Email = user.Email,
            Phone = user.Phone ?? string.Empty,
            Role = user.Role
        };

        return Ok(new AuthResponse
        {
            Success = true,
            Message = "Login successful",
            User = userDto,
            Token = token,
            RefreshToken = refreshTokenString
        });
    }

    [HttpPost("refresh-token")]
    public async Task<ActionResult<AuthResponse>> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Token) || string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return BadRequest(new AuthResponse { Success = false, Message = "Token and refresh token are required" });
        }

        var principal = _authService.GetPrincipalFromExpiredToken(request.Token);
        if (principal == null)
        {
            return Unauthorized(new AuthResponse { Success = false, Message = "Invalid token" });
        }

        var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(userIdClaim, out int userId))
        {
            return Unauthorized(new AuthResponse { Success = false, Message = "Invalid token claims" });
        }

        // Validate refresh token
        var isValidRefreshToken = await _authService.ValidateRefreshTokenAsync(userId, request.RefreshToken);
        if (!isValidRefreshToken)
        {
            return Unauthorized(new AuthResponse { Success = false, Message = "Invalid or expired refresh token" });
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null || !user.IsActive)
        {
            return NotFound(new AuthResponse { Success = false, Message = "User not found or inactive" });
        }

        // ✅ SECURITY FIX: Revoke the old refresh token to prevent replay attacks
        await _authService.RevokeSpecificRefreshTokenAsync(request.RefreshToken);

        // Generate new tokens
        var newToken = _authService.GenerateJwtToken(user);
        var (newRefreshTokenString, _) = await _authService.GenerateRefreshTokenAsync(
            user.Id,
            GetDeviceInfo(),
            GetClientIpAddress());

        var userDto = new UserDto
        {
            Id = user.Id,
            Name = user.Name,
            Email = user.Email,
            Phone = user.Phone ?? string.Empty,
            Role = user.Role
        };

        return Ok(new AuthResponse
        {
            Success = true,
            Message = "Token refreshed successfully",
            User = userDto,
            Token = newToken,
            RefreshToken = newRefreshTokenString
        });
    }

    [HttpPost("logout")]
    public async Task<ActionResult<AuthResponse>> Logout()
    {
        // Accept logout with either valid token or expired token
        // This prevents infinite 401 loops when token is already expired
        
        int userId = 0;

        // Try to get userId from authorization header (even if expired)
        var authHeader = Request.Headers["Authorization"].ToString();
        if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
        {
            var token = authHeader.Substring("Bearer ".Length);
            var principal = _authService.GetPrincipalFromExpiredToken(token);
            
            if (principal != null)
            {
                var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (int.TryParse(userIdClaim, out int extractedUserId))
                {
                    userId = extractedUserId;
                }
            }
        }

        if (userId == 0)
        {
            return Unauthorized(new AuthResponse { Success = false, Message = "Invalid or missing token" });
        }

        await _authService.RevokeRefreshTokenAsync(userId);
        
        // Clear the refresh token cookie
        ClearRefreshTokenCookie();

        return Ok(new AuthResponse
        {
            Success = true,
            Message = "Logout successful"
        });
    }
}
