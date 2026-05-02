# SDD — Produto e Controle de Estoque

> Gerado pelo Reversa (Redator) em 2026-05-01 | Nível: Detalhado
> Rastreabilidade: `src/BatatasFritas.Domain/Entities/Produto.cs`, `Insumo.cs`, `MovimentacaoEstoque.cs`

---

## Visão Geral

O sistema de estoque do BatatasFritas opera em dois níveis: **Produto Direto** (para itens sem receita, ex: Bebidas) e **Insumos** (para itens com receita, ex: Batatas). A entidade `Produto` gerencia seu próprio estoque ou delega para a entidade `Insumo` através de `ItemReceita`. Toda movimentação física de insumos é registrada de forma auditável em `MovimentacaoEstoque`.

---

## Responsabilidades

| Responsabilidade | MoSCoW |
|---|---|
| Manter nível de estoque atual do produto (sem receita) | **Must** |
| Desativar produto automaticamente ao zerar estoque | **Must** |
| Controlar estoque de insumos via receitas (ItemReceita) | **Must** |
| Registrar movimentações de entrada e saída de insumos | **Must** |
| Calcular custo de movimentação baseado no preço unitário | **Must** |
| Emitir alertas SignalR em alterações críticas de estoque | **Should** |
| Bloquear venda de produto sem estoque no checkout | **Should** |

---

## Interface

### Produto.cs
| Método | Parâmetros | Efeito |
|---|---|---|
| `AjustarEstoque(quantidade)` | `int` | Soma/Subtrai do `EstoqueAtual`. Se ≤ 0, chama `Desativar()`. |
| `Ativar()` / `Desativar()` | - | Altera flag `Ativo`. |

### Insumo.cs
| Método | Parâmetros | Efeito |
|---|---|---|
| `AjustarEstoque(quantidade)` | `decimal` | Atualiza `EstoqueAtual` do insumo. |

### MovimentacaoEstoque.cs (Construtor)
```csharp
MovimentacaoEstoque(
    Insumo insumo,
    TipoMovimentacao tipo,
    decimal quantidade,
    decimal valorUnitario,
    string motivo,
    string fornecedor = "",
    string numeroNF = ""
)
```

---

## Regras de Negócio

1. 🟢 **Dualidade de Controle** — Se um produto possui `ItemReceita`, o campo `Produto.EstoqueAtual` é ignorado. A baixa ocorre nos `Insumos` vinculados.
2. 🟢 **Auto-Desativação** — Ao atingir `EstoqueAtual <= 0`, o método `AjustarEstoque` do `Produto` chama `Desativar()`, tornando o item invisível no cardápio.
3. 🟢 **Cálculo de Custo** — Toda `MovimentacaoEstoque` deve registrar o `valorUnitario` para possibilitar auditoria financeira e cálculo de CMVs futuros.
4. 🟢 **Auditabilidade de Insumos** — O estoque de `Insumo` não deve ser alterado sem uma `MovimentacaoEstoque` correspondente.
5. 🟡 **Estoque Negativo de Insumos** — Diferente do Produto, o Insumo **permite** estoque negativo para não travar a operação da cozinha se o admin esquecer de registrar a entrada.
6. 🔴 **Falta de Validação em Receitas** — Não há validação se a soma de insumos em uma receita é coerente com o rendimento do produto. 🔴 LACUNA

---

## Fluxo Principal — Baixa de Estoque no Pedido

1. `PedidosController` recebe novo pedido.
2. Para cada item:
    - Se **NÃO tem receita**: Chama `produto.AjustarEstoque(-quantidade)`. Se zerar, emite `ProdutoDesativado` via SignalR.
    - Se **TEM receita**: Itera `ItemReceita`, calcula `consumo = quantidade * receita.QuantidadePorUnidade`.
    - Cria `MovimentacaoEstoque` de Saída para cada insumo.
    - Chama `insumo.AjustarEstoque(-consumo)`.
3. Persiste via UnitOfWork.

---

## Requisitos Não Funcionais

| Tipo | Requisito inferido | Evidência | Confiança |
|---|---|---|---|
| Disponibilidade | Notificação em tempo real via SignalR ao desativar produto | `PedidosController.cs:225` | 🟢 |
| Integridade | Baixa de estoque e criação de pedido devem estar na mesma transação | `PedidosController.cs:102` | 🟢 |

---

## Critérios de Aceitação

```gherkin
# Happy Path — Baixa de produto simples (Refrigerante)
Dado um produto "Coca-Cola" com estoque 10
Quando um pedido de 2 unidades é finalizado
Então o estoque deve ser 8
  E o produto deve continuar Ativo

# Happy Path — Baixa de insumo via Receita
Dado um produto "Batata P" com receita de 200g de "Insumo Batata"
  E estoque do insumo de 1kg
Quando um pedido de 3 unidades de "Batata P" é feito
Então o estoque do "Insumo Batata" deve ser 400g
  E uma MovimentacaoEstoque de saída de 600g deve ser criada

# Cenário de Borda — Produto zerado
Dado um produto "Cerveja" com estoque 1
Quando um pedido de 1 unidade é finalizado
Então o estoque deve ser 0
  E o produto deve ser marcado como Ativo = false
  E um evento SignalR "ProdutoDesativado" deve ser emitido
```

---

## Rastreabilidade de Código

| Arquivo | Classe / Função | Cobertura |
|---|---|---|
| `src/BatatasFritas.Domain/Entities/Produto.cs` | `Produto` | 🟢 |
| `src/BatatasFritas.Domain/Entities/Insumo.cs` | `Insumo` | 🟢 |
| `src/BatatasFritas.Domain/Entities/MovimentacaoEstoque.cs` | `MovimentacaoEstoque` | 🟢 |
| `src/BatatasFritas.API/Controllers/PedidosController.cs` | `BaixarEstoque` | 🟢 |
