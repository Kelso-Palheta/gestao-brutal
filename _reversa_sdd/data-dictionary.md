# Dicionário de Dados — BatatasFritas

> Gerado pelo Reversa (Arqueólogo) em 2026-05-01 | Nível: Detalhado

---

## `Pedido`
| Campo | Tipo | Obrigatório | Valor Padrão | Notas |
|---|---|---|---|---|
| `Id` | int | sim | auto | Herdado de EntityBase |
| `NomeCliente` | string | sim | — | — |
| `TelefoneCliente` | string | sim | — | Usado como chave da CarteiraCashback |
| `EnderecoEntrega` | string | sim | — | Vazio para Balcão/Totem |
| `BairroEntrega` | Bairro? | não | null | Nulo para Balcão/Totem |
| `TaxaEntrega` | decimal | computed | 0 | `BairroEntrega?.TaxaEntrega ?? 0` |
| `ValorTotalItens` | decimal | computed | — | Soma `PrecoUnitario * Quantidade` de todos os itens |
| `ValorElegivelCashback` | decimal | computed | — | Apenas itens de Batatas (1) e Porções (3) |
| `ValorCashbackUsado` | decimal | sim | 0 | Desconto aplicado no total |
| `ValorTotal` | decimal | computed | — | `Max(0, ValorTotalItens + TaxaEntrega - ValorCashbackUsado)` |
| `DataHoraPedido` | DateTime | sim | `DateTime.UtcNow` | UTC |
| `Status` | StatusPedido | sim | `Recebido` | Enum de status do KDS |
| `StatusPagamento` | StatusPagamento | sim | `Pendente` | Controlado pelo webhook |
| `MetodoPagamento` | MetodoPagamento | sim | — | Primário |
| `SegundoMetodoPagamento` | MetodoPagamento? | não | null | Divisão de pagamento |
| `ValorSegundoPagamento` | decimal? | não | null | Valor da segunda forma de pagamento |
| `TrocoPara` | decimal? | não | null | Apenas para Dinheiro |
| `TipoAtendimento` | TipoAtendimento | sim | `Delivery` | Delivery/Balcão/Totem |
| `LinkPagamento` | string | não | "" | URL do pagamento PIX |
| `Observacao` | string | não | "" | Anotações do operador |
| `Itens` | IList\<ItemPedido\> | sim | [] | Coleção de itens |

---

## `Produto`
| Campo | Tipo | Obrigatório | Valor Padrão | Notas |
|---|---|---|---|---|
| `Id` | int | sim | auto | — |
| `Nome` | string | sim | — | — |
| `Descricao` | string | sim | — | — |
| `CategoriaId` | CategoriaEnum | sim | — | Batatas, Bebidas, Porções, Sobremesas |
| `PrecoBase` | decimal | sim | — | — |
| `ImagemUrl` | string | não | "" | URL da imagem do produto |
| `Ativo` | bool | sim | true | Soft delete |
| `Ordem` | int | sim | 0 | Ordenação no cardápio |
| `ComplementosPermitidos` | string | não | "" | IDs delimitados por vírgula |
| `EstoqueAtual` | int | sim | 0 | — |
| `EstoqueMinimo` | int | sim | 0 | — |

---

## `Insumo`
| Campo | Tipo | Obrigatório | Valor Padrão | Notas |
|---|---|---|---|---|
| `Id` | int | sim | auto | — |
| `Nome` | string | sim | — | — |
| `Unidade` | string | sim | "un" | kg, L, un, g |
| `EstoqueAtual` | decimal | sim | 0 | Modificado por `MovimentacaoEstoque` |
| `EstoqueMinimo` | decimal | sim | 0 | — |
| `CustoPorUnidade` | decimal | sim | 0 | Custo médio |
| `Ativo` | bool | sim | true | Soft delete |
| `AbaixoDoMinimo` | bool | computed | — | `EstoqueAtual <= EstoqueMinimo` |

---

## `CarteiraCashback`
| Campo | Tipo | Obrigatório | Valor Padrão | Notas |
|---|---|---|---|---|
| `Id` | int | sim | auto | — |
| `Telefone` | string | sim | — | **Chave natural do cliente** |
| `NomeCliente` | string | sim | — | — |
| `SaldoAtual` | decimal | sim | 0 | Guard: nunca fica negativo |
| `CriadoEm` | DateTime | sim | `UtcNow` | — |
| `Transacoes` | IList\<TransacaoCashback\> | sim | [] | Histórico completo |

---

## `TransacaoCashback`
| Campo | Tipo | Obrigatório | Notas |
|---|---|---|---|
| `Id` | int | sim | — |
| `Carteira` | CarteiraCashback | sim | Parent |
| `Valor` | decimal | sim | Sempre positivo |
| `Tipo` | TipoTransacaoCashback | sim | Entrada (1) / Saída (2) |
| `Motivo` | string | sim | Texto livre de auditoria |
| `PedidoReferenciaId` | int? | não | Opcional — liga a um pedido |
| `DataHora` | DateTime | sim | `UtcNow` |

---

## `MovimentacaoEstoque`
| Campo | Tipo | Obrigatório | Notas |
|---|---|---|---|
| `Id` | int | sim | — |
| `Insumo` | Insumo | sim | — |
| `Tipo` | TipoMovimentacao | sim | Entrada(1)/Saida(2)/Ajuste(3) |
| `Quantidade` | decimal | sim | — |
| `ValorUnitario` | decimal | sim | Custo por unidade neste lote |
| `ValorTotal` | decimal | computed | `Quantidade * ValorUnitario` |
| `DataMovimentacao` | DateTime | sim | `UtcNow` |
| `Motivo` | string | sim | — |
| `Fornecedor` | string | não | "" |
| `NumeroNF` | string | não | "" | Nota Fiscal |

---

## `Bairro`
| Campo | Tipo | Obrigatório | Notas |
|---|---|---|---|
| `Id` | int | sim | — |
| `Nome` | string | sim | — |
| `TaxaEntrega` | decimal | sim | — |
| `OrdemExibicao` | int | sim | 0 — menor aparece primeiro |

---

## `ItemPedido`
| Campo | Tipo | Obrigatório | Notas |
|---|---|---|---|
| `Id` | int | sim | — |
| `Pedido` | Pedido | sim | Parent (cascade) |
| `Produto` | Produto | sim | — |
| `Quantidade` | int | sim | — |
| `PrecoUnitario` | decimal | sim | Preço no momento da venda (não muda se o produto for atualizado) |
| `Observacao` | string | não | "" |
