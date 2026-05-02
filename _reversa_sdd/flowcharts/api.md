# Fluxograma — BatatasFritas.API

> Gerado pelo Reversa (Arqueólogo) em 2026-05-01 | Nível: Detalhado

## Fluxo Completo: POST /api/pedidos

```mermaid
flowchart TD
    A([POST /api/pedidos]) --> B[Pré-check de Estoque\nantes da transação]
    B --> C{Produto tem receita\nde insumos?}
    C -- sim --> D[Validação ignorada\npelo controller\nInsumos validados na baixa]
    C -- não --> E[SELECT estoque_atual FROM produtos]
    E --> F{estoque < quantidade?}
    F -- sim --> G[BadRequest\nEstoque insuficiente]
    F -- não --> D
    D --> H[Busca Bairro pelo ID]
    H --> I[Instancia Pedido\nStatus=Recebido\nStatusPagamento=Pendente]
    I --> J[AdicionarItem × N]
    J --> K[BeginTransaction]
    K --> L{ValorCashbackUsado > 0?}
    L -- sim --> M[Busca CarteiraCashback\npor telefone limpo\nsomente dígitos]
    M --> N{Saldo suficiente?}
    N -- não --> O[Lança Exception\nFaz Rollback → BadRequest]
    N -- sim --> P[carteira.UsarSaldo\nUpdateAsync]
    P --> Q[SaveAsync pedido\nID gerado pelo NHibernate]
    L -- não --> Q
    Q --> R[Atualiza PedidoReferenciaId\nna TransacaoCashback]
    R --> S[BaixarEstoque]
    S --> T[CommitAsync]
    T --> U["SignalR: hub.SendAsync('NovoPedido', pedidoId)"]
    U --> V[Ok com PedidoId + Status + LinkPagamento]
```

## Fluxo: BaixarEstoque (função privada)

```mermaid
flowchart TD
    A([BaixarEstoque chamado]) --> B[Para cada item do pedido]
    B --> C[Busca receitas do produto\nvia receitaRepository.GetAllAsync]
    C --> D{Tem receitas\nde insumos?}
    D -- sim --> E[Para cada receita\nqtdConsumida = receita.QuantidadePorUnidade × item.Quantidade]
    E --> F{insumo.EstoqueAtual >= qtdConsumida?}
    F -- sim --> G["Cria MovimentacaoEstoque (Saida)\nAtualiza insumo via NHibernate"]
    F -- não --> H["AVISO: vai para negativo\nAtualiza insumo mesmo assim\n🔴 sem erro — estoque negativo permitido"]
    D -- não --> I[session.RefreshAsync produto\npara evitar cache stale]
    I --> J{produto.EstoqueAtual >= item.Quantidade?}
    J -- sim --> K["produto.AjustarEstoque(-quantidade)\nUpdateAsync"]
    K --> L{EstoqueAtual <= 0?}
    L -- sim --> M["produto.Desativar()\nSignalR: ProdutoDesativado"]
    L -- não --> N[OK]
    J -- não --> O[Lança Exception\nEstoque insuficiente]
```

## Fluxo: MercadoPagoService — Polly Retry (Point Smart 2)

```mermaid
flowchart TD
    A([CriarIntentPointAsync ou\nCancelarIntentPointAsync]) --> B[_pointRetryPipeline.ExecuteAsync]
    B --> C[HTTP POST/DELETE para API MP]
    C --> D{Sucesso?}
    D -- sim --> E[Retorna resultado]
    D -- não --> F{HttpRequestException\nou TaskCanceledException?}
    F -- sim --> G{Tentativa < 3?}
    G -- sim --> H[Backoff Exponencial\n1s → 2s → 4s com jitter\nlog Warning Polly]
    H --> C
    G -- não --> I[Lança exceção\nEsgotou tentativas]
    F -- não --> I
```

## Fluxo: ValidarAssinaturaWebhook (HMAC-SHA256)

```mermaid
flowchart TD
    A([webhook recebido]) --> B["Split x-signature por ','"]
    B --> C{parts.Length < 2?}
    C -- sim --> D[Retorna false]
    C -- não --> E["Extrai ts= e v1=\ndo header"]
    E --> F{ts ou hash vazio?}
    F -- sim --> D
    F -- não --> G["Monta manifest:\n'id:{dataId};request-id:{requestId};ts:{ts};'"]
    G --> H[HMAC-SHA256 com segredo\nConverte para HEX lowercase]
    H --> I{computedHex == hash?}
    I -- sim --> J[Retorna true ✅]
    I -- não --> D
```

## SignalR: Eventos Emitidos pelo Servidor

| Evento | Payload | Quando |
|---|---|---|
| `NovoPedido` | `pedidoId: int` | Pedido criado com sucesso |
| `StatusAtualizado` | `pedidoId: int, novoStatus: string` | KDS atualiza status |
| `PedidoCancelado` | `pedidoId: int` | Pedido cancelado |
| `ProdutoDesativado` | `produtoId: int` | Estoque zerado automaticamente |
