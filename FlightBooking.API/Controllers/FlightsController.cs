using FlightBooking.Core.Common;
using FlightBooking.Core.DTOs.Flights;
using FlightBooking.Core.DTOs.Seats;
using FlightBooking.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlightBooking.API.Controllers;

[ApiController]
[Route("api/flights")]
public class FlightsController : ControllerBase
{
    private readonly IFlightService _flights;

    public FlightsController(IFlightService flights)
    {
        _flights = flights;
    }

    /// <summary>חיפוש טיסות לפי מוצא, יעד ותאריך יציאה.</summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PagedResult<FlightResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Search(
        [FromQuery] FlightSearchRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var result = await _flights.SearchAsync(request, ct);

        return result.Status switch
        {
            ResultStatus.Ok => Ok(result.Value),
            _ => BadRequest(new { message = result.ErrorMessage })
        };
    }

    /// <summary>פרטי טיסה לפי מזהה.</summary>
    [HttpGet("{id:int}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(FlightResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById([FromRoute] int id, CancellationToken ct)
    {
        var result = await _flights.GetByIdAsync(id, ct);

        return result.Status switch
        {
            ResultStatus.Ok => Ok(result.Value),
            ResultStatus.NotFound => NotFound(new { message = result.ErrorMessage }),
            _ => BadRequest(new { message = result.ErrorMessage })
        };
    }

    /// <summary>המושבים של טיסה, כולל זמינות.</summary>
    [HttpGet("{id:int}/seats")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<SeatResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSeats([FromRoute] int id, CancellationToken ct)
    {
        var result = await _flights.GetSeatsAsync(id, ct);

        return result.Status switch
        {
            ResultStatus.Ok => Ok(result.Value),
            ResultStatus.NotFound => NotFound(new { message = result.ErrorMessage }),
            _ => BadRequest(new { message = result.ErrorMessage })
        };
    }
}