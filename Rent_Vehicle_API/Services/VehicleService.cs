using Microsoft.EntityFrameworkCore;
using Rent_Vehicle.Data;
using Rent_Vehicle.Models;
using Rent_Vehicle.Models.DTOs;

namespace Rent_Vehicle.Services;

public interface IVehicleService
{
    Task<List<VehicleDto>> GetVehiclesAsync(string? category = null, DateTime? fromDate = null, DateTime? toDate = null);
    Task<VehicleDetailDto?> GetVehicleAsync(int id);
    Task<VehicleAvailabilityDto?> GetVehicleAvailabilityAsync(int id, DateTime? fromDate, DateTime? toDate);
    Task<VehicleDto> CreateVehicleAsync(CreateVehicleRequest request);
    Task<bool> UpdateVehicleAsync(int id, CreateVehicleRequest request);
    Task<bool> DeleteVehicleAsync(int id);
}

public class VehicleService : IVehicleService
{
    private readonly VehicleRentalDbContext _context;

    public VehicleService(VehicleRentalDbContext context)
    {
        _context = context;
    }

    public async Task<List<VehicleDto>> GetVehiclesAsync(
    string? category = null,
    DateTime? fromDate = null,
    DateTime? toDate = null)
    {
        var query = _context.Vehicles
            .Where(v => v.IsActive)
            .AsQueryable();

        if (!string.IsNullOrEmpty(category))
        {
            var normalizedCategory = category.ToLower();
            query = query.Where(v => v.Category.ToLower() == normalizedCategory);
        }

        var blockingBookingStatuses = new[] { "Confirmed", "PickedUp" };

        // Filter out vehicles that have active bookings for the requested dates
        if (fromDate.HasValue || toDate.HasValue)
        {
            var searchStartDate = (fromDate ?? DateTime.UtcNow.Date).Date;
            var hasExplicitEndDate = toDate.HasValue;
            var searchEndDate = toDate?.Date ?? searchStartDate.AddDays(1);

            if (hasExplicitEndDate && searchEndDate <= searchStartDate)
            {
                searchEndDate = searchStartDate.AddDays(1);
            }

            // If caller only provides a start date, treat it as a 1-day search window.
            if (!hasExplicitEndDate)
            {
                searchEndDate = searchStartDate.AddDays(1);
            }

            // Remove vehicles with overlapping blocking bookings (Confirmed/PickedUp).
            query = query.Where(v => !_context.Bookings.Any(b =>
                b.VehicleId == v.Id &&
                blockingBookingStatuses.Contains(b.Status) &&
                b.PickupDate <= searchEndDate &&
                b.ReturnDate >= searchStartDate));

            // For non-blocking "Pending" bookings, return vehicles but flag a warning.
            var vehiclesWithWarnings = await query
                .Select(v => new
                {
                    Vehicle = v,
                    HasPendingConflicts = _context.Bookings.Any(b =>
                        b.VehicleId == v.Id &&
                        b.Status == "Pending" &&
                        b.PickupDate < searchEndDate &&
                        b.ReturnDate > searchStartDate)
                })
                .ToListAsync();

            return vehiclesWithWarnings.Select(x =>
            {
                var dto = MapVehicleDto(x.Vehicle);
                dto.HasPendingConflicts = x.HasPendingConflicts;
                dto.Message = x.HasPendingConflicts
                    ? "Warning: This vehicle has a pending booking for the selected dates. Availability is not guaranteed until an admin confirms."
                    : null;
                return dto;
            }).ToList();
        }

        var vehicles = await query.ToListAsync();
        return vehicles.Select(MapVehicleDto).ToList();
    }

    public async Task<VehicleDetailDto?> GetVehicleAsync(int id)
    {
        var vehicle = await _context.Vehicles.FindAsync(id);
        if (vehicle == null || !vehicle.IsActive)
        {
            return null;
        }

        var today = DateTime.UtcNow.Date;
        var futureDate = today.AddDays(60);

        var upcomingBookings = await _context.Bookings
            .Where(b => b.VehicleId == id &&
                        (b.Status == "Pending" || b.Status == "Confirmed" || b.Status == "PickedUp") &&
                        b.PickupDate < futureDate &&
                        b.ReturnDate > today)
            .OrderBy(b => b.PickupDate)
            .ToListAsync();

        var currentBlockingBooking = upcomingBookings.FirstOrDefault(b =>
            (b.Status == "Confirmed" || b.Status == "PickedUp") &&
            today >= b.PickupDate.Date &&
            today < b.ReturnDate.Date);

        return new VehicleDetailDto
        {
            Id = vehicle.Id,
            Category = vehicle.Category,
            Type = vehicle.Type,
            Brand = vehicle.Brand,
            Model = vehicle.Model,
            Year = vehicle.Year,
            RegistrationNumber = vehicle.RegistrationNumber,
            FuelType = vehicle.FuelType,
            Transmission = vehicle.Transmission,
            SeatingCapacity = vehicle.SeatingCapacity,
            PricePerDay = vehicle.PricePerDay,
            PricePerHour = vehicle.PricePerHour,
            Location = vehicle.Location,
            IsAvailableToday = currentBlockingBooking == null,
            NextAvailableDate = currentBlockingBooking?.ReturnDate ?? today,
            Images = vehicle.Images,
            Features = vehicle.Features,
            UpcomingBookings = upcomingBookings.Select(b => new BookingSummaryDto
            {
                Id = b.Id,
                PickupDate = b.PickupDate,
                ReturnDate = b.ReturnDate,
                Status = b.Status
            }).ToList()
        };
    }

    public async Task<VehicleAvailabilityDto?> GetVehicleAvailabilityAsync(int id, DateTime? fromDate, DateTime? toDate)
    {
        var vehicle = await _context.Vehicles.FindAsync(id);
        if (vehicle == null || !vehicle.IsActive)
        {
            return null;
        }

        var startDate = fromDate ?? DateTime.UtcNow.Date;
        var endDate = toDate ?? startDate.AddDays(30);

        var activeBookings = await _context.Bookings
            .Where(b => b.VehicleId == id &&
                        (b.Status == "Pending" || b.Status == "Confirmed" || b.Status == "PickedUp") &&
                        b.PickupDate < endDate &&
                        b.ReturnDate > startDate)
            .ToListAsync();

        var availability = new List<DateAvailabilityDto>();
        var currentDate = startDate;
        while (currentDate <= endDate)
        {
            var isBooked = activeBookings.Any(b =>
                (b.Status == "Confirmed" || b.Status == "PickedUp") &&
                currentDate >= b.PickupDate.Date &&
                currentDate < b.ReturnDate.Date);

            var hasPending = !isBooked && activeBookings.Any(b =>
                b.Status == "Pending" &&
                currentDate >= b.PickupDate.Date &&
                currentDate < b.ReturnDate.Date);

            availability.Add(new DateAvailabilityDto
            {
                Date = currentDate,
                IsAvailable = !isBooked,
                Status = isBooked ? "Booked" : (hasPending ? "Available (Pending)" : "Available")
            });

            currentDate = currentDate.AddDays(1);
        }

        return new VehicleAvailabilityDto
        {
            VehicleId = vehicle.Id,
            VehicleName = $"{vehicle.Brand} {vehicle.Model}",
            CurrentStatus = vehicle.AvailabilityStatus,
            FromDate = startDate,
            ToDate = endDate,
            TotalDays = (int)(endDate - startDate).TotalDays + 1,
            AvailableDays = availability.Count(a => a.IsAvailable),
            BookedDays = availability.Count(a => !a.IsAvailable),
            DateAvailability = availability
        };
    }

    public async Task<VehicleDto> CreateVehicleAsync(CreateVehicleRequest request)
    {
        var vehicle = new Vehicle
        {
            Category = request.Category,
            Type = request.Type,
            Brand = request.Brand,
            Model = request.Model,
            Year = request.Year,
            RegistrationNumber = request.RegistrationNumber,
            FuelType = request.FuelType,
            Transmission = request.Transmission,
            SeatingCapacity = request.SeatingCapacity,
            PricePerDay = request.PricePerDay,
            PricePerHour = request.PricePerHour,
            Location = request.Location,
            AvailabilityStatus = "Available",
            Images = request.Images,
            Features = request.Features,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Vehicles.Add(vehicle);
        await _context.SaveChangesAsync();

        return MapVehicleDto(vehicle);
    }

    public async Task<bool> UpdateVehicleAsync(int id, CreateVehicleRequest request)
    {
        var vehicle = await _context.Vehicles.FindAsync(id);
        if (vehicle == null)
        {
            return false;
        }

        vehicle.Category = request.Category;
        vehicle.Type = request.Type;
        vehicle.Brand = request.Brand;
        vehicle.Model = request.Model;
        vehicle.Year = request.Year;
        vehicle.RegistrationNumber = request.RegistrationNumber;
        vehicle.FuelType = request.FuelType;
        vehicle.Transmission = request.Transmission;
        vehicle.SeatingCapacity = request.SeatingCapacity;
        vehicle.PricePerDay = request.PricePerDay;
        vehicle.PricePerHour = request.PricePerHour;
        vehicle.Location = request.Location;
        vehicle.Images = request.Images;
        vehicle.Features = request.Features;
        vehicle.UpdatedAt = DateTime.UtcNow;

        _context.Vehicles.Update(vehicle);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteVehicleAsync(int id)
    {
        var vehicle = await _context.Vehicles.FindAsync(id);
        if (vehicle == null)
        {
            return false;
        }

        vehicle.IsActive = false;
        vehicle.UpdatedAt = DateTime.UtcNow;

        _context.Vehicles.Update(vehicle);
        await _context.SaveChangesAsync();
        return true;
    }

    private static VehicleDto MapVehicleDto(Vehicle vehicle)
    {
        return new VehicleDto
        {
            Id = vehicle.Id,
            Category = vehicle.Category,
            Type = vehicle.Type,
            Brand = vehicle.Brand,
            Model = vehicle.Model,
            Year = vehicle.Year,
            RegistrationNumber = vehicle.RegistrationNumber,
            FuelType = vehicle.FuelType,
            Transmission = vehicle.Transmission,
            SeatingCapacity = vehicle.SeatingCapacity,
            PricePerDay = vehicle.PricePerDay,
            PricePerHour = vehicle.PricePerHour,
            Location = vehicle.Location,
            AvailabilityStatus = vehicle.AvailabilityStatus,
            Images = vehicle.Images,
            Features = vehicle.Features,
            HasPendingConflicts = false,
            Message = null
        };
    }
}
