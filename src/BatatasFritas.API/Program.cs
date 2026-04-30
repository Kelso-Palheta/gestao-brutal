using BatatasFritas.API.Hubs;
using FluentMigrator.Runner;
using BatatasFritas.Infrastructure;
using BatatasFritas.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System;
using System.Text;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// ── Controllers ──────────────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// ── Swagger com suporte a JWT ────────────────────────────────────────────────
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "BatatasFritas API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name         = "Authorization",
        Type         = SecuritySchemeType.Http,
        Scheme       = "bearer",
        BearerFormat = "JWT",
        In           = ParameterLocation.Header,
        Description  = "Informe o token JWT: Bearer {token}"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
            System.Array.Empty<string>()
        }
    });
});

// ── JWT Authentication ───────────────────────────────────────────────────────
const string JWT_LITERAL_REJEITADO = "batatas-fritas-palheta-brutal-secret-key-2024-min-32-characters-v2";
var jwtKey = builder.Configuration["Jwt:SecretKey"];
if (string.IsNullOrWhiteSpace(jwtKey))
    throw new InvalidOperationException(
        "Jwt:SecretKey ausente. Defina via variável de ambiente Jwt__SecretKey (mín. 32 chars). Ver .env.example.");
if (jwtKey == JWT_LITERAL_REJEITADO)
    throw new InvalidOperationException(
        "Jwt:SecretKey ainda usa o literal antigo (vazado em git). Rotacione AGORA. Gere com: openssl rand -base64 48");
if (jwtKey.Length < 32)
    throw new InvalidOperationException("Jwt:SecretKey deve ter no mínimo 32 caracteres.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = builder.Configuration["Jwt:Issuer"],
            ValidAudience            = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };

        // Suporte para SignalR: o token pode vir via query string "access_token"
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var path  = ctx.HttpContext.Request.Path;
                var token = ctx.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(token) && path.StartsWithSegments("/hubs"))
                    ctx.Token = token;
                return System.Threading.Tasks.Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// ── Rate Limiting (proteção brute-force no login) ────────────────────────────
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.AddFixedWindowLimiter("login", opt =>
    {
        opt.PermitLimit = 5;
        opt.Window      = TimeSpan.FromMinutes(15);
        opt.QueueLimit  = 0;
    });
});

// Em produção, WebhookSecret é obrigatório — sem ele, qualquer um pode marcar pedido pago
if (builder.Environment.IsProduction())
{
    var webhookSecret = builder.Configuration["MercadoPago:WebhookSecret"];
    if (string.IsNullOrWhiteSpace(webhookSecret))
        throw new InvalidOperationException(
            "MercadoPago:WebhookSecret é obrigatório em Production. Defina via env MercadoPago__WebhookSecret.");
}

// ── SignalR ──────────────────────────────────────────────────────────────────
builder.Services.AddSignalR();

// ── Banco de dados ───────────────────────────────────────────────────────────
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new System.Exception("Connection string 'DefaultConnection' não encontrada.");

var databaseProvider = builder.Configuration["DatabaseProvider"] ?? "sqlite";
builder.Services.AddInfrastructure(connectionString, databaseProvider, builder.Configuration);

// ── Health Checks ────────────────────────────────────────────────────────────
builder.Services.AddHealthChecks()
    .AddCheck<BatatasFritas.API.HealthChecks.NHibernateHealthCheck>("db");

// ── CORS ─────────────────────────────────────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBlazorClient", policy =>
    {
        if (builder.Environment.IsDevelopment())
        {
            // Em desenvolvimento: aceita qualquer origem para facilitar testes
            policy.SetIsOriginAllowed(origin => true)
                  .AllowCredentials()
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        }
        else
        {
            // Em produção: restringe a origens configuradas
            var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                ?? new[] { "https://batatapalhetabrutal.com.br", "https://www.batatapalhetabrutal.com.br" };
            
            policy.WithOrigins(allowedOrigins)
                  .AllowCredentials()
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        }
    });
});

var app = builder.Build();

// ── Pipeline ─────────────────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();
app.UseCors("AllowBlazorClient");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<PedidosHub>("/hubs/pedidos");

app.MapGet("/api/health", () => "OK");
app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");

// ── FluentMigrator: executa migrations versionadas ───────────────────────────
using (var migrationScope = app.Services.CreateScope())
{
    var runner = migrationScope.ServiceProvider.GetRequiredService<IMigrationRunner>();
    runner.MigrateUp();
    Console.WriteLine("[MIGRACAO] FluentMigrator: migrations aplicadas com sucesso.");
}

using (var scope = app.Services.CreateScope())
{
    var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
    var session = scope.ServiceProvider.GetRequiredService<NHibernate.ISession>();
    var produtoRepo = scope.ServiceProvider.GetRequiredService<IRepository<BatatasFritas.Domain.Entities.Produto>>();

    // ── Aviso de estoque zerado (compatível com SQLite e Postgres) ────────
    try
    {
        var produtosSemEstoque = await session.CreateSQLQuery("SELECT COUNT(*) FROM produtos WHERE estoque_atual <= 0").UniqueResultAsync();
        var count = Convert.ToInt32(produtosSemEstoque);
        if (count > 0)
        {
            Console.WriteLine($"[AVISO] {count} produto(s) com estoque zerado ou negativo detectado(s). Acesse o Admin para repor o estoque manualmente.");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[MIGRACAO] Aviso ao verificar estoque: {ex.Message}");
    }
    
    var bairros = await scope.ServiceProvider.GetRequiredService<IRepository<BatatasFritas.Domain.Entities.Bairro>>().GetAllAsync();
    
    var produtos = await produtoRepo.GetAllAsync();
    if (!produtos.Any())
    {
        uow.BeginTransaction();
        await produtoRepo.AddAsync(new BatatasFritas.Domain.Entities.Produto("Batata Suprema Média", "Batata rústica com cheddar, bacon e cebolinha.", BatatasFritas.Shared.Enums.CategoriaEnum.Batatas, 35.90m));
        await produtoRepo.AddAsync(new BatatasFritas.Domain.Entities.Produto("Batata Suprema Gigante", "Batata rústica com o dobro de cheddar, calabresa ralada e bacon.", BatatasFritas.Shared.Enums.CategoriaEnum.Batatas, 55.90m));
        await produtoRepo.AddAsync(new BatatasFritas.Domain.Entities.Produto("Coca-Cola 1L", "Refrigerante gelado.", BatatasFritas.Shared.Enums.CategoriaEnum.Bebidas, 12.00m));
        await uow.CommitAsync();
    }
    
    // Seed default Bairro independent of Produtcs
    if (!bairros.Any())
    {
        var bairroRepo = scope.ServiceProvider.GetRequiredService<IRepository<BatatasFritas.Domain.Entities.Bairro>>();
        uow.BeginTransaction();
        await bairroRepo.AddAsync(new BatatasFritas.Domain.Entities.Bairro("Centro", 5.0m));
        await bairroRepo.AddAsync(new BatatasFritas.Domain.Entities.Bairro("Sul Nascente", 5.0m));
        await bairroRepo.AddAsync(new BatatasFritas.Domain.Entities.Bairro("Arapiranga", 5.0m));
        await bairroRepo.AddAsync(new BatatasFritas.Domain.Entities.Bairro("Vila Nova", 5.0m));
        await bairroRepo.AddAsync(new BatatasFritas.Domain.Entities.Bairro("Outro Bairro (Exemplo)", 10.0m));
        await uow.CommitAsync();
    }

    var complementoRepo = scope.ServiceProvider.GetRequiredService<IRepository<BatatasFritas.Domain.Entities.Complemento>>();
    var complementos = await complementoRepo.GetAllAsync();
    if (!complementos.Any())
    {
        uow.BeginTransaction();
        await complementoRepo.AddAsync(new BatatasFritas.Domain.Entities.Complemento("Molho Billy Jack", 0, "Batatas", "MolhoGratuito"));
        await complementoRepo.AddAsync(new BatatasFritas.Domain.Entities.Complemento("Molho Verde", 0, "Batatas", "MolhoGratuito"));
        await complementoRepo.AddAsync(new BatatasFritas.Domain.Entities.Complemento("Maionese da Casa", 0, "Batatas", "MolhoGratuito"));
        await complementoRepo.AddAsync(new BatatasFritas.Domain.Entities.Complemento("Queijo Extra", 4.50m, "Batatas", "AdicionalPago"));
        await complementoRepo.AddAsync(new BatatasFritas.Domain.Entities.Complemento("Bacon Extra", 5.00m, "Batatas", "AdicionalPago"));
        await complementoRepo.AddAsync(new BatatasFritas.Domain.Entities.Complemento("Sem Cebolinha", 0, "Todas", "Remocao"));
        await uow.CommitAsync();
    }
}

app.Run();
