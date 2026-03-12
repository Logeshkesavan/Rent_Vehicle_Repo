namespace Rent_Vehicle.Models.DTOs;

public class VehicleDto
{
    public int Id { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int Year { get; set; }
    public string RegistrationNumber { get; set; } = string.Empty;
    public string FuelType { get; set; } = string.Empty;
    public string? Transmission { get; set; }
    public int SeatingCapacity { get; set; }
    public decimal PricePerDay { get; set; }
    public decimal? PricePerHour { get; set; }
    public string Location { get; set; } = string.Empty;
    public string AvailabilityStatus { get; set; } = string.Empty;
    public string? Images { get; set; }
    public string? Features { get; set; }

    // Availability warnings (e.g., pending bookings for the searched date range)
    public bool HasPendingConflicts { get; set; }
    public string? Message { get; set; }
}

// Enhanced Vehicle Detail DTO with availability info
public class VehicleDetailDto
{
    public int Id { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int Year { get; set; }
    public string RegistrationNumber { get; set; } = string.Empty;
    public string FuelType { get; set; } = string.Empty;
    public string? Transmission { get; set; }
    public int SeatingCapacity { get; set; }
    public decimal PricePerDay { get; set; }
    public decimal? PricePerHour { get; set; }
    public string Location { get; set; } = string.Empty;
    public bool IsAvailableToday { get; set; }
    public DateTime? NextAvailableDate { get; set; }
    public string? Images { get; set; }
    public string? Features { get; set; }
    public List<BookingSummaryDto> UpcomingBookings { get; set; } = new();
}

public class BookingSummaryDto
{
    public int Id { get; set; }
    public DateTime PickupDate { get; set; }
    public DateTime ReturnDate { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class CreateVehicleRequest
{
    public string Category { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int Year { get; set; }
    public string RegistrationNumber { get; set; } = string.Empty;
    public string FuelType { get; set; } = string.Empty;
    public string? Transmission { get; set; }
    public int SeatingCapacity { get; set; }
    public decimal PricePerDay { get; set; }
    public decimal? PricePerHour { get; set; }
    public string Location { get; set; } = string.Empty;
    public string? Images { get; set; }
    public string? Features { get; set; }
}

public class BookingDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int VehicleId { get; set; }
    public string VehicleName { get; set; } = string.Empty;
    public DateTime PickupDate { get; set; }
    public DateTime ReturnDate { get; set; }
    public int TotalDays { get; set; }
    public decimal RentalAmount { get; set; }
    public decimal SecurityDeposit { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    
    // Additional fields
    public bool HasPendingConflicts { get; set; }
    public string? Message { get; set; }
}

public class CreateBookingRequest
{
    public int VehicleId { get; set; }
    public DateTime PickupDate { get; set; }
    public DateTime ReturnDate { get; set; }
}

public class UpdateBookingStatusRequest
{
    public string Status { get; set; } = string.Empty;
}

public class VehicleUtilizationDto
{
    public int VehicleId { get; set; }
    public string VehicleName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int TotalBookings { get; set; }
    public int DaysBooked { get; set; }
    public decimal UtilizationPercentage { get; set; }
    public decimal RevenueGenerated { get; set; }
}

public class DashboardStatsDto
{
    public int TotalBookings { get; set; }
    public int ActiveRentals { get; set; }
    public decimal TotalRevenue { get; set; }
    public int TotalVehicles { get; set; }
}

// Vehicle Availability DTOs
public class DateAvailabilityDto
{
    public DateTime Date { get; set; }
    public bool IsAvailable { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class VehicleAvailabilityDto
{
    public int VehicleId { get; set; }
    public string VehicleName { get; set; } = string.Empty;
    public string CurrentStatus { get; set; } = string.Empty;
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public int TotalDays { get; set; }
    public int AvailableDays { get; set; }
    public int BookedDays { get; set; }
    public List<DateAvailabilityDto> DateAvailability { get; set; } = new();
}

// Dashboard Real-Time Stats DTOs
public class RealTimeStatsDto
{
    public int TodayPickups { get; set; }
    public int TodayReturns { get; set; }
    public int VehiclesRented { get; set; }
    public int VehiclesAvailable { get; set; }
    public int PendingBookings { get; set; }
    public decimal ExpectedRevenueToday { get; set; }
}

public class RevenueByCategoryDto
{
    public string Category { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
}

// Conflict Detection DTOs
public class BookingConflictDto
{
    public int BookingId { get; set; }
    public int VehicleId { get; set; }
    public string VehicleName { get; set; } = string.Empty;
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public DateTime PickupDate { get; set; }
    public DateTime ReturnDate { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ConfirmBookingResultDto
{
    public bool Success { get; set; }
    public int BookingId { get; set; }
    public int VehicleId { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<int> CancelledBookings { get; set; } = new();
}
