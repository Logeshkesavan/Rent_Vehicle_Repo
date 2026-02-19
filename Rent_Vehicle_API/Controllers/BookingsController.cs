using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rent_Vehicle.Data;
using Rent_Vehicle.Models;
using Rent_Vehicle.Models.DTOs;

namespace Rent_Vehicle.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BookingsController : ControllerBase
{
    private readonly VehicleRentalDbContext _context;

    public BookingsController(VehicleRentalDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<BookingDto>>> GetBookings()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var roleCllaim = User.FindFirst(ClaimTypes.Role)?.Value;

        if (!int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        IQueryable<Booking> query;

        // Admin can see all bookings, customers see only their own
        if (roleCllaim == "Admin")
        {
            query = _context.Bookings;
        }
        else
        {
            query = _context.Bookings.Where(b => b.UserId == userId);
        }

        var bookings = await query
            .Include(b => b.Vehicle)
            .Include(b => b.User)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync();

        var bookingDtos = bookings.Select(b => new BookingDto
        {
            Id = b.Id,
            UserId = b.UserId,
            VehicleId = b.VehicleId,
            VehicleName = $"{b.Vehicle.Brand} {b.Vehicle.Model}",
            PickupDate = b.PickupDate,
            ReturnDate = b.ReturnDate,
            TotalDays = b.TotalDays,
            RentalAmount = b.RentalAmount,
            SecurityDeposit = b.SecurityDeposit,
            TotalAmount = b.TotalAmount,
            Status = b.Status,
            PaymentStatus = b.PaymentStatus
        }).ToList();

        return Ok(bookingDtos);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<BookingDto>> GetBooking(int id)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var roleCllaim = User.FindFirst(ClaimTypes.Role)?.Value;

        if (!int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        var booking = await _context.Bookings
            .Include(b => b.Vehicle)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (booking == null)
        {
            return NotFound();
        }

        // Check if user is authorized to view this booking
        if (roleCllaim != "Admin" && booking.UserId != userId)
        {
            return Forbid();
        }

        var bookingDto = new BookingDto
        {
            Id = booking.Id,
            UserId = booking.UserId,
            VehicleId = booking.VehicleId,
            VehicleName = $"{booking.Vehicle.Brand} {booking.Vehicle.Model}",
            PickupDate = booking.PickupDate,
            ReturnDate = booking.ReturnDate,
            TotalDays = booking.TotalDays,
            RentalAmount = booking.RentalAmount,
            SecurityDeposit = booking.SecurityDeposit,
            TotalAmount = booking.TotalAmount,
            Status = booking.Status,
            PaymentStatus = booking.PaymentStatus
        };

        return Ok(bookingDto);
    }

    [HttpPost]
    public async Task<ActionResult<BookingDto>> CreateBooking([FromBody] CreateBookingRequest request)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        var vehicle = await _context.Vehicles.FindAsync(request.VehicleId);
        if (vehicle == null || !vehicle.IsActive)
        {
            return NotFound("Vehicle not found");
        }

        if (request.PickupDate >= request.ReturnDate)
        {
            return BadRequest("Return date must be after pickup date");
        }

        // Check availability - only check Confirmed/PickedUp (not Pending)
        var conflictingBooking = await _context.Bookings
            .Where(b => b.VehicleId == request.VehicleId &&
                   (b.Status == "Confirmed" || b.Status == "PickedUp") &&
                   b.PickupDate < request.ReturnDate &&
                   b.ReturnDate > request.PickupDate)
            .FirstOrDefaultAsync();

        if (conflictingBooking != null)
        {
            return BadRequest("Vehicle is not available for the selected dates");
        }

        // Check for other PENDING bookings - warn user but allow creation
        var pendingConflicts = await _context.Bookings
            .Where(b => b.VehicleId == request.VehicleId &&
                   b.Status == "Pending" &&
                   b.PickupDate < request.ReturnDate &&
                   b.ReturnDate > request.PickupDate &&
                   b.UserId != userId) // Exclude own pending bookings
            .ToListAsync();

        var hasPendingConflicts = pendingConflicts.Any();

        var totalDays = (int)(request.ReturnDate.Date - request.PickupDate.Date).TotalDays;
        if (totalDays <= 0) totalDays = 1;

        var rentalAmount = vehicle.PricePerDay * totalDays;
        var securityDeposit = vehicle.PricePerDay * 2;
        var totalAmount = rentalAmount + securityDeposit;

        var booking = new Booking
        {
            UserId = userId,
            VehicleId = request.VehicleId,
            PickupDate = request.PickupDate,
            ReturnDate = request.ReturnDate,
            TotalDays = totalDays,
            RentalAmount = rentalAmount,
            SecurityDeposit = securityDeposit,
            TotalAmount = totalAmount,
            Status = "Pending",
            PaymentStatus = "Pending",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Bookings.Add(booking);
        await _context.SaveChangesAsync();

        var bookingDto = new BookingDto
        {
            Id = booking.Id,
            UserId = booking.UserId,
            VehicleId = booking.VehicleId,
            VehicleName = $"{vehicle.Brand} {vehicle.Model}",
            PickupDate = booking.PickupDate,
            ReturnDate = booking.ReturnDate,
            TotalDays = booking.TotalDays,
            RentalAmount = booking.RentalAmount,
            SecurityDeposit = booking.SecurityDeposit,
            TotalAmount = booking.TotalAmount,
            Status = booking.Status,
            PaymentStatus = booking.PaymentStatus,
            HasPendingConflicts = hasPendingConflicts
        };

        // Return warning if there are other pending bookings
        if (hasPendingConflicts)
        {
            bookingDto.Message = "Warning: Other users have pending bookings for the same dates. Admin will confirm one booking.";
        }

        return CreatedAtAction(nameof(GetBooking), new { id = booking.Id }, bookingDto);
    }

    [HttpPut("{id}/status")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateBookingStatus(int id, [FromBody] UpdateBookingStatusRequest request)
    {
        var validStatuses = new[] { "Confirmed", "PickedUp", "Completed", "Cancelled" };

        if (!validStatuses.Contains(request.Status))
        {
            return BadRequest("Invalid status");
        }

        var booking = await _context.Bookings.FindAsync(id);

        if (booking == null)
        {
            return NotFound();
        }

        var previousStatus = booking.Status;
        booking.Status = request.Status;
        booking.UpdatedAt = DateTime.UtcNow;

        // Get the vehicle
        var vehicle = await _context.Vehicles.FindAsync(booking.VehicleId);
        if (vehicle == null)
        {
            return NotFound("Vehicle not found");
        }

        var today = DateTime.UtcNow.Date;

        // Update vehicle availability status based on booking status change
        switch (request.Status)
        {
            case "Confirmed":
                // Only mark as "Booked" if booking dates include today
                if (booking.PickupDate.Date <= today && booking.ReturnDate.Date > today)
                {
                    vehicle.AvailabilityStatus = "Booked"; // Currently being used
                }
                else if (booking.PickupDate.Date > today)
                {
                    vehicle.AvailabilityStatus = "Available"; // Reserved for future, but today is free
                }

                // AUTO-CANCEL conflicting pending bookings for the same vehicle
                var conflictingBookings = await _context.Bookings
                    .Where(b => b.VehicleId == booking.VehicleId &&
                                b.Id != booking.Id && // Exclude the current booking
                                b.Status == "Pending" &&
                                b.PickupDate <= booking.ReturnDate &&
                                b.ReturnDate >= booking.PickupDate)
                    .ToListAsync();

                foreach (var conflicting in conflictingBookings)
                {
                    conflicting.Status = "Cancelled";
                    conflicting.UpdatedAt = DateTime.UtcNow;
                    _context.Update(conflicting);
                }

                if (conflictingBookings.Any())
                {
                    _context.Update(vehicle);
                    await _context.SaveChangesAsync();
                    return Ok(new { 
                        message = $"Booking confirmed. {conflictingBookings.Count} conflicting pending booking(s) automatically cancelled.",
                        cancelledCount = conflictingBookings.Count,
                        cancelledBookingIds = conflictingBookings.Select(b => b.Id).ToList()
                    });
                }
                break;

            case "PickedUp":
                // When customer picks up, vehicle is now rented/out
                vehicle.AvailabilityStatus = "Rented";
                break;

            case "Completed":
            case "Cancelled":
                // When booking completes or is cancelled, check if vehicle has other active bookings
                var hasOtherActiveBookings = await _context.Bookings
                    .AnyAsync(b => b.VehicleId == vehicle.Id &&
                                  b.Id != booking.Id &&
                                  (b.Status == "Confirmed" || b.Status == "PickedUp") &&
                                  b.PickupDate.Date <= today && 
                                  b.ReturnDate.Date > today);
                
                vehicle.AvailabilityStatus = hasOtherActiveBookings ? "Rented" : "Available";
                break;
        }

        _context.Bookings.Update(booking);
        _context.Vehicles.Update(vehicle);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpPost("{id}/cancel")]
    public async Task<IActionResult> CancelBooking(int id)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var roleCllaim = User.FindFirst(ClaimTypes.Role)?.Value;

        if (!int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        var booking = await _context.Bookings.FindAsync(id);

        if (booking == null)
        {
            return NotFound();
        }

        // Check if user is authorized to cancel this booking
        if (roleCllaim != "Admin" && booking.UserId != userId)
        {
            return Forbid();
        }

        if (booking.Status == "Completed" || booking.Status == "Cancelled")
        {
            return BadRequest("Cannot cancel completed or already cancelled bookings");
        }

        var previousStatus = booking.Status;
        booking.Status = "Cancelled";
        booking.UpdatedAt = DateTime.UtcNow;

        // Update vehicle availability status
        var vehicle = await _context.Vehicles.FindAsync(booking.VehicleId);
        if (vehicle != null)
        {
            var today = DateTime.UtcNow.Date;
            
            // Check if vehicle has other active bookings that include today
            var hasOtherActiveBookings = await _context.Bookings
                .AnyAsync(b => b.VehicleId == vehicle.Id &&
                              b.Id != booking.Id &&
                              (b.Status == "Confirmed" || b.Status == "PickedUp") &&
                              b.PickupDate.Date <= today && 
                              b.ReturnDate.Date > today);
            
            vehicle.AvailabilityStatus = hasOtherActiveBookings ? "Rented" : "Available";
            _context.Vehicles.Update(vehicle);
        }

        _context.Bookings.Update(booking);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}
