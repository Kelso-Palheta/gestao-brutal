using Microsoft.JSInterop;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace BatatasFritas.Web.Services;

/// <summary>
/// Serviço de autenticação do KDS com JWT.
/// Obtém token em POST /api/auth/login e o armazena em localStorage.
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

    /// <summary>Expõe o token atual. Se não estiver em memória, tenta recuperar assincronamente da sessão.</summary>
    public async Task<string?> GetTokenAsync()
    {
        if (string.IsNullOrEmpty(_token))
        {
            try
            {
                _token = await _js.InvokeAsync<string?>("localStorage.getItem", TokenKey);
            }
            catch { /* Ignora se o JS não estiver disponível no contexto de pré-renderização */ }
        }
        return _token;
    }

    /// <summary>
    /// Tenta restaurar o token da localStorage (chamado no startup do app).
    /// </summary>
    public async Task RestaurarSessaoAsync()
    {
        _token = await _js.InvokeAsync<string?>("localStorage.getItem", TokenKey);
        if (!string.IsNullOrEmpty(_token))
            _authStateProvider.MarkUserAsAuthenticated();
    }

    /// <summary>Verifica se há um token válido (não expirado) em memória.</summary>
    public async Task<bool> EstaAutenticadoAsync()
    {
        if (string.IsNullOrEmpty(_token))
            _token = await _js.InvokeAsync<string?>("localStorage.getItem", TokenKey);

        if (string.IsNullOrEmpty(_token)) return false;

        // Verifica expiração do JWT sem biblioteca externa
        try
        {
            var parts = _token.Split('.');
            if (parts.Length != 3) { await LogoutAsync(); return false; }

            var payload = parts[1];
            // Padding base64url
            var padded = payload.Replace('-', '+').Replace('_', '/');
            padded = padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '=');
            var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(padded));
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("exp", out var expEl))
            {
                var exp = expEl.GetInt64();
                var expDate = DateTimeOffset.FromUnixTimeSeconds(exp);
                if (expDate < DateTimeOffset.UtcNow)
                {
                    await LogoutAsync();
                    return false;
                }
            }
        }
        catch { /* token malformado — trata como inválido */ await LogoutAsync(); return false; }

        return true;
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
            await _js.InvokeVoidAsync("localStorage.setItem", TokenKey, token);
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
        await _js.InvokeVoidAsync("localStorage.removeItem", TokenKey);
        _authStateProvider.MarkUserAsLoggedOut();
    }
}
