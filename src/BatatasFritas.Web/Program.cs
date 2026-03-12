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

        // Configuração atualizada para usar localhost pra evitar problemas de IP trocado
        builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri("http://localhost:5062") });
        builder.Services.AddSingleton<CarrinhoState>();
        builder.Services.AddScoped<BatatasFritas.Web.Services.KdsAuthService>();

        await builder.Build().RunAsync();
    }
}
