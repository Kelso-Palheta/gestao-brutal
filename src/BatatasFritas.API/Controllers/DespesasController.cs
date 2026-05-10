using BatatasFritas.Domain.Entities;
using BatatasFritas.Infrastructure.Repositories;
using BatatasFritas.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
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
    private readonly IUnitOfWork _uow;
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpClientFactory;

    public DespesasController(IRepository<Despesa> repo, IUnitOfWork uow, IConfiguration config, IHttpClientFactory httpClientFactory)
    {
        _repo = repo;
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

        // Usa a data escolhida pelo usuário ao meio-dia (12:00) para evitar que o offset
        // UTC-3 faça a despesa aparecer no dia anterior ao filtrar (00:00 UTC → 21:00 BRT = dia errado).
        var dataCorrigida = dto.DataRegistro.Date.AddHours(12);
        var disp = new Despesa(dto.Descricao, dto.Valor, dataCorrigida, dto.Categoria, dto.Observacao);
        
        _uow.BeginTransaction();
        await _repo.AddAsync(disp);
        await _uow.CommitAsync();

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
    // Recebe imagem base64, chama Sabiá Vision (Maritaca AI), retorna campos pré-preenchidos.
    [HttpPost("extrair-nf")]
    public async Task<IActionResult> ExtrairNf([FromBody] ExtrairNfRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.ImagemBase64))
            return BadRequest("Imagem não enviada.");

        var apiKey = _config["Maritaca:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            return StatusCode(503, "Chave Maritaca não configurada. Adicione Maritaca:ApiKey no appsettings ou variável de ambiente Maritaca__ApiKey.");

        try
        {
            var prompt = """
                Analise esta imagem de uma nota fiscal ou cupom fiscal brasileiro.
                Extraia as seguintes informações e responda APENAS com um JSON válido, sem markdown:
                {
                  "valor_total": <número decimal, total da NF em reais>,
                  "data": "<data no formato YYYY-MM-DD, ou hoje se não encontrar>",
                  "descricao": "<descrição resumida: o que foi comprado, de qual estabelecimento>",
                  "categoria": "<uma destas opções exatas: Funcionario | Energia/Agua | Imposto | Aluguel | Compra de Insumo | Embalagem | Manutencao | Outros>",
                  "numero_nf": "<número da NF/cupom ou null>",
                  "cnpj_emitente": "<CNPJ do emissor ou null>",
                  "nome_emitente": "<nome do estabelecimento ou null>"
                }
                Se não conseguir extrair algum campo, use null ou um valor razoável.
                """;

            // Maritaca AI — API OpenAI-compatible com suporte a visão (sabiazinho-4)
            var dataUri = $"data:{req.MimeType};base64,{req.ImagemBase64}";
            var body = new
            {
                model = "sabiazinho-4",
                max_tokens = 512,
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
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

            var jsonBody = JsonSerializer.Serialize(body);
            var httpContent = new StringContent(jsonBody, Encoding.UTF8, "application/json");
            var response = await client.PostAsync("https://chat.maritaca.ai/api/v1/chat/completions", httpContent);

            var responseText = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return StatusCode(502, $"Erro da API Maritaca: {responseText}");

            // Resposta OpenAI-compatible: choices[0].message.content
            using var doc = JsonDocument.Parse(responseText);
            var textContent = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? "{}";

            // Remove possível markdown ```json ... ```
            textContent = textContent.Trim();
            if (textContent.StartsWith("```")) textContent = textContent.Split('\n', 2)[1];
            if (textContent.EndsWith("```")) textContent = textContent[..^3];

            using var extracted = JsonDocument.Parse(textContent.Trim());
            var root = extracted.RootElement;

            var valorStr = root.TryGetProperty("valor_total", out var vp) ? vp.GetRawText() : "0";
            decimal.TryParse(valorStr, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var valor);

            var dataStr = root.TryGetProperty("data", out var dp) ? dp.GetString() ?? "" : "";
            DateTime data = DateTime.TryParse(dataStr, out var d) ? d : DateTime.Today;

            var descricao   = root.TryGetProperty("descricao", out var desc) ? desc.GetString() ?? "" : "";
            var categoria   = root.TryGetProperty("categoria", out var cat)  ? cat.GetString()  ?? "Outros" : "Outros";
            var numeroNf    = root.TryGetProperty("numero_nf", out var nf)   ? nf.GetString() : null;
            var cnpj        = root.TryGetProperty("cnpj_emitente", out var cn) ? cn.GetString() : null;
            var nomeEmit    = root.TryGetProperty("nome_emitente", out var ne) ? ne.GetString() : null;

            var result = new ExtrairNfResponse
            {
                Sucesso      = true,
                NumeroNF     = numeroNf,
                CnpjEmitente = cnpj,
                NomeEmitente = nomeEmit,
                Despesa = new DespesaDto
                {
                    Descricao    = descricao,
                    Valor        = valor,
                    DataRegistro = data,
                    Categoria    = categoria,
                    Observacao   = string.IsNullOrEmpty(numeroNf) ? null : $"NF {numeroNf}"
                }
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            return Ok(new ExtrairNfResponse { Sucesso = false, Erro = ex.Message, Despesa = new DespesaDto { DataRegistro = DateTime.Today, Categoria = "Outros" } });
        }
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
