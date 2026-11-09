using FlightBooking.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FlightBooking.Data.Configurations;

public class SeatConfiguration : IEntityTypeConfiguration<Seat>
{
    public void Configure(EntityTypeBuilder<Seat> builder)
    {
        builder.ToTable("Seats");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.SeatNumber)
            .IsRequired()
            .HasMaxLength(5);

        builder.Property(s => s.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        // PostgreSQL: xmin משמש כ-concurrency token במקום rowversion
        builder.Property(s => s.RowVersion)
            .IsRowVersion();

        // אותו מושב לא יכול להופיע פעמיים באותה טיסה
        builder.HasIndex(s => new { s.FlightId, s.SeatNumber })
            .IsUnique();

        builder.HasOne(s => s.Flight)
            .WithMany(f => f.Seats)
            .HasForeignKey(s => s.FlightId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.Booking)
            .WithMany(b => b.Seats)
            .HasForeignKey(s => s.BookingId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}