# Remove Mercado Pago Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement this plan task-by-task.

**Goal:** Remover completamente a integração com Mercado Pago do projeto BatatasFritas.

**Architecture:** Exclusão de serviços e controllers relacionados ao Mercado Pago, limpeza da Injeção de Dependência no `Program.cs` e remoção das seções de configuração nos arquivos `appsettings.json`.

**Tech Stack:** .NET 8, ASP.NET Core.

---

### Task 1: Clean up Mercado Pago Files

**Files:**
- Delete: `src/BatatasFritas.API/Services/MercadoPagoService.cs`
- Delete: `src/BatatasFritas.API/Services/IMercadoPagoService.cs`
- Delete: `src/BatatasFritas.API/Controllers/WebhookController.cs`
- Delete Directory: `src/BatatasFritas.Shared/DTOs/MercadoPago/`

**Step 1: Delete Service files**
Run: `rm -f src/BatatasFritas.API/Services/MercadoPagoService.cs src/BatatasFritas.API/Services/IMercadoPagoService.cs`

**Step 2: Delete Controller file**
Run: `rm -f src/BatatasFritas.API/Controllers/WebhookController.cs`

**Step 3: Delete DTO directory**
Run: `rm -rf src/BatatasFritas.Shared/DTOs/MercadoPago/`

**Step 4: Verify deletion**
Run: `ls src/BatatasFritas.API/Services/MercadoPagoService.cs`
Expected: "No such file or directory"

---

### Task 2: Clean up Dependency Injection in Program.cs

**Files:**
- Modify: `src/BatatasFritas.API/Program.cs`

**Step 1: Remove MercadoPago configuration and service registration**
Remove lines related to:
- `builder.Services.Configure<MercadoPagoOptions>(builder.Configuration.GetSection("MercadoPago"));`
- `builder.Services.AddHttpClient<IMercadoPagoService, MercadoPagoService>(...)`
- `builder.Services.AddScoped<IMercadoPagoService, MercadoPagoService>();`

**Step 2: Remove unused usings**
Remove:
- `using BatatasFritas.API.Services;` (if only used for MP)
- `using BatatasFritas.Shared.DTOs.MercadoPago;`

---

### Task 3: Clean up configuration files

**Files:**
- Modify: `src/BatatasFritas.API/appsettings.json`
- Modify: `src/BatatasFritas.API/appsettings.Development.json`

**Step 1: Remove "MercadoPago" section from appsettings.json**
Remove the entire `MercadoPago` JSON object.

**Step 2: Remove "MercadoPago" section from appsettings.Development.json**
Remove the entire `MercadoPago` JSON object (if it exists).

---

### Task 4: Validation and Build

**Step 1: Run dotnet build**
Run: `dotnet build src/BatatasFritas.API/BatatasFritas.API.csproj`
Expected: Build SUCCESS with NO errors related to missing MP services.

**Step 2: Grep for remaining references**
Run: `grep -r "MercadoPago" src/`
Expected: Zero results in source code.

**Step 3: Run Tests**
Run: `dotnet test`
Expected: All tests pass.
