#!/usr/bin/env node

/**
 * Batata Palheta Brutal - MCP Server
 * v2.0.0 - Implementação real do protocolo MCP via JSON-RPC/stdio
 */

const { Server } = require('@modelcontextprotocol/sdk/server/index.js');
const { StdioServerTransport } = require('@modelcontextprotocol/sdk/server/stdio.js');
const {
  CallToolRequestSchema,
  ListToolsRequestSchema,
} = require('@modelcontextprotocol/sdk/types.js');
const fs = require('fs');
const path = require('path');

const PROJECT_DIR = __dirname;
const SKILLS_DIR = path.join(PROJECT_DIR, 'skills');
const RAG_DIR = path.join(PROJECT_DIR, 'rag_sources');

// ─── helpers ────────────────────────────────────────────────────────────────

function readFileText(filePath) {
  try {
    return fs.readFileSync(filePath, 'utf8');
  } catch (e) {
    return null;
  }
}

function listDir(dirPath) {
  try {
    return fs.readdirSync(dirPath);
  } catch {
    return [];
  }
}

function findSkillFile(skillDir) {
  const candidates = ['SKILL.md', 'skill.md', 'README.md', 'index.md'];
  for (const c of candidates) {
    const p = path.join(skillDir, c);
    if (fs.existsSync(p)) return p;
  }
  // fallback: first .md
  const files = listDir(skillDir).filter(f => f.endsWith('.md'));
  return files.length ? path.join(skillDir, files[0]) : null;
}

function getSkillList() {
  return listDir(SKILLS_DIR).filter(name => {
    const p = path.join(SKILLS_DIR, name);
    return fs.existsSync(p) && fs.statSync(p).isDirectory();
  });
}

function getRagCategories() {
  return listDir(RAG_DIR).filter(name => {
    const p = path.join(RAG_DIR, name);
    return fs.existsSync(p) && fs.statSync(p).isDirectory();
  });
}

// ─── server ─────────────────────────────────────────────────────────────────

const server = new Server(
  { name: 'batata-palheta-mcp', version: '2.0.0' },
  { capabilities: { tools: {} } }
);

// ─── tools/list ─────────────────────────────────────────────────────────────

server.setRequestHandler(ListToolsRequestSchema, async () => ({
  tools: [
    {
      name: 'list_skills',
      description: 'Lista todas as skills disponíveis no projeto com suas descrições.',
      inputSchema: { type: 'object', properties: {} },
    },
    {
      name: 'get_skill',
      description: 'Retorna o conteúdo completo de uma skill específica.',
      inputSchema: {
        type: 'object',
        properties: {
          name: {
            type: 'string',
            description: 'Nome da skill (ex: caveman, sdd-spec, prd-manager)',
          },
        },
        required: ['name'],
      },
    },
    {
      name: 'list_rag_categories',
      description: 'Lista as categorias disponíveis nas RAG sources do projeto.',
      inputSchema: { type: 'object', properties: {} },
    },
    {
      name: 'list_rag_files',
      description: 'Lista os arquivos de uma categoria RAG específica.',
      inputSchema: {
        type: 'object',
        properties: {
          category: {
            type: 'string',
            description: 'Categoria RAG (documentacao, codigo, configuracoes, logs)',
          },
        },
        required: ['category'],
      },
    },
    {
      name: 'read_rag_file',
      description: 'Lê o conteúdo de um arquivo RAG específico.',
      inputSchema: {
        type: 'object',
        properties: {
          category: {
            type: 'string',
            description: 'Categoria RAG',
          },
          filename: {
            type: 'string',
            description: 'Nome do arquivo dentro da categoria',
          },
        },
        required: ['category', 'filename'],
      },
    },
    {
      name: 'search_rag',
      description: 'Busca por termo em todos os arquivos RAG e retorna trechos relevantes.',
      inputSchema: {
        type: 'object',
        properties: {
          query: {
            type: 'string',
            description: 'Termo ou frase a buscar',
          },
        },
        required: ['query'],
      },
    },
    {
      name: 'get_project_status',
      description: 'Retorna status geral do projeto: skills carregadas, RAG sources, versão.',
      inputSchema: { type: 'object', properties: {} },
    },
  ],
}));

// ─── tools/call ─────────────────────────────────────────────────────────────

server.setRequestHandler(CallToolRequestSchema, async (request) => {
  const { name, arguments: args } = request.params;

  try {
    // list_skills
    if (name === 'list_skills') {
      const skills = getSkillList();
      const result = skills.map(skill => {
        const skillDir = path.join(SKILLS_DIR, skill);
        const skillFile = findSkillFile(skillDir);
        let description = '';
        if (skillFile) {
          const content = readFileText(skillFile) || '';
          const match = content.match(/description:\s*[>|]?\s*\n?([\s\S]*?)(?=\n---|\n\w+:|$)/);
          if (match) {
            description = match[1].trim().replace(/\n\s+/g, ' ').substring(0, 200);
          }
        }
        return `**${skill}**${description ? ': ' + description : ''}`;
      });

      return {
        content: [{ type: 'text', text: `Skills disponíveis (${skills.length}):\n\n${result.join('\n\n')}` }],
      };
    }

    // get_skill
    if (name === 'get_skill') {
      const skillName = args.name;
      const skillDir = path.join(SKILLS_DIR, skillName);

      if (!fs.existsSync(skillDir)) {
        const available = getSkillList().join(', ');
        return {
          content: [{ type: 'text', text: `Skill "${skillName}" não encontrada. Disponíveis: ${available}` }],
          isError: true,
        };
      }

      const skillFile = findSkillFile(skillDir);
      if (!skillFile) {
        return {
          content: [{ type: 'text', text: `Skill "${skillName}" encontrada mas sem arquivo de conteúdo.` }],
          isError: true,
        };
      }

      const content = readFileText(skillFile);
      return {
        content: [{ type: 'text', text: `# Skill: ${skillName}\n\n${content}` }],
      };
    }

    // list_rag_categories
    if (name === 'list_rag_categories') {
      const categories = getRagCategories();
      const details = categories.map(cat => {
        const files = listDir(path.join(RAG_DIR, cat)).filter(f => !fs.statSync(path.join(RAG_DIR, cat, f)).isDirectory());
        return `**${cat}** (${files.length} arquivo${files.length !== 1 ? 's' : ''})`;
      });

      return {
        content: [{ type: 'text', text: `Categorias RAG (${categories.length}):\n\n${details.join('\n')}` }],
      };
    }

    // list_rag_files
    if (name === 'list_rag_files') {
      const catDir = path.join(RAG_DIR, args.category);
      if (!fs.existsSync(catDir)) {
        return {
          content: [{ type: 'text', text: `Categoria "${args.category}" não encontrada.` }],
          isError: true,
        };
      }

      const files = listDir(catDir).filter(f => {
        const fp = path.join(catDir, f);
        return fs.existsSync(fp) && !fs.statSync(fp).isDirectory();
      });

      return {
        content: [{ type: 'text', text: `Arquivos em "${args.category}":\n\n${files.join('\n')}` }],
      };
    }

    // read_rag_file
    if (name === 'read_rag_file') {
      const filePath = path.join(RAG_DIR, args.category, args.filename);
      if (!fs.existsSync(filePath)) {
        return {
          content: [{ type: 'text', text: `Arquivo "${args.filename}" não encontrado em "${args.category}".` }],
          isError: true,
        };
      }

      const content = readFileText(filePath);
      if (!content) {
        return {
          content: [{ type: 'text', text: 'Erro ao ler o arquivo.' }],
          isError: true,
        };
      }

      return {
        content: [{ type: 'text', text: `# ${args.category}/${args.filename}\n\n${content}` }],
      };
    }

    // search_rag
    if (name === 'search_rag') {
      const query = args.query.toLowerCase();
      const results = [];

      for (const category of getRagCategories()) {
        const catDir = path.join(RAG_DIR, category);
        const files = listDir(catDir).filter(f => {
          const fp = path.join(catDir, f);
          return fs.existsSync(fp) && !fs.statSync(fp).isDirectory();
        });

        for (const file of files) {
          const content = readFileText(path.join(catDir, file));
          if (!content) continue;

          const lines = content.split('\n');
          const matches = [];

          lines.forEach((line, i) => {
            if (line.toLowerCase().includes(query)) {
              const start = Math.max(0, i - 1);
              const end = Math.min(lines.length - 1, i + 1);
              matches.push(`  [L${i + 1}] ${lines.slice(start, end + 1).join(' | ').substring(0, 200)}`);
            }
          });

          if (matches.length) {
            results.push(`**${category}/${file}** (${matches.length} ocorrência${matches.length !== 1 ? 's' : ''}):\n${matches.slice(0, 5).join('\n')}`);
          }
        }
      }

      if (!results.length) {
        return {
          content: [{ type: 'text', text: `Nenhum resultado para "${args.query}".` }],
        };
      }

      return {
        content: [{ type: 'text', text: `Resultados para "${args.query}":\n\n${results.join('\n\n')}` }],
      };
    }

    // get_project_status
    if (name === 'get_project_status') {
      const skills = getSkillList();
      const ragCategories = getRagCategories();
      const ragFileCounts = ragCategories.map(cat => {
        const files = listDir(path.join(RAG_DIR, cat)).filter(f => !fs.statSync(path.join(RAG_DIR, cat, f)).isDirectory());
        return `  - ${cat}: ${files.length} arquivo${files.length !== 1 ? 's' : ''}`;
      });

      const pkg = JSON.parse(readFileText(path.join(PROJECT_DIR, 'package.json')) || '{}');

      const status = [
        `# Batata Palheta Brutal - Status`,
        ``,
        `**Versão:** ${pkg.version || 'N/A'}`,
        `**MCP Server:** v2.0.0`,
        ``,
        `## Skills (${skills.length})`,
        skills.map(s => `  - ${s}`).join('\n'),
        ``,
        `## RAG Sources`,
        ragFileCounts.join('\n'),
      ].join('\n');

      return {
        content: [{ type: 'text', text: status }],
      };
    }

    return {
      content: [{ type: 'text', text: `Tool desconhecida: ${name}` }],
      isError: true,
    };

  } catch (error) {
    return {
      content: [{ type: 'text', text: `Erro ao executar "${name}": ${error.message}` }],
      isError: true,
    };
  }
});

// ─── start ───────────────────────────────────────────────────────────────────

async function main() {
  const transport = new StdioServerTransport();
  await server.connect(transport);
}

main().catch(err => {
  process.stderr.write(`MCP Server fatal error: ${err.message}\n`);
  process.exit(1);
});
