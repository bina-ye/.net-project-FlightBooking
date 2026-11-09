using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FlightBooking.Core.Entities
{
    public class Airport
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;      // קוד IATA, 3 תווים
        public string City { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }
}
