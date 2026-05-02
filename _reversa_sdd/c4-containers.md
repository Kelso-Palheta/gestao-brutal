# Arquitetura C4 — Containers (Nível 2)

> Gerado pelo Reversa (Arquiteto) em 2026-05-01 | Nível: Detalhado

```mermaid
C4Container
    title BatatasFritas — Containers

    Person(cliente, "Cliente", "Faz pedidos")
    Person(admin, "Admin / KDS", "Gerencia negócio")

    System_Boundary(batatasfritas, "BatatasFritas") {
        Container(web, "BatatasFritas.Web", "Blazor WASM\n.NET 8", "SPA — Cardápio, Admin Panel, KDS Monitor, Totem")
        Container(api, "BatatasFritas.API", "ASP.NET Core\n.NET 8", "REST API + SignalR Hub")
        ContainerDb(db_dev, "SQLite", "SQLite\nbatatasfritas.db", "Banco de desenvolvimento local")
        ContainerDb(db_prod, "PostgreSQL", "PostgreSQL\nDocker", "Banco de produção")
        Container(mcp, "MCP Server", "Node.js\n@modelcontextprotocol/sdk", "Expõe skills e RAG para agentes de IA via JSON-RPC/stdio")
    }

    System_Ext(mp, "MercadoPago API", "Gateway de pagamento")
    System_Ext(nginx, "Nginx", "Proxy reverso + serve arquivos estáticos do WASM")

    Rel(cliente, nginx, "Acessa via HTTPS")
    Rel(admin, nginx, "Acessa via HTTPS + JWT")
    Rel(nginx, web, "Serve arquivos estáticos do WASM")
    Rel(nginx, api, "Proxy /api/* e /hubs/*", "HTTP")
    Rel(web, api, "Chamadas REST", "HTTPS/JSON")
    Rel(web, api, "Eventos tempo real", "WebSocket/SignalR")
    Rel(api, db_dev, "Lê/Escreve (dev)", "NHibernate")
    Rel(api, db_prod, "Lê/Escreve (prod)", "NHibernate")
    Rel(api, mp, "Cria pagamentos", "HTTPS REST")
    Rel(mp, api, "Notifica via webhook", "HTTPS POST + HMAC")
```

# Arquitetura C4 — Componentes (Nível 3)

> Componentes da API (container mais crítico)

```mermaid
C4Component
    title BatatasFritas.API — Componentes

    Container_Boundary(api, "BatatasFritas.API") {
        Component(pedidos_ctrl, "PedidosController", "ASP.NET Controller", "Criação de pedidos, baixa de estoque, cashback")
        Component(kds_ctrl, "KdsController", "ASP.NET Controller", "Atualização de status de pedidos (KDS)")
        Component(auth_ctrl, "AuthController", "ASP.NET Controller", "Login e geração de JWT")
        Component(webhook_ctrl, "WebhookController", "ASP.NET Controller", "Recebe e valida webhooks do MercadoPago")
        Component(mp_service, "MercadoPagoService", "Service", "Cria pagamentos PIX e Checkout Pro\nValida HMAC do webhook\nPolly retry para Point Smart 2")
        Component(hub, "PedidosHub", "SignalR Hub", "Broadcast de eventos para KDS e Cardápio")
        Component(infra, "Infrastructure", "NHibernate + FluentNHibernate", "Repositórios genéricos + UnitOfWork + Mappings")
    }

    ContainerDb(db, "Database", "SQLite ou PostgreSQL")
    System_Ext(mp, "MercadoPago API")

    Rel(pedidos_ctrl, infra, "Usa repositórios")
    Rel(pedidos_ctrl, hub, "Emite NovoPedido / ProdutoDesativado")
    Rel(kds_ctrl, infra, "Atualiza status")
    Rel(kds_ctrl, hub, "Emite StatusAtualizado / PedidoCancelado")
    Rel(webhook_ctrl, mp_service, "Valida HMAC")
    Rel(webhook_ctrl, infra, "Atualiza StatusPagamento do pedido")
    Rel(webhook_ctrl, hub, "Emite eventos de pagamento")
    Rel(mp_service, mp, "REST calls com retry Polly")
    Rel(infra, db, "NHibernate ORM")
```
