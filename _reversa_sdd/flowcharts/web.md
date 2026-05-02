# Fluxograma — BatatasFritas.Web (Blazor WASM)

> Gerado pelo Reversa (Arqueólogo) em 2026-05-01 | Nível: Detalhado

## Mapa de Páginas e Layouts

```mermaid
flowchart TD
    subgraph Layouts
        ML[MainLayout\nNavMenu + Carrinho]
        KL[KdsLayout\nKDS sem carrinho]
        TL[TotemLayout\nSelf-service fullscreen]
        EL[EmptyLayout\nSem chrome]
    end

    subgraph Páginas Públicas
        H[Home\nCardápio + Categorias]
        LG[Login\nAutenticação Admin]
        KLG[KdsLogin]
    end

    subgraph Páginas Admin
        DP[DashboardPedidos]
        DA[DashboardAnalytics]
        DF[DashboardFinanceiro]
        AP[AdminPanel\nProdutos + Insumos + Bairros]
        ES[Estoque]
    end

    subgraph Fluxo Totem
        TT[Totem\nCardápio Totem]
        TCH[TotemCheckout]
        TPR[TotemPagamentoResult]
        TS[TotemSucesso]
    end

    subgraph KDS
        KM[KdsMonitor\nSignalR pedidos ao vivo]
    end

    ML --> H & LG
    KL --> KM
    TL --> TT --> TCH --> TPR --> TS
    EL --> KLG
    ML --> DP & DA & DF & AP & ES
```

## Fluxo: CarrinhoState (Estado Global)

```mermaid
flowchart TD
    A([Usuário clica Adicionar]) --> B{Item com mesmo\nprodutoId + observação\njá existe?}
    B -- sim → simples --> C[itemExistente.Quantidade += n]
    B -- sim → com opções --> D{mesmo preço?}
    D -- sim --> C
    D -- não --> E[Novo item separado]
    B -- não --> E
    C --> F[NotifyStateChanged\nOnChange?.Invoke]
    E --> F
    F --> G[UI re-renderiza\nCarrinhoOffcanvas + badges]

    H([RemoverItemPorIndice]) --> I{index válido?}
    I -- sim --> J[_itens.RemoveAt index\nNotifyStateChanged]
    I -- não --> K[Ignora]

    L([LimparCarrinho]) --> M[_itens.Clear\nLimpa TempNome, Telefone...\nNotifyStateChanged]
```

## Fluxo: AuthStateProvider (Blazor Auth)

```mermaid
flowchart TD
    A([GetAuthenticationStateAsync]) --> B{_isAuthenticated?}
    B -- não --> C[Retorna ClaimsPrincipal anônimo]
    B -- sim --> D["Retorna Claims:\nName=admin, Role=admin\nIdentity=jwt"]

    E([Login bem-sucedido]) --> F[MarkUserAsAuthenticated\n_isAuthenticated = true\nNotifyAuthenticationStateChanged]
    G([Logout]) --> H[MarkUserAsLoggedOut\n_isAuthenticated = false\nNotifyAuthenticationStateChanged]
```

## Fluxo: KdsMonitor (SignalR ao vivo)

```mermaid
sequenceDiagram
    participant KDS as KdsMonitor.razor
    participant Hub as PedidosHub (API)
    participant DB

    KDS->>Hub: HubConnection.StartAsync()
    Hub-->>KDS: Conexão estabelecida

    Note over KDS: Carrega pedidos pendentes via GET /api/pedidos

    Hub->>KDS: NovoPedido (pedidoId)
    KDS->>DB: GET /api/pedidos/{id}
    DB-->>KDS: PedidoDetalheDto
    KDS->>KDS: Adiciona na lista + StateHasChanged

    Hub->>KDS: StatusAtualizado (pedidoId, novoStatus)
    KDS->>KDS: Atualiza status local + StateHasChanged

    Hub->>KDS: PedidoCancelado (pedidoId)
    KDS->>KDS: Remove da lista + StateHasChanged
```
