FROM: SandecoMaestro
TO: Ambiente 1 (Anthropic Planning/Review)
DATE: 2026-05-01T14:46:00Z
STATUS: PRONTO_PARA_INICIAR

═══════════════════════════════════════════════════════════════

MISSÃO: Design + Code Review

Tasks iniciais (sem pré-requisitos, pode começar AGORA):
  ✏️ task-1-design-sm (Projetista)
  ✏️ task-2-review-mp (Auditor)

─────────────────────────────────────────────────────────────────

TASK-1: Design Pedido.CanTransition() Logic

Input:
  - _reversa_sdd/sdd/pedido-state-machine.md
  - src/BatatasFritas.Shared/Enums/StatusPedido.cs (current enum)

Deliverable:
  Design document with:
    a) Pedido.CanTransition() switch statement (C# code)
    b) State machine diagram (ASCII or Mermaid)
    c) Migration script V016 pseudocode (FluentMigrator)
    d) Test cases (list of valid + invalid transitions)

Estimativa: 2 horas
Output: caixa_entrada/amb-1-planning.msg (quando CONCLUIDO)

─────────────────────────────────────────────────────────────────

TASK-2: Review MercadoPago Removal Plan

Input:
  - _reversa_sdd/sdd/remocao-mercadopago.md
  - _reversa_sdd/architecture.md (Upgrade section)

Deliverable:
  Review document with:
    a) Phase A-F validation (each phase is clear, testable)
    b) Risk assessment (identify gotchas, dependencies)
    c) Removal checklist (Phase A-F item list)
    d) Go/no-go recommendation

Estimativa: 1.5 horas
Output: caixa_entrada/amb-1-planning.msg (quando CONCLUIDO)

─────────────────────────────────────────────────────────────────

TASK-5: Security Audit (depois de task-1)

Input:
  - _reversa_sdd/sdd/pagamento-manual.md
  - Design document de task-1
  - _reversa_sdd/sdd/auth.md

Deliverable:
  Security audit report with:
    a) Token handling validation (JWT persistence, F5 issue)
    b) Auth flow in manual payment (Delivery vs Totem)
    c) CORS + HTTPS implications
    d) No new vulnerabilities introduced

Estimativa: 1 hora (AFTER task-1)
Output: caixa_entrada/amb-1-planning.msg (quando CONCLUIDO)

─────────────────────────────────────────────────────────────────

CHECKPOINT: C1-Design-Approved

Target: 2026-05-01T16:30:00Z
Success criteria:
  - task-1-design-sm = CONCLUIDO ✅
  - task-2-review-mp = CONCLUIDO ✅
  - Recommendation = GO (verde para executor começar)

─────────────────────────────────────────────────────────────────

RECURSOS

Specs base: _reversa_sdd/sdd/
  - pedido-state-machine.md (score 9.7/10)
  - pagamento-manual.md (score 9.8/10)
  - remocao-mercadopago.md (score 9.8/10)

Architecture: _reversa_sdd/architecture.md

Current code:
  - src/BatatasFritas.Shared/Enums/StatusPedido.cs
  - src/BatatasFritas.API/Services/MercadoPagoService.cs (to be deleted)
  - src/BatatasFritas.API/Controllers/WebhookController.cs (to be deleted)

─────────────────────────────────────────────────────────────────

PRÓXIMOS PASSOS (após C1)

Quando C1-Design-Approved atingido:
  1. Maestro notifica amb-2-executor: pronto para começar
  2. Maestro notifica amb-3-research: pronto para audit

Wait here até C1 atingido. Então você será notificado.

═══════════════════════════════════════════════════════════════

Sucesso! 🎯
