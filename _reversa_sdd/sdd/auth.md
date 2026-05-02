# SDD — Autenticação e Segurança (JWT)

> Gerado pelo Reversa (Redator) em 2026-05-01 | Nível: Detalhado
> Rastreabilidade: `src/BatatasFritas.API/Program.cs`, `AuthController.cs`, `AuthStateProvider.cs`

---

## Visão Geral

O sistema BatatasFritas utiliza autenticação baseada em **JWT (JSON Web Token)** para proteger as operações administrativas e de gestão. O fluxo é desacoplado (stateless) entre a API e o cliente Blazor WASM, utilizando Bearer Tokens para autorização e um provedor de estado customizado no frontend.

---

## Responsabilidades

| Responsabilidade | MoSCoW |
|---|---|
| Validar credenciais de administrador e emitir tokens JWT | **Must** |
| Proteger endpoints sensíveis via middleware de autorização | **Must** |
| Gerenciar o estado de autenticação no cliente Blazor | **Must** |
| Injetar automaticamente o token em chamadas HTTP no frontend | **Must** |
| Suportar autenticação em conexões WebSocket (SignalR) | **Must** |
| Permitir configuração de segredos e emissores via AppSettings | **Must** |

---

## Interface e Configuração

### Configuração da API (`appsettings.json`)
- `Jwt:SecretKey`: Chave simétrica de assinatura.
- `Jwt:Issuer`: Identificador do emissor do token.
- `Jwt:Audience`: Identificador do destinatário do token.

### Endpoints
- `POST /api/auth/login`: Recebe usuário/senha e retorna o Token JWT.
- `GET /api/auth/validate`: Valida se o token atual ainda é válido.

---

## Regras de Segurança e Implementação

1. 🟢 **Validação Rigorosa** — A API valida `Issuer`, `Audience`, `Lifetime` (expiração) e a `SigningKey` em cada requisição.
2. 🟢 **Segurança SignalR** — Como o protocolo WebSocket não suporta headers customizados em navegadores, o middleware de autenticação está configurado para extrair o token do parâmetro `access_token` na query string quando o path inicia com `/hubs`.
3. 🟢 **AuthDelegatingHandler** — O frontend Blazor utiliza um `DelegatingHandler` que intercepta todas as requisições HTTP e anexa o cabeçalho `Authorization: Bearer {token}` se o usuário estiver logado.
4. 🟢 **Swagger Integrado** — O Swagger UI está configurado com `SecurityDefinition` do tipo Bearer, permitindo testar endpoints protegidos diretamente pela interface.
5. 🔴 **Estado em Memória (F5 Issue)** — No frontend, o `AuthStateProvider` mantém o estado `_isAuthenticated` apenas em uma variável privada. 🔴 **BUG DE UX:** Ao atualizar a página (F5), o estado é perdido e o usuário é deslogado, pois o token não está sendo persistido no `localStorage`.
6. 🔴 **Segredo Hardcoded em Dev** — Em ambiente de desenvolvimento, há uma dependência de segredos configurados manualmente no `appsettings.json`, com risco de commit acidental de chaves de produção.

---

## Fluxo de Autenticação

1. O Admin submete credenciais via `Login.razor`.
2. A API valida e retorna o JWT assinado com HMAC-SHA256.
3. O `AuthStateProvider` chama `MarkUserAsAuthenticated()`.
4. O Blazor emite um evento de mudança de estado, liberando as páginas protegidas por `[Authorize]`.
5. Todas as chamadas subsequentes via `HttpClient` incluem o token no cabeçalho.

---

## Requisitos Não Funcionais

| Tipo | Requisito inferido | Evidência | Confiança |
|---|---|---|---|
| Segurança | Criptografia HMAC-SHA256 para assinatura de tokens | `Program.cs:56` | 🟢 |
| Escalabilidade | Autenticação Stateless permite múltiplas instâncias da API | `Program.cs:45` | 🟢 |
| Interoperabilidade | Suporte a tokens via Header ou Query String (SignalR) | `Program.cs:65` | 🟢 |

---

## Critérios de Aceitação

```gherkin
# Happy Path — Login Admin
Dado credenciais válidas de administrador
Quando a requisição de login é feita
Então um token JWT válido deve ser retornado
  E o menu administrativo deve tornar-se visível no Blazor

# Falha — Token Expirado
Dado um token JWT gerado há mais de 8 horas (ou conforme config)
Quando uma chamada à API é feita com este token
Então a API deve retornar 401 Unauthorized

# Happy Path — SignalR Protegido
Dado uma conexão WebSocket para o Hub de Pedidos
Quando o token é passado via query string `?access_token=...`
Então a conexão deve ser autorizada e o usuário deve receber eventos do Hub
```

---

## Rastreabilidade de Código

| Arquivo | Classe / Função | Cobertura |
|---|---|---|
| `src/BatatasFritas.API/Program.cs` | Configuração JWT | 🟢 |
| `src/BatatasFritas.Web/Services/AuthStateProvider.cs` | Gestão de estado UI | 🟢 |
| `src/BatatasFritas.Web/Services/AuthDelegatingHandler.cs` | Interceptador HTTP | 🟢 |
| `src/BatatasFritas.API/Controllers/AuthController.cs` | Geração de Token | 🟢 |
