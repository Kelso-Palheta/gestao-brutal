# SDD — CarteiraCashback

> Gerado pelo Reversa (Redator) em 2026-05-01 | Nível: Detalhado
> Rastreabilidade: `src/BatatasFritas.Domain/Entities/CarteiraCashback.cs`

---

## Visão Geral

`CarteiraCashback` é a conta virtual de fidelidade do cliente. Identificada pelo número de telefone (apenas dígitos), acumula saldo a partir de compras elegíveis e permite que o cliente use esse saldo como desconto em pedidos futuros. Todo movimento de saldo é auditado via `TransacaoCashback`.

---

## Responsabilidades

| Responsabilidade | MoSCoW |
|---|---|
| Manter o saldo atual do cliente | **Must** |
| Registrar toda entrada de saldo com auditoria | **Must** |
| Registrar toda saída de saldo com auditoria | **Must** |
| Impedir saldo negativo | **Must** |
| Permitir ajuste manual pelo admin com auditoria | **Should** |
| Vincular transações a pedidos específicos | **Should** |

---

## Interface

### Construtor
```csharp
CarteiraCashback(string telefone, string nomeCliente)
// telefone: apenas dígitos (normalizado pelo Controller)
// SaldoAtual inicial = 0m
```

### Métodos Públicos

| Método | Parâmetros | Efeito | Guard Clauses |
|---|---|---|---|
| `AdicionarSaldo(valor, motivo, pedidoId?)` | decimal, string, int? | `SaldoAtual += valor` + cria `TransacaoCashback(Entrada)` | `valor <= 0` → ArgumentException |
| `UsarSaldo(valor, motivo, pedidoId?)` | decimal, string, int? | `SaldoAtual -= valor` + cria `TransacaoCashback(Saida)` | `valor <= 0` → ArgumentException; `SaldoAtual < valor` → InvalidOperationException |
| `SetSaldoManual(novoValor, motivo)` | decimal, string | Define saldo diretamente + cria `TransacaoCashback` com a diferença | `diferenca == 0` → retorno sem ação |

### Propriedades Lidas
| Propriedade | Tipo | Notas |
|---|---|---|
| `Telefone` | string | Chave natural do cliente — apenas dígitos |
| `SaldoAtual` | decimal | `protected set` — só alterado via métodos |
| `Transacoes` | IList\<TransacaoCashback\> | Histórico completo (lazy load NHibernate) |
| `CriadoEm` | DateTime | UTC, definido na criação |

---

## Regras de Negócio

1. 🟢 **Telefone como chave natural** — A chave de lookup é o número de telefone com apenas dígitos. Normalização feita no Controller com `new string(tel.Where(char.IsDigit).ToArray())`.
2. 🟢 **Saldo nunca negativo** — `UsarSaldo` lança `InvalidOperationException("Saldo insuficiente")` se `SaldoAtual < valor`.
3. 🟢 **Nenhum movimento silencioso** — `AdicionarSaldo`, `UsarSaldo` e `SetSaldoManual` sempre criam uma `TransacaoCashback` — auditabilidade total.
4. 🟢 **SetSaldoManual é idempotente para diferença zero** — Se o novo valor for igual ao atual, retorna sem criar transação.
5. 🟢 **SetSaldoManual determina o tipo automaticamente** — `diferenca > 0 → Entrada`, `diferenca < 0 → Saida`.
6. 🟡 **Um cliente = uma carteira** — Não há validação de unicidade de telefone no Domain. A constraint deve existir no banco (via mapeamento NHibernate) ou ser garantida pelo Controller com `FindAsync`.
7. 🔴 **PedidoReferenciaId atualizado de forma tardia** — No fluxo de criação de pedido, o `TransacaoCashback.PedidoReferenciaId` é definido *após* o pedido ser salvo (para ter o ID gerado). Existe risco de estado inconsistente se o Commit falhar entre os dois `UpdateAsync`.

---

## Fluxo Principal — Acúmulo de Cashback pós-pedido

1. Pedido é criado e confirmado (`Status = Entregue` ou lógica do Controller)
2. Controller calcula `pedido.ValorElegivelCashback`
3. Aplica a taxa de cashback (ex: 5% — 🔴 LACUNA: taxa não encontrada no código, pode estar em `Configuracao`)
4. Chama `carteira.AdicionarSaldo(valorCashback, "Cashback pedido #X", pedido.Id)`
5. `SaldoAtual` aumenta e `TransacaoCashback(Entrada)` é criada
6. `UpdateAsync(carteira)` persiste as alterações

## Fluxo Principal — Uso de Cashback no checkout

1. Cliente informa `ValorCashbackUsado` no `NovoPedidoDto`
2. Controller normaliza o telefone e busca a carteira com `FindAsync`
3. Valida `carteira.SaldoAtual >= dto.ValorCashbackUsado`
4. Chama `carteira.UsarSaldo(valor, "Uso em novo pedido")`
5. `SaldoAtual` diminui e `TransacaoCashback(Saida)` é criada
6. Após salvar o pedido, atualiza `transacao.PedidoReferenciaId = pedido.Id`
7. `UpdateAsync(carteira)` persiste

## Fluxos Alternativos

- **Carteira inexistente no checkout:** Se o cliente nunca comprou antes, `FindAsync` retorna `null` → Controller lança Exception → BadRequest.
- **Ajuste manual admin:** Admin chama `SetSaldoManual(novoValor, motivo)` via `CashbackController` → diferença calculada automaticamente → auditoria registrada.
- **Telefone com formatação:** `(99) 99999-9999` → normalizado para `99999999999` antes de buscar/criar carteira.

---

## Dependências

- `TransacaoCashback` — histórico auditável de movimentos
- `TipoTransacaoCashback` — enum `Entrada (1)` / `Saida (2)`
- `PedidosController` — responsável por chamar `UsarSaldo` e `AdicionarSaldo`
- `CashbackController` — expõe endpoints de consulta e ajuste manual

---

## Requisitos Não Funcionais

| Tipo | Requisito inferido | Evidência | Confiança |
|---|---|---|---|
| Consistência | Débito de cashback deve ocorrer dentro da mesma transação de banco que cria o pedido | `PedidosController.cs:102,118` | 🟢 |
| Auditabilidade | 100% dos movimentos de saldo têm registro em `TransacaoCashback` | `CarteiraCashback.cs:36,48,61` | 🟢 |
| Atomicidade | Se o pedido falhar após `UsarSaldo`, o `RollbackAsync` desfaz o débito | `PedidosController.cs:147` | 🟢 |

---

## Critérios de Aceitação

```gherkin
# Happy Path — Uso de cashback no pedido
Dado uma carteira com Telefone="11999990000" e SaldoAtual=R$20,00
Quando o cliente usa R$15,00 de cashback em um pedido
Então SaldoAtual = R$5,00
  E Transacoes.Last().Tipo = Saida
  E Transacoes.Last().Valor = R$15,00

# Happy Path — Acúmulo de cashback
Dado uma carteira com SaldoAtual=R$0,00
Quando AdicionarSaldo(R$5,90, "Cashback pedido #42", 42) é chamado
Então SaldoAtual = R$5,90
  E Transacoes.Count = 1
  E Transacoes.Last().PedidoReferenciaId = 42

# Falha — Saldo insuficiente
Dado uma carteira com SaldoAtual=R$5,00
Quando UsarSaldo(R$10,00, "Uso em pedido") é chamado
Então InvalidOperationException é lançada com mensagem "Saldo insuficiente na carteira de cashback"
  E SaldoAtual permanece R$5,00
  E nenhuma TransacaoCashback é criada

# Falha — Valor inválido
Dado qualquer carteira
Quando AdicionarSaldo(0, "motivo") ou AdicionarSaldo(-5, "motivo") é chamado
Então ArgumentException é lançada com "Valor para adicionar deve ser maior que zero"

# Cenário de Borda — SetSaldoManual sem mudança
Dado uma carteira com SaldoAtual=R$10,00
Quando SetSaldoManual(R$10,00, "ajuste") é chamado
Então nenhuma TransacaoCashback é criada
  E SaldoAtual permanece R$10,00

# Cenário de Borda — SetSaldoManual com redução
Dado uma carteira com SaldoAtual=R$50,00
Quando SetSaldoManual(R$30,00, "ajuste admin") é chamado
Então SaldoAtual = R$30,00
  E Transacoes.Last().Tipo = Saida
  E Transacoes.Last().Valor = R$20,00 (Math.Abs da diferença)
```

---

## Cenários de Borda (Nível Detalhado)

1. **Carteira duplicada por telefone:** Se dois pedidos simultâneos tentarem criar uma carteira para o mesmo telefone, pode haver duplicação (sem constraint de unicidade visível no mapeamento). 🔴 LACUNA — recomendado adicionar `unique: true` no `CarteiraCashbackMap.cs`.
2. **PedidoReferenciaId inconsistente:** Se `CommitAsync` falhar após `UsarSaldo` mas antes da atualização do `PedidoReferenciaId`, a `TransacaoCashback` ficará sem referência ao pedido. 🔴 LACUNA — o Rollback desfaz o débito, mas o log da tentativa não é preservado.

---

## Rastreabilidade de Código

| Arquivo | Classe / Função | Cobertura |
|---|---|---|
| `src/BatatasFritas.Domain/Entities/CarteiraCashback.cs` | `CarteiraCashback` (completo) | 🟢 |
| `src/BatatasFritas.Domain/Entities/TransacaoCashback.cs` | `TransacaoCashback` | 🟢 |
| `src/BatatasFritas.API/Controllers/PedidosController.cs` | `Post()` — linhas 105-133 | 🟢 |
| `src/BatatasFritas.API/Controllers/CashbackController.cs` | (não lido — análise pendente) | 🟡 |
