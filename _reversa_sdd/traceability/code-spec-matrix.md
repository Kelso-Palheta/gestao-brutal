# Matriz de Rastreabilidade (Código vs Spec)

> Gerado pelo Reversa (Redator) em 2026-05-01 | Nível: Detalhado

## Cobertura de Especificação

Esta matriz mapeia os arquivos principais do projeto legado para suas respectivas especificações SDD geradas.

| Arquivo de Código | Especificação SDD | Cobertura |
|---|---|---|
| `src/BatatasFritas.Domain/Entities/Pedido.cs` | `sdd/pedido.md` | 🟢 Completa |
| `src/BatatasFritas.Domain/Entities/ItemPedido.cs` | `sdd/pedido.md` | 🟢 Completa |
| `src/BatatasFritas.Domain/Entities/CarteiraCashback.cs` | `sdd/carteira-cashback.md` | 🟢 Completa |
| `src/BatatasFritas.Domain/Entities/TransacaoCashback.cs` | `sdd/carteira-cashback.md` | 🟢 Completa |
| `src/BatatasFritas.Domain/Entities/Produto.cs` | `sdd/produto-estoque.md` | 🟢 Completa |
| `src/BatatasFritas.Domain/Entities/Insumo.cs` | `sdd/produto-estoque.md` | 🟢 Completa |
| `src/BatatasFritas.Domain/Entities/ItemReceita.cs` | `sdd/produto-estoque.md` | 🟢 Completa |
| `src/BatatasFritas.Domain/Entities/MovimentacaoEstoque.cs` | `sdd/produto-estoque.md` | 🟢 Completa |
| `src/BatatasFritas.API/Controllers/PedidosController.cs` | `sdd/pedidos-controller.md` | 🟢 Completa (Post) |
| `src/BatatasFritas.API/Controllers/PedidosController.cs` | `sdd/baixar-estoque.md` | 🟢 Completa (Privado) |
| `src/BatatasFritas.API/Services/MercadoPagoService.cs` | `sdd/mercadopago-service.md` | 🗑️ Removido (vFASE 3.5) |
| `src/BatatasFritas.API/Controllers/WebhookController.cs` | `sdd/remocao-mercadopago.md` | 🗑️ Removido |
| `(Novo Fluxo Manual)` | `sdd/pagamento-manual.md` | 🟢 Completa |
| `src/BatatasFritas.API/Hubs/PedidosHub.cs` | `sdd/kds.md` | 🟢 Completa |
| `src/BatatasFritas.Web/Pages/KdsMonitor.razor` | `sdd/kds.md` | 🟢 UI Logic |
| `src/BatatasFritas.API/Program.cs` | `sdd/auth.md` | 🟢 Configuração |
| `src/BatatasFritas.Web/Services/AuthStateProvider.cs` | `sdd/auth.md` | 🟢 UI Logic |
| `mcp_server.js` | `sdd/mcp-server.md` | 🟢 Completa |
| `src/BatatasFritas.API/Controllers/KdsController.cs` | `sdd/kds.md` | 🟡 Parcial |
| `src/BatatasFritas.Infrastructure/Mappings/*.cs` | `flowcharts/infrastructure.md` | 🟢 Mapeamentos |
| `src/BatatasFritas.Infrastructure/Repositories/*.cs` | `sdd/pedidos-controller.md` | 🟢 Uso via IRepository |
| `src/BatatasFritas.Domain/Entities/Bairro.cs` | `sdd/pedido.md` | 🟡 Referência apenas |
| `src/BatatasFritas.Domain/Entities/Complemento.cs` | `domain.md` | 🟡 Glossário apenas |
| `src/BatatasFritas.Domain/Entities/Despesa.cs` | `domain.md` | 🟡 Glossário apenas |

## Legenda
- 🟢 **Completa**: Lógica de negócio e interface totalmente documentadas.
- 🟡 **Parcial**: Documentado como dependência ou em glossário, mas sem SDD dedicada.
- — **Não Coberto**: Código sem especificação correspondente (ex: boilerplates, controllers simples de CRUD).

---

## Estatísticas de Geração (v1.1)
- **Specs SDD ativas**: 10
- **Specs depreciadas**: 1 (MercadoPago)
- **APIs documentadas**: 1 (OpenAPI atualizada)
- **User Stories criadas**: 3
- **% de Cobertura estimada**: 90% do Core Business Logic (Fluxo Manual incluído)
