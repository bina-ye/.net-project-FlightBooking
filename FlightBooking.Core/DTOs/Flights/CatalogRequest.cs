using System.ComponentModel.DataAnnotations;

namespace FlightBooking.Core.DTOs.Flights;

public class FlightCatalogRequest
{
    [Range(1, 1000)]
    public int PageNumber { get; set; } = 1;

    [Range(1, 50)]
    public int PageSize { get; set; } = 10;

    // אופציונליים — ריק = ללא סינון
    [StringLength(3, MinimumLength = 3)]
    public string? OriginCode { get; set; }

    [StringLength(3, MinimumLength = 3)]
    public string? DestinationCode { get; set; }

    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }

    public bool IncludePast { get; set; } = false;
}