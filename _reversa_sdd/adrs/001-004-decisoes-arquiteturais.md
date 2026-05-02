# ADR-001 — NHibernate como ORM principal

> **Status:** Aceito | **Data:** ~2025 (inferido dos commits iniciais)
> 🟡 ADR retroativo — reconstruído por análise do código

## Contexto

O projeto precisava de um ORM para persistência em C#/.NET. As opções principais eram Entity Framework Core (padrão .NET) e NHibernate (legado Java trazido para .NET).

## Decisão

Escolheu-se **NHibernate 5.5 + FluentNHibernate 3.4** como ORM.

## Alternativas Consideradas

| Opção | Prós | Contras |
|---|---|---|
| Entity Framework Core | Oficial Microsoft, documentação vasta, migrations nativas | Menos controle sobre SQL gerado |
| **NHibernate** ← escolhido | Maturidade, controle fino de sessão/transação, suporte SQLite+PostgreSQL | Curva de aprendizado maior, menos suporte da comunidade .NET |
| Dapper | SQL explícito, máximo controle | Sem mapeamento ORM — mais código manual |

## Consequências

✅ Suporte dual a SQLite (dev) e PostgreSQL (prod) com troca via configuração.
✅ `SchemaUpdate` automático evita migrations manuais em desenvolvimento.
⚠️ SQL puro (`CreateSQLQuery`) ainda necessário em alguns pontos críticos — indica limitação da abstração atual.
⚠️ `ISession` exposta diretamente nos Controllers (acoplamento).

---

# ADR-002 — SignalR para atualizações em tempo real no KDS

> **Status:** Aceito | **Data:** ~2025 (inferido de commit `feat: estoque realtime sync`)
> 🟡 ADR retroativo

## Contexto

O KDS (Kitchen Display System) precisa receber novos pedidos e atualizações de status em tempo real, sem polling.

## Decisão

Usar **ASP.NET Core SignalR** com hub em `/hubs/pedidos`. O servidor emite eventos; clientes apenas ouvem (unidirecional).

## Alternativas Consideradas

| Opção | Motivo de exclusão |
|---|---|
| Polling HTTP | Latência alta, carga desnecessária no servidor |
| Server-Sent Events | Unidirecional por padrão, menos suporte Blazor |
| **SignalR** ← escolhido | Integração nativa com ASP.NET Core e Blazor |

## Consequências

✅ KDS recebe novos pedidos em < 1s.
✅ Evento `ProdutoDesativado` sincroniza cardápio automaticamente quando estoque zera.
⚠️ JWT via query string necessário para autenticação SignalR (`?access_token=...`) — padrão mas menos seguro que header.

---

# ADR-003 — Abandono de InfinitePay/Point Smart 2, simplificação para 3 métodos

> **Status:** Aceito | **Data:** Abril 2026
> 🟢 CONFIRMADO por commits

## Contexto

O sistema originalmente tinha integração com InfinitePay e múltiplos métodos de pagamento eletrônico (Point Smart 2). Commits de hotfix mostraram instabilidade e problemas de build.

## Evidências no Git

```
228dc06 Fix build: corrigir CartaoCredito/CartaoDebito → Cartao no RelatoriosController
35148b5 Fix build: substituir InfiniteTap/InfinitePayOnline por MetodoPagamento.Cartao
bf87c58 Fix bugs: limpeza de referências de pagamento obsoletas
```

## Decisão

Simplificar para **3 métodos oficiais**: Dinheiro (1), Cartão (2), Pix (4). O valor `3` do enum foi reservado e abandonado.

## Consequências

✅ Menos complexidade, menos pontos de falha.
✅ Compatibilidade garantida entre Loja Web e Totem.
⚠️ `MercadoPagoService` ainda referencia métodos não existentes no enum (`PixOnline`, `PixPoint`, `CartaoCredito`, `CartaoDebito`) — **divergência não resolvida** entre o serviço e o enum atual.

---

# ADR-004 — Seed de dados + migrations no startup

> **Status:** Aceito | **Data:** Inferido de commits de migração
> 🟡 ADR retroativo

## Contexto

Em ambientes de produção com deploy via Docker, precisava-se garantir que o banco sempre tivesse o schema correto e dados mínimos para operação.

## Decisão

Realizar migrations via `SchemaUpdate` do NHibernate + SQL customizados no startup do `Program.cs`, e criar seed automático se o banco estiver vazio.

## Alternativas Consideradas

| Opção | Motivo de exclusão |
|---|---|
| Flyway / Liquibase | Dependência externa, complexidade adicional |
| EF Migrations | Não disponível com NHibernate |
| **Startup migrations** ← escolhido | Zero dependência adicional, simples para escala atual |

## Consequências

✅ Deploy "zero clique" — banco é criado e populado automaticamente.
⚠️ Startup pode falhar se o banco estiver indisponível (sem retry).
⚠️ Migrations via PRAGMA são SQLite-específicas — podem precisar ajuste para PostgreSQL puro.
