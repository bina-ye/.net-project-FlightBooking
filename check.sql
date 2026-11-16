SELECT "Id", "BookingReference", "Status", "CancelledAt" FROM "Bookings" ORDER BY "Id" DESC LIMIT 3;
SELECT "Id", "SeatNumber", "Status", "BookingId", "BookedAt" FROM "Seats" WHERE "Id" = 241;
SELECT "Id", "FlightNumber", "DepartureTime" FROM "Flights" WHERE "Id" = 9;