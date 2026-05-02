# User Story — Operador KDS Atualiza Status

> Gerado pelo Reversa (Redator) em 2026-05-01

## Descrição
**Como** operador da cozinha do Batata Palheta Brutal,
**Eu quero** visualizar os pedidos em tempo real e marcar as etapas de produção,
**Para que** a equipe de entrega e o cliente saibam exatamente em que estágio está o pedido.

---

## Cenários de Aceitação

### Cenário 1: Início de Produção
**Dado** que um novo pedido de "Batata Suprema" apareceu na tela do KDS (Status: Recebido)
**Quando** eu clico no botão "Aceitar" ou "Preparar"
**Então** o status do pedido deve mudar para "Em Preparo"
**E** a cor do card na tela deve mudar para indicar atividade
**E** o cliente deve receber a atualização no status do pedido no celular dele.

### Cenário 2: Finalização de Pedido
**Dado** que o pedido #123 está com status "Em Preparo"
**Quando** eu clico em "Finalizar"
**Então** o status do pedido deve mudar para "Pronto para Entrega" (se for Delivery) ou "Entregue" (se for Balcão)
**E** o pedido deve desaparecer da lista de pendentes da cozinha após alguns segundos.

### Cenário 3: Cancelamento pela Cozinha
**Dado** que um pedido foi feito por erro ou o cliente solicitou cancelamento via telefone
**Quando** eu clico em "Cancelar" no card do KDS e confirmo o motivo
**Então** o status deve mudar para "Cancelado"
**E** todos os outros terminais KDS devem remover o card imediatamente.

---

## Fluxo de Interação (UX)

1. **Monitoramento**: Cards são exibidos por ordem de chegada. Se o pedido demorar mais de 15 min, o card ganha um alerta visual (cor amarela/vermelha).
2. **Ação**: O operador usa interface touch para clicar nos botões de ação rápida no rodapé do card.
3. **Detalhes**: Se o operador clicar no corpo do card, abre um modal com a lista completa de itens, observações (ex: "Sem cebolinha") e complementos.
4. **Sincronia**: O sistema utiliza SignalR. Se o operador A aceita o pedido, o operador B vê a mudança de cor e status no mesmo segundo, sem precisar dar F5.

---

## Notas Técnicas para Agentes de IA
- O KDS filtra pedidos pelo status. Se o status for alterado para algo fora do range de produção (ex: `Entregue`), a UI deve remover o objeto da lista local.
- As ações do KDS disparam patches na API que, por sua vez, emitem eventos de broadcast via SignalR.
- É vital que a chave `@key` do Blazor seja o `PedidoId` para evitar cintilação da tela ou duplicação de cards durante re-renderizações.
