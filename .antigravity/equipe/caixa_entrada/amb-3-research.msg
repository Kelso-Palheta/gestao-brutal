FROM: SandecoMaestro
TO: Ambiente 3 (Model Research/Auditor)
DATE: 2026-05-01T14:48:00Z
STATUS: PRONTO_PARA_INICIAR_AUDIT

═══════════════════════════════════════════════════════════════

MISSÃO: SDD Audit + Documentation Update

Tasks (pode começar audit paralelo com amb-1/2):
  📖 task-7-audit-specs (PODE COMEÇAR AGORA)
  📖 task-8-update-docs (APÓS task-7)
  📖 task-9-impact-analysis (APÓS task-8)

─────────────────────────────────────────────────────────────────

TASK-7: Audit specs for MercadoPago references

Pre-requisito: NENHUM (inicie AGORA em paralelo)

Ações:
  1. Grep _reversa_sdd/ folder:
     grep -r "MercadoPago\|Webhook\|MP\|Polly\|intent\|signature" \
       _reversa_sdd/ --include="*.md"

  2. Listar specs que precisam update:
     - [x] mercadopago-service.md → mark DEPRECATED
     - [ ] domain.md → remove MP business rules?
     - [ ] state-machines.md → remove MP state references?
     - [ ] openapi.yaml → remove MP endpoints?
     - [ ] ... (outras?)

  3. Verificar references in cada spec:
     - pedido-state-machine.md (não deve mencionar MP)
     - pagamento-manual.md (não deve mencionar MP)
     - remocao-mercadopago.md (é sobre MP, ok)

  4. Gerar audit report:
     audit-mp-references.txt
       - Total references encontradas
       - Specs impacted
       - Prioridade de updates

Estimativa: 1 hora
Output: caixa_entrada/amb-3-research.msg (status CONCLUIDO)

─────────────────────────────────────────────────────────────────

TASK-8: Update specs (domain.md, state-machines.md, openapi.yaml)

Pre-requisito: task-7 = CONCLUIDO

Ações baseadas em audit-mp-references.txt:

  A. _reversa_sdd/domain.md
     - Remove section: "MercadoPago Integration Rules"
     - Remove enum descriptions relacionadas a MP
     - Update "Payment Flow" → agora manual-only
     - Add: Referência para pagamento-manual.md

  B. _reversa_sdd/state-machines.md
     - Update "StatusPagamento state machine" diagram
     - Remover: Processando, Recusado states
     - Add: Novo flow para Pendente→Aprovado (manual)
     - Keep: StatusPedido (não afetado por MP removal)

  C. _reversa_sdd/openapi/batatasfritas-api.yaml
     - Delete endpoints:
       POST /api/mercadopago/webhook
       GET /api/pagamentos/{id}/status (if MP-only)
       POST /api/pagamentos/pix (if MP-only)
     - Add (if missing): Manual payment endpoints
       GET /api/pedidos/{id}/payment-info (Pix# ou Order#)

Estimativa: 2.5 horas
Output: caixa_entrada/amb-3-research.msg (status CONCLUIDO)

─────────────────────────────────────────────────────────────────

TASK-9: Document removal impact + migration guide

Pre-requisito: task-8 = CONCLUIDO

Ações:

  Create: _reversa_sdd/removal-impact.md

  Sections:
    1. Breaking Changes
       - MercadoPagoService no longer available
       - WebhookController removed
       - StatusPagamento enum simplified
       - Polly policies removed

    2. Migration Steps for Developers
       - How to move from MercadoPago to manual payment
       - What changed in payment flow
       - New manual payment interface (placeholder Pix#)
       - When InfinitePay arrives, how to integrate (future)

    3. Timeline
       - FASE 3.5 (NOW): Remove MercadoPago
       - FASE X (FUTURE): Implement InfinitePay integration

    4. FAQ
       - "What replaced MercadoPago?" → Manual payment flow
       - "When will payment API be restored?" → After InfinitePay credentials arrive
       - "What about existing webhooks?" → All removed

Estimativa: 1 hora
Output: caixa_entrada/amb-3-research.msg (status CONCLUIDO)

─────────────────────────────────────────────────────────────────

CHECKPOINT: C4-Docs-Updated

Target: 2026-05-01T23:30:00Z
Success criteria:
  - task-7-audit-specs = CONCLUIDO ✅
  - task-8-update-docs = CONCLUIDO ✅
  - task-9-impact-analysis = CONCLUIDO ✅
  - All updated files reviewed + no conflicts with amb-2 changes

─────────────────────────────────────────────────────────────────

COORDENAÇÃO COM AMB-2

Enquanto amb-2 executa code changes:
  - Você faz audit em paralelo (task-7)
  - Quando amb-2 finaliza task-4, você começa task-8
  - Maestro coordena merge timing (code + docs together)

Se amb-2 encontrar NEW MP references durante delete:
  - Notifica via caixa_entrada
  - Você adiciona à audit report
  - Update task-8 com novas findings

─────────────────────────────────────────────────────────────────

RECURSOS

Specs (base para audit):
  - _reversa_sdd/sdd/ (all .md files)

Current docs (to update):
  - _reversa_sdd/domain.md
  - _reversa_sdd/state-machines.md
  - _reversa_sdd/openapi/batatasfritas-api.yaml

Create new:
  - _reversa_sdd/removal-impact.md

Reference:
  - _reversa_sdd/architecture.md
  - _reversa_sdd/sdd/remocao-mercadopago.md

─────────────────────────────────────────────────────────────────

PRÓXIMOS PASSOS

Quando C4-Docs-Updated atingido:
  1. Maestro coordena amb-2 + amb-3 para final merge
  2. Commit message: "FASE 3.5: Remove MercadoPago + State machine + Manual payment"
  3. PR to main

═══════════════════════════════════════════════════════════════

Ready for audit! 🔍
(You can start task-7 NOW in parallel with amb-1/2)
