using FlightBooking.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FlightBooking.Data.Configurations;

public class BookingFlightConfiguration : IEntityTypeConfiguration<BookingFlight>
{
    public void Configure(EntityTypeBuilder<BookingFlight> builder)
    {
        builder.ToTable("BookingFlights");

        // מפתח מורכב — טבלת קישור של many-to-many
        builder.HasKey(bf => new { bf.BookingId, bf.FlightId });

        builder.Property(bf => bf.SegmentOrder)
            .IsRequired();

        builder.HasOne(bf => bf.Booking)
            .WithMany(b => b.BookingFlights)
            .HasForeignKey(bf => bf.BookingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(bf => bf.Flight)
            .WithMany(f => f.BookingFlights)
            .HasForeignKey(bf => bf.FlightId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}