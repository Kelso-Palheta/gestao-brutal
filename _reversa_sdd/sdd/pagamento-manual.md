# SDD — Manual Payment Flow (Sem MercadoPago)

> Gerado pelo sdd-spec em 2026-05-01 | FASE 3.5 | Nível: Detalhado
> Rastreabilidade: `src/BatatasFritas.Web/Pages/Checkout.razor`, `src/BatatasFritas.Domain/Aggregates/Pedido.cs`, `src/BatatasFritas.API/Controllers/PedidosController.cs`

---

## Problem

Hoje MercadoPagoService é hardcoded em Checkout → API de pagamento. Se Mercado Pago cai, checkout falha. Polly retry + Recusado state cause deadlock (sem fluxo de retry automático). Checkout acoplado = difícil testar, difícil mudar. 

FASE 3.5: simplificar para operador APROVAR manualmente. Checkout apenas MOSTRA informação de pagamento, sem API calls.

---

## Goals

- RF-01: Checkout.razor não chama nenhuma API de pagamento (MercadoPago, Point, etc).
- RF-02: Delivery: mostra **Pix #** (números) + botão WhatsApp (envia comprovante).
- RF-03: Totem/Balcão: mostra apenas **Order #** (cliente lê em tela, operador digita no caixa).
- RF-04: StatusPagamento simplificado: `Pendente` (default) → `Aprovado` (admin aprova manual).
- RF-05: Nenhum Polly retry, nenhum webhook, nenhuma integração externa no fluxo de pagamento.
- RF-06: Checkout UX idêntica ao original (mesmos botões, mesma order summary).

---

## Design

### Payment Display por Canal

#### Delivery
```razor
@if (pedido.TipoEntrega == "Delivery" && pedido.StatusPagamento == StatusPagamento.Pendente)
{
    <div class="payment-section">
        <h3>Comprovante de Pagamento (PIX)</h3>
        <p>PIX Dinâmico (copia e cola):</p>
        <code>00020126580014br.gov.bcb.brcode01051.0.063047d6d6-03f4-4e8d-91bf-e4fc8a8eef4e520400005303986540510.005802BR5913PALHETA BRUTAL6009Sao Paulo62410503***630440A1D</code>
        <button @onclick="AbrirWhatsApp">📲 Enviar Comprovante via WhatsApp</button>
        <p class="hint">PIX válido por 30 minutos. Após confirmar pagamento, clique no botão acima.</p>
    </div>
}
```

**Comportamento:**
- Pedido criado com `StatusPagamento = Pendente`.
- PIX Dinâmico **HARDCODED STRING** (ou gerado offline, não via MercadoPago API).
- Botão WhatsApp abre: `https://wa.me/5585987654321?text=Oi!%20Pedido%20${pedidoId}:%20Comprante%20%28screenshot%29`
- Cliente envia print do PIX confirmado.
- Operador aprova manual no Dashboard.

#### Totem / Balcão
```razor
@if ((pedido.TipoEntrega == "Totem" || pedido.TipoEntrega == "Balcao") && pedido.StatusPagamento == StatusPagamento.Pendente)
{
    <div class="payment-section">
        <h2>Seu Pedido</h2>
        <p style="font-size: 2em; font-weight: bold;">#@pedido.Id</p>
        <p>Mostre este número no caixa.</p>
    </div>
}
```

**Comportamento:**
- Pedido criado com `StatusPagamento = Pendente`.
- Tela mostra Order #.
- Cliente lê número, operador digita na maquina de caixa (não integrada).
- Operador marca como pago no caixa → alguém aprova em Dashboard.

---

### StatusPagamento Enum (Simplificado)

**Antes (MercadoPago):**
```csharp
public enum StatusPagamento { Pendente, Processando, Aprovado, Recusado, Cancelado }
```

**Depois (Manual):**
```csharp
public enum StatusPagamento { Pendente, Aprovado, Cancelado }
```

**Remover:**
- `Processando` (não há processing assíncrono).
- `Recusado` (operador rejeita = Cancelado, operador aprova = Aprovado, não há retry).

---

### IPaymentService Abstraction

```csharp
public interface IPaymentService
{
    Task<PaymentInitiationResult> InitiatePaymentAsync(Pedido pedido);
}

public class ManualPaymentService : IPaymentService
{
    public Task<PaymentInitiationResult> InitiatePaymentAsync(Pedido pedido)
    {
        // Retorna resultado com Pix# (se Delivery) ou Order# (se Totem).
        // Nenhuma chamada à API.
        
        var pixNumber = GeneratePixOffline(pedido.ValorTotal); // Hardcoded ou algoritmo local
        return Task.FromResult(new PaymentInitiationResult 
        { 
            Success = true,
            PaymentMethod = pedido.TipoEntrega == "Delivery" ? "PixManual" : "TotemCaixa",
            DisplayData = new { pixNumber, orderId = pedido.Id }
        });
    }
}
```

**Checkout.razor novo flow:**
```csharp
@code {
    [Inject] IPaymentService PaymentService { get; set; }
    
    private async Task ConfirmarPedido()
    {
        var pedido = new Pedido 
        { 
            TipoEntrega = selectedChannel, // Delivery, Totem, Balcao
            StatusPagamento = StatusPagamento.Pendente // sempre inicia Pendente
        };
        
        await PedidosController.CreateAsync(pedido); // POST /api/pedidos
        
        var paymentResult = await PaymentService.InitiatePaymentAsync(pedido);
        // paymentResult.DisplayData tem PixNumber ou OrderId
        
        // Render success screen com Pix# ou Order#
        ShowPaymentConfirmation(paymentResult.DisplayData);
    }
}
```

---

## Edge Cases + Comportamento

### E-01: PIX Expira (30 min)
- **Quando:** Cliente não confirma em 30 min.
- **Então:** Pedido fica em `Pendente` indefinidamente. Operador vê em Dashboard, marca como `Cancelado`.
- **Por quê:** Manual flow. Sem automação de expiry.

### E-02: Cliente Envia Comprovante Falso
- **Quando:** Cliente manda print de outro PIX (de amigo).
- **Então:** Operador revisa, rejeita em Dashboard. Pedido → `Cancelado`.
- **Por quê:** Manual verification. Confiança no operador.

### E-03: Network Falha Após Confirmar
- **Quando:** Cliente clica "Enviar Comprovante" mas conexão cai. WhatsApp não abre.
- **Então:** UI mostra erro "Tente novamente" (retry botão).
- **Por quê:** Usuário trata como manual action. Sem estado transiente no servidor.

### E-04: Totem: Operador Não Vê Pedido
- **Quando:** Pedido criado, cliente sai da tela Totem antes de falar com caixa.
- **Então:** Pedido fica `Pendente` em DB. Operador nunca aprova.
- **Por quê:** Manual process. Operador responsável por aprovar em caixa/Dashboard.

### E-05: Pedido Cancelado, Cliente Quer Reabrir
- **Quando:** Admin cancela `Pendente → Cancelado`. Cliente quer tentar novamente.
- **Então:** Novo pedido. Não há retry automático.
- **Por quê:** Manual flow. Simplidade.

---

## Requisitos Não-Funcionais

| Requisito | Tipo | Prioridade |
|-----------|------|-----------|
| Checkout renderiza sem API calls | Performance | Alta |
| Pix#/Order# visível em < 100ms | Performance | Alta |
| WhatsApp button clicável em mobile | UX | Alta |
| Sem dependency em serviço externo de pagamento | Resiliência | Alta |

---

## Critérios de Aceite (Gherkin)

```gherkin
# Happy Path — Delivery
Dado cliente escolhe "Delivery"
Quando clica "Confirmar Pedido"
Então Pedido.StatusPagamento = Pendente
  E tela mostra "PIX: 00020126..."
  E botão WhatsApp disponível

# Happy Path — Totem
Dado cliente interage com Totem
Quando clica "Confirmar"
Então Pedido.StatusPagamento = Pendente
  E tela mostra "Pedido #12345"
  E sem botão WhatsApp (Totem não usa)

# Admin Approval
Dado Pedido.StatusPagamento = Pendente
Quando admin clica "Aprovar Pagamento" no Dashboard
Então Pedido.StatusPagamento = Aprovado
  E pedido segue para KDS

# Admin Rejection
Dado Pedido.StatusPagamento = Pendente
Quando admin clica "Cancelar" no Dashboard
Então Pedido.StatusPagamento = Cancelado
  E cliente notificado (out of scope FASE 3.5)
```

---

## Rastreabilidade

| Arquivo | Elemento | Status |
|---------|----------|--------|
| `Web/Pages/Checkout.razor` | Display + WhatsApp button | ✏️ Refactor |
| `API/Controllers/PedidosController.cs` | Criar Pedido com Pendente | ✏️ Refactor |
| `Domain/Aggregates/Pedido.cs` | StatusPagamento enum (Pendente, Aprovado, Cancelado) | ✏️ Refactor |
| `Services/IPaymentService.cs` | Interface abstrata | ✏️ Novo |
| `Services/ManualPaymentService.cs` | Implementação stub | ✏️ Novo |
| `Tests/Web/CheckoutPaymentTests.cs` | Testes de renderização | ✏️ Novo |

---

## Decisões Finais

✅ **PIX Dinâmico**: Hardcoded **placeholder string** para FASE 3.5 (ex: `"00020126..."`). Não é gerado dinamicamente. Quando InfinitePay chegar (FASE futura), integração real substitui placeholder.

✅ **Notificação Cliente**: Out of scope FASE 3.5. Dashboard apenas. SMS/Email = próxima feature.

✅ **Migration**: StatusPagamento enum simplificado → V016 migration (pedido-state-machine.md coordena).
