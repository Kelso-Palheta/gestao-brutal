# Build WebAssembly
# NOTA: usa sdk:8.0 (mesma imagem da API — JÁ CACHEADA no servidor Hetzner).
# A imagem customizada ghcr.io foi descartada pois causava download duplicado de 543MB.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# ── LAYER CACHEADA: instala wasm-tools UMA VEZ e reutiliza em todos os deploys ──
# Adicionamos variáveis de ambiente para estabilizar o build (datas e telemetria)
ENV LANG=en_US.UTF-8
ENV LC_ALL=en_US.UTF-8
ENV DOTNET_SKIP_FIRST_TIME_EXPERIENCE=true
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1
ENV CI=true

# Forçamos a instalação limpa do workload e verificamos a versão
RUN dotnet workload install wasm-tools && dotnet workload --version

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
# Nota: sem --no-restore pois o find acima apaga os obj/ criados pelo restore anterior
# E sem o pipe para o 'tee' para não mascarar o erro do dotnet publish
RUN dotnet publish "BatatasFritas.Web.csproj" \
    -c Release \
    -o /app/publish \
    -v n

# Final Nginx image to serve static files
FROM nginx:alpine AS final
WORKDIR /usr/share/nginx/html

# Clean default nginx html files
RUN rm -rf ./*

# Copy the published blazor static files
COPY --from=build /app/publish/wwwroot .

# Copy custom nginx configuration to handle SPA routing + API proxy
COPY src/BatatasFritas.Web/nginx.conf /etc/nginx/conf.d/default.conf

# Em produção, o appsettings.json não precisa de BaseUrl porque o nginx faz proxy
# de /api/ e /hubs/ para o container da API internamente.
# O Blazor WASM usará HostEnvironment.BaseAddress como base, o que funciona
# corretamente com o proxy nginx configurado.
RUN printf '{"ApiSettings":{"BaseUrl":""}}' > /usr/share/nginx/html/appsettings.json

EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
