# User Story — Cliente Realiza Pedido

> Gerado pelo Reversa (Redator) em 2026-05-01

## Descrição
**Como** um cliente do restaurante Batata Palheta Brutal,
**Eu quero** escolher meus itens no cardápio digital, usar meu saldo de cashback e pagar via Pix,
**Para que** eu possa receber meu pedido em casa de forma rápida e prática.

---

## Cenários de Aceitação

### Cenário 1: Pedido Delivery Simples com Pix
**Dado** que eu acessei o cardápio digital
**E** selecionei uma "Batata Suprema Média" (R$ 35,90)
**E** selecionei o bairro "Centro" (Taxa: R$ 5,00)
**Quando** eu finalizo o pedido escolhendo "Pix" como pagamento
**Então** o sistema deve gerar o pedido com status "Recebido"
**E** deve me apresentar o QR Code Pix e o código "Copia e Cola"
**E** o KDS da cozinha deve exibir meu pedido instantaneamente.

### Cenário 2: Utilizando Saldo de Cashback
**Dado** que eu possuo R$ 15,00 de saldo de cashback vinculado ao meu telefone
**E** selecionei itens que totalizam R$ 40,00 (sem taxa)
**Quando** eu informo meu telefone e solicito o uso do saldo
**Então** o sistema deve validar meu saldo
**E** o valor final a pagar deve ser R$ 25,00 + taxa de entrega
**E** meu saldo de cashback deve ser debitado no momento da confirmação.

### Cenário 3: Produto Indisponível (Estoque Zerado)
**Dado** que o estoque de "Coca-Cola 2L" acabou de zerar na cozinha
**Quando** eu tento adicionar este item ao carrinho
**Então** o sistema deve me informar que o produto está temporariamente indisponível
**E** o item deve aparecer desativado (opaco/com aviso) no cardápio.

---

## Fluxo de Interação (UX)

1. **Navegação**: Cliente navega pelas categorias (Batatas, Porções, Bebidas).
2. **Customização**: Ao clicar em uma batata, abre modal para escolher molhos gratuitos ou adicionais pagos.
3. **Checkout**:
    - Preenche Nome, Telefone e Endereço.
    - Escolhe o Bairro (o sistema calcula o subtotal + taxa).
    - Opcional: Aplica Cashback.
4. **Pagamento**:
    - Se Pix/Online: Redireciona para tela com QR Code.
    - Se Dinheiro/Cartão (Presencial): Informa que o pagamento será feito na entrega.
5. **Acompanhamento**: Cliente permanece na tela de sucesso que atualiza o status (Recebido → Em Preparo → Saiu para Entrega) via SignalR.

---

## Notas Técnicas para Agentes de IA
- A validação de cashback exige que o telefone contenha apenas dígitos para bater com o banco.
- O cálculo de cashback acumulado só deve ocorrer após o pedido ser marcado como "Entregue" (regra de segurança para evitar fraude).
- O link de pagamento retornado pela API deve ser aberto em uma nova aba ou modal seguro.
