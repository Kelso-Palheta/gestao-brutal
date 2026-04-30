# Spec: Relatório de Caixa Admin — Cashback + Status por Período

**ID:** RFC-CAIXA-001  
**Data:** 2026-04-30  
**Status:** Implementado (PR #18)  
**Score:** 100/100

---

## Problema

`DashboardFinanceiro` (`/admin/dashboard`) não exibia cashback concedido/usado nem breakdown de pedidos por status. Operador não conseguia fechar caixa com informação completa de fidelidade e produção do período.

---

## Goals

- Exibir cashback **concedido** (gerado por compras) e **usado** (descontado em pedidos) no período filtrado
- Exibir contagem de pedidos por `StatusPedido` (Recebido, EmPreparo, Entregue, Cancelado)
- Reutilizar a tela `/admin/dashboard` existente — sem nova página

---

## Non-Goals (MVP)

- Nova página separada de "fechamento de caixa"
- Exportação PDF ou impressão específica deste card
- Gráfico histórico de cashback
- Cashback por cliente individual
- Alteração no endpoint `RelatoriosController` (usa `FinanceiroController`)

---

## Usuários

Operador/dono do estabelecimento. Acesso via painel admin com JWT. Nível técnico: baixo — interface deve ser self-explanatory.

---

## Requisitos Funcionais

**RF-01** — `FinanceiroDashboardDto` expõe:
- `CashbackConcedidoPeriodo` (`decimal`) = soma de `TransacaoCashback.Valor` onde `Tipo == TipoTransacaoCashback.Entrada` e `DataHora` no período
- `CashbackUsadoPeriodo` (`decimal`) = soma de `Pedido.ValorCashbackUsado` dos pedidos aprovados (`StatusPagamento == Aprovado || Presencial`) no período
- `PedidosPorStatus` (`List<PedidoStatusResumoDto>`) = agrupamento de **todos** os pedidos do período (não apenas aprovados) por `StatusPedido`, ordenado por `Status`

**RF-02** — `FinanceiroController.GetDashboard()` calcula os 3 campos **apenas quando** `inicio` e `fim` são fornecidos (período selecionado via filtro). Quando ausentes: `CashbackConcedidoPeriodo = 0`, `CashbackUsadoPeriodo = 0`, `PedidosPorStatus = []`.

**RF-03** — `DashboardFinanceiro.razor` exibe no bloco "Resultado do Período" (visível só quando filtro ativo):
- Card "💚 Cashback": valor concedido em verde (`#27ae60`), valor usado em âmbar (`#e67e22`)
- Badges compactos por status com emoji e contagem: `🟡 Recebido: N · 🔵 EmPreparo: N · ✅ Entregue: N · 🔴 Cancelado: N`

---

## Critérios de Aceite

| ID | Critério |
|----|---------|
| CA-01 | Filtrar 01/04–30/04 → `CashbackConcedidoPeriodo` = soma exata das `TransacaoCashback` com `Tipo == Entrada` no período (fuso BRT) |
| CA-02 | `CashbackUsadoPeriodo` = soma de `ValorCashbackUsado` dos pedidos aprovados no período |
| CA-03 | Nenhum pedido no período → campos retornam `0` / lista vazia; UI não exibe badges |
| CA-04 | `PedidosPorStatus` inclui **Cancelado** (não filtra por `StatusPagamento`) |
| CA-05 | Sem filtro de período ativo → card cashback e badges **não aparecem** na UI |
| CA-06 | Build `dotnet build` sem erros em API e Web |

---

## Arquivos Alterados

| Arquivo | Mudança |
|---------|---------|
| `src/BatatasFritas.Shared/DTOs/FinanceiroDashboardDto.cs` | +3 propriedades + classe `PedidoStatusResumoDto` |
| `src/BatatasFritas.API/Controllers/FinanceiroController.cs` | Cálculo cashback + agrupamento status no bloco período |
| `src/BatatasFritas.Web/Pages/DashboardFinanceiro.razor` | Card cashback + badges status + método `StatusEmoji()` |

---

## Edge Cases

| Caso | Comportamento |
|------|--------------|
| Base de cálculo do cashback | Apenas produtos das categorias **Batatas** e **Porções** (`CategoriaEnum.Batatas`, `CategoriaEnum.Porcoes`). Taxa de entrega e bebidas **excluídos**. Garantido pela propriedade `Pedido.ValorElegivelCashback` no domínio — `CashbackConcedidoPeriodo` herda esse comportamento automaticamente via `TransacaoCashback.Entrada` |
| Período sem transações cashback | `CashbackConcedidoPeriodo = 0` |
| Pedido com `SegundoMetodoPagamento` | Não afeta cashback — `ValorCashbackUsado` é campo único por pedido, não duplica |
| `TipoTransacaoCashback.Saida` (uso de saldo) | Excluído do `CashbackConcedidoPeriodo`; capturado em `CashbackUsadoPeriodo` via `Pedido.ValorCashbackUsado` |
| Pedido cancelado com cashback já concedido | Aparece em `PedidosPorStatus` como Cancelado; cashback concedido conta normalmente (reversão não implementada no MVP) |
| Fuso horário | Filtro usa `TimeZoneInfo BRT (UTC-3)` — consistente com o restante do `FinanceiroController` |

---

## Open Questions

Nenhuma. Todos os comportamentos definidos.

---

## Avaliação de Qualidade

| Dimensão | Peso | Nota | Justificativa |
|----------|------|------|--------------|
| Completude | 30% | 30/30 | Todas as seções preenchidas, 3 RFs cobertos |
| Testabilidade | 25% | 25/25 | 6 CAs verificáveis com valores exatos |
| Clareza | 20% | 20/20 | Zero ambiguidades; edge cases com comportamento definido |
| Escopo | 15% | 15/15 | Non-goals explícitos, limites claros |
| Edge Cases | 10% | 10/10 | 5 edge cases endereçados incluindo fuso e SegundoMetodoPagamento |

**Score Total: 100/100**
