# SDD — Pedido

> Gerado pelo Reversa (Redator) em 2026-05-01 | Nível: Detalhado
> Rastreabilidade: `src/BatatasFritas.Domain/Entities/Pedido.cs`

---

## Visão Geral

`Pedido` é a entidade central do sistema BatatasFritas. Representa uma transação completa — desde o momento em que o cliente finaliza a compra até a entrega/retirada. Agrega itens, calcula valores com taxas e cashback, e rastreia o ciclo de vida de pagamento e entrega.

---

## Responsabilidades

| Responsabilidade | MoSCoW |
|---|---|
| Agregar itens e calcular o total do pedido | **Must** |
| Aplicar desconto de cashback e garantir total ≥ 0 | **Must** |
| Calcular a taxa de entrega com base no bairro | **Must** |
| Calcular o valor elegível para acúmulo de cashback | **Must** |
| Rastrear status do pedido (KDS) | **Must** |
| Rastrear status do pagamento (MercadoPago) | **Must** |
| Suportar divisão de pagamento em dois métodos | **Should** |
| Armazenar link de pagamento PIX/Checkout Pro | **Should** |
| Registrar observações do operador | **Could** |

---

## Interface

### Construtor
```csharp
Pedido(
    string nomeCliente,
    string telefoneCliente,
    string enderecoEntrega,
    Bairro? bairroEntrega,          // null para Balcão/Totem
    MetodoPagamento metodoPagamento,
    decimal? trocoPara = null,
    TipoAtendimento tipoAtendimento = Delivery,
    decimal valorCashbackUsado = 0m,
    MetodoPagamento? segundoMetodoPagamento = null,
    decimal? valorSegundoPagamento = null
)
```

### Propriedades Computadas (sem setter público)
| Propriedade | Fórmula | Confiança |
|---|---|---|
| `TaxaEntrega` | `BairroEntrega?.TaxaEntrega ?? 0m` | 🟢 |
| `ValorTotalItens` | `Σ(item.PrecoUnitario × item.Quantidade)` | 🟢 |
| `ValorElegivelCashback` | `Σ itens[CategoriaId == Batatas ou Porcoes] (PrecoUnitario × Quantidade)` | 🟢 |
| `ValorTotal` | `Math.Max(0, ValorTotalItens + TaxaEntrega - ValorCashbackUsado)` | 🟢 |

### Métodos Públicos
| Método | Parâmetros | Efeito |
|---|---|---|
| `AdicionarItem(produto, quantidade, precoUnitario, observacao)` | Produto, int, decimal, string | Cria `ItemPedido` e adiciona à coleção |
| `AlterarStatus(novoStatus)` | StatusPedido | Atualiza o status do KDS |
| `SetLinkPagamento(link)` | string | Define o URL de pagamento PIX/Checkout |

---

## Regras de Negócio

1. 🟢 **Cashback apenas em Batatas e Porções** — `ValorElegivelCashback` filtra somente `CategoriaEnum.Batatas (1)` e `CategoriaEnum.Porcoes (3)`.
2. 🟢 **Total nunca negativo** — `Math.Max(0, ...)` garante que cashback excessivo não gera valor negativo.
3. 🟢 **Snapshot de preço** — `PrecoUnitario` é salvo em `ItemPedido` no momento da criação. Mudanças futuras no `Produto.PrecoBase` não afetam pedidos existentes.
4. 🟢 **Status inicial é sempre Recebido** — definido no construtor, não configurável externamente.
5. 🟢 **StatusPagamento inicial é sempre Pendente** — definido no construtor.
6. 🟢 **TipoAtendimento padrão é Delivery** — se não informado.
7. 🟡 **Transições de status não validadas no Domain** — `AlterarStatus` aceita qualquer `StatusPedido` sem verificar a sequência lógica. A validação fica por conta do KdsController.
8. 🟡 **BairroEntrega pode ser null** — para pedidos Balcão/Totem. TaxaEntrega retorna 0 nesses casos.

---

## Fluxo Principal — Criação

1. Construtor inicializa todos os campos obrigatórios
2. `DataHoraPedido = DateTime.UtcNow` (UTC sempre)
3. `Status = StatusPedido.Recebido`
4. `StatusPagamento = StatusPagamento.Pendente`
5. Controller chama `AdicionarItem()` para cada item do DTO
6. Controller persiste via `IRepository<Pedido>.AddAsync()`
7. NHibernate gera o `Id` no banco

## Fluxos Alternativos

- **Balcão/Totem:** `bairroEntrega = null`, `TaxaEntrega = 0`
- **Com cashback:** `valorCashbackUsado > 0` — `ValorTotal` é reduzido; CarteiraCashback é debitada *antes* de salvar o pedido
- **Pagamento duplo:** `SegundoMetodoPagamento` + `ValorSegundoPagamento` não nulos — sem validação de soma no Domain

---

## Dependências

- `ItemPedido` — coleção de itens (cascade)
- `Produto` — referenciado em cada `ItemPedido`
- `Bairro` — determina taxa de entrega
- `CategoriaEnum` — controla elegibilidade de cashback
- `StatusPedido`, `StatusPagamento`, `MetodoPagamento`, `TipoAtendimento` — enums de estado

---

## Requisitos Não Funcionais

| Tipo | Requisito inferido | Evidência | Confiança |
|---|---|---|---|
| Consistência | Pedido deve ser salvo em transação única com baixa de estoque e débito de cashback | `PedidosController.cs:102` — `_uow.BeginTransaction()` | 🟢 |
| Auditabilidade | Toda modificação de cashback gera `TransacaoCashback` | `CarteiraCashback.cs:36,48` | 🟢 |
| Imutabilidade de preço | `PrecoUnitario` não pode ser alterado após criação do `ItemPedido` | `ItemPedido.cs` — `protected set` | 🟢 |

---

## Critérios de Aceitação

```gherkin
# Happy Path — Delivery com cashback
Dado um cliente com saldo de cashback de R$ 10,00
  E um pedido com 1 Batata Suprema (R$ 35,90) + taxa de entrega R$ 5,00
  E ValorCashbackUsado = 10,00
Quando o pedido é criado
Então ValorTotal = R$ 30,90
  E ValorElegivelCashback = R$ 35,90 (apenas Batatas)
  E Status = Recebido
  E StatusPagamento = Pendente

# Cenário de Borda — Cashback maior que total
Dado um pedido com ValorTotalItens = R$ 8,00 e TaxaEntrega = R$ 0,00
  E ValorCashbackUsado = R$ 10,00
Quando o pedido é calculado
Então ValorTotal = R$ 0,00 (protegido por Math.Max)
  E não há valor negativo

# Cenário de Borda — Bebida não gera cashback
Dado um pedido com apenas 1 Coca-Cola (CategoriaEnum.Bebidas)
Quando o pedido é criado
Então ValorElegivelCashback = R$ 0,00

# Falha — Sem itens
Dado um pedido sem itens adicionados
Quando ValorTotalItens é calculado
Então ValorTotalItens = R$ 0,00 (lista vazia, Linq.Sum = 0)
  E pedido pode ser criado (sem validação mínima de itens no Domain) 🔴 LACUNA

# Cenário de Borda — Totem sem bairro
Dado um pedido com TipoAtendimento = Totem
  E bairroEntrega = null
Quando o pedido é criado
Então TaxaEntrega = R$ 0,00
  E ValorTotal = ValorTotalItens
```

---

## Cenários de Borda (Nível Detalhado)

1. **Dois métodos de pagamento com soma incorreta** — O Domain não valida que `MetodoPagamento + SegundoMetodoPagamento == ValorTotal`. Um pedido pode ser criado com valores incoerentes. 🔴 LACUNA
2. **Pedido sem telefone com cashback** — O Domain não valida o telefone; a validação está apenas no Controller. Se o Controller for bypassado, um pedido com `ValorCashbackUsado > 0` e `TelefoneCliente = ""` pode ser persistido sem debitar a carteira. 🔴 LACUNA

---

## Rastreabilidade de Código

| Arquivo | Classe / Função | Cobertura |
|---|---|---|
| `src/BatatasFritas.Domain/Entities/Pedido.cs` | `Pedido` (completo) | 🟢 |
| `src/BatatasFritas.Domain/Entities/ItemPedido.cs` | `ItemPedido` | 🟢 |
| `src/BatatasFritas.Shared/Enums/StatusPedido.cs` | `StatusPedido` | 🟢 |
| `src/BatatasFritas.Shared/Enums/StatusPagamento.cs` | `StatusPagamento` | 🟢 |
| `src/BatatasFritas.Shared/Enums/MetodoPagamento.cs` | `MetodoPagamento` | 🟢 |
| `src/BatatasFritas.Shared/DTOs/NovoPedidoDto.cs` | `NovoPedidoDto` | 🟢 |
