#!/bin/bash
# Script para debugar problema de build do projeto

set -e

echo "=========================================="
echo "BatatasFritas Build Diagnostic"
echo "=========================================="
echo ""

# Verifica se .NET está instalado
echo "[1] Verificando .NET SDK..."
if ! command -v dotnet &> /dev/null; then
    echo "❌ ERRO: .NET SDK não está instalado!"
    echo "   Baixe em: https://dotnet.microsoft.com/en-us/download"
    exit 1
fi

DOTNET_VERSION=$(dotnet --version)
echo "✓ .NET SDK encontrado: $DOTNET_VERSION"
echo ""

# Verifica arquivos críticos
echo "[2] Verificando arquivos críticos..."
REQUIRED_FILES=(
    "BatatasFritas.sln"
    "src/BatatasFritas.API/BatatasFritas.API.csproj"
    "src/BatatasFritas.Domain/BatatasFritas.Domain.csproj"
    "src/BatatasFritas.Infrastructure/BatatasFritas.Infrastructure.csproj"
    "src/BatatasFritas.Shared/BatatasFritas.Shared.csproj"
    "src/BatatasFritas.Web/BatatasFritas.Web.csproj"
    "src/BatatasFritas.API/Program.cs"
    "src/BatatasFritas.API/Controllers/AuthController.cs"
    "src/BatatasFritas.API/Hubs/PedidosHub.cs"
)

for file in "${REQUIRED_FILES[@]}"; do
    if [ -f "$file" ]; then
        echo "✓ $file"
    else
        echo "❌ ERRO: Arquivo não encontrado: $file"
        exit 1
    fi
done
echo ""

# Limpa cache anterior
echo "[3] Limpando cache anterior..."
rm -rf bin obj src/*/bin src/*/obj
echo "✓ Cache limpo"
echo ""

# Restaura dependências
echo "[4] Restaurando dependências do NuGet..."
dotnet restore BatatasFritas.sln 2>&1 | tail -20
if [ $? -ne 0 ]; then
    echo "❌ ERRO: Falha ao restaurar dependências"
    exit 1
fi
echo "✓ Dependências restauradas"
echo ""

# Tenta fazer build
echo "[5] Fazendo build da solução (Debug)..."
dotnet build BatatasFritas.sln -c Debug 2>&1 | tail -50
if [ $? -ne 0 ]; then
    echo "❌ ERRO: Falha no build!"
    echo ""
    echo "Tente novamente com mais detalhes:"
    echo "  dotnet build BatatasFritas.sln -v detailed"
    exit 1
fi
echo "✓ Build bem-sucedido"
echo ""

# Tenta publicar (como no Docker)
echo "[6] Testando publish do API (como no Docker)..."
cd src/BatatasFritas.API
dotnet publish BatatasFritas.API.csproj -c Release -o /tmp/publish_test /p:UseAppHost=false 2>&1 | tail -20
if [ $? -ne 0 ]; then
    echo "❌ ERRO: Falha ao publicar a API!"
    echo ""
    echo "Tente novamente com mais detalhes:"
    echo "  cd src/BatatasFritas.API"
    echo "  dotnet publish -c Release -v detailed"
    exit 1
fi
cd ../..
echo "✓ Publish bem-sucedido"
echo ""

echo "=========================================="
echo "✅ Todos os testes passaram!"
echo "O projeto deve compilar corretamente."
echo "=========================================="
