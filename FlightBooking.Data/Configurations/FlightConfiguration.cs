using FlightBooking.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FlightBooking.Data.Configurations;

public class FlightConfiguration : IEntityTypeConfiguration<Flight>
{
    public void Configure(EntityTypeBuilder<Flight> builder)
    {
        builder.ToTable("Flights");

        builder.HasKey(f => f.Id);

        builder.Property(f => f.FlightNumber)
            .IsRequired()
            .HasMaxLength(10);

        builder.Property(f => f.Gate)
            .IsRequired()
            .HasMaxLength(10);

        builder.Property(f => f.AircraftType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(f => f.Price)
            .HasPrecision(10, 2);

        builder.HasIndex(f => f.FlightNumber)
            .IsUnique();

        // אינדקס לחיפוש לפי מוצא, יעד ותאריך
        builder.HasIndex(f => new { f.OriginAirportId, f.DestinationAirportId, f.DepartureTime });

        // שני קשרים נפרדים לאותה ישות — חייבים לציין את הניווט ההפוך
        builder.HasOne(f => f.OriginAirport)
            .WithMany()
            .HasForeignKey(f => f.OriginAirportId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(f => f.DestinationAirport)
            .WithMany()
            .HasForeignKey(f => f.DestinationAirportId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}