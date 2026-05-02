# SDD — Pedido State Machine Validation

> Gerado pelo sdd-spec em 2026-05-01 | FASE 3.5 | Nível: Detalhado
> Rastreabilidade: `src/BatatasFritas.Domain/Aggregates/Pedido.cs`, `src/BatatasFritas.API/Controllers/PedidosController.cs`

---

## Problem

Hoje `Pedido.StatusPedido` aceita qualquer transição: `Entregue → Aceito`, `Cancelado → EmPreparo`, etc. violando regras de negócio. Controllers não validam. Resultado: bugs em produção quando operadores clicam botões em ordem errada, ou estado fica inconsistente após falha parcial.

---

## Goals

- RF-01: `Pedido.CanTransition(newStatus) → bool` retorna true/false (não throws).
- RF-02: `PedidosController.UpdateStatus()` valida transição antes de persistir; rejeita com HTTP 400 + mensagem.
- RF-03: Máquina estado está documentada no código (enum comments + state diagram).
- RF-04: Testes unitários cobrem todas transições válidas e rejeições esperadas.

---

## Design

### State Machine (Máquina de Estados)

```
┌─────────────┐
│  Recebido   │  (inicial, após POST /pedidos)
└──────┬──────┘
       │ (cliente paga OU admin aprova)
       ▼
┌─────────────┐
│   Aceito    │  (cozinha aceita pedido)
└──────┬──────┘
       │
       ▼
┌─────────────┐
│  EmPreparo  │  (cozinha está preparando)
└──────┬──────┘
       │
       ▼
┌──────────────────┐
│ ProntoParaEntrega│  (pronto, aguardando entrega)
└──────┬───────────┘
       │
       ▼
┌──────────────────┐
│ SaiuParaEntrega  │  (em rota com cliente)
└──────┬───────────┘
       │
       ▼
┌─────────────┐
│  Entregue   │  (terminal — entregue ao cliente)
└─────────────┘

Cancelados (de qualquer estado não-terminal):
  Recebido → Cancelado
  Aceito → Cancelado
  EmPreparo → Cancelado
  ProntoParaEntrega → Cancelado
  SaiuParaEntrega → Cancelado
```

### Implementação: `Pedido.CanTransition(newStatus)`

```csharp
public bool CanTransition(StatusPedido newStatus)
{
    return (StatusPedido, newStatus) switch
    {
        // Recebido → ...
        (StatusPedido.Recebido, StatusPedido.Aceito) => true,
        (StatusPedido.Recebido, StatusPedido.Cancelado) => true,
        
        // Aceito → ...
        (StatusPedido.Aceito, StatusPedido.EmPreparo) => true,
        (StatusPedido.Aceito, StatusPedido.Cancelado) => true,
        
        // EmPreparo → ...
        (StatusPedido.EmPreparo, StatusPedido.ProntoParaEntrega) => true,
        (StatusPedido.EmPreparo, StatusPedido.Cancelado) => true,
        
        // ProntoParaEntrega → ...
        (StatusPedido.ProntoParaEntrega, StatusPedido.SaiuParaEntrega) => true,
        (StatusPedido.ProntoParaEntrega, StatusPedido.Cancelado) => true,
        
        // SaiuParaEntrega → ...
        (StatusPedido.SaiuParaEntrega, StatusPedido.Entregue) => true,
        (StatusPedido.SaiuParaEntrega, StatusPedido.Cancelado) => true,
        
        // Qualquer outro = inválido
        _ => false
    };
}
```

Modelo: guarda lógica no Domain (Aggregate Root). Controllers CONSULTAM antes de atualizar:

```csharp
[HttpPatch("pedidos/{id}/status")]
public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateStatusRequest req)
{
    var pedido = await _repository.GetByIdAsync(id);
    if (!pedido.CanTransition(req.NovoStatus))
        return BadRequest(new { 
            error = "Transição inválida",
            current = pedido.StatusPedido,
            attempted = req.NovoStatus,
            allowed = pedido.GetAllowedTransitions()
        });
    
    pedido.StatusPedido = req.NovoStatus;
    await _repository.SaveAsync(pedido);
    return Ok(pedido);
}
```

---

## Edge Cases + Comportamento

### E-01: Transição Inválida
- **Quando:** Cliente tenta `EmPreparo → Recebido` via botão desatualizado.
- **Então:** Controller retorna 400 Bad Request + lista transições válidas.
- **Por quê:** UI correto mostra botões condicionais; mas network lag pode permitir click inválido.

### E-02: Duplicate Status Update
- **Quando:** Pedido já está `Entregue`, cozinha tenta `EmPreparo → Entregue` novamente.
- **Então:** CanTransition retorna false (não há transição Entregue → Entregue).
- **Por quê:** Idempotência. Não há efeito colateral se operador clica botão 2x.

### E-03: Parallel Requests
- **Quando:** 2 operadores clicam simultaneamente: A tenta `Aceito → EmPreparo`, B tenta `Aceito → Cancelado`.
- **Então:** Race condition. DB último commit vence. Primeiro operador recebe OK, segundo recebe "Transição inválida (status é EmPreparo agora)".
- **Por quê:** Pessimistic lock (SELECT FOR UPDATE) não implementado aqui. Aceitável para restaurante (baixa concorrência).

### E-04: Admin Force Cancel
- **Quando:** Admin tenta cancelar pedido que já foi `Entregue`.
- **Então:** CanTransition retorna false. Retorna 400.
- **Por quê:** Business rule: não refundar após entrega. Admin deve usar dashboard de devoluções (outside scope).

---

## Requisitos Não-Funcionais

| Requisito | Tipo | Prioridade |
|-----------|------|-----------|
| CanTransition() executa em < 1ms | Performance | Alta |
| State machine documentada no código | Manutenibilidade | Alta |
| Testes cobrem 100% das transições | Qualidade | Alta |
| Sem breaking changes em API REST | Compatibilidade | Alta |

---

## Critérios de Aceite (Gherkin)

```gherkin
# Happy Path — Transição Válida
Quando Pedido.StatusPedido = "Recebido"
  E CanTransition("Aceito") é chamado
Então retorna true
  E Controller persiste novo status

# Falha — Transição Inválida
Quando Pedido.StatusPedido = "Entregue"
  E CanTransition("Cancelado") é chamado
Então retorna false
  E Controller retorna 400 Bad Request com motivo

# Falha — Transição Não Listada
Quando Pedido.StatusPedido = "EmPreparo"
  E POST /api/pedidos/{id}/status { novoStatus: "Recebido" }
Então retorna 400
  E response.allowed = ["ProntoParaEntrega", "Cancelado"]
```

---

## Rastreabilidade

| Arquivo | Elemento | Status |
|---------|----------|--------|
| `Infrastructure/Migrations/V016__SimplifyStatusPedido.cs` | Migration enum simplification | ✏️ Novo |
| `Shared/Enums/StatusPedido.cs` | Enum values (remove Processando, Recusado) | ✏️ Refactor |
| `Domain/Aggregates/Pedido.cs` | `Pedido.CanTransition()` | ✏️ Novo |
| `API/Controllers/PedidosController.cs:UpdateStatus()` | Validação pré-persist | ✏️ Refactor |
| `Tests/Domain/PedidoStateTransitionTests.cs` | Suite completa (cover SaiuParaEntrega) | ✏️ Novo |

---

## Open Questions

> ⚠️ Nenhuma. Escopo derivado do architecture.md. Pronto para implementação.
