using System;
using System.Collections.Generic;

namespace Rent_Vehicle.Models;

public partial class Vehicle
{
    public int Id { get; set; }

    public string Category { get; set; } = null!;

    public string Type { get; set; } = null!;

    public string Brand { get; set; } = null!;

    public string Model { get; set; } = null!;

    public int Year { get; set; }

    public string RegistrationNumber { get; set; } = null!;

    public string FuelType { get; set; } = null!;

    public string? Transmission { get; set; }

    public int SeatingCapacity { get; set; }

    public decimal PricePerDay { get; set; }

    public decimal? PricePerHour { get; set; }

    public string Location { get; set; } = null!;

    public string AvailabilityStatus { get; set; } = null!;

    public string? Images { get; set; }

    public string? Features { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}
