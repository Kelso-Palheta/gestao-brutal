# Build API
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Copy the solution file and project files for restore
COPY ["BatatasFritas.sln", "./"]
COPY ["src/BatatasFritas.API/BatatasFritas.API.csproj", "src/BatatasFritas.API/"]
COPY ["src/BatatasFritas.Domain/BatatasFritas.Domain.csproj", "src/BatatasFritas.Domain/"]
COPY ["src/BatatasFritas.Infrastructure/BatatasFritas.Infrastructure.csproj", "src/BatatasFritas.Infrastructure/"]
COPY ["src/BatatasFritas.Shared/BatatasFritas.Shared.csproj", "src/BatatasFritas.Shared/"]
COPY ["src/BatatasFritas.Web/BatatasFritas.Web.csproj", "src/BatatasFritas.Web/"]

# Restore dependencies
RUN dotnet restore "src/BatatasFritas.API/BatatasFritas.API.csproj"

# Copy the remaining source code
COPY . .
WORKDIR "/app/src/BatatasFritas.API"

# Publish the application
RUN dotnet publish "BatatasFritas.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Final API image
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS final
WORKDIR /app
COPY --from=build /app/publish .

# .NET 8 containers listen on 8080 by default
EXPOSE 8080
ENTRYPOINT ["dotnet", "BatatasFritas.API.dll"]
