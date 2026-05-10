# Spec: Vínculo Produto ↔ Insumo (Revenda 1:1)

**Status:** Draft
**Owner:** KPM
**Data:** 2026-05-09
**Branch:** claude/stoic-tesla-80a44e

---

## 1. Problema

Produtos revendidos sem transformação (bebidas, doces, snacks de fornecedor) hoje exigem um dos dois caminhos:

1. **Lançar estoque duas vezes** — uma vez no Insumo (controle financeiro/CMV) e outra vez no Produto (`Produto.EstoqueAtual`). Operador esquece, lança em um só, pedido falha.
2. **Cadastrar Receita 1:1** — criar Receita ligando Produto → Insumo com quantidade 1. Funciona, mas polui tabela de receitas com itens triviais e onera operador.

Bug atual: Coca cola 350ml com 6 unid no Insumo retorna `Estoque insuficiente. Disponível: 0` no carrinho. Ver [PedidosController.cs:60-72](src/BatatasFritas.API/Controllers/PedidosController.cs:60).

## 2. Objetivos

- Permitir que Produto vincule diretamente a 1 Insumo, com quantidade configurável.
- Pre-check e baixa de estoque consultam Insumo vinculado quando presente.
- Eliminar duplicidade de lançamento para itens revendidos.
- Não quebrar Receita atual (produtos compostos seguem com receita).

## 3. Non-Goals

- Não substituir Receita.
- Não suportar múltiplos insumos por vínculo direto (caso composto = use Receita).
- Não auto-vincular por nome/heurística — vínculo é manual e explícito.
- Não migrar produtos existentes automaticamente.

## 4. Usuários

- **Operador admin** (cadastra produtos no painel Blazor).
- **Cliente final** (cardápio digital, totem) — impacto indireto: pedidos param de falhar.

## 5. Requisitos Funcionais

| ID | Requisito | Critério de aceite |
|---|---|---|
| RF-01 | Produto pode opcionalmente referenciar 1 Insumo via `InsumoVinculadoId` (nullable). | Migration adiciona coluna nullable. Produto sem vínculo continua funcionando. |
| RF-02 | Produto vinculado armazena `QuantidadePorUnidade` (decimal, default 1). | Ex: produto "Pack 2 Cocas" vinculado ao insumo "Coca 350ml" com QtdPorUnidade=2 → cada venda baixa 2 do insumo. |
| RF-03 | Pre-check de estoque em `POST /api/pedidos` resolve estoque na ordem: receita > insumo vinculado > `Produto.EstoqueAtual`. | Produto com vínculo + insumo com 6 unid → pedido de 1 unid passa. Produto com vínculo + insumo com 0 → retorna 400 com nome do produto e disponibilidade. |
| RF-04 | Baixa de estoque na transação respeita mesma ordem da RF-03. | Após pedido confirmado, `Insumo.EstoqueAtual` cai por `item.Quantidade × QuantidadePorUnidade`. `MovimentacaoEstoque` registrada com tipo Saída e referência ao pedido. |
| RF-05 | Form de cadastro/edição de Produto no admin Blazor expõe dropdown de Insumos + campo numérico de QuantidadePorUnidade. | Campos são opcionais. Quando preenchidos, campo `EstoqueAtual` do produto fica oculto/desabilitado com aviso "Estoque controlado pelo Insumo vinculado". |
| RF-06 | DTOs `ProdutoDto` (POST e PUT) ganham `InsumoVinculadoId?` e `QuantidadePorUnidade`. | Backend valida: se `InsumoVinculadoId` setado, `QuantidadePorUnidade` deve ser > 0. |
| RF-07 | Excluir Insumo referenciado por Produto: bloquear ou setar FK como null. | FK `ON DELETE SET NULL`. Admin recebe aviso de produtos afetados antes da exclusão. |
| RF-08 | Mensagem de erro de estoque inclui nome do Insumo quando origem é vínculo. | Ex: `"Estoque insuficiente para Coca cola 350ml (insumo: Coca 350ml). Disponível: 2, Solicitado: 5."` |

## 6. Requisitos Não-Funcionais

| ID | Requisito |
|---|---|
| RNF-01 | Migration nullable → zero downtime, zero impacto em produtos existentes. |
| RNF-02 | Pre-check + baixa permanecem dentro da mesma transação NHibernate (consistência). |
| RNF-03 | Pre-check executa em ≤ 50ms para carrinho de até 20 itens. |
| RNF-04 | Logs incluem produto_id, insumo_id, quantidade resolvida para auditoria. |

## 7. Design

### 7.1 Modelo de domínio

```
Produto
├── Id, Nome, ...
├── EstoqueAtual (legado, usado só quando sem receita E sem vínculo)
├── InsumoVinculado : Insumo? (FK nullable)
└── QuantidadePorUnidade : decimal (default 1.0)

Resolução de estoque na venda:
  if produto.Receitas.Any() → consumir via receita (atual)
  else if produto.InsumoVinculado != null → consumir InsumoVinculado × QuantidadePorUnidade
  else → consumir Produto.EstoqueAtual (atual)
```

### 7.2 Mudanças por arquivo

| Arquivo | Mudança |
|---|---|
| `src/BatatasFritas.Domain/Entities/Produto.cs` | Add `InsumoVinculado`, `QuantidadePorUnidade`. Helper `ResolverFonteEstoque()`. |
| `src/BatatasFritas.Infrastructure/Mappings/ProdutoMap.cs` | `References(x => x.InsumoVinculado).Column("insumo_vinculado_id").Nullable()`. `Map(x => x.QuantidadePorUnidade).Default("1")`. |
| Migration FluentNH/SQL | `ALTER TABLE produtos ADD COLUMN insumo_vinculado_id INT NULL REFERENCES insumos(id) ON DELETE SET NULL, ADD COLUMN quantidade_por_unidade NUMERIC(10,3) NOT NULL DEFAULT 1`. |
| `src/BatatasFritas.Shared/DTOs/ProdutoDto.cs` | `int? InsumoVinculadoId`, `decimal QuantidadePorUnidade = 1`. |
| `src/BatatasFritas.API/Controllers/PedidosController.cs` (~L60 pre-check, ~L278 baixa) | Inserir branch `else if InsumoVinculado != null` antes do branch `Produto.EstoqueAtual`. |
| `src/BatatasFritas.API/Controllers/ProdutosController.cs` (POST L67, PUT L89-93) | Atribuir `InsumoVinculado` (lookup via repo) e `QuantidadePorUnidade`. |
| `src/BatatasFritas.Web/Pages/Admin/Produtos*.razor` (form) | Dropdown insumos + input QuantidadePorUnidade. Esconder `EstoqueAtual` quando vinculado. |
| Testes domain | `ProdutoTests` cobrindo resolução de fonte de estoque. |

### 7.3 Pseudocódigo do pre-check (substitui [PedidosController.cs:60-72](src/BatatasFritas.API/Controllers/PedidosController.cs:60))

```csharp
foreach (var item in dto.Itens)
{
    var produto = await _produtoRepository.GetByIdAsync(item.ProdutoId);
    if (produto == null)
        return BadRequest($"Produto {item.ProdutoId} não encontrado.");

    var receitas = await _receitaRepository.FindManyAsync(ir => ir.Produto.Id == item.ProdutoId);
    if (receitas.Any())
    {
        // fluxo atual: checar insumos da receita (já existe)
        continue;
    }

    if (produto.InsumoVinculado != null)
    {
        var consumo = item.Quantidade * produto.QuantidadePorUnidade;
        if (produto.InsumoVinculado.EstoqueAtual < consumo)
            return BadRequest(
                $"Estoque insuficiente para {produto.Nome} " +
                $"(insumo: {produto.InsumoVinculado.Nome}). " +
                $"Disponível: {produto.InsumoVinculado.EstoqueAtual}, Solicitado: {consumo}.");
        continue;
    }

    if (produto.EstoqueAtual < item.Quantidade)
        return BadRequest($"Estoque insuficiente para {produto.Nome}. Disponível: {produto.EstoqueAtual}, Solicitado: {item.Quantidade}.");
}
```

## 8. Edge Cases

| Caso | Comportamento |
|---|---|
| Produto vinculado mas insumo deletado (FK virou NULL) | Trata como produto sem vínculo → cai em `Produto.EstoqueAtual`. Admin vê aviso. |
| `QuantidadePorUnidade = 0` ou negativo | Validação rejeita no PUT/POST. |
| Produto tem receita E vínculo direto | Receita ganha. Vínculo ignorado. UI mostra aviso de redundância. |
| Pedido com 2 produtos vinculados ao mesmo insumo | Pre-check soma consumo antes de validar. Evita aprovar pedido que estoura estoque. |
| Cashback + cancelamento de pedido | Reverter baixa do insumo segue lógica atual de `MovimentacaoEstoque` em [FinanceiroController.cs:315-326](src/BatatasFritas.API/Controllers/FinanceiroController.cs:315). |
| Migração: produtos existentes com `EstoqueAtual > 0` sem vínculo | Mantêm comportamento atual. Nenhuma migração de dados. |
| Concorrência: 2 pedidos simultâneos do mesmo insumo | NHibernate lock otimista no `Insumo` (existente) cobre. |

## 9. Plano de Implementação

| Fase | Entrega | Esforço |
|---|---|---|
| 1 | Domain + Map + Migration + testes unidade | 1d |
| 2 | API: pre-check + baixa + DTO + ProdutosController | ½d |
| 3 | UI Admin Blazor (form Produto) | ½d |
| 4 | Testes integração + deploy staging | ½d |

Total: ~2,5 dias.

## 10. Open Questions

- ⚠️ ABERTO: `QuantidadePorUnidade` decimal(10,3) suficiente? (cobre 0,001 unid — ok p/ ml, g, %).
- ⚠️ ABERTO: `ON DELETE SET NULL` ou bloquear delete de Insumo referenciado? Recomendo SET NULL + aviso.
- ⚠️ ABERTO: UI deve oferecer auto-sugestão de insumo por similaridade de nome? (fora do escopo desta spec.)

## 11. Critérios de Aceite (testáveis)

- [ ] Migration aplicada em staging sem erros, produtos legados intactos.
- [ ] Cadastrar Produto vinculado via admin, fazer pedido no carrinho → pedido aprovado, insumo baixado pela quantidade × QuantidadePorUnidade.
- [ ] Pedido com insumo vinculado zerado → 400 com mensagem incluindo nome do insumo.
- [ ] 2 produtos no mesmo pedido, ambos vinculados ao mesmo insumo, soma > estoque → 400.
- [ ] Produto sem vínculo nem receita → comportamento legado preservado (regressão zero).
- [ ] Cancelamento de pedido → estorno de `MovimentacaoEstoque` no insumo vinculado.

---

## Score (auto-avaliação)

| Dimensão | Peso | Nota | Total |
|---|---|---|---|
| Completude | 30% | 90 | 27 |
| Testabilidade | 25% | 92 | 23 |
| Clareza | 20% | 88 | 17,6 |
| Escopo | 15% | 95 | 14,25 |
| Edge Cases | 10% | 85 | 8,5 |
| **Total** | | | **90,35 / 100** |

**Status:** Pronta para implementação. 3 open questions são preferências, não bloqueadores.
