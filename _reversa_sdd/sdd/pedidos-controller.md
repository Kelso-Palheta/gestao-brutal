# SDD — PedidosController (Criação de Pedido)

> Gerado pelo Reversa (Redator) em 2026-05-01 | Nível: Detalhado
> Rastreabilidade: `src/BatatasFritas.API/Controllers/PedidosController.cs` (Método Post)

---

## Visão Geral

O endpoint `POST /api/pedidos` é a porta de entrada para todas as vendas do sistema (Delivery, Balcão e Totem). Ele orquestra a validação de estoque, aplicação de cashback, persistência do pedido e notificação em tempo real para a cozinha (KDS). É o fluxo mais crítico e complexo do backend.

---

## Responsabilidades

| Responsabilidade | MoSCoW |
|---|---|
| Validar disponibilidade de estoque antes de iniciar transação | **Must** |
| Converter `NovoPedidoDto` para entidade `Pedido` | **Must** |
| Validar e debitar saldo de cashback do cliente | **Must** |
| Persistir pedido e itens em uma única transação atômica | **Must** |
| Executar a baixa de estoque automática | **Must** |
| Emitir notificação via SignalR para o Hub de Pedidos | **Must** |
| Retornar o ID do pedido e link de pagamento gerado | **Should** |

---

## Interface

### Endpoint
`POST /api/pedidos`

### Payload de Entrada (`NovoPedidoDto`)
```json
{
  "nomeCliente": "string",
  "telefoneCliente": "string",
  "enderecoEntrega": "string",
  "bairroEntregaId": "int",
  "metodoPagamento": "int",
  "trocoPara": "decimal?",
  "tipoAtendimento": "int",
  "valorCashbackUsado": "decimal",
  "itens": [
    {
      "produtoId": "int",
      "quantidade": "int",
      "precoUnitario": "decimal",
      "observacao": "string"
    }
  ]
}
```

### Resposta de Sucesso (200 OK)
```json
{
  "pedidoId": "int",
  "status": "string",
  "linkPagamento": "string"
}
```

---

## Regras de Negócio

1. 🟢 **Pré-Check de Estoque** — Antes de abrir a transação de banco, o sistema verifica se há estoque suficiente para produtos que NÃO possuem receita. Produtos com receita são validados apenas na fase de baixa.
2. 🟢 **Obrigatoriedade de Telefone para Cashback** — Se `valorCashbackUsado > 0`, o campo `telefoneCliente` é obrigatório e validado.
3. 🟢 **Normalização de Telefone** — O telefone é limpo (apenas dígitos) antes da busca na `CarteiraCashback`.
4. 🟢 **Atomicidade** — Todo o fluxo (salvar pedido, debitar cashback, baixar estoque) ocorre dentro de um `IUnitOfWork.BeginTransaction()`. Qualquer erro dispara `RollbackAsync()`.
5. 🟢 **Rastreabilidade de Cashback** — A transação de débito de cashback é vinculada ao `PedidoId` gerado logo após o `AddAsync(pedido)`.
6. 🟢 **Notificação KDS** — O sistema dispara `NovoPedido` via SignalR para todos os clientes conectados logo após o Commit.
7. 🟡 **SQL Puro para Validação** — A validação inicial de estoque usa `CreateSQLQuery`, desviando do padrão de Repositórios genéricos por performance ou histórico.

---

## Fluxo Principal

1. Recebe `NovoPedidoDto`.
2. Executa loop nos itens para verificar `estoque_atual` via SQL direto.
3. Instancia a entidade `Pedido`.
4. Inicia transação via `_uow`.
5. Se houver cashback:
    - Busca carteira pelo telefone.
    - Valida saldo.
    - Executa `UsarSaldo`.
6. Salva o `Pedido` (Gera ID).
7. Vincula ID do pedido à transação de cashback.
8. Chama método privado `BaixarEstoque`.
9. Executa `CommitAsync()`.
10. Notifica Hub SignalR.
11. Retorna resultado.

---

## Requisitos Não Funcionais

| Tipo | Requisito inferido | Evidência | Confiança |
|---|---|---|---|
| Integridade | Rollback automático em qualquer falha de processo | `PedidosController.cs:147` | 🟢 |
| Performance | Pré-check de estoque fora da transação para reduzir lock | `PedidosController.cs:64-90` | 🟢 |

---

## Critérios de Aceitação

```gherkin
# Happy Path — Pedido Delivery com Pix
Dado um payload válido com método de pagamento Pix
Quando a requisição é processada
Então o pedido deve ser salvo
  E o status retornado deve ser "Recebido"
  E um link de pagamento MercadoPago deve estar presente no retorno

# Falha — Estoque insuficiente
Dado um produto com estoque 1
Quando um pedido solicita 2 unidades desse produto
Então a API deve retornar 400 BadRequest
  E a mensagem deve indicar estoque insuficiente para o produto específico

# Falha — Saldo de Cashback insuficiente
Dado um cliente com R$ 5,00 de saldo
Quando o pedido tenta usar R$ 10,00 de cashback
Então a API deve retornar 400 BadRequest
  E a transação de banco deve sofrer rollback
```

---

## Rastreabilidade de Código

| Arquivo | Classe / Função | Cobertura |
|---|---|---|
| `src/BatatasFritas.API/Controllers/PedidosController.cs` | `Post` | 🟢 |
| `src/BatatasFritas.Shared/DTOs/NovoPedidoDto.cs` | `NovoPedidoDto` | 🟢 |
| `src/BatatasFritas.API/Hubs/PedidosHub.cs` | `PedidosHub` | 🟢 |
