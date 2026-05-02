# SDD — Algoritmo BaixarEstoque

> Gerado pelo Reversa (Redator) em 2026-05-01 | Nível: Detalhado
> Rastreabilidade: `src/BatatasFritas.API/Controllers/PedidosController.cs` (Método privado BaixarEstoque)

---

## Visão Geral

O método `BaixarEstoque` é responsável pela lógica de subtração de inventário logo após a criação de um pedido. Ele lida com a complexidade de diferenciar produtos finais (estoque direto) de produtos compostos (estoque baseado em insumos/receitas).

---

## Responsabilidades

| Responsabilidade | MoSCoW |
|---|---|
| Diferenciar produtos com e sem receita | **Must** |
| Subtrair estoque direto de produtos sem receita | **Must** |
| Calcular e subtrair consumo de insumos via receitas | **Must** |
| Registrar movimentações auditáveis de estoque para cada insumo | **Must** |
| Ativar/Desativar produtos baseados no saldo zerado | **Must** |
| Notificar front-ends sobre desativação de produtos em tempo real | **Must** |

---

## Interface

### Método Interno
`private async Task BaixarEstoque(List<NovoItemPedidoDto> itens, int pedidoId)`

- **Entradas**: Lista de itens do pedido e o ID do pedido gerado (para o motivo da movimentação).
- **Saídas**: Nenhuma (executa mutação de estado no banco de dados).

---

## Regras de Negócio

1. 🟢 **Diferenciação por Receita** — Se um produto tem ao menos um `ItemReceita`, a baixa ocorre **exclusivamente** nos insumos. O `EstoqueAtual` do produto é ignorado.
2. 🟢 **Snapshot de Insumos** — O algoritmo carrega todas as receitas (`receitaRepository.GetAllAsync()`) antes do loop para evitar múltiplas queries N+1.
3. 🟢 **Consumo Proporcional** — O consumo de um insumo é: `receita.QuantidadePorUnidade * item.Quantidade`.
4. 🟢 **Permissividade de Negativo (Insumos)** — Se um insumo não tem estoque suficiente, o sistema **permite** o saldo negativo e registra a saída normalmente. Isso evita travar pedidos se o registro de entrada de insumos estiver atrasado.
5. 🟢 **Bloqueio de Negativo (Produtos)** — Se um produto sem receita não tem estoque, o sistema lança uma `Exception` (que gera Rollback do pedido). Note: Isso deve ser capturado pelo pré-check antes, mas o método garante a integridade.
6. 🟢 **Auto-Desativação** — Se o estoque de um produto sem receita chega a 0 ou menos, o método `Desativar()` é chamado e um evento SignalR `ProdutoDesativado` é emitido.
7. 🟡 **Uso de RefreshAsync** — O código utiliza `_session.RefreshAsync(produto)` antes da baixa direta para garantir que o valor do estoque não esteja obsoleto no cache do NHibernate.

---

## Fluxo de Execução

1. Itera sobre cada item do pedido.
2. Busca o `Produto` correspondente.
3. Filtra as receitas vinculadas ao `ProdutoId`.
4. **Caminho A: Com Receita**
    - Para cada `ItemReceita`:
        - Calcula `qtdConsumida`.
        - Cria nova `MovimentacaoEstoque` (Tipo: Saída, Motivo: "Baixa automática — Pedido #X").
        - Atualiza `Insumo.EstoqueAtual`.
5. **Caminho B: Sem Receita**
    - Executa `RefreshAsync` no produto.
    - Valida estoque disponível.
    - Chama `produto.AjustarEstoque(-quantidade)`.
    - Se `estoque <= 0`, chama `Desativar()` e emite SignalR.
6. Atualiza o repositório (`Produto` ou `Insumo`).

---

## Critérios de Aceitação

```gherkin
# Happy Path — Produto sem receita (Bebida)
Dado um pedido de 2 unidades de "Coca-Cola" (estoque: 5)
Quando BaixarEstoque é executado
Então o estoque da "Coca-Cola" no banco deve ser 3

# Happy Path — Produto com receita (Batata)
Dado um pedido de 1 unidade de "Batata M"
  E a "Batata M" tem receita de 200g de "Batata Crua"
Quando BaixarEstoque é executado
Então o estoque de "Batata Crua" deve diminuir 200g
  E uma MovimentacaoEstoque de saída deve ser registrada vinculada ao PedidoId

# Cenário de Borda — Estoque de Insumo Insuficiente
Dado um insumo "Bacon" com estoque de 100g
Quando um pedido consome 150g de "Bacon"
Então o estoque de "Bacon" deve ir para -50g
  E o pedido deve ser finalizado com sucesso
```

---

## Rastreabilidade de Código

| Arquivo | Classe / Função | Cobertura |
|---|---|---|
| `src/BatatasFritas.API/Controllers/PedidosController.cs` | `BaixarEstoque` | 🟢 |
| `src/BatatasFritas.Domain/Entities/MovimentacaoEstoque.cs` | `Constructor` | 🟢 |
| `src/BatatasFritas.Infrastructure/Repositories/IRepository.cs` | `UpdateAsync` | 🟢 |
