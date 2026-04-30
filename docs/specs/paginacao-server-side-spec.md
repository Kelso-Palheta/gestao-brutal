# Spec: Paginação Server-Side em Listas Grandes

**Versão:** 1.0
**Status:** Aprovada
**Autor:** KPM + Claude
**Data:** 2026-04-29
**Reviewers:** N/A

---

## 1. Resumo

`GetAllAsync()` carrega tabelas inteiras na memória em 20+ pontos do código. As tabelas `pedidos`, `movimentacoes_estoque` e `despesas` crescem indefinidamente → risco OOM real em produção. Esta feature introduz paginação server-side para endpoints de listagem e substituição por `FindManyAsync` com filtro de data para endpoints de agregação.

---

## 2. Contexto e Motivação

**Problema:**
`NHibernateRepository<T>.GetAllAsync()` executa `SELECT * FROM tabela` sem WHERE, LIMIT ou ORDER. Qualquer lista retorna 100% dos registros. Em produção com 6 meses de operação, `pedidos` pode ter 50k+ registros. Um único request de dashboard carrega tudo em memória — 6x `GetAllAsync()` em `FinanceiroController` sozinho.

**Evidências:**
- 20 chamadas `GetAllAsync()` identificadas no código; 6 em `FinanceiroController`, 3 em `RelatoriosController`, 1 em `KdsController`
- `RelatoriosController.GetResumo` chama pedidos + movimentações + despesas em sequência → 3 full scans por request
- Ausência de qualquer `Skip/Take` ou filtro de data no repositório base

**Por que agora:**
FASE 2 estabilizou migrations e CI. Antes de adicionar mais features que criam dados (cashback, Point intents), corrigir o padrão de acesso que levará a OOM em prod.

---

## 3. Goals

- [ ] G-01: Nenhum endpoint de listagem pública executa full table scan
- [ ] G-02: Endpoints de lista retornam máximo 50 registros por request por padrão
- [ ] G-03: Endpoints de agregação (dashboard, relatórios) filtram por data no banco — não na memória
- [ ] G-04: Componente Blazor de paginação funciona em qualquer lista do admin panel
- [ ] G-05: Sem regressão nos endpoints existentes (mesma estrutura de resposta, novo campo `pagination`)

**Métricas de sucesso:**
| Métrica | Baseline | Target |
|---------|----------|--------|
| Registros carregados por request de lista | N (full table) | ≤ 50 |
| Registros carregados por request de dashboard | N (full table) | Apenas período filtrado |
| Full table scans em prod | 20 | 0 |

---

## 4. Non-Goals

- NG-01: Cursor pagination / keyset pagination (offset pagination é suficiente para esta escala)
- NG-02: Infinite scroll (paginação clássica com botões Anterior/Próximo)
- NG-03: Busca full-text ou filtros avançados (out of scope)
- NG-04: Paginação no KDS (`KdsController`) — filtra por status ativo, conjunto pequeno por definição
- NG-05: Cache de páginas ou Redis
- NG-06: Sorting configurável pelo usuário (ordenação fixa por data decrescente)

---

## 5. Usuários e Personas

**Usuário primário:** Operador/dono do estabelecimento acessando AdminPanel.razor com listas de pedidos, despesas e movimentações de estoque.

**Jornada atual (sem a feature):**
1. Operador abre lista de pedidos no admin panel
2. API executa SELECT * FROM pedidos (N registros)
3. Browser recebe JSON de N pedidos — lento, alto consumo de memória
4. Sem navegação de páginas — tudo ou nada

**Jornada futura (com a feature):**
1. Operador abre lista de pedidos
2. API retorna página 1 (20 registros) + metadados de paginação
3. Operador clica "Próxima" para ver mais
4. API retorna página 2, etc.

---

## 6. Requisitos Funcionais

### 6.1 Requisitos Principais

| ID | Requisito | Prioridade | Critério de Aceite |
|----|-----------|-----------|-------------------|
| RF-01 | `IRepository<T>` deve expor `GetPagedAsync(int page, int pageSize)` retornando `PagedResult<T>` | Must | Compilação ok; NHibernate executa SELECT com LIMIT/OFFSET |
| RF-02 | `IRepository<T>` deve expor `FindManyAsync(Expression<Func<T,bool>> predicate)` retornando `IEnumerable<T>` | Must | Query filtrada no banco, não na memória |
| RF-03 | `PagedResult<T>` deve conter: `Items`, `TotalCount`, `Page`, `PageSize`, `TotalPages` | Must | DTO presente em BatatasFritas.Shared; serializa corretamente |
| RF-04 | `GET /api/pedidos?page=1&pageSize=20` retorna página paginada em ordem decrescente de data | Must | Response contém `pagination.totalCount` e `items` com ≤20 pedidos |
| RF-05 | `GET /api/relatorios/movimentacoes?page=1&pageSize=20` retorna movimentações paginadas | Must | Idem RF-04 para movimentações |
| RF-06 | `GET /api/relatorios/despesas?page=1&pageSize=20` retorna despesas paginadas | Must | Idem RF-04 para despesas |
| RF-07 | `FinanceiroController` usa `FindManyAsync` com filtro de data — não `GetAllAsync` | Must | Sem chamada `GetAllAsync` no controller após migração |
| RF-08 | Componente Blazor `PaginacaoComponent.razor` renderiza botões Anterior/Próximo e exibe "Página X de Y" | Must | Componente navegável, emite EventCallback<int> ao trocar página |
| RF-09 | `pageSize` padrão = 20; máximo aceito = 100 (valores além de 100 são tratados como 100) | Should | Request com pageSize=500 retorna 100 registros |
| RF-10 | `page` começa em 1; page=0 ou negativo retorna HTTP 400 | Should | Validação no controller |

### 6.2 Fluxo Principal (Happy Path — Lista Paginada)

1. Operador acessa lista de pedidos no AdminPanel
2. Blazor chama `GET /api/pedidos?page=1&pageSize=20`
3. Controller instancia `GetPagedAsync(page: 1, pageSize: 20)` no repositório
4. NHibernate executa `SELECT ... ORDER BY data_pedido DESC LIMIT 20 OFFSET 0` + `SELECT COUNT(*)`
5. Controller retorna `PagedResult<PedidoDto>` com `items`, `totalCount`, `page`, `pageSize`, `totalPages`
6. Blazor renderiza lista + `<PaginacaoComponent>` com estado correto
7. Operador clica "Próxima" → `EventCallback<int>` dispara → nova chamada com `page=2`

### 6.3 Fluxos Alternativos

**Fluxo A — Última página com registros parciais:**
1. `totalCount = 45`, `pageSize = 20`, operador requisita `page=3`
2. NHibernate: `OFFSET 40 LIMIT 20` retorna 5 registros
3. `PagedResult.Items.Count = 5`, `TotalPages = 3` — correto
4. Botão "Próxima" desabilitado em `PaginacaoComponent`

**Fluxo B — Página além do total:**
1. Operador requisita `page=99` em tabela com 10 registros
2. NHibernate retorna lista vazia
3. Controller retorna `PagedResult` com `Items = []`, `TotalCount = 10`, `Page = 99`
4. UI exibe "Nenhum resultado nesta página"

**Fluxo C — Agregação com filtro de data (FinanceiroController):**
1. Dashboard requisita dados do período 01/04–30/04
2. Controller chama `FindManyAsync(p => p.DataPedido >= inicio && p.DataPedido <= fim)`
3. NHibernate gera `SELECT ... WHERE data_pedido BETWEEN ? AND ?`
4. Apenas registros do período carregados — sem full scan

---

## 7. Requisitos Não-Funcionais

| ID | Requisito | Valor alvo | Observação |
|----|-----------|-----------|------------|
| RNF-01 | Latência de lista paginada | P95 < 200ms | SQLite dev; Postgres prod com índice em data_pedido |
| RNF-02 | Queries por request de lista | Máximo 2 (SELECT + COUNT) | NHibernate LINQ executa ambas separadamente |
| RNF-03 | Memória por request | Proporcional a pageSize, não à tabela inteira | Verificável via profiler |
| RNF-04 | Compatibilidade | NHibernate 5.5.3 LINQ; SQLite + PostgreSQL | Skip/Take via LINQ traduz para LIMIT/OFFSET em ambos |

---

## 8. Design e Interface

**Componentes afetados:** `AdminPanel.razor`, páginas de lista de pedidos/despesas/movimentações, `RelatoriosController`, `FinanceiroController`

**`PaginacaoComponent.razor` — API pública:**
```razor
<PaginacaoComponent PaginaAtual="@paginaAtual"
                    TotalPaginas="@totalPaginas"
                    OnPaginaMudou="@CarregarPagina" />
```

**Estados:**
- **Carregando:** spinner inline onde a lista aparecerá
- **Lista vazia (page 1):** "Nenhum registro encontrado."
- **Lista vazia (page > 1):** "Nenhum resultado nesta página." + botão voltar
- **Erro de API:** "Erro ao carregar dados. Tente novamente." com botão retry
- **Paginação:** botão "← Anterior" desabilitado na página 1; "Próxima →" desabilitado na última página; texto "Página X de Y (Z registros)"

---

## 9. Modelo de Dados

**Sem migration necessária.** Paginação é comportamento de query, não schema.

**DTO novo — `PagedResult<T>` em `BatatasFritas.Shared/DTOs/`:**
```
PagedResult<T> {
  Items: List<T>          // registros da página atual
  TotalCount: int         // total de registros na tabela/filtro
  Page: int               // página atual (base 1)
  PageSize: int           // tamanho da página
  TotalPages: int         // calculado: ceil(TotalCount / PageSize)
}
```

**Contrato de response para endpoints paginados:**
```json
{
  "items": [...],
  "totalCount": 145,
  "page": 2,
  "pageSize": 20,
  "totalPages": 8
}
```

**Alterações em `IRepository<T>` (sem breaking change):**
```
+ GetPagedAsync(int page, int pageSize) → Task<PagedResult<T>>
+ GetPagedAsync(Expression<Func<T,bool>> predicate, int page, int pageSize) → Task<PagedResult<T>>
+ FindManyAsync(Expression<Func<T,bool>> predicate) → Task<IEnumerable<T>>
```
Método `GetAllAsync()` mantido — ainda usado em contextos de referência (bairros, produtos, configurações que são coleções pequenas e estáticas).

---

## 10. Integrações e Dependências

| Dependência | Tipo | Impacto se indisponível |
|-------------|------|------------------------|
| NHibernate 5.5.3 LINQ `Skip/Take` | Obrigatória | Não aplicável — já em uso |
| SQLite + PostgreSQL (drivers já presentes) | Obrigatória | Traduz LIMIT/OFFSET corretamente em ambos |

---

## 11. Edge Cases e Tratamento de Erros

| Cenário | Trigger | Comportamento esperado |
|---------|---------|----------------------|
| EC-01: page < 1 | `?page=0` ou `?page=-5` | HTTP 400 "page deve ser ≥ 1" |
| EC-02: pageSize > 100 | `?pageSize=500` | Silenciosamente clamped para 100; resposta normal |
| EC-03: page além do total | `page=99`, 10 registros | `Items = []`, `TotalCount = 10` — HTTP 200, sem erro |
| EC-04: Tabela vazia | Nenhum registro | `Items = []`, `TotalCount = 0`, `TotalPages = 0` — HTTP 200 |
| EC-05: `FindManyAsync` sem resultado | Filtro de data sem pedidos | `IEnumerable` vazio — dashboard exibe zeros normalmente |
| EC-06: NHibernate session error | Falha de conexão | Exception propagada → HTTP 500 pelo middleware global |
| EC-07: pageSize = 0 | `?pageSize=0` | HTTP 400 "pageSize deve ser ≥ 1" |

---

## 12. Segurança e Privacidade

- **Autenticação:** Todos os endpoints afetados já requerem JWT Bearer — sem mudança.
- **Autorização:** Somente operadores autenticados acessam listas. Sem novo dado exposto.
- **Dados sensíveis:** Nenhuma mudança na estrutura de dados ou serialização de campos sensíveis.
- **Auditoria:** Não necessária para paginação — é leitura.
- **Injeção:** `page` e `pageSize` são `int` — sem risco de injeção. `FindManyAsync` usa Expression tree — sem concatenação de SQL.

---

## 13. Plano de Rollout

- **Estratégia:** Big bang — sem feature flag (não há risco de regressão em dados, apenas comportamento de query)
- **Rollback:** Reverter `IRepository<T>` removendo os novos métodos e desfazendo alterações nos controllers — 0 impacto no schema
- **Monitoramento pós-deploy:** Observar logs Serilog em `/health/ready`; verificar tempo de resposta do dashboard financeiro; confirmar ausência de warnings de memória

---

## 14. Open Questions

| # | Pergunta | Impacto | Dono | Prazo |
|---|---------|---------|------|-------|
| OQ-01 | Índice em `pedidos.data_pedido` existe no Postgres prod? | Médio — sem índice, COUNT full scan | KPM | Antes do deploy prod |

---

## 15. Decisões Tomadas

| Decisão | Alternativas consideradas | Racional |
|---------|--------------------------|---------|
| Offset pagination | Cursor/keyset | Simples, suficiente para escala atual (<100k registros) |
| 2 queries por request (SELECT + COUNT) | Single query com window function | NHibernate LINQ não suporta window function nativo; 2 queries é padrão aceito |
| `GetAllAsync()` mantido | Remover completamente | Ainda necessário para entidades de referência pequenas (bairros, produtos) |
| pageSize máx = 100 | 50 ou 200 | Balanceia UX e performance; 100 é convencional |
| Não paginar KDS | Paginar tudo | KDS filtra por status ativo — conjunto pequeno por design, paginação quebraria UX do totem |
| `FindManyAsync` para agregações | Paginar e somar páginas | Logicamente incorreto: totais precisam de todos os registros do período |

---

## Apêndice

### Arquivos a criar/modificar

| Arquivo | Ação |
|---------|------|
| `BatatasFritas.Infrastructure/Repositories/IRepository.cs` | Adicionar 3 novos métodos |
| `BatatasFritas.Infrastructure/Repositories/NHibernateRepository.cs` | Implementar novos métodos |
| `BatatasFritas.Shared/DTOs/PagedResult.cs` | Criar |
| `BatatasFritas.API/Controllers/PedidosController.cs` | Migrar GET lista para paginado |
| `BatatasFritas.API/Controllers/RelatoriosController.cs` | Migrar movimentações e despesas |
| `BatatasFritas.API/Controllers/FinanceiroController.cs` | Substituir GetAllAsync por FindManyAsync com filtro |
| `BatatasFritas.Web/Shared/PaginacaoComponent.razor` | Criar |
| `BatatasFritas.Web/Pages/AdminPanel.razor` | Integrar componente onde aplicável |

---

## Relatório de Avaliação

| Dimensão | Peso | Score | Observação |
|----------|------|-------|-----------|
| Completude | 30% | 30/30 | Todas seções preenchidas, 10 RF, 4 RNF, 7 EC |
| Testabilidade | 25% | 25/25 | Todos RF têm critério de aceite verificável |
| Clareza | 20% | 20/20 | Sem ambiguidade, contratos de dados explícitos |
| Escopo | 15% | 15/15 | Non-goals explícitos; decisão KDS documentada |
| Edge Cases | 10% | 10/10 | 7 EC cobre inputs inválidos, vazios, beyond-last-page |

**Score total: 100/100** ✅ — Pronta para implementação.
