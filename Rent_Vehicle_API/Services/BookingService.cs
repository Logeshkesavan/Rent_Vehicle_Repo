using Microsoft.EntityFrameworkCore;
using Rent_Vehicle.Data;
using Rent_Vehicle.Models;
using Rent_Vehicle.Models.DTOs;

namespace Rent_Vehicle.Services;

public interface IBookingService
{
    Task<List<BookingDto>> GetBookingsAsync(int userId, string role);
    Task<BookingDto?> GetBookingAsync(int id, int userId, string role);
    Task<(BookingDto? Booking, string? Error)> CreateBookingAsync(int userId, CreateBookingRequest request);
    Task<(bool Success, string? Error, object? Result)> UpdateBookingStatusAsync(int id, string status);
    Task<(bool Success, string? Error)> CancelBookingAsync(int id, int userId);
}

public class BookingService : IBookingService
{
    private readonly VehicleRentalDbContext _context;

    public BookingService(VehicleRentalDbContext context)
    {
        _context = context;
    }

    public async Task<List<BookingDto>> GetBookingsAsync(int userId, string role)
    {
        IQueryable<Booking> query;

        // Admin can see all bookings, customers see only their own
        if (role == "Admin")
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

        return bookings.Select(MapToDto).ToList();
    }

    public async Task<BookingDto?> GetBookingAsync(int id, int userId, string role)
    {
        var booking = await _context.Bookings
            .Include(b => b.Vehicle)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (booking == null)
        {
            return null;
        }

        // Check if user is authorized to view this booking
        if (role != "Admin" && booking.UserId != userId)
        {
            return null;
        }

        return MapToDto(booking);
    }

    public async Task<(BookingDto? Booking, string? Error)> CreateBookingAsync(int userId, CreateBookingRequest request)
    {
        var vehicle = await _context.Vehicles.FindAsync(request.VehicleId);
        if (vehicle == null || !vehicle.IsActive)
        {
            return (null, "Vehicle not found");
        }

        if (request.PickupDate >= request.ReturnDate)
        {
            return (null, "Return date must be after pickup date");
        }

        // Check availability - block only on Confirmed/PickedUp.
        // Pending bookings are allowed, but we return a warning if there are pending conflicts.
        var blockingStatuses = new[] { "Confirmed", "PickedUp" };
        var conflictingBooking = await _context.Bookings
            .Where(b => b.VehicleId == request.VehicleId &&
                   blockingStatuses.Contains(b.Status) &&
                   b.PickupDate < request.ReturnDate &&
                   b.ReturnDate > request.PickupDate)
            .FirstOrDefaultAsync();

        if (conflictingBooking != null)
        {
            return (null, "Vehicle is not available for the selected dates");
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

        var bookingDto = MapToDto(booking);
        bookingDto.HasPendingConflicts = hasPendingConflicts;

        if (hasPendingConflicts)
        {
            bookingDto.Message = "Warning: Other users have pending bookings for the same dates. Admin will confirm one booking.";
        }

        return (bookingDto, null);
    }

    public async Task<(bool Success, string? Error, object? Result)> UpdateBookingStatusAsync(int id, string status)
    {
        var validStatuses = new[] { "Confirmed", "PickedUp", "Completed", "Cancelled" };

        if (!validStatuses.Contains(status))
        {
            return (false, "Invalid status", null);
        }

        var booking = await _context.Bookings.FindAsync(id);

        if (booking == null)
        {
            return (false, "Booking not found", null);
        }

        booking.Status = status;
        booking.UpdatedAt = DateTime.UtcNow;

        // Get the vehicle
        var vehicle = await _context.Vehicles.FindAsync(booking.VehicleId);
        if (vehicle == null)
        {
            return (false, "Vehicle not found", null);
        }

        var today = DateTime.UtcNow.Date;

        // Update vehicle availability status based on booking status change
        switch (status)
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
                                b.PickupDate < booking.ReturnDate &&
                                b.ReturnDate > booking.PickupDate)
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
                    return (true, null, new
                    {
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

        return (true, null, null);
    }

    public async Task<(bool Success, string? Error)> CancelBookingAsync(int id, int userId)
    {
        var booking = await _context.Bookings.FindAsync(id);

        if (booking == null)
        {
            return (false, "Booking not found");
        }

       

        if (booking.Status == "Completed" || booking.Status == "Cancelled")
        {
            return (false, "Cannot cancel completed or already cancelled bookings");
        }

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

        return (true, null);
    }

    private static BookingDto MapToDto(Booking booking)
    {
        return new BookingDto
        {
            Id = booking.Id,
            UserId = booking.UserId,
            VehicleId = booking.VehicleId,
            VehicleName = booking.Vehicle != null ? $"{booking.Vehicle.Brand} {booking.Vehicle.Model}" : string.Empty,
            PickupDate = booking.PickupDate,
            ReturnDate = booking.ReturnDate,
            TotalDays = booking.TotalDays,
            RentalAmount = booking.RentalAmount,
            SecurityDeposit = booking.SecurityDeposit,
            TotalAmount = booking.TotalAmount,
            Status = booking.Status,
            PaymentStatus = booking.PaymentStatus
        };
    }
}
