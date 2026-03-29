using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;

namespace BatatasFritas.Web.Services;

/// <summary>
/// Intercepta todas as requisições do HttpClient e adiciona
/// o header "Authorization: Bearer {token}" quando o usuário está autenticado.
/// </summary>
public class AuthDelegatingHandler : DelegatingHandler
{
    private readonly IServiceProvider _sp;

    public AuthDelegatingHandler(IServiceProvider sp) => _sp = sp;

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var auth = _sp.GetRequiredService<KdsAuthService>();
        var token = auth.GetToken();
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        return base.SendAsync(request, cancellationToken);
    }
}
