using BatatasFritas.API.Hubs;
using BatatasFritas.Infrastructure;
using BatatasFritas.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;

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
var jwtKey = builder.Configuration["Jwt:SecretKey"]
    ?? throw new InvalidOperationException("Jwt:SecretKey não está configurado no appsettings.json.");

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

// ── SignalR ──────────────────────────────────────────────────────────────────
builder.Services.AddSignalR();

// ── Banco de dados ───────────────────────────────────────────────────────────
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new System.Exception("Connection string 'DefaultConnection' não encontrada.");

var databaseProvider = builder.Configuration["DatabaseProvider"] ?? "sqlite";
builder.Services.AddInfrastructure(connectionString, databaseProvider);

// ── Serviços de domínio ──────────────────────────────────────────────────────
builder.Services.AddHttpClient<BatatasFritas.API.Services.IInfinitePayService, BatatasFritas.API.Services.InfinitePayService>(client =>
{
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

// ── CORS ─────────────────────────────────────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBlazorClient", policy =>
        policy.SetIsOriginAllowed(origin => true)
              .AllowCredentials()
              .AllowAnyHeader()
              .AllowAnyMethod());
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
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<PedidosHub>("/hubs/pedidos");

app.MapGet("/api/health", () => "OK");

using (var scope = app.Services.CreateScope())
{
    var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
    var session = scope.ServiceProvider.GetRequiredService<NHibernate.ISession>();
    var produtoRepo = scope.ServiceProvider.GetRequiredService<IRepository<BatatasFritas.Domain.Entities.Produto>>();
    
    // ── Migração: Adicionar colunas de estoque se não existirem ──────────────
    try
    {
        var colunas = await session.CreateSQLQuery("PRAGMA table_info(produtos)").ListAsync();
        bool temEstoqueAtual = false;
        bool temEstoqueMinimo = false;
        
        foreach (System.Collections.IList col in colunas)
        {
            if (col[1]?.ToString() == "estoque_atual") temEstoqueAtual = true;
            if (col[1]?.ToString() == "estoque_minimo") temEstoqueMinimo = true;
        }
        
        if (!temEstoqueAtual)
        {
            await session.CreateSQLQuery("ALTER TABLE produtos ADD COLUMN estoque_atual INTEGER DEFAULT 0 NOT NULL").ExecuteUpdateAsync();
            Console.WriteLine("[MIGRACAO] Coluna estoque_atual adicionada.");
        }
        
        if (!temEstoqueMinimo)
        {
            await session.CreateSQLQuery("ALTER TABLE produtos ADD COLUMN estoque_minimo INTEGER DEFAULT 0 NOT NULL").ExecuteUpdateAsync();
            Console.WriteLine("[MIGRACAO] Coluna estoque_minimo adicionada.");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[MIGRACAO] Aviso: {ex.Message}");
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
