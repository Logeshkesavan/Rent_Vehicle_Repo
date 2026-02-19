using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rent_Vehicle.Data;
using Rent_Vehicle.Models.DTOs;

namespace Rent_Vehicle.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class DashboardController : ControllerBase
{
    private readonly VehicleRentalDbContext _context;

    public DashboardController(VehicleRentalDbContext context)
    {
        _context = context;
    }

    [HttpGet("stats")]
    public async Task<ActionResult<DashboardStatsDto>> GetDashboardStats()
    {
        var totalBookings = await _context.Bookings.CountAsync();
        var activeRentals = await _context.Bookings
            .CountAsync(b => b.Status == "Confirmed" || b.Status == "PickedUp");
        var totalRevenue = await _context.Bookings
            .Where(b => b.Status == "Completed")
            .SumAsync(b => b.TotalAmount);
        var totalVehicles = await _context.Vehicles.CountAsync(v => v.IsActive);

        return Ok(new DashboardStatsDto
        {
            TotalBookings = totalBookings,
            ActiveRentals = activeRentals,
            TotalRevenue = totalRevenue,
            TotalVehicles = totalVehicles
        });
    }

    [HttpGet("realtime")]
    public async Task<ActionResult<RealTimeStatsDto>> GetRealTimeStats()
    {
        var today = DateTime.UtcNow.Date;
        var todayStart = today;
        var todayEnd = today.AddDays(1);

        var todayPickups = await _context.Bookings
            .CountAsync(b => b.Status == "Confirmed" && 
                             b.PickupDate >= todayStart && b.PickupDate < todayEnd);

        var todayReturns = await _context.Bookings
            .CountAsync(b => b.Status == "PickedUp" && 
                             b.ReturnDate >= todayStart && b.ReturnDate < todayEnd);

        var vehiclesRented = await _context.Bookings
            .CountAsync(b => b.Status == "PickedUp");

        var vehiclesAvailable = await _context.Vehicles
            .CountAsync(v => v.IsActive && v.AvailabilityStatus == "Available");

        var pendingBookings = await _context.Bookings
            .CountAsync(b => b.Status == "Pending");

        var expectedRevenueToday = await _context.Bookings
            .Where(b => b.Status == "PickedUp" && 
                        b.ReturnDate >= todayStart && b.ReturnDate < todayEnd)
            .SumAsync(b => b.TotalAmount);

        return Ok(new RealTimeStatsDto
        {
            TodayPickups = todayPickups,
            TodayReturns = todayReturns,
            VehiclesRented = vehiclesRented,
            VehiclesAvailable = vehiclesAvailable,
            PendingBookings = pendingBookings,
            ExpectedRevenueToday = expectedRevenueToday
        });
    }

    [HttpGet("vehicle-utilization")]
    public async Task<ActionResult<List<VehicleUtilizationDto>>> GetVehicleUtilization()
    {
        var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);

        var vehicles = await _context.Vehicles
            .Where(v => v.IsActive)
            .ToListAsync();

        var utilization = new List<VehicleUtilizationDto>();

        foreach (var vehicle in vehicles)
        {
            var bookings = await _context.Bookings
                .Where(b => b.VehicleId == vehicle.Id &&
                       b.PickupDate >= thirtyDaysAgo &&
                       (b.Status == "Completed" || b.Status == "PickedUp" || b.Status == "Confirmed"))
                .ToListAsync();

            var totalBookings = bookings.Count;
            var daysBooked = bookings.Sum(b => (int)(b.ReturnDate.Date - b.PickupDate.Date).TotalDays);
            var utilizationPercentage = daysBooked > 0 ? (decimal)daysBooked / 30 * 100 : 0;
            var revenueGenerated = bookings.Where(b => b.Status == "Completed").Sum(b => b.TotalAmount);

            utilization.Add(new VehicleUtilizationDto
            {
                VehicleId = vehicle.Id,
                VehicleName = $"{vehicle.Brand} {vehicle.Model}",
                Category = vehicle.Category,
                TotalBookings = totalBookings,
                DaysBooked = daysBooked,
                UtilizationPercentage = Math.Round(utilizationPercentage, 2),
                RevenueGenerated = revenueGenerated
            });
        }

        return Ok(utilization.OrderByDescending(u => u.UtilizationPercentage).ToList());
    }

    [HttpGet("revenue-by-category")]
    public async Task<ActionResult<List<RevenueByCategoryDto>>> GetRevenueByCategory()
    {
        var revenueByCategory = await _context.Bookings
            .Where(b => b.Status == "Completed")
            .Include(b => b.Vehicle)
            .GroupBy(b => b.Vehicle.Category)
            .Select(g => new RevenueByCategoryDto { Category = g.Key, Revenue = g.Sum(b => b.TotalAmount) })
            .ToListAsync();

        return Ok(revenueByCategory);
    }

    [HttpGet("bookings-by-status")]
    public async Task<ActionResult<Dictionary<string, int>>> GetBookingsByStatus()
    {
        var bookingsByStatus = await _context.Bookings
            .GroupBy(b => b.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        var result = bookingsByStatus.ToDictionary(b => b.Status, b => b.Count);

        return Ok(result);
    }
}
