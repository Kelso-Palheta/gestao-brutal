using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace BatatasFritas.Web.Services;

public class AuthStateProvider : AuthenticationStateProvider
{
    private readonly IJSRuntime _js;
    private readonly ClaimsPrincipal _anonymous = new(new ClaimsIdentity());

    public AuthStateProvider(IJSRuntime js)
    {
        _js = js;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            var token = await _js.InvokeAsync<string?>("localStorage.getItem", "kds_jwt_token");
            if (string.IsNullOrEmpty(token))
                return new AuthenticationState(_anonymous);

            var claims = new List<Claim>
            {
                new(ClaimTypes.Name, "admin"),
                new(ClaimTypes.Role, "admin")
            };

            var identity = new ClaimsIdentity(claims, "jwt");
            var principal = new ClaimsPrincipal(identity);
            return new AuthenticationState(principal);
        }
        catch
        {
            return new AuthenticationState(_anonymous);
        }
    }

    public void MarkUserAsAuthenticated()
    {
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    public void MarkUserAsLoggedOut()
    {
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }
}