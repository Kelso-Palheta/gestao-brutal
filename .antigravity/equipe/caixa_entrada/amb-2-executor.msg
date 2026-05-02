FROM: SandecoMaestro
TO: Ambiente 2 (macOS Terminal Executor)
DATE: 2026-05-01T14:47:00Z
STATUS: AGUARDANDO_C1_CHECKPOINT

═══════════════════════════════════════════════════════════════

MISSÃO: Code Execution + Build Validation

⚠️ AGUARDE C1-Design-Approved antes de começar task-3

Tasks (sequencial com pré-requisitos):
  🔧 task-3-git-checkout (AGUARDANDO C1)
  🔧 task-4-delete-mp (APÓS task-3)
  ✅ task-6-dotnet-test (APÓS task-4)

─────────────────────────────────────────────────────────────────

TASK-3: git checkout feat/remove-mercadopago

Pre-requisito: C1-Design-Approved (Maestro notifica)

Ações:
  1. git status (confirma main clean)
  2. git checkout -b feat/remove-mercadopago
  3. dotnet restore (prep ambiente)
  4. Notify: task-3-git-checkout = CONCLUIDO

Estimativa: 30 min
Output: caixa_entrada/amb-2-executor.msg (status CONCLUIDO)

─────────────────────────────────────────────────────────────────

TASK-4: Delete MP files + DI cleanup

Pre-requisito: task-3 = CONCLUIDO

Files to DELETE:
  ❌ src/BatatasFritas.API/Services/MercadoPagoService.cs
  ❌ src/BatatasFritas.API/Services/IMercadoPagoService.cs
  ❌ src/BatatasFritas.API/Controllers/WebhookController.cs
  ❌ src/BatatasFritas.Shared/DTOs/MercadoPago/ (entire folder)

Files to MODIFY:
  ✏️ src/BatatasFritas.API/Program.cs
     - Remove: services.AddMercadoPago();
     - Remove: services.Configure<MercadoPagoOptions>(...)
     - Remove: AddPolly retry policies (if only used by MP)

  ✏️ appsettings.json
     - Remove: "MercadoPago": { ... } section
     - Keep: "Jwt": { ... } for Auth

Files to CREATE:
  ✨ src/BatatasFritas.Infrastructure/Migrations/V016__SimplifyStatusPedido.cs
     - Drop enum values: Processando, Recusado
     - (Migration script: consult amb-1 design document)

Validation AFTER deletes:
  ✓ dotnet build --no-restore (ZERO errors)
  ✓ grep -r "MercadoPago" src/ (ZERO matches except comments)
  ✓ grep -r "Polly" src/ (ZERO matches if only MP used it)

Estimativa: 3 horas
Output: caixa_entrada/amb-2-executor.msg (status CONCLUIDO or ERRO)

─────────────────────────────────────────────────────────────────

TASK-6: dotnet test + validation

Pre-requisito: task-4 = CONCLUIDO

Ações:
  1. dotnet test (run full suite)
     Expected: 100% green (0 failures, 0 skipped)

  2. Build validation:
     dotnet build --no-restore
     Expected: ZERO warnings, ZERO errors

  3. Grep final verification:
     grep -r "MercadoPago\|WebhookController" src/ tests/
     Expected: ZERO results (except deprecated comments)

  4. Commit draft:
     git status (review changes)
     (DO NOT COMMIT YET — wait for amb-3 docs update)

Estimativa: 2 horas
Output: caixa_entrada/amb-2-executor.msg (status CONCLUIDO)

─────────────────────────────────────────────────────────────────

CHECKPOINT: C2-Code-Deleted → C3-Tests-Green

Target: 2026-05-01T21:30:00Z
Success criteria:
  - task-3-git-checkout = CONCLUIDO ✅
  - task-4-delete-mp = CONCLUIDO ✅
  - task-6-dotnet-test = CONCLUIDO (100% green) ✅

─────────────────────────────────────────────────────────────────

WAIT STATUS

Until C1-Design-Approved:
  ⏳ Do NOT start task-3
  ⏳ Monitor caixa_entrada/aviso_geral.msg for C1 notification

Maestro notificará quando C1 atingido.

─────────────────────────────────────────────────────────────────

PRÓXIMOS PASSOS

Quando C3-Tests-Green atingido:
  1. Aguarde amb-3 finalizar C4-Docs-Updated
  2. Maestro aprova merge para main
  3. PR created + merged

═══════════════════════════════════════════════════════════════

Ready to execute! ⚡
(Waiting for C1-Design-Approved notification)
