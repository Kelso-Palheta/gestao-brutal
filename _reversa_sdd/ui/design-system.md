# Design System — BatatasFritas

> Gerado pelo Reversa Design System em 2026-05-01
> Fonte: CSS Analysis | Estilo: Dark Premium

## 1. Cores (Paleta Brutal)

### 1.1 Cores Base
| Token | Cor | Hex | Uso |
|---|---|---|---|
| `bg-primary` | ![#2a2a2a](https://via.placeholder.com/15/2a2a2a/000000?text=+) | `#2a2a2a` | Fundo principal da aplicação |
| `bg-secondary` | ![#333333](https://via.placeholder.com/15/333333/000000?text=+) | `#333333` | Cards e elementos secundários |
| `border-default` | ![#444444](https://via.placeholder.com/15/444444/000000?text=+) | `#444444` | Bordas de inputs e chips |

### 1.2 Cores Semânticas (Estados)
| Estado | Cor | Hex | Gradiente |
|---|---|---|---|
| **Sucesso (Incluído)** | ![#4CAF50](https://via.placeholder.com/15/4CAF50/000000?text=+) | `#4CAF50` | `linear-gradient(135deg, #1b5e20 0%, #2e7d32 100%)` |
| **Erro (Removido)** | ![#f44336](https://via.placeholder.com/15/f44336/000000?text=+) | `#f44336` | `linear-gradient(135deg, #b71c1c 0%, #c62828 100%)` |
| **Aviso (Títulos)** | ![#888888](https://via.placeholder.com/15/888888/000000?text=+) | `#888888` | Usado em labels e títulos de seção |

---

## 2. Componentes (Tokens de UI)

### 2.1 Chips de Customização
- **Border Radius**: `12px`
- **Font Weight**: `600`
- **Shadow**: `0 2px 8px rgba(0, 0, 0, 0.3)`
- **Hover**: `transform: translateY(-1px)` com elevação de sombra.

### 2.2 Controles de Quantidade
- **Shape**: Pill (`border-radius: 20px`)
- **Background**: `rgba(0, 0, 0, 0.3)`
- **Interação**: Botões circulares com efeito de overlay no hover.

---

## 3. Tipografia

- **Títulos de Seção**: `0.95rem`, Peso `700`, `uppercase`, `letter-spacing: 0.5px`.
- **Corpo**: `0.95rem`, Peso `600` (Semi-bold).
- **Valores/Preços**: `1rem` a `1.2rem`, Peso `700`.

---

## 4. Breakpoints (Responsividade)

| Breakpoint | Nome | Uso |
|---|---|---|
| `< 768px` | Mobile | Cardápio Delivery, Botões menores |
| `> 1024px` | Desktop/Totem | Interface de autoatendimento, botões grandes para touch |
