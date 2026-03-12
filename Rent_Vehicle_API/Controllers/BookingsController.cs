using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Rent_Vehicle.Models.DTOs;
using Rent_Vehicle.Services;

namespace Rent_Vehicle.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BookingsController : ControllerBase
{
    private readonly IBookingService _bookingService;
    private readonly IAuthService _authService;

    public BookingsController(IBookingService bookingService, IAuthService authService)
    {
        _bookingService = bookingService;
        _authService = authService;
    }

    [HttpGet]
    public async Task<ActionResult<List<BookingDto>>> GetBookings()
    {
        var emailClaim = User.FindFirst(ClaimTypes.Email)?.Value;
        var roleClaim = User.FindFirst(ClaimTypes.Role)?.Value;

        if (string.IsNullOrEmpty(emailClaim))
        {
            return Unauthorized();
        }

        var userId = await _authService.GetUserIdByEmailAsync(emailClaim);
        if (userId == null)
        {
            return Unauthorized();
        }

        var bookings = await _bookingService.GetBookingsAsync(userId.Value, roleClaim ?? string.Empty);
        return Ok(bookings);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<BookingDto>> GetBooking(int id)
    {
        var emailClaim = User.FindFirst(ClaimTypes.Email)?.Value;
        var roleClaim = User.FindFirst(ClaimTypes.Role)?.Value;

        if (string.IsNullOrEmpty(emailClaim))
        {
            return Unauthorized();
        }

        var userId = await _authService.GetUserIdByEmailAsync(emailClaim);
        if (userId == null)
        {
            return Unauthorized();
        }

        var booking = await _bookingService.GetBookingAsync(id, userId.Value, roleClaim ?? string.Empty);

        if (booking == null)
        {
            // Check if it was authorization issue or not found
            var existingBooking = await _bookingService.GetBookingAsync(id, 0, "Admin");
            if (existingBooking == null)
            {
                return NotFound();
            }
            return Forbid();
        }

        return Ok(booking);
    }

    [HttpPost]
    public async Task<ActionResult<BookingDto>> CreateBooking([FromBody] CreateBookingRequest request)
    {
        var emailClaim = User.FindFirst(ClaimTypes.Email)?.Value;

        if (string.IsNullOrEmpty(emailClaim))
        {
            return Unauthorized();
        }

        var userId = await _authService.GetUserIdByEmailAsync(emailClaim);
        if (userId == null)
        {
            return Unauthorized();
        }

        var (booking, error) = await _bookingService.CreateBookingAsync(userId.Value, request);

        if (error != null)
        {
            if (error.Contains("not found", StringComparison.OrdinalIgnoreCase))
            {
                return NotFound(error);
            }
            return BadRequest(error);
        }

        return CreatedAtAction(nameof(GetBooking), new { id = booking!.Id }, booking);
    }

    [HttpPut("{id}/status")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateBookingStatus(int id, [FromBody] UpdateBookingStatusRequest request)
    {
        var (success, error, result) = await _bookingService.UpdateBookingStatusAsync(id, request.Status);

        if (!success)
        {
            if (error?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true)
            {
                return NotFound(error);
            }
            return BadRequest(error);
        }

        if (result != null)
        {
            return Ok(result);
        }

        return NoContent();
    }

    [HttpPost("{id}/cancel")]
    public async Task<IActionResult> CancelBooking(int id)
    {
        var emailClaim = User.FindFirst(ClaimTypes.Email)?.Value;
        var roleClaim = User.FindFirst(ClaimTypes.Role)?.Value;

        if (string.IsNullOrEmpty(emailClaim))
        {
            return Unauthorized();
        }

        var userId = await _authService.GetUserIdByEmailAsync(emailClaim);
        if (userId == null)
        {
            return Unauthorized();
        }

        var (success, error) = await _bookingService.CancelBookingAsync(id, userId.Value);

        if (!success)
        {
            if (error?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true)
            {
                return NotFound(error);
            }
            if (error?.Contains("Unauthorized", StringComparison.OrdinalIgnoreCase) == true)
            {
                return Forbid();
            }
            return BadRequest(error);
        }

        return NoContent();
    }
}
