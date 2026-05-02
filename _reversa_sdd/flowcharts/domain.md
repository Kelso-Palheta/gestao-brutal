# Fluxograma — BatatasFritas.Domain

> Gerado pelo Reversa (Arqueólogo) em 2026-05-01 | Nível: Detalhado

## Fluxo de Criação de Pedido (Domain)

```mermaid
flowchart TD
    A([Início: Novo Pedido]) --> B[Instanciar Pedido\nnomeCliente, telefone, endereço,\nbairro, metodoPagamento...]
    B --> C[Status = Recebido\nStatusPagamento = Pendente\nDataHoraPedido = UtcNow]
    C --> D{TipoAtendimento?}
    D -- Delivery --> E[BairroEntrega preenchido\nTaxaEntrega = Bairro.TaxaEntrega]
    D -- Balcão/Totem --> F[BairroEntrega = null\nTaxaEntrega = 0]
    E --> G[AdicionarItem × N]
    F --> G
    G --> H{ValorCashbackUsado > 0?}
    H -- sim --> I[Valida saldo na CarteiraCashback\nCarteira.UsarSaldo]
    H -- não --> J[ValorTotal = Max 0\nValorTotalItens + Taxa - Cashback]
    I --> J
    J --> K([Pedido criado])
```

## Fluxo de Cálculo de Cashback (Domain)

```mermaid
flowchart TD
    A([Pedido Finalizado]) --> B[Calcular ValorElegivelCashback]
    B --> C{Itens de categoria\nBatatas ou Porções?}
    C -- sim --> D[Soma PrecoUnitario × Quantidade\ndestes itens]
    C -- não --> E[Excluídos do cálculo\nBebidas e Sobremesas]
    D --> F[ValorElegivelCashback]
    E --> F
    F --> G[Regra de Cashback\napplicada pelo Controller]
    G --> H[CarteiraCashback.AdicionarSaldo]
    H --> I[Nova TransacaoCashback tipo Entrada]
```

## Fluxo de Movimentação de Estoque (Domain)

```mermaid
flowchart TD
    A([new MovimentacaoEstoque]) --> B{Tipo?}
    B -- Entrada --> C["insumo.AjustarEstoque(+quantidade)"]
    B -- Saida --> D["insumo.AjustarEstoque(-quantidade)"]
    B -- Ajuste --> E["insumo.AjustarEstoque(quantidade)\npode ser + ou -"]
    C --> F[EstoqueAtual atualizado]
    D --> F
    E --> F
    F --> G{AbaixoDoMinimo?}
    G -- sim --> H[🔴 Alerta de Estoque Crítico]
    G -- não --> I[✅ Estoque OK]
```

## Fluxo de Máquina de Estados — StatusPedido

```mermaid
stateDiagram-v2
    [*] --> Recebido
    Recebido --> Aceito: KDS aceita pedido
    Recebido --> EmPreparo: inicia preparo direto
    Aceito --> EmPreparo
    EmPreparo --> ProntoParaEntrega
    ProntoParaEntrega --> SaiuParaEntrega: Delivery
    ProntoParaEntrega --> Entregue: Balcão
    SaiuParaEntrega --> Entregue
    Recebido --> Cancelado
    EmPreparo --> Cancelado
```

## Fluxo de Máquina de Estados — StatusPagamento

```mermaid
stateDiagram-v2
    [*] --> Pendente
    Pendente --> Aprovado: Webhook MercadoPago OK
    Pendente --> Recusado: Webhook MercadoPago falha
    Pendente --> Cancelado: Pedido cancelado
    Pendente --> Presencial: Pagamento manual (Dinheiro/Cartão)
```
