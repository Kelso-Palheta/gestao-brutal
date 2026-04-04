using Microsoft.JSInterop;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace BatatasFritas.Web.Services;

/// <summary>
/// Serviço de autenticação do KDS com JWT.
/// Obtém token em POST /api/auth/login e o armazena em sessionStorage.
/// O HttpClient do KDS envia o token automaticamente via AuthDelegatingHandler.
/// </summary>
public class KdsAuthService
{
    private readonly IJSRuntime  _js;
    private readonly HttpClient  _http;
    private readonly AuthStateProvider _authStateProvider;
    private const string TokenKey = "kds_jwt_token";

    // Token em memória para leitura síncrona pelo AuthDelegatingHandler
    private string? _token;

    public KdsAuthService(IJSRuntime js, HttpClient http, AuthStateProvider authStateProvider)
    {
        _js   = js;
        _http = http;
        _authStateProvider = authStateProvider;
    }

    /// <summary>Expõe o token atual de forma síncrona (para o DelegatingHandler).</summary>
    public string? GetToken() => _token;

    /// <summary>
    /// Tenta restaurar o token da sessionStorage (chamado no startup do app).
    /// </summary>
    public async Task RestaurarSessaoAsync()
    {
        _token = await _js.InvokeAsync<string?>("sessionStorage.getItem", TokenKey);
        if (!string.IsNullOrEmpty(_token))
            _authStateProvider.MarkUserAsAuthenticated();
    }

    /// <summary>Verifica se há um token válido em memória.</summary>
    public async Task<bool> EstaAutenticadoAsync()
    {
        if (!string.IsNullOrEmpty(_token)) return true;
        _token = await _js.InvokeAsync<string?>("sessionStorage.getItem", TokenKey);
        return !string.IsNullOrEmpty(_token);
    }

    /// <summary>
    /// Autentica via POST /api/auth/login. Armazena o JWT retornado.
    /// </summary>
    public async Task<bool> LoginAsync(string senha)
    {
        try
        {
            var response = await _http.PostAsJsonAsync("api/auth/login", new { senha });
            if (!response.IsSuccessStatusCode) return false;

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var token = doc.RootElement.GetProperty("token").GetString();
            if (string.IsNullOrEmpty(token)) return false;

            _token = token;
            await _js.InvokeVoidAsync("sessionStorage.setItem", TokenKey, token);
            _authStateProvider.MarkUserAsAuthenticated();
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Encerra a sessão removendo o token.</summary>
    public async Task LogoutAsync()
    {
        _token = null;
        await _js.InvokeVoidAsync("sessionStorage.removeItem", TokenKey);
        _authStateProvider.MarkUserAsLoggedOut();
    }
}
