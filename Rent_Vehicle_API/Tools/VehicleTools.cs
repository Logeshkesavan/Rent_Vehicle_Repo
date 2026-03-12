using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;
using Rent_Vehicle.Models.DTOs;
using Rent_Vehicle.Services;
using System.ComponentModel;
using System.Security.Claims;


namespace Rent_Vehicle.Tools
{
    [McpServerToolType]
    // [Authorize]
    public class VehicleTools
    {
        private readonly IVehicleService _vehicleService;
        private readonly IBookingService _bookingService;
        private readonly IAuthService _authService;
        private readonly IHttpContextAccessor _httpContextAccessor;



        public VehicleTools(
            IVehicleService vehicleService,
            IBookingService bookingService,
            IAuthService authService,
            IHttpContextAccessor httpContextAccessor
           )
        {
            _vehicleService = vehicleService;
            _bookingService = bookingService;
            _authService = authService;
            _httpContextAccessor = httpContextAccessor;
        }

        private async Task<int?> GetUserIdFromTokenAsync()
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null)
            {
                return null;
            }

            var emailClaim = httpContext.User.FindFirst(ClaimTypes.Email)?.Value;
            if (string.IsNullOrEmpty(emailClaim))
            {
                return null;
            }

            return await _authService.GetUserIdByEmailAsync(emailClaim);
        }

        private async Task<string?> GetUserRoleFromTokenAsync()
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null)
            {
                return null;
            }

            var roleClaim = httpContext.User.FindFirst(ClaimTypes.Role)?.Value;
            return roleClaim;
        }


        [McpServerTool(Name = "search_available_vehicles")]
        [Description("""
                        Search for available vehicles by category and date.
                        
                        Use this tool when:
                        - the user asks to book a vehicle
                        - the vehicle ID is unknown
                        - the user wants to see available vehicles
                        
                        Supported categories:
                        - car
                        - bike
                        - scooter

                        After calling this tool:
                        1. Show the available vehicles
                        2. Ask the user to choose a vehicleId
                        3. Then call create_booking
                        
                        Typical user requests:
                        - "show available cars"
                        - "book a bike tomorrow"
                        - "find scooters between 5 May and 7 May"
                        - list available vehicles
                        - list vehicles for tomorrow
                        - list cars from 5 May 2026
                        - list all the available cars
                        """)]
        public async Task<object> SearchAvailableVehicles(
         [Description("Vehicle category like car, scooter, bike (optional)")] string? category,
         [Description("Pickup date (optional)")] DateTime? fromDate,
         [Description("Return date (optional)")] DateTime? toDate)
        {
            var vehicles = await _vehicleService.GetVehiclesAsync(category, fromDate, toDate);

            return new
            {
                success = true,
                vehicles = vehicles.Select(v => new
                {
                    vehicleId = v.Id,
                    vehicleName = $"{v.Brand} {v.Model}",
                    brand = v.Brand,
                    model = v.Model,
                    category = v.Category,
                    pricePerDay = v.PricePerDay,
                    location = v.Location,
                    hasPendingConflicts = v.HasPendingConflicts,
                    message = v.Message
                })
            };
        }

        [McpServerTool(Name = "get_vehicle_details")]
        [Description("Retrieve detailed information about a vehicle by its unique ID. Returns vehicle specifications, pricing, availability for today, next available date, and upcoming bookings within the next 60 days.")]
        public async Task<object> GetVehicle(int vehicleId)
        {
            if (vehicleId == 0)
            {
                return new
                {
                    success = false,
                    message = "Vehicle ID is required to create booking."
                };
            }
            var vehicle = await _vehicleService.GetVehicleAsync(vehicleId);
            if (vehicle == null)
            {
                return new
                {
                    success = false,
                    message = "Vehicle not found."
                };
            }
            return vehicle;
        }

        [McpServerTool(Name = "create_booking")]
        [Description("""
                Create a vehicle booking for the authenticated user.
                
                Required parameters:
                - vehicleId
                Optional parameters:
                - pickupDate
                - returnDate
                
                Default behavior:
                - If pickupDate is not provided, use today's date.
                - If returnDate is not provided, set it to 1 day after pickupDate.
                
                Important workflow rules for the AI:
                
                1. Always ensure the correct vehicleId before calling this tool.
                
                2. If the user provides a vehicle name or model (e.g., "Nexon", "Swift", "BMW"),
                   first call `search_available_vehicles` to identify the correct vehicleId.
                
                3. If the user asks to "book a car" without specifying a vehicle,
                   first call `search_available_vehicles` and present the available vehicles
                   to the user. Ask the user to choose which vehicle they want to book.
                
                4. If multiple vehicles match the user's request, ask the user to choose
                   the correct vehicle before calling this tool.
                
                5. Never automatically select a vehicle if the user did not specify one.
                
                6. Only call this tool once the vehicleId is clearly determined.
                
                Example workflows:
                
                User: "book a car tomorrow"
                Assistant:
                1. Call `search_available_vehicles`
                2. Show available vehicles
                3. Ask the user which vehicle to book
                
                User: "book a nexon car tomorrow"
                Assistant:
                1. Call `search_available_vehicles`
                2. Find the Tata Nexon vehicleId
                3. Call `create_booking` with that vehicleId
                
                User: "book vehicle id 6 tomorrow"
                Assistant:
                Directly call `create_booking`.
                
                The userId is automatically extracted from the authentication token.
                """)]
        public async Task<object> CreateBooking(
        [Description("Vehicle ID to be booked")] int vehicleId,
        [Description("Pickup date of the vehicle (optional)")] DateTime? pickupDate,
        [Description("Return date of the vehicle (optional)")] DateTime? returnDate)
        {
            try
            {
                // 1️⃣ Validate vehicle id
                if (vehicleId <= 0)
                {
                    return new
                    {
                        success = false,
                        message = "Invalid vehicleId. Please provide a valid vehicle ID."
                    };
                }

                // 2️⃣ Get user from token
                var userId = await GetUserIdFromTokenAsync();

                if (userId == null)
                {
                    return new
                    {
                        success = false,
                        message = "User authentication required. Please login before booking. (Dev/MCP: set McpTools:AllowUnauthenticatedBookings=true and optionally McpTools:DefaultUserEmail in appsettings.Development.json)"
                    };
                }

                // 3️⃣ Apply default dates
                var startDate = pickupDate?.Date ?? DateTime.UtcNow.Date;
                var endDate = returnDate?.Date ?? startDate.AddDays(1);

                if (startDate < DateTime.UtcNow.Date)
                {
                    return new
                    {
                        success = false,
                        message = "Pickup date cannot be in the past."
                    };
                }
                // 4️⃣ Validate date range
                if (endDate <= startDate)
                {
                    return new
                    {
                        success = false,
                        message = "Return date must be after the pickup date."
                    };
                }

                // 5️⃣ Create booking request
                var request = new CreateBookingRequest
                {
                    VehicleId = vehicleId,
                    PickupDate = startDate,
                    ReturnDate = endDate
                };

                // 6️⃣ Call service
                var (booking, error) = await _bookingService.CreateBookingAsync(userId.Value, request);

                if (error != null)
                {
                    return new
                    {
                        success = false,
                        message = error
                    };
                }

                if (booking == null)
                {
                    return new
                    {
                        success = false,
                        message = "Booking could not be created."
                    };
                }

                // 7️⃣ Success response
                return new
                {
                    success = true,
                    message = "Booking created successfully.",
                    data = new
                    {
                        booking.Id,
                        booking.VehicleId,
                        booking.PickupDate,
                        booking.ReturnDate
                    }
                };
            }
            catch (Exception ex)
            {
                return new
                {
                    success = false,
                    message = "An unexpected error occurred while creating the booking.",
                    details = ex.Message
                };
            }
        }
        [McpServerTool(Name = "cancel_booking")]
        [Description("""
                        Cancel an existing vehicle booking.
                        
                        Required:
                        - bookingId
                        
                        Use this tool when the bookingId is known.
                        
                        If the bookingId is NOT known:
                        1. First call `get_my_bookings`
                        2. Identify the correct booking using vehicle name, brand, or booking details
                        3. Then call `cancel_booking` with the identified bookingId
                        
                        This tool may also be used as part of a multi-step workflow.
                        
                        Example workflows:
                        - "Cancel booking 10"
                        - "Cancel my booking id 15"
                        - "Cancel my Maruti booking and book a new car"
                        - "Cancel my old booking before booking a new vehicle"
                        
                        Only the booking owner or an admin is allowed to cancel a booking.
                        """)]
        public async Task<object> CancelBooking(
                        [Description("Booking ID to cancel")] int bookingId)
        {
            var userId = await GetUserIdFromTokenAsync();

            if (userId == null)
            {
                return new
                {
                    success = false,
                    message = "User authentication required. Please login before booking. (Dev/MCP: set McpTools:AllowUnauthenticatedBookings=true and optionally McpTools:DefaultUserEmail in appsettings.Development.json)"
                };
            }
            var (success, error) = await _bookingService.CancelBookingAsync(bookingId, userId.Value);

            if (!success)
            {
                return new
                {
                    success = false,
                    message = error
                };
            }

            return new
            {
                success = true,
                message = "Booking cancelled successfully"
            };
        }

        [McpServerTool(Name = "get_my_bookings")]
        [Description("""
            
                Retrieve all bookings for the authenticated user.

                Use this tool when:
                - The user asks to view their bookings
                - The user asks to cancel a booking but does not provide a bookingId
                - The user refers to a booking using vehicle name (example: "cancel my Maruti booking")

                Workflow guidance:

                1. If the user provides bookingId → call cancel_booking directly.

                2. If bookingId is unknown:
                   - Call get_my_bookings
                   - Identify the correct booking using vehicleName, brand, or dates.

                3. If multiple bookings match, ask the user to choose.

                Examples:
                - "Show my bookings"
                - "Cancel my booking"
                - "Cancel my Maruti car booking"
                - "Is any Tata vehicle booked?"
            
                """)]
        public async Task<object> GetMyBookings()
        {
            var userId = await GetUserIdFromTokenAsync();
            var role = await GetUserRoleFromTokenAsync();

            if (userId == null)
            {
                return new
                {
                    success = false,
                    message = "User authentication required."
                };
            }

            var bookings = await _bookingService.GetBookingsAsync(userId.Value, role ?? string.Empty);

            return new
            {
                success = true,
                message = "User bookings retrieved successfully.",
                bookings = bookings.Select(b => new
                {
                    bookingId = b.Id,
                    vehicleId = b.VehicleId,
                    vehicleName = b.VehicleName,
                    pickupDate = b.PickupDate,
                    returnDate = b.ReturnDate,
                    status = b.Status,
                    bookingLabel = $"{b.VehicleName} booking on {b.PickupDate:dd MMM yyyy}"
                })
            };
        }
    }
}
