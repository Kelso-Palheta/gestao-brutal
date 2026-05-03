# Dicionário de Dados — BatatasFritas

> Gerado pelo Reversa Data Master em 2026-05-01
> Fonte: FluentNHibernate Mappings | Confiança: 🟢

## Domínio: Vendas e Pagamentos

### Tabela: `Pedidos`
Armazena o cabeçalho de todas as vendas.
| Coluna | Tipo | PK/FK | Descrição |
|---|---|---|---|
| `Id` | int | PK | Auto-incremento |
| `DataHoraPedido` | datetime | - | UTC |
| `NomeCliente` | varchar | - | Nome para entrega ou retirada |
| `TelefoneCliente` | varchar | - | Chave de busca para Cashback |
| `EnderecoEntrega` | text | - | Null para Balcão/Totem |
| `BairroId` | int | FK | Referência para `Bairros` |
| `StatusPedido` | int | - | Enum (Recebido, EmPreparo, etc) |
| `StatusPagamento` | int | - | Enum (Pendente, Aprovado, Cancelado) |
| `MetodoPagamento` | int | - | Enum (Dinheiro, Cartão, Pix) |
| `ValorTotal` | decimal | - | Valor final (Itens + Taxa - Cashback) |
| `ValorCashbackUsado` | decimal | - | Valor debitado da carteira |
| `DadosPagamento` | text | - | Link MercadoPago (antigo) ou PIX Manual |

### Tabela: `ItensPedido`
Itens individuais de um pedido com snapshot de preço.
| Coluna | Tipo | PK/FK | Descrição |
|---|---|---|---|
| `Id` | int | PK | |
| `PedidoId` | int | FK | |
| `ProdutoId` | int | FK | |
| `Quantidade` | int | - | |
| `PrecoUnitario` | decimal | - | Preço praticado no momento da venda |
| `Observacao` | text | - | Ex: "Sem sal" |

---

## Domínio: Cashback

### Tabela: `CarteirasCashback`
| Coluna | Tipo | PK/FK | Descrição |
|---|---|---|---|
| `Id` | int | PK | |
| `Telefone` | varchar | - | Identificador único do cliente (Índice) |
| `SaldoAtual` | decimal | - | |
| `UltimaAtualizacao` | datetime | - | |

### Tabela: `TransacoesCashback`
| Coluna | Tipo | PK/FK | Descrição |
|---|---|---|---|
| `Id` | int | PK | |
| `CarteiraId` | int | FK | |
| `PedidoId` | int | FK | Null para ajustes manuais |
| `Valor` | decimal | - | Positivo para entrada, absoluto para saída |
| `Tipo` | int | - | Enum: Entrada (1), Saida (2) |
| `Descricao` | varchar | - | Motivo (Venda, Uso, Ajuste) |

---

## Domínio: Estoque e Produção

### Tabela: `Insumos`
| Coluna | Tipo | PK/FK | Descrição |
|---|---|---|---|
| `Id` | int | PK | |
| `Nome` | varchar | - | Ex: "Batata Asterix" |
| `UnidadeMedida` | varchar | - | kg, un, l |
| `EstoqueAtual` | decimal | - | |
| `EstoqueMinimo` | decimal | - | Gatilho para alertas |

### Tabela: `ItensReceita`
Relacionamento muitos-para-muitos entre Produto e Insumo.
| Coluna | Tipo | PK/FK | Descrição |
|---|---|---|---|
| `Id` | int | PK | |
| `ProdutoId` | int | FK | |
| `InsumoId` | int | FK | |
| `QuantidadeNecessaria` | decimal | - | Quantidade consumida por 1 un de Produto |
