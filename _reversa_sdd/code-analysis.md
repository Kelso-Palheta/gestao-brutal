# Análise Técnica — BatatasFritas

> Gerado pelo Reversa (Arqueólogo) em 2026-05-01 | Nível: Detalhado

---

## Módulo 1: BatatasFritas.Domain

**Responsabilidade:** Núcleo do sistema (Rich Domain). Entidades de negócio puras, sem dependências externas.

### Entidades Identificadas

| Entidade | Campos-Chave | Lógica Embutida |
|---|---|---|
| `Pedido` | Status, MetodoPagamento, ValorTotal | 🟢 Cálculo de total com cashback e taxa de entrega; Máquina de estados de status |
| `CarteiraCashback` | Telefone, SaldoAtual | 🟢 Guard clauses em `UsarSaldo` e `AdicionarSaldo` |
| `Produto` | CategoriaId, EstoqueAtual, Ativo | 🟢 Soft delete via `Ativo`; `AjustarEstoque` |
| `Insumo` | EstoqueAtual, EstoqueMinimo | 🟢 Propriedade computed `AbaixoDoMinimo` |
| `MovimentacaoEstoque` | Tipo, Quantidade, ValorUnitario | 🟢 Atualização automática de estoque no construtor |
| `ItemPedido` | Quantidade, PrecoUnitario | 🟡 Sem lógica própria — pure data |
| `TransacaoCashback` | Tipo (Entrada/Saída), PedidoReferenciaId | 🟡 Ligação ao pedido é opcional |
| `Bairro` | TaxaEntrega | 🟢 Usado como Value Object de localização |

### Algoritmos Não-Triviais

#### `Pedido.ValorTotal` (computed property)
```csharp
ValorTotal => Math.Max(0, ValorTotalItens + TaxaEntrega - ValorCashbackUsado)
```
🟢 **Proteção contra valores negativos** — usa `Math.Max(0, ...)` garantindo que cashback não ultrapasse o total.

#### `Pedido.ValorElegivelCashback` (computed property)
```csharp
ValorElegivelCashback => Itens
    .Where(i => i.Produto.CategoriaId == CategoriaEnum.Batatas ||
                i.Produto.CategoriaId == CategoriaEnum.Porcoes)
    .Sum(i => i.PrecoUnitario * i.Quantidade)
```
🟢 **Regra de negócio crítica:** Cashback é ganho apenas em categorias `Batatas` (1) e `Porcoes` (3). Bebidas e Sobremesas são excluídas.

#### `CarteiraCashback.SetSaldoManual` (ajuste admin)
```csharp
var diferenca = novoValor - SaldoAtual;
var tipo = diferenca > 0 ? TipoTransacaoCashback.Entrada : TipoTransacaoCashback.Saida;
```
🟢 Garante auditabilidade: todo ajuste manual gera uma `TransacaoCashback` — nunca altera o saldo silenciosamente.

#### `MovimentacaoEstoque` — efeito colateral no construtor
```csharp
if (tipo == TipoMovimentacao.Entrada)
    insumo.AjustarEstoque(quantidade);
else if (tipo == TipoMovimentacao.Saida)
    insumo.AjustarEstoque(-quantidade);
else // Ajuste: pode ser positivo ou negativo
    insumo.AjustarEstoque(quantidade);
```
🟡 **Padrão raro:** Side effect no construtor. Criar uma `MovimentacaoEstoque` modifica o `Insumo` imediatamente.

### Máquina de Estados: `StatusPedido`

```
Recebido (1) → EmPreparo (2) → ProntoParaEntrega (3) → SaiuParaEntrega (4) → Entregue (5)
                     ↘ Cancelado (6)
Recebido (1) → Aceito (7) → [sequência normal]
```
🟡 A transição de estados não é validada no Domain — é feita diretamente pelo `KdsController`.

### Máquina de Estados: `StatusPagamento`

```
Pendente (1) → Aprovado (2)
             → Recusado (3)
             → Cancelado (4)
Presencial (10) — estado especial para pagamentos físicos
```

---

## Módulo 2: BatatasFritas.Shared

**Responsabilidade:** Contratos de comunicação entre API e Frontend.

### DTOs Principais

| DTO | Uso |
|---|---|
| `NovoPedidoDto` | Criação de pedido — suporta divisão de pagamento dupla |
| `NovoItemPedidoDto` | Item do pedido com informações de categoria e preço |
| `PedidoDetalheDto` | Retorno detalhado de um pedido para o KDS |
| `ListaPedidosDto` | Paginação de pedidos |
| `CashbackDto` | Saldo e histórico de transações de cashback |
| `DashboardAnalyticsDto` | Dados agregados para o painel gerencial |
| `FinanceiroDashboardDto` | DRE simplificado (receitas vs despesas) |
| `EstoqueDto` | Dados de estoque de insumos com alertas de mínimo |

### Enums de Domínio

| Enum | Valores |
|---|---|
| `StatusPedido` | Recebido(1), EmPreparo(2), ProntoParaEntrega(3), SaiuParaEntrega(4), Entregue(5), Cancelado(6), Aceito(7) |
| `StatusPagamento` | Pendente(1), Aprovado(2), Recusado(3), Cancelado(4), Presencial(10) |
| `MetodoPagamento` | Dinheiro(1), Cartao(2), Pix(4) |
| `CategoriaEnum` | Batatas(1), Bebidas(2), Porcoes(3), Sobremesas(4) |
| `TipoAtendimento` | Delivery(1), Balcao(2), Totem(3) |

🔴 **LACUNA:** O valor `3` do `MetodoPagamento` está ausente (pulou de 2 para 4). Provavelmente foi um método removido (ex: MercadoPago Point Smart 2). Confirmar se é intencional.

---

## Módulo 3: BatatasFritas.API

**Responsabilidade:** Porta de entrada REST. Controla transações, segurança e eventos SignalR.

### Controllers Identificados

| Controller | Responsabilidade |
|---|---|
| `PedidosController` | CRUD de pedidos, baixa de estoque, cashback |
| `KdsController` | Atualização de status de pedidos (KDS) |
| `AuthController` | Login JWT |
| `ProdutosController` | Catálogo e estoque de produtos |
| `InsumosController` | Gestão de insumos e movimentações |
| `CashbackController` | Consulta e ajuste de carteiras |
| `FinanceiroController` | Dashboard financeiro e despesas |
| `RelatoriosController` | Relatórios de vendas e analytics |
| `BairrosController` | Cadastro de bairros e taxas |
| `ComplementosController` | Complementos de produtos |
| `ConfiguracoesController` | Configurações do sistema |

### Serviços e Hubs
- `MercadoPagoService` — Integração com Pix e Point Smart 2 (com Polly retry)
- `PedidosHub` (SignalR) — Broadcast de novos pedidos e atualizações de status para KDS

---
*🟢 CONFIRMADO | 🟡 INFERIDO | 🔴 LACUNA*
