# Erro de Deploy no Coolify

## Problema
O Coolify falhou ao fazer `dotnet publish` da API durante o build do Docker:

```
dotnet publish "BatatasFritas.API.csproj" -c Release -o /app/publish /p:UseAppHost=false
exit code: 1
```

A mensagem de erro específico não foi capturada na saída do Coolify.

## Diagnóstico

### 1. Executar localmente para encontrar o erro real

Execute o script de diagnóstico:

```bash
cd /caminho/para/BatatasFritas
chmod +x BUILD_DEBUG.sh
./BUILD_DEBUG.sh
```

Isso vai:
- Verificar se .NET SDK está instalado
- Validar que todos os arquivos críticos existem
- Tentar restaurar dependências (NuGet)
- Tentar fazer build (Debug)
- Tentar publicar (como o Docker faz)

Se algum passo falhar, a mensagem de erro específica será exibida.

### 2. Build com mais detalhes

Se o script falhar, tente:

```bash
# Build com output detalhado
dotnet build BatatasFritas.sln -c Debug -v detailed

# Ou direto na API
cd src/BatatasFritas.API
dotnet publish BatatasFritas.API.csproj -c Release -v detailed /p:UseAppHost=false
```

### 3. Possíveis causas

#### a) Versão do .NET SDK incompatível
- Verifique: `dotnet --version`
- Deve estar em torno de 8.0 (ex: 8.0.x ou 9.0.x)
- Download: https://dotnet.microsoft.com/download

#### b) Dependência NuGet não instalada
- Pode falhar ao tentar restaurar `BCrypt.Net-Next`, `Microsoft.AspNetCore.SignalR`, etc.
- Verifique o arquivo `.csproj` e versões dos pacotes

#### c) Erro de sintaxe no código C#
- Pode estar em um dos novos arquivos:
  - `src/BatatasFritas.API/Controllers/AuthController.cs`
  - `src/BatatasFritas.API/Hubs/PedidosHub.cs`
  - `src/BatatasFritas.API/Program.cs` (JWT setup)
  - Controllers modificados (KDS, Pagamentos, Financeiro, etc.)

#### d) Referência circular ou arquivo faltando
- Verifique o `.sln` e que todos os `.csproj` estão corretos

### 4. Deploy após corrigir

Depois de encontrar e corrigir o erro:

```bash
# Commit da correção
git add .
git commit -m "fix: resolve erro de compilação no Docker"

# Push para o Coolify redetectar
git push

# Ou dispare manualmente o deploy no Coolify
```

## Arquivo de Build

O Dockerfile da API está em: `src/BatatasFritas.API/Dockerfile`

Se modificar a API, certifique-se de:
1. Compilação local funciona
2. Commit do código
3. Push do código
4. Redeploy no Coolify

## Configuração de Produção

O `appsettings.Development.json` **não** é usado em produção.

Para production, você precisa definir variáveis de ambiente no Coolify:

- `ASPNETCORE_ENVIRONMENT=Production`
- `Jwt__SecretKey=6yxXTcIgAA7CW-0PZ5OKvrUjmcplR90aDt65MeRj6ujGMCZaeWxQRo-yOGkf97AB`
- `ConnectionStrings__DefaultConnection=Host=...;Database=...;Username=...;Password=...`
- `InfinitePay__WebhookSecret=...`

Ver: `src/BatatasFritas.API/appsettings.Production.json.example` para referência.

## Próximas tentativas de deploy

Após resolver o erro local e fazer commit:

1. O Coolify detectará o novo commit
2. Executará o build novamente
3. Desta vez, o `dotnet publish` deve funcionar

Se ainda falhar, verifique os logs do Docker no Coolify mais detalhadamente.
