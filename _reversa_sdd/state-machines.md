# Máquinas de Estado — BatatasFritas

> Gerado pelo Reversa (Detetive) em 2026-05-01 | Nível: Detalhado

---

## Pedido.StatusPedido

```mermaid
stateDiagram-v2
    [*] --> Recebido: Pedido criado (POST /api/pedidos)

    Recebido --> Aceito: KDS aceita (operador clica em Aceitar)
    Recebido --> Cancelado: Cancelamento imediato

    Aceito --> EmPreparo: Operador inicia o preparo
    Aceito --> Cancelado: Cancelamento antes de iniciar

    EmPreparo --> ProntoParaEntrega: Preparo concluído
    EmPreparo --> Cancelado: Cancelamento durante preparo

    ProntoParaEntrega --> SaiuParaEntrega: Delivery — entregador saiu
    ProntoParaEntrega --> Entregue: Balcão/Totem — retirado no local
    ProntoParaEntrega --> Cancelado: Cancelamento tardio (raro)

    SaiuParaEntrega --> Entregue: Entrega confirmada
    SaiuParaEntrega --> Cancelado: Cancelamento em rota (raro)

    Entregue --> [*]
    Cancelado --> [*]
```

### Transições Válidas (Pedido.CanTransition())

| De | Para | Gatilho | Permitido |
|---|---|---|---|
| Recebido | Aceito | PATCH /api/kds/{id}/status | ✅ |
| Recebido | Cancelado | PATCH /api/kds/{id}/cancelar | ✅ |
| Aceito | EmPreparo | PATCH /api/kds/{id}/status | ✅ |
| Aceito | Cancelado | PATCH /api/kds/{id}/cancelar | ✅ |
| EmPreparo | ProntoParaEntrega | PATCH /api/kds/{id}/status | ✅ |
| EmPreparo | Cancelado | PATCH /api/kds/{id}/cancelar | ✅ |
| ProntoParaEntrega | SaiuParaEntrega | PATCH /api/kds/{id}/status | ✅ |
| ProntoParaEntrega | Entregue | PATCH /api/kds/{id}/status | ✅ |
| ProntoParaEntrega | Cancelado | PATCH /api/kds/{id}/cancelar | ✅ |
| SaiuParaEntrega | Entregue | PATCH /api/kds/{id}/status | ✅ |
| SaiuParaEntrega | Cancelado | PATCH /api/kds/{id}/cancelar | ✅ |
| Entregue | * | (Terminal state) | ❌ |
| Cancelado | * | (Terminal state) | ❌ |

🟢 **FASE 3.5:** State machine validation now enforced in Domain via `Pedido.CanTransition()`. Invalid transitions are blocked at the application layer.

---

## Pedido.StatusPagamento

```mermaid
stateDiagram-v2
    [*] --> Pendente: Pedido criado

    Pendente --> Aprovado: Pagamento confirmado manualmente\n(operador aprova no Dashboard ou cliente via WhatsApp)
    Pendente --> Cancelado: Pedido cancelado antes do pagamento
    Pendente --> Presencial: Método é Dinheiro ou Cartão físico

    Aprovado --> [*]
    Presencial --> [*]
    Cancelado --> [*]
```

### Notas

🟢 **FASE 3.5 (Atual):** Fluxo manual somente. MercadoPago removido. Pagamentos aprovados por:
  - **Delivery:** Operador verifica comprovante Pix via WhatsApp, clica "Aprovar" no Dashboard
  - **Totem/Balcão:** Operador recebe Order#, digita no caixa, clica "Aprovar" no Dashboard

🟢 `Presencial (valor=10)` é um estado especial para pedidos cujo pagamento é dinheiro/cartão físico — não passa por nenhum sistema de pagamento online.

🟡 **Retentativa de pagamento:** Não implementada. Se cliente não envia comprovante, operador não aprova e pedido permanece em `Pendente`.

🟡 **InfinitePay (FASE 4):** Quando credenciais forem disponíveis, nova máquina de estado será implementada com suporte a webhooks e retry automático.

---

## CarrinhoState (Frontend — Blazor)

> Não é persistido. Estado em memória do cliente durante a sessão de navegação.

```mermaid
stateDiagram-v2
    [*] --> Vazio: Sessão iniciada

    Vazio --> ComItens: AdicionarItem()
    ComItens --> ComItens: AdicionarItem() / RemoverItemPorIndice()
    ComItens --> Vazio: LimparCarrinho()

    ComItens --> [*]: Pedido submetido (POST /api/pedidos)\nLimparCarrinho() chamado após sucesso
```
