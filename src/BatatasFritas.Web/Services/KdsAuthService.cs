using BatatasFritas.Shared.DTOs;
using Microsoft.JSInterop;
using System.Net.Http.Json;

namespace BatatasFritas.Web.Services;

/// <summary>
/// Serviço de autenticação do KDS.
/// A validação da senha é feita pela API (POST api/configuracoes/auth/login),
/// que compara contra o hash BCrypt armazenado no PostgreSQL.
/// O estado de sessão continua na sessionStorage do navegador.
/// </summary>
public class KdsAuthService
{
    private readonly IJSRuntime _js;
    private readonly HttpClient _http;
    private const string SessionKey = "kds_autenticado";

    public KdsAuthService(IJSRuntime js, HttpClient http)
    {
        _js   = js;
        _http = http;
    }

    /// <summary>Verifica se o usuário está autenticado nessa aba/sessão.</summary>
    public async Task<bool> EstaAutenticadoAsync()
    {
        var valor = await _js.InvokeAsync<string?>("sessionStorage.getItem", SessionKey);
        return valor == "true";
    }

    /// <summary>
    /// Autentica contra a API. Se a API confirmar, marca a sessão local como autenticada.
    /// </summary>
    public async Task<bool> LoginAsync(string senha)
    {
        try
        {
            var response = await _http.PostAsJsonAsync(
                "api/configuracoes/auth/login",
                new LoginKdsRequest { Senha = senha });

            if (response.IsSuccessStatusCode)
            {
                await _js.InvokeVoidAsync("sessionStorage.setItem", SessionKey, "true");
                return true;
            }
            return false;
        }
        catch
        {
            // Fallback offline — não autentica se a API estiver fora
            return false;
        }
    }

    /// <summary>Encerra a sessão limpando o sessionStorage.</summary>
    public async Task LogoutAsync()
    {
        await _js.InvokeVoidAsync("sessionStorage.removeItem", SessionKey);
    }
}
