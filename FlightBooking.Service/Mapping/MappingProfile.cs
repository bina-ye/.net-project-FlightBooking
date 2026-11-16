using AutoMapper;
using FlightBooking.Core.DTOs.Flights;
using FlightBooking.Core.DTOs.Seats;
using FlightBooking.Core.Entities;
using FlightBooking.Core.Enums;

namespace FlightBooking.Service.Mapping;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        // Flight → FlightResponse
        CreateMap<Flight, FlightResponse>()
            .ForMember(d => d.OriginCode,
                o => o.MapFrom(s => s.OriginAirport.Code))
            .ForMember(d => d.OriginCity,
                o => o.MapFrom(s => s.OriginAirport.City))
            .ForMember(d => d.DestinationCode,
                o => o.MapFrom(s => s.DestinationAirport.Code))
            .ForMember(d => d.DestinationCity,
                o => o.MapFrom(s => s.DestinationAirport.City))
            .ForMember(d => d.AvailableSeatsCount,
                o => o.MapFrom(s => s.Seats.Count(x => x.Status == SeatStatus.Available)));

        // Seat → SeatResponse (IsAvailable מחושב ב-Service, כי תלוי בזמן)
        CreateMap<Seat, SeatResponse>()
            .ForMember(d => d.Status,
                o => o.MapFrom(s => s.Status.ToString()))
            .ForMember(d => d.IsAvailable, o => o.Ignore());
    }
}