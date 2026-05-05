using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace BatatasFritas.Web.Services;

/// <summary>
/// Intercepta todas as requisições do HttpClient e adiciona
/// o header "Authorization: Bearer {token}" quando o usuário está autenticado.
/// </summary>
public class AuthDelegatingHandler : DelegatingHandler
{
    private readonly IServiceProvider _sp;

    public AuthDelegatingHandler(IServiceProvider sp) => _sp = sp;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            var js = _sp.GetRequiredService<IJSRuntime>();
            var token = await js.InvokeAsync<string?>("localStorage.getItem", "kds_jwt_token");
            if (!string.IsNullOrEmpty(token))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        catch { /* JS não disponível — prossegue sem token */ }

        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            try
            {
                var js = _sp.GetRequiredService<IJSRuntime>();
                await js.InvokeVoidAsync("localStorage.removeItem", "kds_jwt_token");
            }
            catch { }
        }

        return response;
    }
}
