using System.Security.Claims;
using FlightBooking.Core.Common;
using FlightBooking.Core.DTOs.Bookings;
using FlightBooking.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlightBooking.API.Controllers;

[ApiController]
[Route("api/bookings")]
[Authorize]
public class BookingsController : ControllerBase
{
    private readonly IBookingService _bookings;

    public BookingsController(IBookingService bookings)
    {
        _bookings = bookings;
    }

    /// <summary>יצירת הזמנה וקבלת אישור עם פרטי הטיסה.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(BookingResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] BookingCreateRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var userId = GetUserId();
        if (userId is null)
            return Unauthorized(new { message = "טוקן לא תקין." });

        var result = await _bookings.CreateAsync(userId.Value, request, ct);

        return result.Status switch
        {
            ResultStatus.Created => Created(
                $"/api/bookings/{result.Value!.Id}", result.Value),

            ResultStatus.Conflict => Conflict(new { message = result.ErrorMessage }),
            ResultStatus.NotFound => NotFound(new { message = result.ErrorMessage }),
            ResultStatus.BadRequest => BadRequest(new { message = result.ErrorMessage }),
            _ => BadRequest(new { message = result.ErrorMessage })
        };
    }

    /// <summary>פרטי הזמנה. לקוח רואה רק את ההזמנות שלו.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(BookingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById([FromRoute] int id, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized(new { message = "טוקן לא תקין." });

        var isAdmin = User.IsInRole("Admin");

        var result = await _bookings.GetByIdAsync(id, userId.Value, isAdmin, ct);

        return result.Status switch
        {
            ResultStatus.Ok => Ok(result.Value),
            ResultStatus.Forbidden => StatusCode(
                StatusCodes.Status403Forbidden, new { message = result.ErrorMessage }),
            ResultStatus.NotFound => NotFound(new { message = result.ErrorMessage }),
            _ => BadRequest(new { message = result.ErrorMessage })
        };
    }

    /// <summary>ההזמנות שלי, עם pagination.</summary>
    [HttpGet("my")]
    [ProducesResponseType(typeof(PagedResult<BookingSummaryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyBookings(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken ct = default)
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized(new { message = "טוקן לא תקין." });

        var result = await _bookings.GetMyBookingsAsync(userId.Value, pageNumber, pageSize, ct);

        return result.Status switch
        {
            ResultStatus.Ok => Ok(result.Value),
            _ => BadRequest(new { message = result.ErrorMessage })
        };
    }

    private int? GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(value, out var id) ? id : null;
    }
}