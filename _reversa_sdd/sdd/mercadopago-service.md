# SDD — MercadoPagoService (Integração de Pagamentos)

> Gerado pelo Reversa (Redator) em 2026-05-01 | Nível: Detalhado
> Rastreabilidade: `src/BatatasFritas.API/Services/MercadoPagoService.cs`

---

## Visão Geral

O `MercadoPagoService` é o componente responsável por toda a comunicação com a API do Mercado Pago. Ele gerencia diferentes fluxos de pagamento (PIX Dinâmico, Checkout Pro e integração com máquinas Point Smart 2) e implementa padrões de resiliência e segurança necessários para transações financeiras.

---

## Responsabilidades

| Responsabilidade | MoSCoW |
|---|---|
| Iniciar pagamentos PIX Dinâmicos para Delivery | **Must** |
| Gerar links de pagamento para Checkout Pro | **Must** |
| Integrar com máquinas Point Smart 2 para pagamentos presenciais | **Must** |
| Validar assinaturas HMAC-SHA256 de webhooks recebidos | **Must** |
| Implementar retentativas (Polly) em chamadas à API do Point | **Must** |
| Consultar status de pagamentos de forma assíncrona | **Should** |
| Cancelar intents pendentes na maquininha Point | **Should** |

---

## Interface

### Métodos Principais

| Método | Parâmetros | Retorno |
|---|---|---|
| `IniciarPagamentoAsync` | `Pedido` | `Task` (atualiza entidade Pedido) |
| `CriarPixOnlineAsync` | `int pedidoId, decimal valor, string descricao` | `MpPaymentResultDto` |
| `CriarIntentPointAsync` | `int pedidoId, decimal valor, string tipo, string deviceId` | `MpPointIntentResultDto` |
| `ValidarAssinaturaWebhook` | `string dataId, string requestId, string signature, string secret` | `bool` |

### Configuração (`MercadoPagoOptions`)
- `AccessToken`: Token de autenticação Bearer.
- `DeviceId`: Identificador único da maquininha Point.
- `NotificationUrl`: URL para recebimento de webhooks.
- `WebhookSecret`: Segredo para validação de assinatura HMAC.

---

## Regras de Negócio e Implementação

1. 🟢 **Resiliência com Polly** — Chamadas para a API de integração com dispositivos Point possuem uma política de retentativa exponencial (3 tentativas, 1s de delay base + jitter) para lidar com falhas transitórias de rede.
2. 🟢 **Validação de Webhook (HMAC-SHA256)** — O sistema valida rigorosamente a assinatura do Mercado Pago comparando o hash recebido no header `x-signature` com um hash computado localmente a partir do `manifest` (id, request-id e timestamp).
3. 🟢 **Expiração de PIX** — Pagamentos PIX Online são gerados com expiração fixa de 30 minutos.
4. 🟢 **Estratégia por Método** — O método `IniciarPagamentoAsync` atua como uma Factory, decidindo qual API chamar baseando-se no `MetodoPagamento` do pedido.
5. 🟡 **Payer Fixo em PIX** — O e-mail do pagador para PIX está hardcoded como `cliente@batatapalhetabrutal.com` no código atual.
6. 🔴 **Divergência de Enums** — O serviço utiliza valores de `MetodoPagamento` como `PixOnline` e `PixPoint` que não estão presentes no enum central do projeto, indicando uma dessincronização entre a implementação do serviço e o domínio. 🔴 LACUNA

---

## Fluxo de Resiliência (Point Smart 2)

```mermaid
flowchart TD
    A[Chamada Point API] --> B{Erro Transitório?}
    B -- Sim --> C[Retry #1 (1s)]
    C --> D[Retry #2 (2s)]
    D --> E[Retry #3 (4s)]
    E --> F[Falha Fatal]
    B -- Não --> G[Erro Imediato]
```

---

## Requisitos Não Funcionais

| Tipo | Requisito inferido | Evidência | Confiança |
|---|---|---|---|
| Segurança | Validação de assinatura em todas as notificações | `MercadoPagoService.cs:209` | 🟢 |
| Confiabilidade | Retentativas automáticas em integrações de hardware | `MercadoPagoService.cs:44` | 🟢 |
| Performance | Uso de `HttpClient` com `BaseAddress` e headers pré-configurados | `MercadoPagoService.cs:37` | 🟢 |

---

## Critérios de Aceitação

```gherkin
# Happy Path — Validação de Webhook
Dado uma notificação do Mercado Pago com x-signature válida
Quando ValidarAssinaturaWebhook é chamado com o segredo correto
Então o retorno deve ser true

# Falha — Timeout na maquininha
Dado um erro de Timeout na chamada do Point Smart 2
Quando CriarIntentPointAsync é executado
Então o sistema deve tentar até 3 vezes antes de retornar erro ao usuário

# Happy Path — Geração de PIX
Dado um pedido válido para PIX Online
Quando IniciarPagamentoAsync é chamado
Então as propriedades MercadoPagoPaymentId, LinkPagamento e DataExpiracaoPagamento do pedido devem ser preenchidas
```

---

## Rastreabilidade de Código

| Arquivo | Classe / Função | Cobertura |
|---|---|---|
| `src/BatatasFritas.API/Services/MercadoPagoService.cs` | `MercadoPagoService` | 🟢 |
| `src/BatatasFritas.API/Services/IMercadoPagoService.cs` | Interface | 🟢 |
| `src/BatatasFritas.Shared/DTOs/MercadoPago/` | DTOs de integração | 🟢 |
