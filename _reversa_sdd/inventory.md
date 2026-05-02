# Inventário do Sistema — BatatasFritas

## Estrutura de Pastas
- `src/` (Código fonte)
  - `BatatasFritas.API` (ASP.NET Core Web API)
  - `BatatasFritas.Domain` (Regras de negócio, Entidades)
  - `BatatasFritas.Infrastructure` (NHibernate, SQLite/PostgreSQL)
  - `BatatasFritas.Shared` (DTOs, Enums)
  - `BatatasFritas.Web` (Blazor WebAssembly)
- `tests/` (Testes)
- `docker/` (Configurações Docker)

## Tecnologias Identificadas
- **Backend:** C# (.NET 8.0), ASP.NET Core, SignalR, NHibernate 5.5, FluentNHibernate
- **Frontend:** Blazor WASM
- **Node.js / MCP:** MCP SDK para integração com agentes locais (`mcp_server.js`)
- **Bancos de Dados:** SQLite (`batatasfritas.db`) e PostgreSQL
- **Segurança:** JWT Bearer, BCrypt.Net-Next

## Pontos de Entrada
- `.NET API:` `src/BatatasFritas.API/Program.cs`
- `.NET Web:` `src/BatatasFritas.Web/Program.cs`
- `Node.js:` `mcp_server.js`
- `Docker:` `Dockerfile`, `docker-compose.yml`, `docker-compose.prod.yml`
- `CI/CD:` `.github/workflows/build-base-image.yml`
