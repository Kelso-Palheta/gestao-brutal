# SDD — Remoção MercadoPago (Code Cleanup)

> Gerado pelo sdd-spec em 2026-05-01 | FASE 3.5 | Nível: Detalhado
> Rastreabilidade: `src/BatatasFritas.API/Services/MercadoPagoService.cs`, `src/BatatasFritas.API/Controllers/WebhookController.cs`, `src/BatatasFritas.API/Program.cs`

---

## Problem

`MercadoPagoService.cs` + `WebhookController.cs` estão mortos (já não usados após migração para manual payment). Dependencies no Program.cs DI, references em `.csproj`. Dead code = confusão, tech debt, risk de acidental reuse.

---

## Goals

- RF-01: Delete `MercadoPagoService.cs`, `IMercadoPagoService.cs`, `WebhookController.cs`.
- RF-02: Remove DI registration: `services.AddMercadoPago()`, `services.Configure<MercadoPagoOptions>()`, Polly policies.
- RF-03: Remove NuGet package `Polly` (se não usado em outro lugar).
- RF-04: Update `appsettings.json`: remove `MercadoPago:*` keys (keep `Jwt:*` para Auth).
- RF-05: Clean SDD: `_reversa_sdd/sdd/mercadopago-service.md` mark deprecated, link para `pagamento-manual.md`.
- RF-06: Audit DTOs/Models: delete `MercadoPagoOptions`, `MercadoPagoPaymentRequest`, etc (se orphaned).
- RF-07: Solution compila, testes passam, no dangling references.

---

## Design

### Phase A: Audit (Pre-Delete)

**Ação:** Grep no codebase para encontrar **todos** os callsites:
```bash
grep -r "MercadoPago" src/ --include="*.cs"
grep -r "WebhookController" src/ --include="*.cs"
grep -r "Polly" src/ --include="*.cs"
grep -r "AddMercadoPago\|Configure<MercadoPago" src/ --include="*.cs"
```

**Esperado:** Nenhum resultado após cleanup (exceto comments históricos).

### Phase B: DI Removal

**Arquivo:** `src/BatatasFritas.API/Program.cs`

**Remover:**
```csharp
// ❌ DELETE ESTAS LINHAS
builder.Services.AddMercadoPago(); // custom extension method
builder.Services.Configure<MercadoPagoOptions>(
    builder.Configuration.GetSection("MercadoPago")
);
builder.Services.AddSingleton<IHttpClientFactory>(); // ⚠️ VERIFICAR se usado em outro lugar
```

**Mantém:**
```csharp
// ✅ KEEP JWT
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => { ... });
```

### Phase C: appsettings.json Cleanup

**Remover seção:**
```json
{
  "MercadoPago": {
    "AccessToken": "...",
    "DeviceId": "...",
    "WebhookSecret": "...",
    "NotificationUrl": "..."
  }
}
```

**Mantém:**
```json
{
  "Jwt": {
    "SecretKey": "...",
    "Issuer": "...",
    "Audience": "..."
  }
}
```

### Phase D: File Deletion

```
DELETE:
  src/BatatasFritas.API/Services/MercadoPagoService.cs
  src/BatatasFritas.API/Services/IMercadoPagoService.cs
  src/BatatasFritas.API/Controllers/WebhookController.cs
  
DELETE (if orphaned):
  src/BatatasFritas.Shared/DTOs/MercadoPago/ (entire folder)
    └─ MercadoPagoOptions.cs
    └─ MercadoPagoPaymentRequest.cs
    └─ MercadoPagoPaymentResult.cs
    └─ MercadoPagoWebhookPayload.cs
    └─ ... (other MP-specific DTOs)
```

**⚠️ VERIFY BEFORE DELETE:** Grep para certeza que nenhum outro serviço importa esses DTOs.

### Phase E: NuGet Package Removal

**Se Polly não é usado em outro lugar:**
```bash
dotnet remove BatatasFritas.API package Polly
```

**Verificar:**
```bash
grep -r "using Polly" src/ --include="*.cs"
grep -r "IAsyncPolicy" src/ --include="*.cs"
```

Se zero results → seguro remover.

### Phase F: SDD Deprecation

**Arquivo:** `_reversa_sdd/sdd/mercadopago-service.md`

**Adicionar no topo:**
```markdown
> ⚠️ **DEPRECATED** (2026-05-01 FASE 3.5)
> MercadoPago integration removed. See [`pagamento-manual.md`](pagamento-manual.md) for replacement.
```

**Manter arquivo** (histórico auditável).

---

## Edge Cases + Comportamento

### E-01: Dangling Import
- **Quando:** Arquivo `.cs` ainda importa `using BatatasFritas.API.Services.MercadoPago;`.
- **Então:** Compilation error.
- **Ação:** Remove import statement. Compilar. Se error persiste, procurar class name.

### E-02: Orphaned DTO
- **Quando:** `MercadoPagoWebhookPayload` é referenciada apenas em `WebhookController`.
- **Então:** DELETE junto com controller.
- **Ação:** Grep para confirmar orfandade antes de delete.

### E-03: Polly Used Elsewhere
- **Quando:** `Polly` é também usado em `EstoqueService` para retry de DB.
- **Então:** NÃO remover `dotnet remove Polly`.
- **Ação:** Manter dependency. Just delete MercadoPago-specific policies.

### E-04: Appsettings Override Prod
- **Quando:** Production `appsettings.Production.json` tem `MercadoPago:AccessToken`.
- **Então:** Remover lá também.
- **Ação:** Auditar **todos** os `appsettings.*.json`.

### E-05: Secrets Manager Cleanup
- **Quando:** Azure Key Vault / Coolify tem `MercadoPago-AccessToken` etc.
- **Então:** Remover manualmente via Coolify dashboard.
- **Ação:** Outside scope SDD. Document como manual task.

---

## Requisitos Não-Funcionais

| Requisito | Tipo | Prioridade |
|-----------|------|-----------|
| Solution compila (zero CS errors) | Qualidade | Alta |
| Testes passam (100% green) | Qualidade | Alta |
| No orphaned classes/methods | Code cleanup | Alta |
| Git history preservado (não amend) | Auditoria | Alta |

---

## Critérios de Aceite (Gherkin)

```gherkin
# Build Success
Quando dotnet build
Então exit code = 0
  E zero compilation warnings
  E zero references to "MercadoPago"

# Tests Pass
Quando dotnet test
Então all tests pass
  E no skipped tests (MercadoPago)

# Grep Verification
Quando grep -r "MercadoPago" src/
Então returns 0 results (except comments)

# Diff Inspection
Quando git diff feat/remove-mercadopago..main
Então shows clean deletion (only *.cs files + *.json)
  E no partial/incomplete removals
```

---

## Rastreabilidade

| Arquivo | Elemento | Status |
|---------|----------|--------|
| `Services/MercadoPagoService.cs` | Classe inteira | 🗑️ Delete |
| `Services/IMercadoPagoService.cs` | Interface inteira | 🗑️ Delete |
| `Controllers/WebhookController.cs` | Classe inteira | 🗑️ Delete |
| `API/Program.cs` | DI registration | ✏️ Refactor (remove 5 linhas) |
| `appsettings.json` | MercadoPago section | ✏️ Refactor (remove) |
| `Shared/DTOs/MercadoPago/` | Pasta inteira | 🗑️ Delete |
| `BatatasFritas.API.csproj` | Polly package ref | ✏️ Conditional (remove if orphaned) |
| `_reversa_sdd/sdd/mercadopago-service.md` | Deprecation notice | ✏️ Refactor (add notice) |

---

## Sequência Implementação

1. **Audit:** Grep para confirmar callsites.
2. **DI Removal:** Delete linhas do Program.cs.
3. **Config Cleanup:** Remove `appsettings.json` section.
4. **File Deletion:** Delete `.cs` files, pasta DTOs.
5. **Compile:** `dotnet build` → zero errors.
6. **Test:** `dotnet test` → 100% green.
7. **Verify:** Final grep para confirmar removidos.
8. **SDD Update:** Add deprecation notice em mercadopago-service.md.

---

## Decisões Finais

✅ **Polly Package**: APENAS em MercadoPagoService (confirmado via grep). Seguro remover com `dotnet remove Polly`.

✅ **WebhookController**: Zero references em testes ou outro lugar. Seguro deletar arquivo.

✅ **Azure Key Vault**: Manual cleanup in Coolify dashboard (outside SDD scope, documented como task manual).

✅ **Migration Version**: V016 (depois de V015__AddMpIntentIdToPedidos).
