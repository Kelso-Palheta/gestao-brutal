# Permissões e Papéis — BatatasFritas

> Gerado pelo Reversa (Detetive) em 2026-05-01 | Nível: Detalhado

---

## Papéis Identificados

| Papel | Descrição | Como é autenticado |
|---|---|---|
| **Admin** | Acesso completo — gerencia produtos, pedidos, financeiro e estoque | JWT Bearer (8h de validade) |
| **Operador KDS** | Apenas visualiza e atualiza status de pedidos | Senha simples via `KdsAuthService` (sem JWT) |
| **Cliente (Público)** | Acessa o cardápio e cria pedidos | Sem autenticação |
| **Totem (Público)** | Fluxo de self-service autônomo | Sem autenticação |

---

## Matriz de Permissões

| Funcionalidade | Público/Totem | Operador KDS | Admin |
|---|---|---|---|
| Ver cardápio | ✅ | ✅ | ✅ |
| Criar pedido | ✅ | ❌ | ✅ |
| Ver status pedido | ✅ (próprio) | ✅ (todos) | ✅ |
| Atualizar status (KDS) | ❌ | ✅ | ✅ |
| Cancelar pedido | ❌ | ✅ | ✅ |
| Gerenciar produtos | ❌ | ❌ | ✅ |
| Gerenciar insumos/estoque | ❌ | ❌ | ✅ |
| Dashboard financeiro | ❌ | ❌ | ✅ |
| Dashboard analytics | ❌ | ❌ | ✅ |
| Gerenciar cashback | ❌ | ❌ | ✅ |
| Gerenciar bairros | ❌ | ❌ | ✅ |
| Lançar despesas | ❌ | ❌ | ✅ |
| Limpar histórico | ❌ | ❌ | ✅ |

---

## Observações de Segurança

🟢 **JWT com expiração de 8h** — configurado em `appsettings.json` → `Jwt:SecretKey`.

🔴 **CORS aberto:** `SetIsOriginAllowed(origin => true)` — qualquer origem é permitida. Deve ser restringido em produção.

🟡 **KDS sem JWT:** O `KdsAuthService` usa uma senha simples (🔴 LACUNA: a senha parece estar hardcoded ou via configuração simples). Não há revogação ou expiração de sessão KDS.

🔴 **AuthStateProvider sem persistência:** O estado de autenticação do Admin no Blazor está apenas em memória — um refresh de página desloga o usuário. Deve persistir o token no `localStorage`.

🟡 **Endpoints sem [Authorize]:** O `WebhookController` e o endpoint `/api/health` são públicos por design. Outros controllers devem ter `[Authorize]` verificado individualmente.
