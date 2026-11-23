FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project files first to leverage the restore cache
COPY FlightBooking.Core/FlightBooking.Core.csproj FlightBooking.Core/
COPY FlightBooking.Data/FlightBooking.Data.csproj FlightBooking.Data/
COPY FlightBooking.Service/FlightBooking.Service.csproj FlightBooking.Service/
COPY FlightBooking.API/FlightBooking.API.csproj FlightBooking.API/

RUN dotnet restore FlightBooking.API/FlightBooking.API.csproj

COPY . .
RUN dotnet publish FlightBooking.API/FlightBooking.API.csproj \
    -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_HTTP_PORTS=10000
EXPOSE 10000

ENTRYPOINT ["dotnet", "FlightBooking.API.dll"]