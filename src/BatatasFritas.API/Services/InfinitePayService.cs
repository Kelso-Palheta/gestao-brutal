using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using BatatasFritas.Domain.Entities;
using BatatasFritas.Shared.Enums;
using Microsoft.Extensions.Configuration;

namespace BatatasFritas.API.Services;

public interface IInfinitePayService
{
    Task<string> GerarLinkPagamento(Pedido pedido, string infiniteTag);
}

public class InfinitePayService : IInfinitePayService
{
    private readonly HttpClient _httpClient;
    private readonly string _checkoutUrl;
    private readonly string _redirectBaseUrl;
    private readonly string _webhookUrl;

    public InfinitePayService(HttpClient httpClient, IConfiguration config)
    {
        _httpClient     = httpClient;
        _checkoutUrl    = config["InfinitePay:CheckoutApiUrl"]  ?? "https://api.infinitepay.io/invoices/public/checkout/links";
        _redirectBaseUrl = config["InfinitePay:RedirectBaseUrl"] ?? "http://localhost:5255";
        _webhookUrl     = config["InfinitePay:WebhookUrl"]      ?? string.Empty;
    }

    public async Task<string> GerarLinkPagamento(Pedido pedido, string infiniteTag)
    {
        var items = pedido.Itens.Select(i => new
        {
            quantity    = i.Quantidade,
            price       = (long)(i.PrecoUnitario * 100), // centavos
            description = i.Produto.Nome
        }).ToList();

        if (pedido.TaxaEntrega > 0)
        {
            items.Add(new
            {
                quantity    = 1,
                price       = (long)(pedido.TaxaEntrega * 100),
                description = "Taxa de Entrega"
            });
        }

        var payload = new
        {
            handle      = infiniteTag,
            order_nsu   = pedido.Id.ToString(),
            items       = items,
            redirect_url = $"{_redirectBaseUrl}/pedido/sucesso?id={pedido.Id}",
            webhook_url  = _webhookUrl
        };

        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(_checkoutUrl, content);

        if (response.IsSuccessStatusCode)
        {
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<JsonElement>(json);
            
            if (result.TryGetProperty("invoice_url", out var invoiceUrlElement))
            {
                return invoiceUrlElement.GetString() ?? string.Empty;
            }
            if (result.TryGetProperty("checkout_url", out var checkoutUrlElement))
            {
                return checkoutUrlElement.GetString() ?? string.Empty;
            }
        }
        else 
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Erro InfinitePay: {errorContent}");
        }

        return string.Empty;
    }
}
