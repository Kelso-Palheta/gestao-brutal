# Build WebAssembly
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
RUN dotnet restore "BatatasFritas.sln"

# Copy the remaining source code
COPY . .
WORKDIR "/app/src/BatatasFritas.Web"

# Publish the frontend
RUN dotnet publish "BatatasFritas.Web.csproj" -c Release -o /app/publish

# Final Nginx image to serve static files
FROM nginx:alpine AS final
WORKDIR /usr/share/nginx/html

# Clean default nginx html files
RUN rm -rf ./*

# Copy the published blazor static files
COPY --from=build /app/publish/wwwroot .

# Copy custom nginx configuration to handle SPA routing
COPY src/BatatasFritas.Web/nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
