# SDD — MCP Server (Model Context Protocol)

> Gerado pelo Reversa (Redator) em 2026-05-01 | Nível: Detalhado
> Rastreabilidade: `mcp_server.js` (Root)

---

## Visão Geral

O `MCP Server` é um componente de infraestrutura desenvolvido em Node.js que implementa o **Model Context Protocol**. Sua finalidade é expor a inteligência do projeto (Skills) e a base de conhecimento (RAG) de forma estruturada para agentes de IA externos (como Claude Code ou Antigravity). Ele atua como uma interface de "ferramentas" que permite à IA ler e buscar documentação específica sem acesso direto desordenado ao filesystem.

---

## Responsabilidades

| Responsabilidade | MoSCoW |
|---|---|
| Implementar o protocolo JSON-RPC via transporte STDIO | **Must** |
| Listar e retornar o conteúdo de Skills (diretório `skills/`) | **Must** |
| Gerenciar e buscar em fontes de RAG (diretório `rag_sources/`) | **Must** |
| Realizar buscas full-text em arquivos RAG com contexto de linhas | **Must** |
| Fornecer status de versão e integridade do servidor | **Must** |
| Auto-detecção de arquivos de skill (SKILL.md, README.md, etc.) | **Should** |

---

## Interface (Tools)

O servidor expõe as seguintes "ferramentas" para os agentes de IA:

| Nome da Tool | Parâmetros | Função |
|---|---|---|
| `list_skills` | - | Lista todas as skills com suas descrições curtas. |
| `get_skill` | `name: string` | Retorna o markdown completo de uma skill. |
| `list_rag_categories` | - | Lista as pastas em `rag_sources/` (ex: logs). |
| `list_rag_files` | `category: string` | Lista arquivos dentro de uma categoria específica. |
| `read_rag_file` | `category: string, filename: string` | Lê o conteúdo bruto de um arquivo RAG. |
| `search_rag` | `query: string` | Busca o termo em todos os arquivos RAG e retorna trechos contextuais. |
| `get_project_status` | - | Retorna versão do servidor, contagem de skills e arquivos RAG. |

---

## Regras de Implementação e Lógica

1. 🟢 **Busca Contextual** — O algoritmo de `search_rag` não retorna apenas a linha que contém o termo, mas um contexto de `±1 linha` para dar clareza ao agente de IA sobre onde o termo está inserido.
2. 🟢 **Resiliência de Leitura** — O servidor captura erros de leitura de arquivos e diretórios, retornando respostas de erro amigáveis via protocolo MCP em vez de travar o processo.
3. 🟢 **Priorização de Arquivos de Skill** — Ao buscar uma skill, o servidor tenta localizar arquivos na ordem: `SKILL.md` → `skill.md` → `README.md` → `index.md`.
4. 🟡 **Filtragem de Diretórios** — Atualmente, o servidor filtra pastas `bin/` e `obj/` no `list_skills`, mas o mapeamento é manual e pode precisar de atualização para novas estruturas.
5. 🔴 **Categorias Inexistentes na Spec** — A descrição da tool `list_rag_files` menciona categorias como `documentacao`, `codigo` e `configuracoes`, mas o filesystem atual possui apenas a pasta `logs/`. 🔴 LACUNA

---

## Fluxo de Busca RAG

1. Recebe termo de busca via tool `search_rag`.
2. Itera sobre todas as categorias em `rag_sources/`.
3. Abre cada arquivo de texto.
4. Aplica busca case-insensitive em cada linha.
5. Coleta até 5 ocorrências por arquivo.
6. Monta string formatada com números de linha (ex: `[L42] texto...`).
7. Retorna o bloco de texto para o agente.

---

## Requisitos Não Funcionais

| Tipo | Requisito inferido | Evidência | Confiança |
|---|---|---|---|
| Interoperabilidade | Conformidade com o SDK oficial da Anthropic/ModelContextProtocol | `mcp_server.js:8` | 🟢 |
| Performance | Leitura síncrona de arquivos pequenos (apropriada para ferramentas de contexto) | `mcp_server.js:23` | 🟢 |
| Portabilidade | Uso de caminhos relativos via `__dirname` e `path.join` | `mcp_server.js:18` | 🟢 |

---

## Critérios de Aceitação

```gherkin
# Happy Path — Listar Skills
Quando o agente solicita `list_skills`
Então o servidor deve retornar uma lista formatada
  E cada item deve conter o nome da skill e o resumo extraído do arquivo markdown

# Happy Path — Busca RAG
Dado que existe um erro "OutOfMemory" em `logs/error.log`
Quando o agente busca por "OutOfMemory" via `search_rag`
Então o servidor deve retornar o arquivo, a linha exata e as linhas vizinhas como contexto

# Falha — Skill Inexistente
Quando o agente solicita `get_skill` com o nome "fantasma"
Então o servidor deve retornar uma mensagem de erro `isError: true`
  E listar as skills que estão realmente disponíveis
```

---

## Rastreabilidade de Código

| Arquivo | Classe / Função | Cobertura |
|---|---|---|
| `mcp_server.js` | Configuração do Server | 🟢 |
| `mcp_server.js` | `server.setRequestHandler` (CallToolRequestSchema) | 🟢 |
| `mcp_server.js` | `search_rag` logic | 🟢 |
| `package.json` | Dependência `@modelcontextprotocol/sdk` | 🟢 |
