# Fluxograma por Função — Pedido.ValorTotal

> Nível Detalhado: fluxograma por função principal com lógica não-trivial

## `Pedido.ValorTotal` — Cálculo Final com Proteção Negativa

```mermaid
flowchart TD
    A([Acesso a ValorTotal]) --> B["ValorTotalItens = Σ(PrecoUnitario × Quantidade)"]
    B --> C["TaxaEntrega = BairroEntrega?.TaxaEntrega ?? 0"]
    C --> D["Subtotal = ValorTotalItens + TaxaEntrega - ValorCashbackUsado"]
    D --> E{"Subtotal < 0?"}
    E -- sim --> F["Retorna 0\n(cashback maior que total)"]
    E -- não --> G["Retorna Subtotal"]
```

## `Pedido.ValorElegivelCashback` — Filtro por Categoria

```mermaid
flowchart TD
    A([Calcular Elegível]) --> B["Iterar todos os Itens"]
    B --> C{"CategoriaId == Batatas\nOU CategoriaId == Porcoes?"}
    C -- sim --> D["Acumula: PrecoUnitario × Quantidade"]
    C -- não --> E["Ignora item\n(Bebidas e Sobremesas)"]
    D --> F["Soma total elegível"]
    E --> F
    F --> G([Retorna ValorElegivelCashback])
```

## `CarteiraCashback.UsarSaldo` — Guard Clauses

```mermaid
flowchart TD
    A([UsarSaldo chamado]) --> B{"valor <= 0?"}
    B -- sim --> C[Lança ArgumentException]
    B -- não --> D{"SaldoAtual < valor?"}
    D -- sim --> E[Lança InvalidOperationException\nSaldo insuficiente]
    D -- não --> F["SaldoAtual -= valor"]
    F --> G["Cria TransacaoCashback\nTipo = Saída"]
    G --> H([✅ OK])
```

## `CarteiraCashback.SetSaldoManual` — Ajuste Auditável

```mermaid
flowchart TD
    A([SetSaldoManual chamado]) --> B["diferenca = novoValor - SaldoAtual"]
    B --> C{"diferenca == 0?"}
    C -- sim --> D([Retorna sem ação])
    C -- não --> E{"diferenca > 0?"}
    E -- sim --> F["tipo = Entrada"]
    E -- não --> G["tipo = Saída"]
    F --> H["SaldoAtual = novoValor\nCria TransacaoCashback com Math.Abs(diferenca)"]
    G --> H
    H --> I([✅ Ajuste auditado])
```
