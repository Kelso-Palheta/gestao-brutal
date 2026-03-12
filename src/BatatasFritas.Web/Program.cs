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

        // Configuração para consultar a API através do domínio que o Coolify gerar (usando o caminho atual /api não funcionou no proxy do Nginx no Coolify)
        var apiUrl = builder.HostEnvironment.BaseAddress.Contains("localhost") 
            ? "http://localhost:5062" 
            : builder.HostEnvironment.BaseAddress.Replace("xw4owos4wwckck440w4gg48k", "t00skwc00sg0csgw0ck8g4ks"); // Mapeia o frontend (xw4) pro dominio da API (t00) gerado pelo Coolify
        
        builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(apiUrl) });
        builder.Services.AddSingleton<CarrinhoState>();
        builder.Services.AddScoped<BatatasFritas.Web.Services.KdsAuthService>();

        await builder.Build().RunAsync();
    }
}
