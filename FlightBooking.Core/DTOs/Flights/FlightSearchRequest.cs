using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.ComponentModel.DataAnnotations;

namespace FlightBooking.Core.DTOs.Flights;

public class FlightSearchRequest
{
    [Required(ErrorMessage = "נמל מוצא חובה")]
    [StringLength(3, MinimumLength = 3, ErrorMessage = "קוד נמל מוצא חייב להיות 3 תווים")]
    public string OriginCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "נמל יעד חובה")]
    [StringLength(3, MinimumLength = 3, ErrorMessage = "קוד נמל יעד חייב להיות 3 תווים")]
    public string DestinationCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "תאריך יציאה חובה")]
    public DateOnly DepartureDate { get; set; }

    public DateOnly? ReturnDate { get; set; }

    [Range(1, 100, ErrorMessage = "מספר עמוד חייב להיות בין 1 ל-100")]
    public int PageNumber { get; set; } = 1;

    [Range(1, 50, ErrorMessage = "גודל עמוד חייב להיות בין 1 ל-50")]
    public int PageSize { get; set; } = 10;
}