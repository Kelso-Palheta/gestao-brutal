# User Story — Administrador Gerencia Estoque

> Gerado pelo Reversa (Redator) em 2026-05-01

## Descrição
**Como** gestor do Batata Palheta Brutal,
**Eu quero** acompanhar os níveis de estoque de insumos e produtos e registrar entradas de mercadoria,
**Para que** eu possa evitar rupturas de estoque e garantir que o cardápio esteja sempre atualizado.

---

## Cenários de Aceitação

### Cenário 1: Reposição de Insumo (Entrada)
**Dado** que recebi um fardo de 10kg de "Batata Asterix" do fornecedor
**Quando** eu acesso o painel de Estoque e registro uma entrada de 10kg
**Então** o sistema deve atualizar o saldo atual do insumo
**E** deve criar um registro em "Movimentações de Estoque" com o valor pago, fornecedor e data.

### Cenário 2: Alerta de Estoque Baixo
**Dado** que o "Insumo Bacon" possui estoque mínimo configurado para 2kg
**Quando** a venda de um pedido faz o estoque atual cair para 1.5kg
**Então** o sistema deve exibir um alerta visual (cor vermelha ou ícone de aviso) no dashboard administrativo.

### Cenário 3: Reativação de Produto após Reposição
**Dado** que o produto "Coca-Cola 2L" estava desativado automaticamente por estoque zerado
**Quando** eu edito o produto e altero o estoque para 24 unidades
**E** marco o produto como "Ativo" novamente
**Então** o item deve voltar a aparecer instantaneamente no cardápio digital dos clientes.

---

## Fluxo de Interação (UX)

1. **Dashboard**: O Admin visualiza cards de resumo com itens abaixo do estoque mínimo.
2. **Lista de Insumos**: Tabela com filtros por nome, exibindo saldo atual, custo médio e última movimentação.
3. **Lançamento de Entrada**:
    - Seleciona o Insumo.
    - Informa Quantidade, Valor Unitário (da NF), Fornecedor e Número da NF.
    - Clica em "Salvar".
4. **Histórico**: Tela de auditoria onde é possível ver quem lançou a entrada/saída e por qual motivo (venda, perda, ajuste manual).

---

## Notas Técnicas para Agentes de IA
- As movimentações de saída por venda são geradas automaticamente pelo algoritmo `BaixarEstoque`.
- Ajustes manuais de inventário (perdas, quebras) devem ser registrados como tipo "Saída" com o motivo devidamente preenchido.
- O sistema permite estoque negativo de insumos para não bloquear a frente de caixa, mas o Admin deve ser notificado para regularizar.
