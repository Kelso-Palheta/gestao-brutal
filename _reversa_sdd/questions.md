# Questões de Validação — Reversa Reviewer

> Projeto: BatatasFritas | Data: 2026-05-01
> Nível: Detalhado | Severidade: Moderada/Crítica

Para finalizar o Relatório de Confiança, preciso que você esclareça os pontos abaixo sobre o novo fluxo de **Pagamento Manual**:

---

## 🔴 Crítico — Integridade e Fraude

### 1. Prevenção de Reuso de Comprovante
**Contexto:** O cliente envia o comprovante via WhatsApp. O operador aprova no Dashboard.
**Pergunta:** Existe alguma regra ou campo no Dashboard para registrar o ID da Transação do Banco (E2E ID do PIX) para evitar que o mesmo comprovante seja usado em dois pedidos?
- [ ] **Sim** (definir campo no SDD)
- [ ] **Não** (confiança total no operador)

### 2. Fluxo de Estorno Manual
**Contexto:** Pedido pago manualmente mas cancelado pela cozinha (ex: falta de insumo).
**Pergunta:** O sistema deve registrar que o valor foi devolvido ao cliente ou o controle financeiro de estornos será feito totalmente fora do sistema?
- [ ] **Fora do sistema** (apenas marcar como Cancelado)
- [ ] **Dentro do sistema** (adicionar status `Estornado` ou campo de observação)

---

## 🟡 Moderado — Operação e UX

### 3. Limpeza de Pedidos "Zumbis"
**Contexto:** O PIX manual tem validade de 30 min (conforme `pagamento-manual.md`).
**Pergunta:** Haverá um alerta visual no Dashboard para pedidos pendentes há mais de 30 minutos ou um processo automático para movê-los para `Cancelado`?
- [ ] **Alerta Visual** (Dashboard destaca em vermelho)
- [ ] **Cancelamento Automático** (Worker de 1 em 1 hora)
- [ ] **Manual** (Operador limpa quando quiser)

### 4. Persistência do PIX Hardcoded
**Contexto:** A string do PIX é fixa no frontend (vFASE 3.5).
**Pergunta:** Essa string será alterada frequentemente? Devemos movê-la para o `appsettings.json` ou uma tabela de `Configuracoes` para evitar novos deploys a cada troca de chave PIX?
- [ ] **Manter Hardcoded** (vFASE 3.5 apenas)
- [ ] **Mover para Configuração** (Recomendado)

---

> **Instruções:** Responda marcando os checkboxes ou adicionando comentários abaixo de cada item. Avise-me quando terminar.
