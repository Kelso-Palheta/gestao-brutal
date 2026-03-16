using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using BatatasFritas.Web.Services;

namespace BatatasFritas.Web;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebAssemblyHostBuilder.CreateDefault(args);
        builder.RootComponents.Add<App>("#app");
        builder.RootComponents.Add<HeadOutlet>("head::after");

        // URL da API: usa variável de ambiente API_BASE_URL injetada pelo Resfrie/Coolify,
        // ou cai para localhost em desenvolvimento.
        var apiUrl = builder.Configuration["ApiSettings:BaseUrl"] 
            ?? Environment.GetEnvironmentVariable("API_BASE_URL")
            ?? (builder.HostEnvironment.IsDevelopment() ? "http://localhost:5062" : builder.HostEnvironment.BaseAddress);
        
        builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(apiUrl) });
        builder.Services.AddSingleton<CarrinhoState>();
        builder.Services.AddScoped<BatatasFritas.Web.Services.KdsAuthService>();

        await builder.Build().RunAsync();
    }
}
