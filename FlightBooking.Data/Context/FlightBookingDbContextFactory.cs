using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace FlightBooking.Data.Context;

public class FlightBookingDbContextFactory : IDesignTimeDbContextFactory<FlightBookingDbContext>
{
    public FlightBookingDbContext CreateDbContext(string[] args)
    {


        var configuration = new ConfigurationBuilder().AddUserSecrets("76bd56a8-b0db-4968-9cc9-2f0565e880aa").Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        var optionsBuilder = new DbContextOptionsBuilder<FlightBookingDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new FlightBookingDbContext(optionsBuilder.Options);
    }
}