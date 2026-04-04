using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace BatatasFritas.Web.Services;

public class AuthStateProvider : AuthenticationStateProvider
{
    private bool _isAuthenticated = false;
    private readonly ClaimsPrincipal _anonymous = new(new ClaimsIdentity());

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        if (!_isAuthenticated)
            return Task.FromResult(new AuthenticationState(_anonymous));

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, "admin"),
            new(ClaimTypes.Role, "admin")
        };

        var identity = new ClaimsIdentity(claims, "jwt");
        var principal = new ClaimsPrincipal(identity);
        return Task.FromResult(new AuthenticationState(principal));
    }

    public void MarkUserAsAuthenticated()
    {
        _isAuthenticated = true;
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    public void MarkUserAsLoggedOut()
    {
        _isAuthenticated = false;
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }
}