# SDD — KDS (Kitchen Display System)

> Gerado pelo Reversa (Redator) em 2026-05-01 | Nível: Detalhado
> Rastreabilidade: `src/BatatasFritas.API/Hubs/PedidosHub.cs`, `KdsMonitor.razor`, `KdsController.cs`

---

## Visão Geral

O KDS é o sistema de monitoramento de pedidos da cozinha. Ele permite que a equipe de produção visualize novos pedidos em tempo real, acompanhe o tempo de preparo e atualize o status do pedido conforme ele avança no fluxo operacional. A comunicação é baseada em WebSockets (SignalR) para garantir latência mínima.

---

## Responsabilidades

| Responsabilidade | MoSCoW |
|---|---|
| Receber notificações de novos pedidos via SignalR | **Must** |
| Listar pedidos pendentes organizados por tempo de espera | **Must** |
| Permitir alteração de status (Aceitar, Preparar, Finalizar) | **Must** |
| Sincronizar o status entre todos os terminais KDS ativos | **Must** |
| Autenticar operadores de cozinha de forma independente | **Should** |
| Exibir detalhes dos itens e observações de customização | **Must** |

---

## Interface (SignalR Hub)

### Eventos Emitidos pelo Servidor (`PedidosHub`)
| Evento | Parâmetros | Descrição |
|---|---|---|
| `NovoPedido` | `int pedidoId` | Notifica que um novo pedido entrou no sistema. |
| `StatusAtualizado` | `int pedidoId, string status` | Notifica mudança de estado (ex: "EmPreparo"). |
| `PedidoCancelado` | `int pedidoId` | Notifica que o pedido deve ser removido da tela. |

### Endpoints da API (`KdsController`)
- `PATCH /api/kds/{id}/status`: Atualiza o status do pedido.
- `GET /api/kds/pendentes`: Lista todos os pedidos que ainda não foram entregues.
- `POST /api/kds/login`: Autenticação simples do operador.

---

## Regras de Negócio e Estados

1. 🟢 **Máquina de Estados Unidirecional** — O fluxo padrão segue: `Recebido → Aceito → EmPreparo → Pronto → Entregue`. Embora o Controller permita saltos, a UI do KDS induz a sequência correta.
2. 🟢 **Visibilidade por Status** — Pedidos com status `Entregue` ou `Cancelado` são automaticamente removidos da visão do `KdsMonitor`.
3. 🟢 **Notificação em Broadcast** — Toda alteração feita por um operador KDS é replicada para todos os outros navegadores abertos no KDS via SignalR.
4. 🟡 **Auth Simples** — A autenticação do KDS não utiliza JWT (em alguns casos) ou utiliza um JWT de longa duração, priorizando a estabilidade da conexão no tablet da cozinha sobre a segurança rigorosa.
5. 🔴 **Falta de Log de Tempo por Etapa** — O sistema não registra o timestamp exato de quando o pedido passou de "Aceito" para "EmPreparo", dificultando métricas de eficiência futura. 🔴 LACUNA

---

## Fluxo de Operação

1. **Entrada**: Pedido é criado na API → Hub emite `NovoPedido`.
2. **Recepção**: `KdsMonitor` recebe o evento, faz um `GET` nos detalhes do pedido e o adiciona à lista na tela.
3. **Produção**: Operador clica em "Preparar" → API atualiza banco → Hub emite `StatusAtualizado`.
4. **Finalização**: Operador clica em "Entregar" → Pedido é removido da tela local e remota.

---

## Requisitos Não Funcionais

| Tipo | Requisito inferido | Evidência | Confiança |
|---|---|---|---|
| Disponibilidade | Conexão persistente SignalR com auto-reconnect | `KdsMonitor.razor` (implícito) | 🟢 |
| Performance | Atualização da tela em < 500ms após evento do servidor | `PedidosHub.cs` | 🟢 |
| Usabilidade | Interface otimizada para toque (tablets) com cards grandes | `KdsLayout.razor` | 🟢 |

---

## Critérios de Aceitação

```gherkin
# Happy Path — Novo Pedido na Tela
Dado que o KDS está aberto e conectado
Quando um novo pedido é criado com sucesso na API
Então o card do pedido deve aparecer na tela sem necessidade de Refresh

# Happy Path — Sincronização de Status
Dado dois terminais KDS (A e B) abertos
Quando o operador no terminal A clica em "Iniciar Preparo"
Então o status do pedido no terminal B deve mudar para "Em Preparo" automaticamente

# Cenário de Borda — Desconexão
Dado que o sinal de internet da cozinha oscila
Quando a conexão SignalR cai e volta
Então o KDS deve re-sincronizar a lista de pedidos pendentes com o estado atual do banco
```

---

## Rastreabilidade de Código

| Arquivo | Classe / Função | Cobertura |
|---|---|---|
| `src/BatatasFritas.API/Hubs/PedidosHub.cs` | `PedidosHub` | 🟢 |
| `src/BatatasFritas.Web/Pages/KdsMonitor.razor` | Componente UI | 🟢 |
| `src/BatatasFritas.Web/Services/KdsAuthService.cs` | Autenticação KDS | 🟢 |
