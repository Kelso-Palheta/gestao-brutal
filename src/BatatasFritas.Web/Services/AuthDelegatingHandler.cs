using System.Net.Http.Headers;

namespace BatatasFritas.Web.Services;

/// <summary>
/// Intercepta todas as requisições do HttpClient e adiciona
/// o header "Authorization: Bearer {token}" quando o usuário está autenticado.
/// </summary>
public class AuthDelegatingHandler : DelegatingHandler
{
    private readonly KdsAuthService _auth;

    public AuthDelegatingHandler(KdsAuthService auth) => _auth = auth;

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = _auth.GetToken();
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return base.SendAsync(request, cancellationToken);
    }
}
