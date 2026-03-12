using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Rent_Vehicle.Models.DTOs;
using Rent_Vehicle.Services;

namespace Rent_Vehicle.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VehiclesController : ControllerBase
{
    private readonly IVehicleService _vehicleService;

    public VehiclesController(IVehicleService vehicleService)
    {
        _vehicleService = vehicleService;
    }

    [HttpGet]
    public async Task<ActionResult<List<VehicleDto>>> GetVehicles(
        [FromQuery] string? category = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        var vehicles = await _vehicleService.GetVehiclesAsync(category, fromDate, toDate);
        return Ok(vehicles);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<VehicleDetailDto>> GetVehicle(int id)
    {
        var vehicle = await _vehicleService.GetVehicleAsync(id);
        if (vehicle == null)
        {
            return NotFound();
        }

        return Ok(vehicle);
    }

    [HttpGet("{id}/availability")]
    public async Task<ActionResult<VehicleAvailabilityDto>> GetVehicleAvailability(
        int id, 
        [FromQuery] DateTime? fromDate, 
        [FromQuery] DateTime? toDate)
    {
        var availability = await _vehicleService.GetVehicleAvailabilityAsync(id, fromDate, toDate);
        if (availability == null)
        {
            return NotFound();
        }

        return Ok(availability);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<VehicleDto>> CreateVehicle([FromBody] CreateVehicleRequest request)
    {
        var vehicleDto = await _vehicleService.CreateVehicleAsync(request);
        return CreatedAtAction(nameof(GetVehicle), new { id = vehicleDto.Id }, vehicleDto);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateVehicle(int id, [FromBody] CreateVehicleRequest request)
    {
        var updated = await _vehicleService.UpdateVehicleAsync(id, request);
        if (!updated)
        {
            return NotFound();
        }

        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteVehicle(int id)
    {
        var deleted = await _vehicleService.DeleteVehicleAsync(id);
        if (!deleted)
        {
            return NotFound();
        }

        return NoContent();
    }
}
