# Análise Técnica — Módulos Web e MCP Server

> Continuação do code-analysis.md | Gerado pelo Reversa (Arqueólogo) em 2026-05-01

---

## Módulo 4: BatatasFritas.Web (Blazor WASM)

### Arquitetura de Páginas

| Página | Layout | Função |
|---|---|---|
| `Home` | MainLayout | Cardápio público — listagem de produtos por categoria |
| `Login` | MainLayout | Autenticação admin — envia JWT para localStorage |
| `KdsMonitor` | KdsLayout | Monitor KDS ao vivo com SignalR — atualiza pedidos em tempo real |
| `KdsLogin` | EmptyLayout | Login específico para operadores KDS |
| `DashboardPedidos` | MainLayout | Gestão de pedidos pelo admin |
| `DashboardAnalytics` | MainLayout | Analytics de vendas |
| `DashboardFinanceiro` | MainLayout | DRE simplificado (receitas vs despesas) |
| `AdminPanel` | MainLayout | Cadastro de Produtos, Insumos, Bairros, Complementos |
| `Estoque` | MainLayout | Gestão de estoque de insumos |
| `Totem` | TotemLayout | Cardápio self-service do totem |
| `TotemCheckout` | TotemLayout | Tela de checkout do totem |
| `TotemPagamentoResult` | TotemLayout | Resultado do pagamento (polling) |
| `TotemSucesso` | TotemLayout | Confirmação de sucesso |

### Serviços do Frontend

| Serviço | Padrão | Responsabilidade |
|---|---|---|
| `CarrinhoState` | Singleton (estado Scoped Blazor) | Estado global do carrinho com `event Action OnChange` |
| `AuthStateProvider` | Herda `AuthenticationStateProvider` | Claims simples em memória — sem persistência em localStorage |
| `AuthDelegatingHandler` | DelegatingHandler | Injeta Bearer token JWT em todas as chamadas ao HttpClient |
| `KdsAuthService` | Serviço | Autenticação específica do KDS (senha simples) |

### Algoritmos de CarrinhoState

- **Deduplicação inteligente**: `AdicionarItem` agrupa por `(ProdutoId + Observacao)` para o fluxo simples e por `(ProdutoId + Observacao + PrecoUnitario)` para itens com opções. Isso garante que a mesma batata sem observação não crie duas linhas, mas batatas com complementos diferentes ficam separadas.
- **Remoção por índice**: `RemoverItemPorIndice` usa posição exata — necessário porque um mesmo produto pode aparecer mais de uma vez com observações diferentes.

### 🔴 Lacuna Crítica — AuthStateProvider

```csharp
// Estado _isAuthenticated é apenas em memória
private bool _isAuthenticated = false;
```
🔴 **Um F5 desloga o usuário.** O JWT não é persistido em `localStorage` ou `sessionStorage`. O login deve ser refeito a cada refresh de página. Isso é um bug de UX relevante para produção.

---

## Módulo 5: MCP Server (Node.js)

### O que é

Um servidor de protocolo MCP (Model Context Protocol) que expõe as **skills** e o **RAG** do projeto como ferramentas consumíveis por agentes de IA (Claude Code, Antigravity, Kiro, etc.) via JSON-RPC/stdio.

### Ferramentas Expostas

| Tool | Descrição |
|---|---|
| `list_skills` | Lista todas as skills com descrição extraída do YAML frontmatter |
| `get_skill` | Retorna o conteúdo completo de uma skill por nome |
| `list_rag_categories` | Lista categorias de RAG com contagem de arquivos |
| `list_rag_files` | Lista arquivos de uma categoria RAG específica |
| `read_rag_file` | Lê o conteúdo de um arquivo RAG |
| `search_rag` | Busca full-text em todos os arquivos RAG — retorna trechos com número de linha |
| `get_project_status` | Status geral: versão, skills carregadas, RAG count |

### Arquitetura

```
mcp_server.js
├── skills/           → diretórios com SKILL.md (skills locais do projeto)
└── rag_sources/
    └── logs/         → logs do sistema (única categoria disponível atualmente)
```

### Algoritmo: search_rag

```javascript
// Busca por query em todos os arquivos de todas as categorias RAG
// Retorna até 5 matches por arquivo com contexto de ±1 linha
const start = Math.max(0, i - 1);
const end = Math.min(lines.length - 1, i + 1);
matches.push(`[L${i+1}] ${lines.slice(start, end+1).join(' | ').substring(0, 200)}`);
```
🟢 **Implementação eficiente**: usa `indexOf` implícito via `includes()` — O(n) por arquivo, adequado para repositórios pequenos/médios.

### 🟡 Observação

O RAG tem apenas a categoria `logs` atualmente. As pastas `documentacao`, `codigo` e `configuracoes` **mencionadas na descrição das ferramentas ainda não existem** no filesystem — só `logs` está presente.
