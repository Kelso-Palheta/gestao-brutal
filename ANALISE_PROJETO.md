# Análise Completa do Projeto BatatasFritas
> Gerado em 24/03/2026

---

## Visão Geral da Arquitetura

O projeto está bem estruturado seguindo uma arquitetura em camadas:

- **BatatasFritas.API** — ASP.NET Core Web API com controllers REST
- **BatatasFritas.Domain** — Entidades de domínio ricas (DDD)
- **BatatasFritas.Infrastructure** — NHibernate ORM, repositórios, mappings, DI
- **BatatasFritas.Shared** — DTOs e Enums compartilhados entre API e Frontend
- **BatatasFritas.Web** — Blazor WebAssembly (frontend)

A separação de responsabilidades está correta. O padrão Repository + Unit of Work está implementado de forma consistente.

---

## Bugs Corrigidos (aplicados automaticamente)

### 🔴 BUG CRÍTICO 1 — Relatórios com valor errado (ignorava cashback)
**Arquivo:** `RelatoriosController.cs`

O helper `Valor(Pedido p)` calculava manualmente `Itens.Sum + TaxaEntrega`, ignorando o `ValorCashbackUsado`. Isso fazia os relatórios **superestimar o faturamento** em pedidos onde o cliente usou cashback como desconto.

**Corrigido:** agora usa `p.ValorTotal`, que já desconta o cashback corretamente (definido no domínio como `Math.Max(0, ValorTotalItens + TaxaEntrega - ValorCashbackUsado)`).

---

### 🔴 BUG CRÍTICO 2 — KDS exibia total errado para pedidos com cashback
**Arquivo:** `KdsController.cs`

O `GetPedidosAtivos` recalculava o total manualmente (`somaItens + somaTaxa`), também ignorando `ValorCashbackUsado`. O operador no KDS via um valor maior do que o cliente realmente pagou.

**Corrigido:** agora usa `p.ValorTotal` direto da entidade de domínio.

---

### 🔴 BUG CRÍTICO 3 — Consulta duplicada à carteira de cashback
**Arquivo:** `PedidosController.cs`

Após deduzir o cashback (linhas ~80–90), o código fazia **um segundo `GetAllAsync()`** completo para encontrar a mesma carteira e atualizar o `PedidoReferenciaId`. Isso gerava duas queries desnecessárias e, em teoria, poderia pegar uma instância diferente em cache.

**Corrigido:** a variável `carteiraCashback` é reutilizada da primeira consulta, eliminando o segundo `GetAllAsync()`.

---

### 🔴 BUG MÉDIO 4 — Motivo de baixa de estoque referenciava ProdutoId no lugar do PedidoId
**Arquivo:** `PedidosController.cs`, método `BaixarEstoque`

A string de motivo dizia `"Baixa automática — Pedido #{item.ProdutoId}"` — mas `ProdutoId` é o ID do produto, não do pedido. Histórico de movimentações ficava com a referência errada.

**Corrigido:** o método agora recebe `pedidoId` como parâmetro e usa `"Baixa automática — Pedido #{pedidoId}"`.

---

### 🟠 BUG MÉDIO 5 — URLs hardcoded no InfinitePayService
**Arquivo:** `InfinitePayService.cs`

Dois valores estavam **hardcoded no código-fonte de produção**:
- `redirect_url = "http://localhost:5255/..."` — não funciona em produção
- `webhook_url = "https://6d5e-API-NGROK-TEMPORARIO.ngrok-free.app/..."` — URL de ngrok temporária (certamente expirada)

**Corrigido:** ambas as URLs agora são lidas de `appsettings.json` via `IConfiguration`:
```json
"InfinitePay": {
  "RedirectBaseUrl": "http://localhost:5255",
  "WebhookUrl": ""
}
```
Em produção, basta configurar os valores corretos no ambiente (variável de ambiente ou `appsettings.Production.json`).

---

### 🟠 BUG MÉDIO 6 — Falta de try-catch + rollback em vários controllers
**Arquivos:** `ProdutosController`, `KdsController`, `FinanceiroController`, `CashbackController`, `ConfiguracoesController`

Vários endpoints faziam `_uow.BeginTransaction()` mas, se uma exceção fosse lançada, a transação ficava aberta/suja sem rollback. Isso pode corromper o estado do banco.

**Corrigido:** adicionado `try/catch` com `await _uow.RollbackAsync()` em todos os endpoints de escrita que estavam sem proteção.

---

### 🟡 LIMPEZA — Arquivos Class1.cs vazios
**Arquivos:** `BatatasFritas.Domain/Class1.cs`, `BatatasFritas.Infrastructure/Class1.cs`, `BatatasFritas.Shared/Class1.cs`

Três arquivos de scaffolding sem conteúdo útil. Foram marcados com comentário indicando que podem ser excluídos fisicamente do projeto.

---

## Problemas Identificados (para corrigir manualmente)

### 🔴 SEGURANÇA — Nenhum endpoint da API está autenticado
**Impacto:** Qualquer pessoa com acesso à URL da API pode criar pedidos, ver relatórios, alterar configurações, zerar o banco, etc.

**Recomendação:** Implementar JWT Bearer Authentication. Sugestão de estrutura:
- Endpoint público: `POST /api/pedidos` (clientes fazem pedidos), `GET /api/produtos`
- Endpoints protegidos: tudo em `/api/kds/*`, `/api/relatorios/*`, `/api/financeiro/*`, `/api/configuracoes/*`
- Adicionar `[Authorize]` nos controllers administrativos

```csharp
// No Program.cs
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(...);
app.UseAuthentication();
app.UseAuthorization();
```

---

### 🔴 SEGURANÇA — Webhook InfinitePay sem verificação de assinatura
**Arquivo:** `PagamentosController.cs`

Qualquer pessoa pode enviar um POST para `/api/pagamentos/webhook` e marcar qualquer pedido como pago.

**Recomendação:** A InfinitePay envia um header de assinatura (HMAC ou similar). Verificar o header `X-Signature` antes de processar o payload.

---

### 🟠 PERFORMANCE — GetAllAsync() em tabelas que crescem
**Arquivos:** `CashbackController.GetSaldo`, `KdsController.AtualizarStatus`, `PedidosController.Post`

Vários endpoints carregam **todas** as carteiras ou pedidos para memória e filtram em C# com `.FirstOrDefault()`. Com muitos clientes, isso vai ficar lento.

**Recomendação:** Adicionar métodos específicos no `IRepository<T>` que façam a query filtrada no banco:

```csharp
// No IRepository<T>
Task<T?> FindAsync(Expression<Func<T, bool>> predicate);

// No NHibernateRepository<T>
public async Task<T?> FindAsync(Expression<Func<T, bool>> predicate)
    => await _session.Query<T>().Where(predicate).FirstOrDefaultAsync();
```

---

### 🟠 CONFIGURAÇÃO — Detecção de banco frágil no DependencyInjection.cs
**Arquivo:** `DependencyInjection.cs`

A lógica para detectar se é PostgreSQL ou SQLite usa `connectionString.Contains("Host=")` etc., o que é frágil. Um caminho de arquivo com "Server" no nome quebraria a detecção.

**Recomendação:** Usar uma chave explícita no `appsettings.json`:

```json
"DatabaseProvider": "sqlite"  // ou "postgres"
```

```csharp
var provider = builder.Configuration["DatabaseProvider"] ?? "sqlite";
bool isPostgres = provider.Equals("postgres", StringComparison.OrdinalIgnoreCase);
```

---

### 🟠 DADOS — ValorCashbackUsado ausente no PedidoDetalheDto
**Arquivo:** `PedidoDetalheDto.cs`

O DTO não expõe o campo `ValorCashbackUsado`. No frontend, não é possível mostrar ao cliente/operador quanto de cashback foi usado em um pedido específico.

**Recomendação:**
```csharp
// Adicionar ao PedidoDetalheDto:
public decimal ValorCashbackUsado { get; set; }
public decimal TaxaEntrega { get; set; }
public decimal SubtotalItens { get; set; }
```

E preencher nos controllers (`PedidosController.Get` e `KdsController.GetPedidosAtivos`).

---

### 🟠 FUNCIONAL — CarrinhoState.RemoverItem remove o primeiro produto com aquele ID
**Arquivo:** `CarrinhoState.cs`

Se o mesmo produto estiver duas vezes com observações diferentes (ex: "sem sal" e "com bastante sal"), `RemoverItem(produtoId)` remove sempre o primeiro, não necessariamente o que o usuário clicou.

**Recomendação:** Usar um índice ou GUID para identificar itens unicamente:

```csharp
public void RemoverItemPorIndice(int index)
{
    if (index >= 0 && index < _itens.Count)
    {
        _itens.RemoveAt(index);
        NotifyStateChanged();
    }
}
```

---

### 🟡 TIMEZONE — DataHoraPedido pode ficar errado em comparações de data
**Arquivos:** `FinanceiroController`, `RelatoriosController`

O código mistura `DateTime.UtcNow` com `.Date` (que é local). Em servidor rodando em UTC+0, `DateTime.Today` retorna a data correta. Mas se o servidor estiver em outro fuso (ou mudar), os filtros de "hoje" e "mês atual" ficam errados.

**Recomendação:** Definir e seguir uma convenção clara. Se todos os `DataHoraPedido` forem salvos em UTC (o que parece ser o caso, já que o construtor usa `DateTime.UtcNow`), os filtros também devem comparar com datas UTC.

---

### 🟡 FUNCIONAL — Senha padrão exposta no código-fonte
**Arquivo:** `ConfiguracoesController.cs`

```csharp
private const string SenhaPadrao = "palheta2025";
```

A senha de primeiro acesso está legível no repositório. Se o código for público ou vazar, qualquer pessoa sabe a senha inicial.

**Recomendação:** Mover para variável de ambiente:
```csharp
var senhaPadrao = Environment.GetEnvironmentVariable("KDS_DEFAULT_PASSWORD") ?? "trocar-em-producao";
```

---

## O que Ainda Falta Implementar

### Alta Prioridade

1. **Autenticação JWT na API** — Hoje qualquer pessoa acessa todos os endpoints sem login.

2. **Validação de assinatura do webhook InfinitePay** — Risco de fraude (marcar pedido como pago sem ter pago).

3. **Rate limiting** — Sem limite de requisições, a API é vulnerável a flood/DoS. Adicionar `app.UseRateLimiter()` (.NET 7+).

4. **Queries filtradas no banco** — Substituir `GetAllAsync().Where(...)` por queries diretas para as consultas de cashback, configurações e pedidos ativos.

### Média Prioridade

5. **Notificação em tempo real no KDS** — Hoje o KDS precisa fazer polling (atualização manual). Implementar **SignalR** para que novos pedidos apareçam instantaneamente.

6. **Histórico de cashback no frontend** — A entidade `TransacaoCashback` existe, mas não há endpoint `GET /api/cashback/historico/{telefone}` para exibir o extrato ao cliente.

7. **Reembolso de cashback no cancelamento** — Se um pedido for cancelado e o cliente tinha usado cashback, o saldo não é devolvido automaticamente. O `KdsController.CancelarPedido` deveria verificar e creditar de volta.

8. **Paginação nos relatórios e listas** — Endpoints como `GET /api/pedidos` (se existir) e `GET /api/insumos/movimentacoes` carregam tudo sem paginação.

9. **Soft-delete consistente em Insumos** — `InsumosController.Delete` faz soft-delete (`.Ativo = false`) corretamente, mas o `GET /api/insumos/dashboard` ainda mostra movimentações de insumos inativos.

10. **Campo `PrecoUnitario` no `ItemPedidoDetalheDto`** — O DTO de item retornado ao frontend não inclui o preço unitário, impossibilitando recalcular subtotais por item no frontend sem depender do produto atual (que pode ter mudado de preço).

### Baixa Prioridade

11. **Testes automatizados** — O projeto não tem nenhum arquivo de teste (`.Tests`). Implementar ao menos testes unitários para `CarteiraCashback` (lógica de saldo) e `Pedido` (cálculo de ValorTotal).

12. **Logging estruturado** — Os `Console.WriteLine` nos controllers devem ser substituídos por `ILogger<T>` para integrar com ferramentas de observabilidade (ex: Seq, Datadog, Loki).

13. **Migração de schema versionada** — Hoje usa `SchemaUpdate` do NHibernate, que é perigoso em produção (pode perder dados). Considerar **Flyway** ou **DbUp** para migrações controladas.

14. **BairrosController e ComplementosController** — Não foram encontrados problemas nesses controllers (são CRUD simples corretos), mas falta paginação.

---

## Resumo dos Arquivos Alterados

| Arquivo | Tipo de Mudança |
|---|---|
| `RelatoriosController.cs` | Correção de bug (cálculo de faturamento ignorava cashback) |
| `KdsController.cs` | Correção de bug (total errado) + try/catch + rollback |
| `PedidosController.cs` | Correção de bug (query dupla cashback + motivo errado) |
| `ProdutosController.cs` | try/catch + rollback em todos os endpoints de escrita |
| `FinanceiroController.cs` | try/catch + rollback em SalvarMetaDiaria |
| `CashbackController.cs` | try/catch + rollback em SetConfiguracao |
| `ConfiguracoesController.cs` | try/catch + rollback em AlterarSenha |
| `InfinitePayService.cs` | URLs hardcoded movidas para appsettings.json |
| `appsettings.json` | Adicionadas chaves `RedirectBaseUrl` e `WebhookUrl` |
| `Class1.cs` (3 arquivos) | Limpeza — marcados para exclusão |
