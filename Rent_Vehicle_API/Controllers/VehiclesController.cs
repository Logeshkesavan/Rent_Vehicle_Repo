using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rent_Vehicle.Data;
using Rent_Vehicle.Models;
using Rent_Vehicle.Models.DTOs;

namespace Rent_Vehicle.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VehiclesController : ControllerBase
{
    private readonly VehicleRentalDbContext _context;

    public VehiclesController(VehicleRentalDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<VehicleDto>>> GetVehicles(
        [FromQuery] string? category = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        var query = _context.Vehicles.AsQueryable();

        if (!string.IsNullOrEmpty(category))
        {
            query = query.Where(v => v.Category.ToLower() == category.ToLower());
        }

        var vehicles = await query.Where(v => v.IsActive).ToListAsync();

        if (fromDate.HasValue && toDate.HasValue)
        {
            vehicles = vehicles.Where(v =>
            {
                var conflictingBookings = _context.Bookings
                    .Where(b => b.VehicleId == v.Id &&
                           (b.Status == "Confirmed" || b.Status == "PickedUp") &&
                           b.PickupDate < toDate &&
                           b.ReturnDate > fromDate)
                    .Count();

                return conflictingBookings == 0;
            }).ToList();
        }

        var vehicleDtos = vehicles.Select(v => new VehicleDto
        {
            Id = v.Id,
            Category = v.Category,
            Type = v.Type,
            Brand = v.Brand,
            Model = v.Model,
            Year = v.Year,
            RegistrationNumber = v.RegistrationNumber,
            FuelType = v.FuelType,
            Transmission = v.Transmission,
            SeatingCapacity = v.SeatingCapacity,
            PricePerDay = v.PricePerDay,
            PricePerHour = v.PricePerHour,
            Location = v.Location,
            AvailabilityStatus = v.AvailabilityStatus,
            Images = v.Images,
            Features = v.Features
        }).ToList();

        return Ok(vehicleDtos);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<VehicleDetailDto>> GetVehicle(int id)
    {
        var vehicle = await _context.Vehicles.FindAsync(id);

        if (vehicle == null || !vehicle.IsActive)
        {
            return NotFound();
        }

        // Get upcoming bookings for the next 60 days
        var today = DateTime.UtcNow.Date;
        var futureDate = today.AddDays(60);
        
        var upcomingBookings = await _context.Bookings
            .Where(b => b.VehicleId == id &&
                   (b.Status == "Confirmed" || b.Status == "PickedUp") &&
                   b.PickupDate < futureDate &&
                   b.ReturnDate > today)
            .OrderBy(b => b.PickupDate)
            .ToListAsync();

        // Check if vehicle is currently available (today)
        var isCurrentlyBooked = upcomingBookings.Any(b => 
            today >= b.PickupDate.Date && 
            today < b.ReturnDate.Date);

        var nextAvailableDate = isCurrentlyBooked 
            ? upcomingBookings.FirstOrDefault(b => today >= b.PickupDate.Date)?.ReturnDate 
            : today;

        var vehicleDto = new VehicleDetailDto
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
            IsAvailableToday = !isCurrentlyBooked,
            NextAvailableDate = nextAvailableDate,
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

        return Ok(vehicleDto);
    }

    [HttpGet("{id}/availability")]
    public async Task<ActionResult<VehicleAvailabilityDto>> GetVehicleAvailability(
        int id, 
        [FromQuery] DateTime? fromDate, 
        [FromQuery] DateTime? toDate)
    {
        var vehicle = await _context.Vehicles.FindAsync(id);
        if (vehicle == null || !vehicle.IsActive)
        {
            return NotFound();
        }

        // Default to next 30 days if no date range provided
        var startDate = fromDate ?? DateTime.UtcNow.Date;
        var endDate = toDate ?? startDate.AddDays(30);

        // Get all confirmed/picked up bookings for this vehicle in the date range
        var activeBookings = await _context.Bookings
            .Where(b => b.VehicleId == id &&
                   (b.Status == "Confirmed" || b.Status == "PickedUp") &&
                   b.PickupDate < endDate &&
                   b.ReturnDate > startDate)
            .ToListAsync();

        // Generate date availability
        var availability = new List<DateAvailabilityDto>();
        var currentDate = startDate;
        while (currentDate <= endDate)
        {
            var isBooked = activeBookings.Any(b => 
                currentDate >= b.PickupDate.Date && 
                currentDate < b.ReturnDate.Date);

            availability.Add(new DateAvailabilityDto
            {
                Date = currentDate,
                IsAvailable = !isBooked,
                Status = isBooked ? "Booked" : "Available"
            });

            currentDate = currentDate.AddDays(1);
        }

        var result = new VehicleAvailabilityDto
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

        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<VehicleDto>> CreateVehicle([FromBody] CreateVehicleRequest request)
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

        var vehicleDto = new VehicleDto
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
            Features = vehicle.Features
        };

        return CreatedAtAction(nameof(GetVehicle), new { id = vehicle.Id }, vehicleDto);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateVehicle(int id, [FromBody] CreateVehicleRequest request)
    {
        var vehicle = await _context.Vehicles.FindAsync(id);

        if (vehicle == null)
        {
            return NotFound();
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

        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteVehicle(int id)
    {
        var vehicle = await _context.Vehicles.FindAsync(id);

        if (vehicle == null)
        {
            return NotFound();
        }

        vehicle.IsActive = false;
        vehicle.UpdatedAt = DateTime.UtcNow;

        _context.Vehicles.Update(vehicle);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}
