# ERD Completo — BatatasFritas

> Gerado pelo Reversa (Arquiteto) em 2026-05-01 | Nível: Detalhado

```mermaid
erDiagram
    Pedido {
        int Id PK
        string NomeCliente
        string TelefoneCliente
        string EnderecoEntrega
        int BairroEntrega_id FK
        decimal ValorCashbackUsado
        datetime DataHoraPedido
        int Status
        int StatusPagamento
        int MetodoPagamento
        int SegundoMetodoPagamento
        decimal ValorSegundoPagamento
        decimal TrocoPara
        int TipoAtendimento
        string LinkPagamento
        string Observacao
    }

    ItemPedido {
        int Id PK
        int Pedido_id FK
        int Produto_id FK
        int Quantidade
        decimal PrecoUnitario
        string Observacao
    }

    Produto {
        int Id PK
        string Nome
        string Descricao
        int CategoriaId
        decimal PrecoBase
        string ImagemUrl
        bool Ativo
        int Ordem
        string ComplementosPermitidos
        int EstoqueAtual
        int EstoqueMinimo
    }

    Bairro {
        int Id PK
        string Nome
        decimal TaxaEntrega
        int OrdemExibicao
    }

    CarteiraCashback {
        int Id PK
        string Telefone
        string NomeCliente
        decimal SaldoAtual
        datetime CriadoEm
    }

    TransacaoCashback {
        int Id PK
        int Carteira_id FK
        decimal Valor
        int Tipo
        string Motivo
        int PedidoReferenciaId
        datetime DataHora
    }

    Insumo {
        int Id PK
        string Nome
        string Unidade
        decimal EstoqueAtual
        decimal EstoqueMinimo
        decimal CustoPorUnidade
        bool Ativo
    }

    MovimentacaoEstoque {
        int Id PK
        int Insumo_id FK
        int Tipo
        decimal Quantidade
        decimal ValorUnitario
        datetime DataMovimentacao
        string Motivo
        string Fornecedor
        string NumeroNF
    }

    ItemReceita {
        int Id PK
        int Produto_id FK
        int Insumo_id FK
        decimal QuantidadePorUnidade
    }

    Complemento {
        int Id PK
        string Nome
        decimal Preco
        string Categoria
        string Tipo
    }

    Despesa {
        int Id PK
        string Descricao
        decimal Valor
        datetime Data
        string Categoria
        string Observacao
    }

    Configuracao {
        int Id PK
        string Chave
        string Valor
    }

    Pedido ||--o{ ItemPedido : "contém"
    Pedido }o--|| Bairro : "entregue em"
    ItemPedido }o--|| Produto : "refere-se a"
    CarteiraCashback ||--o{ TransacaoCashback : "registra"
    Insumo ||--o{ MovimentacaoEstoque : "rastreado em"
    Produto ||--o{ ItemReceita : "composto por"
    Insumo ||--o{ ItemReceita : "usado em"
```

## Relacionamentos com Cardinalidades

| Entidade A | Cardinalidade | Entidade B | Notas |
|---|---|---|---|
| Pedido | 1:N | ItemPedido | Cascade delete |
| ItemPedido | N:1 | Produto | Snapshot de preço — produto pode mudar |
| Pedido | N:1 | Bairro | Nullable — Balcão/Totem não têm bairro |
| CarteiraCashback | 1:N | TransacaoCashback | Histórico completo de saldo |
| Produto | N:M | Insumo | Via ItemReceita |
| Insumo | 1:N | MovimentacaoEstoque | Rastreabilidade de estoque |
