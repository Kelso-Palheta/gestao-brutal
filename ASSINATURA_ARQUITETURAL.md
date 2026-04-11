# Assinatura Arquitetural — Projeto BatatasFritas
> **Versão:** 1.1 (Abril 2026 — Pós-simplificação de pagamentos)
> **Objetivo:** Fonte única de verdade para contexto arquitetural e desenvolvimento técnico, servindo como guia mestre para IAs e Desenvolvedores.

---

## 1. Stack Tecnológica Core

- **Linguagem Principal:** C# (.NET 8.0)
- **Frontend:** Blazor WebAssembly (WASM)
- **API:** ASP.NET Core Web API
- **ORM:** NHibernate 5.5 + FluentNHibernate 3.4
- **Bancos de Dados:**
  - **Desenvolvimento:** SQLite (local `batatasfritas.db`)
  - **Produção:** PostgreSQL (via Docker/Coolify)
- **Real-time:** SignalR (Notificações de KDS)
- **Segurança:** 
  - JWT Bearer Authentication (Tokens de 8h)
  - BCrypt.Net-Next (Hashing de senhas)
- **Deployment:** Docker, Nginx (Proxy/Serving Static Files)

---

## 2. Visão Geral da Arquitetura (Clean Architecture)

O projeto está dividido em camadas com responsabilidades bem definidas:

### `BatatasFritas.Domain`
- **Responsabilidade:** O "coração" do sistema. Contém entidades ricas, enums e lógica de negócio pura.
- **Entidades Principais:** `Pedido`, `Produto`, `Insumo`, `CarteiraCashback`, `TransacaoCashback`.
- **Regras de Ouro:** Não deve depender de nenhuma outra camada.

### `BatatasFritas.Infrastructure`
- **Responsabilidade:** Implementações técnicas e acesso a dados.
- **Destaques:** 
  - Mapeamentos NHibernate (Fluent).
  - Repositório Genérico (`NHibernateRepository<T>`).
  - Gerenciamento de Transações (Unit of Work).
  - Configuração de Injeção de Dependências.

### `BatatasFritas.API`
- **Responsabilidade:** Porta de entrada REST e SignalR.
- **Destaques:**
  - Controllers para Pedidos, KDS, Relatórios, Financeiro, Cashback.
  - Hubs SignalR para atualizações em tempo real no KDS.
  - Middlewares de Autenticação e Autorização.

### `BatatasFritas.Shared`
- **Responsabilidade:** Contratos compartilhados.
- **Destaques:** DTOs (Data Transfer Objects), Enums de status (`StatusPedido`, `TipoMovimentacao`).

### `BatatasFritas.Web`
- **Responsabilidade:** Interface do usuário (Frontend).
- **Destaques:**
  - SPA Blazor WASM.
  - Gerenciamento de Estado (`CarrinhoState`).
  - Proxy reverso Nginx para comunicação com a API.

---

## 3. Padrões de Desenvolvimento e Interações

### Fluxo de uma Requisição (Ex: Criar Pedido)
1. **Web:** O usuário finaliza o carrinho -> Chama `POST /api/pedidos`.
2. **API:** O `PedidosController` recebe o DTO, valida o token JWT.
3. **API:** Abre uma transação via `_uow.BeginTransaction()`.
4. **Infra/Domain:** Salva o `Pedido`, atualiza a `CarteiraCashback`, gera `MovimentacaoEstoque` (se necessário).
5. **API:** Faz `commit` da transação.
6. **API:** Emite evento via SignalR (`NovoPedido`) para todos os KDS conectados.
7. **API:** Retorna o ID do pedido e o status inicial. 
   - *Nota:* O sistema suporta múltiplos métodos de pagamento para um único pedido (Divisão de Pagamento).

### Gerenciamento de Transações
**Sempre** utilize o padrão `try/catch` com `Rollback` em métodos de escrita nos Controllers:
```csharp
using var tx = _uow.BeginTransaction();
try {
    // ... lógica ...
    await _uow.CommitAsync();
} catch {
    await _uow.RollbackAsync();
    throw;
}
```

---

## 4. Guia do Desenvolvedor (Contexto para IAs)

> [!IMPORTANT]
> Se você é uma IA trabalhando neste projeto, siga estas diretrizes rigorosamente para manter a integridade do sistema.

1. **Novas Entidades:** Crie no `Domain.Entities`, herde de `EntityBase` e adicione o mapeamento Fluent no `Infrastructure`.
2. **Novos DTOs:** Coloque sempre no `Shared`. Evite retornar entidades de domínio diretamente na API.
3. **Cashback:** Toda operação de cashback deve ser registrada na `CarteiraCashback` E gerar uma `TransacaoCashback` para histórico.
4. **Estoque:** Baixas de estoque devem ser automáticas no processamento do pedido (`PedidosController.BaixarEstoque`).
5. **Soft Delete:** Para entidades como `Insumo` e `Produto`, use a propriedade `.Ativo = false` em vez de deletar fisicamente se houver vínculos históricos.
6. **Timezone:** O sistema assume UTC internamente. Aplique conversões apenas na camada de exibição se necessário.
7. **Segurança:** Sempre valide inputs e use HTTPS em produção. O sistema usa JWT para endpoints administrativos.
8. **Divisão de Pagamentos:** Sempre use os IDs `1` (Dinheiro), `2` (Cartão) e `4` (Pix). A lógica de divisão deve sempre validar se a soma dos valores informados bate com o total do pedido.

---

## 5. Modelo de Pagamentos (Simplificado)

O sistema foi simplificado para suportar apenas **três métodos oficiais**, garantindo consistência entre Loja Web e Totem:
1. **Dinheiro (1):** Pagamento presencial. No Totem, instrui o cliente a pagar no caixa.
2. **Cartão (2):** Crédito ou Débito na maquininha física (presencial).
3. **PIX (4):** Pagamento manual via chave PIX da loja.

### Divisão de Pagamento
O sistema permite que um pedido seja pago com **dois meios simultâneos** (ex: R$ 50,00 em Pix e o restante em Dinheiro). Esta lógica é centrada no `NovoPedidoDto` e refletida no KDS como badges duplos.
---

## 6. Comandos e Manutenção Comuns

- **Build local:** `dotnet build`
- **Rodar Docker (Dev):** `docker-compose up --build`
- **Publicar Frontend:** `dotnet publish src/BatatasFritas.Web/BatatasFritas.Web.csproj -c Release`
- **Proxy Nginx:** Arquivo `nginx.conf` no site Web lida com o roteamento SPA e `proxy_pass` para a API.

---
*Este documento é a Assinatura do Projeto. Qualquer mudança estrutural deve ser refletida aqui.*
