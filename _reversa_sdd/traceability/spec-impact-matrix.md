# Spec Impact Matrix — BatatasFritas

> Gerado pelo Reversa (Arquiteto) em 2026-05-01

## Como Ler

- **🔴 ALTO** — Mudança aqui quebra outros componentes
- **🟡 MÉDIO** — Mudança requer atenção e testes em componentes relacionados
- **🟢 BAIXO** — Mudança isolada, baixo risco de regressão

---

## Matriz de Impacto por Componente

| Componente Alterado | Impacta | Nível |
|---|---|---|
| `Pedido.cs` (Domain) | PedidosController, WebhookController, KdsController, BatatasFritas.Web (todos os DTOs de pedido), Testes de integração | 🔴 ALTO |
| `CarteiraCashback.cs` | PedidosController (cashback logic), CashbackController, DashboardAnalytics | 🔴 ALTO |
| `StatusPedido enum` | KdsController, KdsMonitor.razor, DashboardPedidos, PedidoDetalheDto, Testes | 🔴 ALTO |
| `MetodoPagamento enum` | PedidosController, MercadoPagoService, FinanceiroController, RelatoriosController, Totem.razor, Home.razor | 🔴 ALTO |
| `NHibernateRepository.cs` | Todos os Controllers (via IRepository genérico) | 🔴 ALTO |
| `NHibernateUnitOfWork.cs` | PedidosController, KdsController, todos os Controllers de escrita | 🔴 ALTO |
| `PedidosController.BaixarEstoque` | Produto, Insumo, MovimentacaoEstoque, PedidosHub (SignalR) | 🟡 MÉDIO |
| `MercadoPagoService.cs` | WebhookController, PedidosController (IniciarPagamento) | 🟡 MÉDIO |
| `PedidosHub.cs` | KdsMonitor.razor, PedidosController, KdsController | 🟡 MÉDIO |
| `DependencyInjection.cs` (Infra) | Program.cs, todos os serviços injetados | 🟡 MÉDIO |
| `Program.cs` (API) | Toda a API — middlewares, seed, migrations | 🟡 MÉDIO |
| `CarrinhoState.cs` | Home.razor, TotemCheckout.razor, CarrinhoOffcanvas.razor | 🟡 MÉDIO |
| `AuthStateProvider.cs` | Login.razor, todos os componentes protegidos com [Authorize] | 🟡 MÉDIO |
| `NovoPedidoDto.cs` | PedidosController.Post, Home.razor, TotemCheckout.razor | 🟡 MÉDIO |
| `mcp_server.js` | Agentes de IA externos (Claude Code, Antigravity) | 🟢 BAIXO |
| `Bairro.cs` | BairrosController, PedidosController (taxa de entrega) | 🟢 BAIXO |
| `Complemento.cs` | ComplementosController, CustomizadorItem.razor | 🟢 BAIXO |
| `Despesa.cs` | DespesasController, DashboardFinanceiro | 🟢 BAIXO |
| `Configuracao.cs` | ConfiguracoesController | 🟢 BAIXO |

---

## Dívidas Técnicas Identificadas

| # | Dívida | Severidade | Componente |
|---|---|---|---|
| 1 | `MercadoPagoService` referencia `MetodoPagamento.PixOnline`, `PixPoint`, `CartaoCredito`, `CartaoDebito` — valores inexistentes no enum atual | 🔴 Alta | MercadoPagoService, MetodoPagamento |
| 2 | `AuthStateProvider` sem persistência — refresh desloga o admin | 🔴 Alta | AuthStateProvider.cs |
| 3 | CORS aberto `SetIsOriginAllowed(_ => true)` em produção | 🔴 Alta | Program.cs |
| 4 | Insumo pode ficar com estoque negativo sem bloqueio | 🟡 Média | PedidosController.BaixarEstoque |
| 5 | SQL puro (`CreateSQLQuery`) em PedidosController quebrando a abstração do repositório | 🟡 Média | PedidosController |
| 6 | Ausência de validação de transições de estado no Domain (qualquer status → qualquer status) | 🟡 Média | StatusPedido, KdsController |
| 7 | RAG sources incompleto — `documentacao/`, `codigo/`, `configuracoes/` ausentes | 🟡 Média | mcp_server.js |
| 8 | Migrations SQLite-específicas (`PRAGMA`) podem quebrar em PostgreSQL puro | 🟡 Média | Program.cs |
| 9 | `KdsAuthService` com autenticação simples sem JWT — sem revogação/expiração | 🟢 Baixa | KdsAuthService.cs |
