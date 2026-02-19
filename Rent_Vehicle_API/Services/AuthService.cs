using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Rent_Vehicle.Data;
using Rent_Vehicle.Models;
using Rent_Vehicle.Models.DTOs;
using Microsoft.EntityFrameworkCore;

namespace Rent_Vehicle.Services;

public interface IAuthService
{
    Task<(bool, string, UserDto?)> RegisterAsync(string name, string email, string password, string? phone);
    Task<(bool, string, UserDto?, string?, string?)> LoginAsync(string email, string password);
    string GenerateJwtToken(User user);
    Task<(string, RefreshToken)> GenerateRefreshTokenAsync(int userId, string? deviceInfo = null, string? ipAddress = null);
    ClaimsPrincipal? GetPrincipalFromExpiredToken(string token);
    Task<bool> ValidateRefreshTokenAsync(int userId, string token);
    Task RevokeRefreshTokenAsync(int userId);
    Task RevokeSpecificRefreshTokenAsync(string tokenString);
}

public class AuthService : IAuthService
{
    private readonly IConfiguration _configuration;
    private readonly JwtSettings _jwtSettings;
    private readonly VehicleRentalDbContext _context;

    public AuthService(IConfiguration configuration, VehicleRentalDbContext context)
    {
        _configuration = configuration;
        _context = context;
        _jwtSettings = configuration.GetSection("JwtSettings").Get<JwtSettings>()
            ?? throw new InvalidOperationException("JwtSettings not found in configuration");
    }

    public async Task<(bool, string, UserDto?)> RegisterAsync(string name, string email, string password, string? phone)
    {
        try
        {
            // ✅ FIX: Check if email already exists
            var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (existingUser != null)
            {
                return (false, "Email already registered", null);
            }

            var passwordHash = BCrypt.Net.BCrypt.HashPassword(password);

            var user = new User
            {
                Name = name,
                Email = email,
                PasswordHash = passwordHash,
                Phone = phone,
                Role = "Customer",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // ✅ FIX: Save user to database
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var userDto = new UserDto
            {
                Id = user.Id,
                Name = user.Name,
                Email = user.Email,
                Phone = user.Phone ?? string.Empty,
                Role = user.Role
            };

            return (true, "Registration successful", userDto);
        }
        catch (Exception ex)
        {
            return (false, $"Registration failed: {ex.Message}", null);
        }
    }

    public async Task<(bool, string, UserDto?, string?, string?)> LoginAsync(string email, string password)
    {
        try
        {
            // This will be called from controller with actual user from DB
            // The verification happens there
            return (true, "Login successful", null, null, null);
        }
        catch (Exception ex)
        {
            return (false, $"Login failed: {ex.Message}", null, null, null);
        }
    }

    public string GenerateJwtToken(User user)
    {
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Secret));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.Name),
            new Claim(ClaimTypes.Role, user.Role)
        };

        var token = new JwtSecurityToken(
            issuer: _jwtSettings.ValidIssuer,
            audience: _jwtSettings.ValidAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_jwtSettings.ExpirationMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public async Task<(string, RefreshToken)> GenerateRefreshTokenAsync(int userId, string? deviceInfo = null, string? ipAddress = null)
    {
        var randomNumber = new byte[64];
        using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
        {
            rng.GetBytes(randomNumber);
        }

        var tokenString = Convert.ToBase64String(randomNumber);

        var expiryDate = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpirationDays);

        var refreshToken = new RefreshToken
        {
            UserId = userId,
            Token = tokenString,
            ExpiryDate = expiryDate,
            IsRevoked = false,
            CreatedAt = DateTime.UtcNow,
            DeviceInfo = deviceInfo,
            IpAddress = ipAddress
        };
        try
        {

            _context.RefreshTokens.Add(refreshToken);
            await _context.SaveChangesAsync();
        }
        catch(Exception e)
        {
           
        }

        return (tokenString, refreshToken);
    }

    public ClaimsPrincipal? GetPrincipalFromExpiredToken(string token)
    {
        var tokenValidationParameters = new TokenValidationParameters
        {
            ValidateAudience = false,
            ValidateIssuer = false,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Secret)),
            ValidateLifetime = false
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out SecurityToken securityToken);

        if (!(securityToken is JwtSecurityToken jwtSecurityToken) ||
            !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256,
                StringComparison.InvariantCultureIgnoreCase))
        {
            return null;
        }

        return principal;
    }

    public async Task<bool> ValidateRefreshTokenAsync(int userId, string token)
    {
        var refreshToken = await _context.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.UserId == userId && rt.Token == token && !rt.IsRevoked);

        if (refreshToken == null)
            return false;

        if (refreshToken.ExpiryDate < DateTime.UtcNow)
        {
            // Token has expired, mark it as revoked
            refreshToken.IsRevoked = true;
            await _context.SaveChangesAsync();
            return false;
        }

        return true;
    }

    public async Task RevokeRefreshTokenAsync(int userId)
    {
        // Revoke all refresh tokens for this user (logout from all devices)
        var refreshTokens = await _context.RefreshTokens
            .Where(rt => rt.UserId == userId && !rt.IsRevoked)
            .ToListAsync();

        foreach (var token in refreshTokens)
        {
            token.IsRevoked = true;
        }

        await _context.SaveChangesAsync();
    }

    public async Task RevokeSpecificRefreshTokenAsync(string tokenString)
    {
        // Revoke a specific refresh token (used for token rotation)
        var refreshToken = await _context.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == tokenString && !rt.IsRevoked);

        if (refreshToken != null)
        {
            refreshToken.IsRevoked = true;
            await _context.SaveChangesAsync();
        }
    }
}
