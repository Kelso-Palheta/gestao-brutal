using BatatasFritas.Domain.Entities;
using BatatasFritas.Infrastructure.Repositories;
using BatatasFritas.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace BatatasFritas.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DespesasController : ControllerBase
{
    private readonly IRepository<Despesa> _repo;
    private readonly IRepository<Insumo> _insumoRepo;
    private readonly IRepository<MovimentacaoEstoque> _movRepo;
    private readonly IUnitOfWork _uow;
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpClientFactory;

    public DespesasController(
        IRepository<Despesa> repo,
        IRepository<Insumo> insumoRepo,
        IRepository<MovimentacaoEstoque> movRepo,
        IUnitOfWork uow,
        IConfiguration config,
        IHttpClientFactory httpClientFactory)
    {
        _repo = repo;
        _insumoRepo = insumoRepo;
        _movRepo = movRepo;
        _uow = uow;
        _config = config;
        _httpClientFactory = httpClientFactory;
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] DateTime? de, [FromQuery] DateTime? ate)
    {
        var todas = await _repo.GetAllAsync();
        
        if (de.HasValue) todas = todas.Where(d => d.DataRegistro.Date >= de.Value.Date);
        if (ate.HasValue) todas = todas.Where(d => d.DataRegistro.Date <= ate.Value.Date);

        return Ok(todas.OrderByDescending(x => x.DataRegistro).Select(ToDto).ToList());
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] DespesaDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Descricao) || dto.Valor <= 0) return BadRequest();

        // Valida insumo antes de abrir transação
        Insumo? insumo = null;
        var vincularEstoque = dto.InsumoId.HasValue && dto.QuantidadeInsumo.GetValueOrDefault() > 0;
        if (vincularEstoque)
        {
            insumo = await _insumoRepo.GetByIdAsync(dto.InsumoId!.Value);
            if (insumo == null) return BadRequest("Insumo não encontrado.");
        }

        // Usa a data escolhida pelo usuário ao meio-dia (12:00) para evitar que o offset
        // UTC-3 faça a despesa aparecer no dia anterior ao filtrar (00:00 UTC → 21:00 BRT = dia errado).
        var dataCorrigida = dto.DataRegistro.Date.AddHours(12);
        var disp = new Despesa(dto.Descricao, dto.Valor, dataCorrigida, dto.Categoria, dto.Observacao);

        _uow.BeginTransaction();
        try
        {
            await _repo.AddAsync(disp);

            // ── Movimentação de estoque automática ────────────────────────
            if (vincularEstoque && insumo != null)
            {
                var quantidade = dto.QuantidadeInsumo!.Value;
                var valorUnitario = quantidade > 0 ? dto.Valor / quantidade : 0;
                var movimentacao = new MovimentacaoEstoque(
                    insumo,
                    TipoMovimentacao.Entrada,
                    quantidade,
                    valorUnitario,
                    motivo: $"Compra via despesa #{disp.Id}",
                    numeroNF: dto.NumeroNFMovimentacao ?? string.Empty
                );
                await _movRepo.AddAsync(movimentacao);
            }

            await _uow.CommitAsync();
        }
        catch
        {
            await _uow.RollbackAsync();
            throw;
        }

        return Ok(ToDto(disp));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var disp = await _repo.GetByIdAsync(id);
        if (disp == null) return NotFound();

        _uow.BeginTransaction();
        await _repo.DeleteAsync(disp);
        await _uow.CommitAsync();

        return NoContent();
    }

    // ── DELETE api/despesas/limpar-tudo ────────────────────────────────────
    [HttpDelete("limpar-tudo")]
    public async Task<IActionResult> LimparTudo()
    {
        try
        {
            _uow.BeginTransaction();
            await _uow.ExecuteHqlAsync("DELETE FROM Despesa");
            await _uow.CommitAsync();
            return Ok(new { mensagem = "Todas as despesas foram apagadas." });
        }
        catch (Exception ex)
        {
            await _uow.RollbackAsync();
            return BadRequest($"Erro: {ex.Message}");
        }
    }

    // ── POST api/despesas/extrair-nf ──────────────────────────────────────
    // Recebe imagem base64, chama Sabiá Vision (Maritaca AI), retorna campos pré-preenchidos
    // com lista de produtos extraídos da NF e insumos correspondentes.
    [HttpPost("extrair-nf")]
    public async Task<IActionResult> ExtrairNf([FromBody] ExtrairNfRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.ImagemBase64))
            return BadRequest("Imagem não enviada.");

        // Tenta config hierárquico (.NET padrão) e depois env var direta como fallback
        var apiKey = _config["Maritaca:ApiKey"]
            ?? Environment.GetEnvironmentVariable("Maritaca__ApiKey")
            ?? Environment.GetEnvironmentVariable("MARITACA_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
            return StatusCode(503, "Chave Maritaca não configurada. Adicione Maritaca__ApiKey ou MARITACA_API_KEY nas variáveis de ambiente.");

        try
        {
            var prompt = """
                Analise esta nota fiscal ou cupom fiscal brasileiro.
                Responda APENAS com JSON válido (sem markdown, sem explicações):
                {
                  "valor_total": <decimal total pago em reais>,
                  "data": "<YYYY-MM-DD, ou hoje se não encontrar>",
                  "descricao": "<resumo: o que foi comprado e de qual estabelecimento>",
                  "numero_nf": "<número da NF/cupom ou null>",
                  "cnpj_emitente": "<CNPJ do emissor ou null>",
                  "nome_emitente": "<nome do estabelecimento ou null>",
                  "itens": [
                    {
                      "nome": "<nome do produto exatamente como na nota>",
                      "unidade": "<un|kg|g|L|ml|cx|pct — infira pela embalagem se não estiver explícito>",
                      "quantidade": <decimal — quantas unidades/kg/L foram compradas>,
                      "valor_unitario": <decimal — preço por unidade/kg/L>,
                      "valor_total_item": <decimal — valor_unitario × quantidade>
                    }
                  ]
                }
                Liste TODOS os produtos distintos da nota em "itens". Se não conseguir ler algum campo use null ou valor razoável.
                """;

            var dataUri = $"data:{req.MimeType};base64,{req.ImagemBase64}";
            var body = new
            {
                model = "sabiazinho-4",
                max_tokens = 1024,
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = new object[]
                        {
                            new { type = "image_url", image_url = new { url = dataUri } },
                            new { type = "text", text = prompt }
                        }
                    }
                }
            };

            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(300); // Maritaca vision pode demorar com imagens grandes
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

            var jsonBody = JsonSerializer.Serialize(body);
            var httpContent = new StringContent(jsonBody, Encoding.UTF8, "application/json");
            var response = await client.PostAsync("https://chat.maritaca.ai/api/v1/chat/completions", httpContent);

            var responseText = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
                return StatusCode(502, $"Erro da API Maritaca: {responseText}");

            using var doc = JsonDocument.Parse(responseText);
            var textContent = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? "{}";

            // Remove markdown fences
            textContent = textContent.Trim();
            if (textContent.StartsWith("```")) textContent = textContent.Split('\n', 2)[1];
            if (textContent.EndsWith("```")) textContent = textContent[..^3];

            using var extracted = JsonDocument.Parse(textContent.Trim());
            var root = extracted.RootElement;

            decimal ParseDecimal(string raw) =>
                decimal.TryParse(raw.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0;

            var valorStr = root.TryGetProperty("valor_total", out var vp) ? vp.GetRawText() : "0";
            var valor = ParseDecimal(valorStr);

            var dataStr = root.TryGetProperty("data", out var dp) ? dp.GetString() ?? "" : "";
            DateTime data = DateTime.TryParse(dataStr, out var d) ? d : DateTime.Today;

            var descricao = root.TryGetProperty("descricao", out var desc) ? desc.GetString() ?? "" : "";
            var numeroNf  = root.TryGetProperty("numero_nf",  out var nf)  ? nf.GetString() : null;
            var cnpj      = root.TryGetProperty("cnpj_emitente", out var cn) ? cn.GetString() : null;
            var nomeEmit  = root.TryGetProperty("nome_emitente", out var ne) ? ne.GetString() : null;

            // ── Parse itens ───────────────────────────────────────────────
            var itensResponse = new List<ExtrairNfItemResponse>();
            if (root.TryGetProperty("itens", out var itensEl) && itensEl.ValueKind == JsonValueKind.Array)
            {
                var todosInsumos = (await _insumoRepo.GetAllAsync()).ToList();

                foreach (var el in itensEl.EnumerateArray())
                {
                    var nomeProd    = el.TryGetProperty("nome", out var nom) ? nom.GetString() ?? "" : "";
                    var unidade     = el.TryGetProperty("unidade", out var un) ? un.GetString() ?? "un" : "un";
                    var qtd         = ParseDecimal(el.TryGetProperty("quantidade", out var qtdP) ? qtdP.GetRawText() : "0");
                    var vlrUnit     = ParseDecimal(el.TryGetProperty("valor_unitario", out var vuP) ? vuP.GetRawText() : "0");
                    var vlrTotal    = ParseDecimal(el.TryGetProperty("valor_total_item", out var vtP) ? vtP.GetRawText() : "0");

                    if (vlrTotal == 0 && vlrUnit > 0 && qtd > 0) vlrTotal = vlrUnit * qtd;

                    // Correspondência por nome normalizado
                    var insumoMatch = MatchInsumo(nomeProd, todosInsumos);

                    itensResponse.Add(new ExtrairNfItemResponse
                    {
                        NomeProduto  = nomeProd,
                        Unidade      = unidade,
                        Quantidade   = qtd,
                        ValorUnitario = vlrUnit,
                        ValorTotal   = vlrTotal,
                        InsumoId     = insumoMatch?.Id,
                        InsumoNome   = insumoMatch?.Nome,
                        InsumoNovo   = insumoMatch == null
                    });
                }
            }

            var result = new ExtrairNfResponse
            {
                Sucesso      = true,
                NumeroNF     = numeroNf,
                CnpjEmitente = cnpj,
                NomeEmitente = nomeEmit,
                Itens        = itensResponse,
                Despesa = new DespesaDto
                {
                    Descricao    = descricao,
                    Valor        = valor,
                    DataRegistro = data,
                    Categoria    = "Compra de Insumo",
                    Observacao   = string.IsNullOrEmpty(numeroNf) ? null : $"NF {numeroNf}"
                }
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            return Ok(new ExtrairNfResponse
            {
                Sucesso = false,
                Erro = ex.Message,
                Despesa = new DespesaDto { DataRegistro = DateTime.Today, Categoria = "Compra de Insumo" }
            });
        }
    }

    // ── POST api/despesas/confirmar-nf ────────────────────────────────────
    // Cria 1 Despesa + N MovimentacaoEstoque(Entrada), auto-cria insumos ausentes.
    [HttpPost("confirmar-nf")]
    public async Task<IActionResult> ConfirmarNf([FromBody] ConfirmarNfRequest req)
    {
        if (req.Despesa.Valor <= 0)
            return BadRequest("Valor da despesa deve ser maior que zero.");
        if (!req.Itens.Any())
            return BadRequest("Nenhum item informado.");

        var dataCorrigida = req.Despesa.DataRegistro.Date.AddHours(12);
        var disp = new Despesa(req.Despesa.Descricao, req.Despesa.Valor, dataCorrigida,
            "Compra de Insumo", req.Despesa.Observacao);

        _uow.BeginTransaction();
        try
        {
            await _repo.AddAsync(disp);

            foreach (var item in req.Itens)
            {
                if (item.Quantidade <= 0) continue;

                Insumo? insumo;
                if (item.InsumoId.HasValue)
                {
                    insumo = await _insumoRepo.GetByIdAsync(item.InsumoId.Value);
                }
                else if (item.CriarInsumo && !string.IsNullOrWhiteSpace(item.NomeProduto))
                {
                    insumo = new Insumo(item.NomeProduto, item.Unidade, 0, item.ValorUnitario);
                    await _insumoRepo.AddAsync(insumo);
                }
                else
                {
                    continue; // sem insumo vinculado e sem flag de criação → pula
                }

                if (insumo == null) continue;

                var mov = new MovimentacaoEstoque(
                    insumo,
                    TipoMovimentacao.Entrada,
                    item.Quantidade,
                    item.ValorUnitario,
                    motivo: $"Compra NF \"{req.NumeroNF}\" — despesa #{disp.Id}",
                    numeroNF: req.NumeroNF
                );
                await _movRepo.AddAsync(mov);
            }

            await _uow.CommitAsync();
            return Ok(new { despesaId = disp.Id });
        }
        catch
        {
            await _uow.RollbackAsync();
            throw;
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    /// <summary>Normaliza string para comparação: minúsculo, sem acentos, sem pontuação.</summary>
    private static string Normalizar(string s)
    {
        var norm = s.Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder();
        foreach (var c in norm)
        {
            var cat = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
            if (cat != System.Globalization.UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }
        return sb.ToString().ToLowerInvariant().Trim();
    }

    /// <summary>Retorna o insumo mais parecido com nomeProduto ou null se não houver match razoável.</summary>
    private static Insumo? MatchInsumo(string nomeProduto, IList<Insumo> insumos)
    {
        if (string.IsNullOrWhiteSpace(nomeProduto) || !insumos.Any()) return null;

        var normProd = Normalizar(nomeProduto);
        var palavrasProd = normProd.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                                   .Where(p => p.Length > 2).ToHashSet();

        Insumo? melhor = null;
        int melhorScore = 0;

        foreach (var ins in insumos)
        {
            var normIns = Normalizar(ins.Nome);

            // Exact match
            if (normIns == normProd) return ins;

            // Containment
            int score = 0;
            if (normProd.Contains(normIns) || normIns.Contains(normProd))
                score += 10;

            // Palavras em comum
            var palavrasIns = normIns.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                                     .Where(p => p.Length > 2).ToHashSet();
            score += palavrasProd.Intersect(palavrasIns).Count() * 3;

            if (score > melhorScore && score >= 3)
            {
                melhorScore = score;
                melhor = ins;
            }
        }

        return melhor;
    }

    private static DespesaDto ToDto(Despesa d) => new()
    {
        Id = d.Id,
        Descricao = d.Descricao,
        Valor = d.Valor,
        DataRegistro = d.DataRegistro,
        Categoria = d.Categoria,
        Observacao = d.Observacao
    };
}
