# Domínio — BatatasFritas

> Gerado pelo Reversa (Detetive) em 2026-05-01 | Nível: Detalhado

---

## Glossário de Domínio

| Termo | Definição |
|---|---|
| **Pedido** | Unidade central de transação. Criado no checkout (Delivery, Balcão ou Totem), rastreado até entrega. |
| **Item do Pedido** | Produto + quantidade + preço snapshot no momento da venda. Preço não muda se o produto for atualizado depois. |
| **KDS** | Kitchen Display System — tela da cozinha que mostra os pedidos em tempo real via SignalR. |
| **Cashback** | Sistema de fidelidade: o cliente ganha saldo sobre compras de Batatas e Porções. Pode usar o saldo como desconto em compras futuras. |
| **Carteira de Cashback** | Conta virtual do cliente, identificada pelo telefone (apenas dígitos). Um cliente = uma carteira. |
| **Complemento** | Adicional de um produto (molho, ingrediente extra, remoção). Pode ser gratuito ou pago. |
| **Insumo** | Ingrediente/componente controlado por estoque em peso ou volume (kg, L, g, un). |
| **Receita** | Mapeamento `Produto → Insumo + Quantidade`. Define quais insumos são consumidos por unidade vendida. |
| **Movimentação de Estoque** | Registro auditável de toda entrada, saída ou ajuste de insumo — inclui custo por unidade e nota fiscal. |
| **Despesa** | Custo operacional do negócio (aluguel, gás, embalagem) — separado do custo de insumos. |
| **TipoAtendimento** | Contexto de onde o pedido foi criado: Delivery (com entrega), Balcão (retirada no local), Totem (self-service). |
| **Split Payment** | Divisão de um pedido em dois métodos de pagamento simultâneos (ex: R$ 20 em Pix + R$ 30 em Dinheiro). |

---

## Regras de Negócio Identificadas

### 🟢 Cashback — Elegibilidade por Categoria
> **Regra:** Cashback só é acumulado sobre produtos das categorias **Batatas (1)** e **Porções (3)**. Bebidas e Sobremesas são excluídas.
> **Onde:** `Pedido.ValorElegivelCashback` (Domain)
> **Evidência:** Comentário inline no código `// Novo: Valor elegível para cashback (apenas produtos de categoria Batatas, Porções ou complementos pagos)`

### 🟢 Cashback — Proteção de Saldo Negativo
> **Regra:** Não é possível usar mais cashback do que o saldo disponível. Se o cliente tentar, a operação é bloqueada com exceção.
> **Onde:** `CarteiraCashback.UsarSaldo` — guard clause `if (SaldoAtual < valor) throw InvalidOperationException`

### 🟢 Valor do Pedido — Proteção Contra Negativo
> **Regra:** O total de um pedido nunca pode ser negativo, mesmo que o cashback supere o valor dos itens.
> **Onde:** `Pedido.ValorTotal => Math.Max(0, ValorTotalItens + TaxaEntrega - ValorCashbackUsado)`

### 🟢 Estoque — Produto Sem Receita vs Com Receita
> **Regra:** Produtos com receita de insumos têm estoque controlado pelos **insumos** — o campo `Produto.EstoqueAtual` não é decrementado nesses casos. Apenas produtos sem receita usam o estoque direto do produto.
> **Onde:** `PedidosController.BaixarEstoque`

### 🟢 Desativação Automática por Estoque Zero
> **Regra:** Quando o estoque de um produto (sem receita) chega a zero, o produto é automaticamente desativado e o KDS recebe um evento `ProdutoDesativado` via SignalR.
> **Onde:** `PedidosController.BaixarEstoque` — `if (produto.EstoqueAtual <= 0) produto.Desativar()`

### 🟡 Soft Delete Universal
> **Regra inferida:** `Produto` e `Insumo` usam `Ativo = false` em vez de exclusão física. Isso preserva histórico de pedidos.
> **Evidência:** Propriedade `Ativo` em ambas as entidades + método `Desativar()`

### 🟡 Snapshot de Preço
> **Regra inferida:** O `PrecoUnitario` é salvo no `ItemPedido` no momento da criação. Se o preço do produto mudar depois, os pedidos antigos não são afetados.
> **Evidência:** `ItemPedido.PrecoUnitario` separado de `Produto.PrecoBase`

### 🟡 Insumo Pode Ficar Negativo
> **Regra inferida (por ausência):** Quando o estoque de insumo é insuficiente para a baixa, o sistema **permite** ir para negativo e registra sem lançar exceção. Pode ser uma decisão pragmática para não bloquear vendas.
> **Evidência:** `else { insumo.AjustarEstoque(-qtdConsumida); }` sem throw

### 🟢 Seed Automático no Startup
> **Regra:** Na primeira execução (banco vazio), o sistema cria automaticamente: 3 produtos padrão, 5 bairros de entrega e 6 complementos básicos.
> **Onde:** `Program.cs` — bloco de seed no startup

### 🟢 Migração Manual em Startup
> **Regra:** O sistema aplica migrations via `SchemaUpdate` (NHibernate) e também migrações SQL customizadas (ADD COLUMN) no startup. Isso permite deploy sem downtime em produção.
> **Onde:** `DependencyInjection.cs` (`SchemaUpdate`) + `Program.cs` (PRAGMA + ALTER TABLE)

### 🟡 JWT SignalR via Query String
> **Regra:** O token JWT pode ser passado como `?access_token=...` na query string para conexões SignalR (websocket não suporta headers customizados nativamente).
> **Evidência:** `Program.cs` — `OnMessageReceived` com `ctx.Request.Query["access_token"]`

### 🟢 CORS Aberto em Desenvolvimento
> **Regra:** O CORS está configurado com `SetIsOriginAllowed(origin => true)` + `AllowCredentials()`. Aceita qualquer origem.
> **Risco:** Em produção, isso deve ser restrito às origens conhecidas (domínio do Blazor e do Totem).
