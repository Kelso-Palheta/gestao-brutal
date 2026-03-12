#!/bin/bash

# Define paths
API_PROJECT="src/BatatasFritas.API/BatatasFritas.API.csproj"
WEB_PROJECT="src/BatatasFritas.Web/BatatasFritas.Web.csproj"

echo "========================================="
echo "   Iniciando o Sistema Batatas Fritas    "
echo "========================================="

# Stop previous instances
pkill -f dotnet

echo "[1/3] Restaurando pacotes globalmente..."
export TMPDIR=$(pwd)/.tmp
export NUGET_PACKAGES=$(pwd)/.nuget_packages
export NUGET_HTTP_CACHE_PATH=$(pwd)/.tmp_http_cache
dotnet restore BatatasFritas.sln --configfile nuget.config --disable-parallel --ignore-failed-sources --force-evaluate

# Run API in background
echo "[2/3] Iniciando a API em segundo plano..."
export TMPDIR=$(pwd)/.tmp
export NUGET_PACKAGES=$(pwd)/.nuget_packages
dotnet run --no-restore --project $API_PROJECT > api_log.txt 2>&1 &
API_PID=$!
echo "API iniciada (PID: $API_PID) - Logs em api_log.txt"

# Run Frontend
echo "[3/3] Iniciando o Frontend (Blazor WebAssembly)..."
echo "Aguarde o navegador abrir a página..."
dotnet run --no-restore --project $WEB_PROJECT

echo "========================================="
echo "Encerrando os processos..."
kill $API_PID
echo "Sessão finalizada."
