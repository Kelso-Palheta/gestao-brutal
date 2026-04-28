using BatatasFritas.PrintAgent;
using System.Text;

// Registra provider de code pages (necessário para CP850 — ESC/POS Bematech)
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<ApiOptions>(builder.Configuration.GetSection("Api"));
builder.Services.Configure<ImpressoraOptions>(builder.Configuration.GetSection("Impressora"));
builder.Services.AddSingleton<PrinterService>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
