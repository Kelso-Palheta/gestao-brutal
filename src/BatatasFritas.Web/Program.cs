using BatatasFritas.Web.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

namespace BatatasFritas.Web;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebAssemblyHostBuilder.CreateDefault(args);
        builder.RootComponents.Add<App>("#app");
        builder.RootComponents.Add<HeadOutlet>("head::after");

        // URL da API: usa configuração do appsettings (dev) ou herda a origem do app (prod).
        // Em produção, o nginx faz proxy de /api/ e /hubs/ internamente para o container da API,
        // portanto basta usar a BaseAddress do host (mesma origem que a página web).
        var apiUrlConfig = builder.Configuration["ApiSettings:BaseUrl"];
        var apiUrl = !string.IsNullOrWhiteSpace(apiUrlConfig)
            ? apiUrlConfig
            : (builder.HostEnvironment.IsDevelopment() ? "http://localhost:5062" : builder.HostEnvironment.BaseAddress);

        // Registra KdsAuthService primeiro (o handler depende dele)
        builder.Services.AddScoped<KdsAuthService>();

        // DelegatingHandler que injeta o JWT em cada request
        builder.Services.AddScoped<AuthDelegatingHandler>();

        // HttpClient principal com o handler de autenticação
        builder.Services.AddScoped(sp =>
        {
            var handler = sp.GetRequiredService<AuthDelegatingHandler>();
            handler.InnerHandler = new HttpClientHandler();
            return new HttpClient(handler) { BaseAddress = new Uri(apiUrl) };
        });

        builder.Services.AddSingleton<CarrinhoState>();

        var host = builder.Build();

        // Restaura o token JWT da sessionStorage antes de renderizar qualquer página
        var kdsAuth = host.Services.GetRequiredService<KdsAuthService>();
        await kdsAuth.RestaurarSessaoAsync();

        await host.RunAsync();
    }
}
