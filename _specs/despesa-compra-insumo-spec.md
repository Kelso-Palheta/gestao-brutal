# Spec: Despesa "Compra de Insumo" → Movimentação de Estoque Automática

**Versão:** 1.0  
**Status:** Aprovada  
**Autor:** Kelso Palheta  
**Data:** 2026-05-09  

---

## 1. Resumo

Quando o gestor registra uma despesa com categoria "Compra de Insumo", o sistema deve exibir campos adicionais (qual insumo e quantidade comprada). Ao salvar, além da despesa financeira, o sistema cria automaticamente uma `MovimentacaoEstoque` do tipo Entrada, atualizando o saldo do insumo sem etapa manual adicional.

---

## 2. Contexto e Motivação

**Problema:** Hoje registrar uma compra de insumo exige dois passos separados: (1) lançar a despesa no Dashboard Financeiro e (2) ajustar o estoque manualmente em Admin → Estoque. É redundante e propenso a esquecer o segundo passo.

**Por que agora:** Feature de OCR de NF (PR #54) já captura valor, data e categoria da nota. Faz sentido fechar o ciclo: NF lida → despesa + estoque atualizados de uma vez.

---

## 3. Goals

- [ ] G-01: Quando categoria = "Compra de Insumo", exibir seletor de insumo + campo de quantidade na modal de despesa
- [ ] G-02: Ao salvar, criar `MovimentacaoEstoque` (Entrada) atomicamente com a despesa (mesma transação)
- [ ] G-03: `EstoqueAtual` do insumo selecionado refletir a quantidade comprada imediatamente após salvar
- [ ] G-04: Campos de insumo são opcionais — se não preenchidos, salva só a despesa (retrocompatibilidade)

---

## 4. Non-Goals

- NG-01: Não criar insumos novos a partir dessa tela
- NG-02: Não vincular despesa a múltiplos insumos (1 despesa → 1 insumo por ora)
- NG-03: Não reverter movimentação ao deletar a despesa (exclusão simples da despesa não desfaz estoque)
- NG-04: Não alterar fluxo de despesas sem categoria "Compra de Insumo"

---

## 5. Usuários e Personas

**Usuário primário:** Gestor/dono da lanchonete, acessa via browser (mobile ou desktop).

**Jornada atual (sem a feature):**
1. Vai em Dashboard Financeiro → lança despesa "Compra de Insumo"
2. Vai em Admin → Estoque → Insumos → encontra o insumo → ajusta saldo manualmente
3. Dois passos, fácil esquecer o segundo

**Jornada futura (com a feature):**
1. Vai em Dashboard Financeiro → lança despesa, seleciona categoria "Compra de Insumo"
2. Aparece: dropdown de insumo + campo de quantidade
3. Salva → despesa registrada + estoque atualizado automaticamente

---

## 6. Requisitos Funcionais

### 6.1 Requisitos Principais

| ID | Requisito | Prioridade | Critério de Aceite |
|----|-----------|-----------|-------------------|
| RF-01 | Quando categoria = "Compra de Insumo", exibir dropdown de insumos e campo numérico de quantidade | Must | Campos aparecem ao selecionar categoria; somem ao mudar para outra |
| RF-02 | Dropdown listar todos os insumos ativos do sistema | Must | Lista carregada do endpoint GET api/insumos |
| RF-03 | Campo quantidade aceita apenas números positivos (decimal) | Must | Valor ≤ 0 ou vazio → bloquear envio com mensagem |
| RF-04 | Ao salvar despesa com InsumoId + Quantidade preenchidos, criar MovimentacaoEstoque (Entrada) na mesma transação | Must | Após salvar: GET api/insumos/{id} retorna EstoqueAtual incrementado pela quantidade |
| RF-05 | Valor unitário da MovimentacaoEstoque = Despesa.Valor / Quantidade | Must | valorTotal da movimentação = Despesa.Valor |
| RF-06 | Se InsumoId/Quantidade não preenchidos, salvar só a despesa (comportamento atual) | Must | Despesas antigas não são afetadas |
| RF-07 | Motivo da movimentação preenchido automaticamente: "Compra via despesa #{DespesaId}" | Should | Visível no histórico de estoque |
| RF-08 | Se NF foi extraída por OCR (numero_nf preenchido), propagar NumeroNF para a MovimentacaoEstoque | Should | Campo NumeroNF da movimentação = número extraído da NF |

### 6.2 Fluxo Principal (Happy Path)

1. Gestor clica "Nova Despesa" no Dashboard Financeiro
2. Preenche valor e descrição (ou usa OCR da NF)
3. Seleciona categoria "Compra de Insumo"
4. Sistema exibe: dropdown "Insumo" + campo "Quantidade comprada"
5. Gestor seleciona insumo e informa quantidade
6. Clica "Salvar"
7. Sistema: cria Despesa + MovimentacaoEstoque (Entrada) em uma transação
8. Modal fecha; lista de despesas atualizada; estoque do insumo incrementado

### 6.3 Fluxos Alternativos

**Fluxo A — Categoria mudada antes de salvar:**
1. Gestor seleciona "Compra de Insumo" → campos extras aparecem
2. Gestor muda para outra categoria → campos extras somem, valores resetados
3. Salva → apenas despesa, sem movimentação

**Fluxo B — Campos de insumo não preenchidos:**
1. Gestor seleciona "Compra de Insumo" mas não preenche insumo/quantidade
2. Salva → apenas despesa financeira salva (sem erro, retrocompatibilidade)

---

## 7. Requisitos Não-Funcionais

| ID | Requisito | Valor alvo |
|----|-----------|-----------|
| RNF-01 | Atomicidade | Despesa + Movimentação na mesma UoW transaction — rollback se qualquer uma falhar |
| RNF-02 | Autenticação | [Authorize] obrigatório — igual aos endpoints existentes |
| RNF-03 | Performance | Dropdown de insumos carrega na abertura do modal (não bloqueia render) |

---

## 8. Design e Interface

**Componentes afetados:**
- `DashboardFinanceiro.razor` — modal de nova despesa
- `DespesasController.cs` — POST api/despesas
- `DespesaDto.cs` — campos opcionais InsumoId + QuantidadeInsumo + NumeroNFMovimentacao

**Comportamento esperado:**

Dentro do modal, abaixo do select de categoria:
```
[Se categoria == "Compra de Insumo"]
  Label: "Insumo comprado"
  <select> lista de insumos ativos (nome + unidade)
  
  Label: "Quantidade comprada"
  <input type="number" min="0.001" step="0.001">
```

**Estados da UI:**
- Campos ocultos quando outra categoria selecionada
- Dropdown mostra placeholder "Selecione o insumo..." 
- Erro inline se quantidade ≤ 0 ao tentar salvar
- Spinner durante salvamento (já existente)

---

## 9. Modelo de Dados

**`DespesaDto` — campos novos (opcionais):**
```
InsumoId: int?           // null = não vinculado a insumo
QuantidadeInsumo: decimal?  // quantidade comprada
NumeroNFMovimentacao: string?  // propagar para MovimentacaoEstoque.NumeroNF
```

**Migrações:** Não necessárias — nenhum campo novo na tabela `despesas`. A relação é criada pela MovimentacaoEstoque com Motivo referenciando o ID da despesa.

---

## 10. Integrações e Dependências

| Dependência | Tipo | Impacto se indisponível |
|-------------|------|------------------------|
| GET api/insumos | Obrigatória | Dropdown vazio — campos ficam ocultos, despesa salva normalmente |
| IRepository\<Insumo\> | Obrigatória | 400 se InsumoId inválido |
| IRepository\<MovimentacaoEstoque\> | Obrigatória | Rollback de toda a transação |

---

## 11. Edge Cases e Tratamento de Erros

| Cenário | Trigger | Comportamento esperado |
|---------|---------|----------------------|
| EC-01: InsumoId inválido | Id não existe no banco | API retorna 400 "Insumo não encontrado" |
| EC-02: Quantidade zero ou negativa | Campo preenchido com ≤ 0 | Frontend bloqueia; API retorna 400 se bypass |
| EC-03: Falha ao criar movimentação | Erro de DB após criar despesa | Rollback completo — nem despesa nem movimentação persistem |
| EC-04: Insumo inativo | Insumo desativado após carregar dropdown | API retorna 400 "Insumo inativo" |
| EC-05: Categoria "Compra de Insumo" sem insumo | Campos não preenchidos | Salva só a despesa (RF-06) — sem erro |

---

## 12. Segurança e Privacidade

- **Autenticação:** `[Authorize]` já presente no controller — sem mudança
- **Dados financeiros:** Valor da despesa usado para calcular ValorUnitario — mesmo nível de proteção atual

---

## 13. Plano de Rollout

- **Estratégia:** Big bang (feature pequena, sem breaking change)
- **Rollback:** Campos opcionais no DTO — se remover a lógica do controller, comportamento volta ao atual sem migration
- **Monitoramento:** Verificar se movimentações criadas via despesa aparecem corretamente no histórico de estoque

---

## 14. Open Questions

| # | Pergunta | Impacto | Decisão |
|---|---------|---------|---------|
| OQ-01 | Ao deletar despesa, reverter movimentação de estoque? | Médio | **Não** nesta versão (NG-03) |

---

## 15. Decisões Tomadas

| Decisão | Alternativas | Racional |
|---------|-------------|---------|
| Campos opcionais no DTO | Endpoint separado | Retrocompatibilidade — não quebra despesas existentes |
| ValorUnitario = Valor/Quantidade | Campo manual | Deriva do que já existe — menos friction |
| Sem nova tabela de vínculo | despesa_insumo join table | Motivo da movimentação referencia a despesa — suficiente para rastreio |
