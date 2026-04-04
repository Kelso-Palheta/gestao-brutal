using BatatasFritas.Web.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.AspNetCore.Components.Authorization;

namespace BatatasFritas.Web;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebAssemblyHostBuilder.CreateDefault(args);
        
        // Configurar autenticação
        builder.Services.AddAuthorizationCore();
        builder.Services.AddCascadingAuthenticationState();
        builder.Services.AddSingleton<AuthStateProvider>();
        builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<AuthStateProvider>());
        
        builder.RootComponents.Add<App>("#app");
        builder.RootComponents.Add<HeadOutlet>("head::after");

        var apiUrlConfig = builder.Configuration["ApiSettings:BaseUrl"];
        var apiUrl = !string.IsNullOrWhiteSpace(apiUrlConfig)
            ? apiUrlConfig
            : (builder.HostEnvironment.IsDevelopment() ? "http://localhost:5062" : builder.HostEnvironment.BaseAddress);

        builder.Services.AddScoped<KdsAuthService>();
        builder.Services.AddScoped<AuthDelegatingHandler>();

        builder.Services.AddScoped(sp =>
        {
            var handler = sp.GetRequiredService<AuthDelegatingHandler>();
            handler.InnerHandler = new HttpClientHandler();
            return new HttpClient(handler) { BaseAddress = new Uri(apiUrl) };
        });

        builder.Services.AddSingleton<CarrinhoState>();

        var host = builder.Build();
        var kdsAuth = host.Services.GetRequiredService<KdsAuthService>();
        await kdsAuth.RestaurarSessaoAsync();

        await host.RunAsync();
    }
}
