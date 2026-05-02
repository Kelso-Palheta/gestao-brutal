# Fluxograma — BatatasFritas.Infrastructure

> Gerado pelo Reversa (Arqueólogo) em 2026-05-01 | Nível: Detalhado

## Configuração da SessionFactory (DependencyInjection)

```mermaid
flowchart TD
    A([AddInfrastructure chamado]) --> B{"databaseProvider == 'postgres'?"}
    B -- sim --> C[PostgreSQLConfiguration\nNpgsql driver]
    B -- não --> D[SQLiteConfiguration\nficheiro local]
    C --> E[Fluently.Configure\n+ AddFromAssemblyOf ProdutoMap\n+ SchemaUpdate auto-execute]
    D --> E
    E --> F{"BuildSessionFactory ok?"}
    F -- sim --> G[Registra Singleton: ISessionFactory\nRegistra Scoped: ISession\nRegistra Scoped: IRepository genérico\nRegistra Scoped: IUnitOfWork]
    F -- não --> H[Lança exceção + log FATAL]
```

## Fluxo de Transação — UnitOfWork

```mermaid
sequenceDiagram
    participant Controller
    participant UoW as NHibernateUnitOfWork
    participant Session as NHibernate ISession
    participant DB

    Controller->>UoW: BeginTransaction()
    UoW->>Session: BeginTransaction()
    Session->>DB: START TRANSACTION

    Controller->>Session: SaveAsync / UpdateAsync / DeleteAsync
    Session->>DB: SQL commands

    alt Sucesso
        Controller->>UoW: CommitAsync()
        UoW->>Session: CommitAsync() [se ativo]
        Session->>DB: COMMIT
    else Erro
        Controller->>UoW: RollbackAsync()
        UoW->>Session: RollbackAsync() [se ativo]
        Session->>DB: ROLLBACK
    end

    Controller->>UoW: Dispose()
    UoW->>Session: Dispose()
```

## Repositório Genérico — NHibernateRepository

```mermaid
flowchart LR
    A[IRepository&lt;T&gt;] --> B[NHibernateRepository&lt;T&gt;]
    B --> C[GetByIdAsync → session.GetAsync]
    B --> D[GetAllAsync → session.Query.ToListAsync]
    B --> E["FindAsync → session.Query.Where(predicate).FirstOrDefaultAsync"]
    B --> F[AddAsync → session.SaveAsync]
    B --> G[UpdateAsync → session.UpdateAsync]
    B --> H[DeleteAsync → session.DeleteAsync]
```
