using AutoMapper;
using FlightBooking.Core.Common;
using FlightBooking.Core.DTOs.Flights;
using FlightBooking.Core.Entities;
using FlightBooking.Core.Enums;
using FlightBooking.Core.Interfaces;
using FlightBooking.Service.Mapping;
using FlightBooking.Service.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FlightBooking.Tests;

public class FlightServiceTests
{
    private readonly Mock<IFlightRepository> _flights = new();

    // AutoMapper אמיתי עם ה-Profile — הבדיקות מכסות גם את המיפוי עצמו
    private static readonly IMapper Mapper =
      new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>(), NullLoggerFactory.Instance)
          .CreateMapper();
    private FlightService CreateSut() =>
        new(_flights.Object, NullLogger<FlightService>.Instance, Mapper);

    // ---------- עזרי בנייה ----------

    private static Flight BuildFlight(
        int id = 1,
        string number = "LY999",
        DateTime? departure = null,
        int availableSeats = 10)
    {
        var departureTime = departure ?? DateTime.UtcNow.AddDays(30);

        return new Flight
        {
            Id = id,
            FlightNumber = number,
            Price = 1500m,
            DepartureTime = departureTime,
            ArrivalTime = departureTime.AddHours(5),
            CheckInOpensAt = departureTime.AddHours(-3),
            BoardingTime = departureTime.AddMinutes(-40),
            Gate = "A1",
            AircraftType = "Boeing 787",
            OriginAirportId = 1,
            DestinationAirportId = 2,
            OriginAirport = new Airport
            {
                Id = 1,
                Code = "TLV",
                City = "תל אביב",
                Name = "נתב\"ג"
            },
            DestinationAirport = new Airport
            {
                Id = 2,
                Code = "JFK",
                City = "ניו יורק",
                Name = "קנדי"
            },
            Seats = Enumerable.Range(1, availableSeats)
                .Select(i => new Seat
                {
                    Id = id * 100 + i,
                    FlightId = id,
                    SeatNumber = $"{i}A",
                    Status = SeatStatus.Available
                })
                .ToList()
        };
    }

    // ---------- GetCatalogAsync ----------

    [Fact]
    public async Task GetCatalogAsync_HidesPastFlights_WhenIncludePastIsFalse()
    {
        // Arrange
        var future = BuildFlight(1, "LY001", DateTime.UtcNow.AddDays(30));

        _flights.Setup(r => r.GetCatalogAsync(
                    It.Is<FlightCatalogQuery>(q =>
                        q.PageNumber == 1 && q.PageSize == 10 && !q.IncludePast),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(((IReadOnlyList<Flight>)new[] { future }, 1));

        var sut = CreateSut();

        // Act
        var result = await sut.GetCatalogAsync(1, 10, includePast: false);

        // Assert
        Assert.Equal(ResultStatus.Ok, result.Status);
        Assert.Single(result.Value!.Items);
        Assert.Equal(1, result.Value.TotalCount);

        _flights.Verify(r => r.GetCatalogAsync(
            It.Is<FlightCatalogQuery>(q => !q.IncludePast),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetCatalogAsync_IncludesPastFlights_WhenIncludePastIsTrue()
    {
        // Arrange
        var past = BuildFlight(1, "LY001", DateTime.UtcNow.AddDays(-5));
        var future = BuildFlight(2, "LY002", DateTime.UtcNow.AddDays(30));

        _flights.Setup(r => r.GetCatalogAsync(
                    It.IsAny<FlightCatalogQuery>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(((IReadOnlyList<Flight>)new[] { past, future }, 2));

        var sut = CreateSut();

        // Act
        var result = await sut.GetCatalogAsync(1, 10, includePast: true);

        // Assert
        Assert.Equal(ResultStatus.Ok, result.Status);
        Assert.Equal(2, result.Value!.Items.Count);

        _flights.Verify(r => r.GetCatalogAsync(
            It.Is<FlightCatalogQuery>(q => q.IncludePast),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-3, 1)]
    [InlineData(2, 2)]
    public async Task GetCatalogAsync_NormalizesPageNumber(int requested, int expected)
    {
        _flights.Setup(r => r.GetCatalogAsync(It.IsAny<FlightCatalogQuery>(),
                        It.IsAny<CancellationToken>()))
                .ReturnsAsync(((IReadOnlyList<Flight>)Array.Empty<Flight>(), 0));

        var sut = CreateSut();

        var result = await sut.GetCatalogAsync(requested, 10);

        Assert.Equal(ResultStatus.Ok, result.Status);
        Assert.Equal(expected, result.Value!.PageNumber);

        _flights.Verify(r => r.GetCatalogAsync(
            It.Is<FlightCatalogQuery>(q => q.PageNumber == expected && q.PageSize == 10),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1000, 50)]
    [InlineData(10, 10)]
    public async Task GetCatalogAsync_ClampsPageSize(int requested, int expected)
    {
        _flights.Setup(r => r.GetCatalogAsync(It.IsAny<FlightCatalogQuery>(),
                        It.IsAny<CancellationToken>()))
                .ReturnsAsync(((IReadOnlyList<Flight>)Array.Empty<Flight>(), 0));

        var sut = CreateSut();

        var result = await sut.GetCatalogAsync(1, requested);

        Assert.Equal(expected, result.Value!.PageSize);

        _flights.Verify(r => r.GetCatalogAsync(
            It.Is<FlightCatalogQuery>(q => q.PageNumber == 1 && q.PageSize == expected),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetCatalogAsync_ComputesPagingFlags()
    {
        // Arrange
        var flight = BuildFlight(1, "LY001", DateTime.UtcNow.AddDays(30));

        _flights.Setup(r => r.GetCatalogAsync(
                    It.Is<FlightCatalogQuery>(q => q.PageNumber == 2),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(((IReadOnlyList<Flight>)new[] { flight }, 25));

        var sut = CreateSut();

        // Act
        var result = await sut.GetCatalogAsync(2, 10);

        // Assert
        Assert.Equal(2, result.Value!.PageNumber);
        Assert.Equal(25, result.Value.TotalCount);
        Assert.Equal(3, result.Value.TotalPages);
        Assert.True(result.Value.HasPrevious);
        Assert.True(result.Value.HasNext);
    }

    [Fact]
    public async Task GetCatalogAsync_NoFlights_ReturnsEmptyPage()
    {
        _flights.Setup(r => r.GetCatalogAsync(It.IsAny<FlightCatalogQuery>(),
                        It.IsAny<CancellationToken>()))
                .ReturnsAsync(((IReadOnlyList<Flight>)Array.Empty<Flight>(), 0));

        var sut = CreateSut();

        var result = await sut.GetCatalogAsync(1, 10);

        Assert.Equal(ResultStatus.Ok, result.Status);
        Assert.Empty(result.Value!.Items);
        Assert.Equal(0, result.Value.TotalPages);
        Assert.False(result.Value.HasNext);
        Assert.False(result.Value.HasPrevious);
    }

    [Fact]
    public async Task GetCatalogAsync_MapsFlightFieldsCorrectly()
    {
        // Arrange
        var departure = DateTime.UtcNow.AddDays(30);
        var flight = BuildFlight(9, "LY999", departure, availableSeats: 18);

        _flights.Setup(r => r.GetCatalogAsync(It.IsAny<FlightCatalogQuery>(),
                        It.IsAny<CancellationToken>()))
                .ReturnsAsync(((IReadOnlyList<Flight>)new[] { flight }, 1));

        var sut = CreateSut();

        // Act
        var result = await sut.GetCatalogAsync(1, 10);
        var dto = result.Value!.Items[0];

        // Assert
        Assert.Equal(9, dto.Id);
        Assert.Equal("LY999", dto.FlightNumber);
        Assert.Equal("TLV", dto.OriginCode);
        Assert.Equal("תל אביב", dto.OriginCity);
        Assert.Equal("JFK", dto.DestinationCode);
        Assert.Equal("ניו יורק", dto.DestinationCity);
        Assert.Equal(departure, dto.DepartureTime);
        Assert.Equal(1500m, dto.Price);
        Assert.Equal(18, dto.AvailableSeatsCount);
    }

    [Fact]
    public async Task GetCatalogAsync_PassesCancellationToken_ToRepository()
    {
        using var cts = new CancellationTokenSource();

        _flights.Setup(r => r.GetCatalogAsync(It.IsAny<FlightCatalogQuery>(), cts.Token))
                .ReturnsAsync(((IReadOnlyList<Flight>)Array.Empty<Flight>(), 0));

        var sut = CreateSut();

        await sut.GetCatalogAsync(1, 10, includePast: false, cts.Token);

        _flights.Verify(r => r.GetCatalogAsync(
            It.IsAny<FlightCatalogQuery>(), cts.Token), Times.Once);
    }
}