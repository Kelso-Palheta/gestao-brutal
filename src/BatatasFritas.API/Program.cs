using BatatasFritas.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using BatatasFritas.Infrastructure.Repositories;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrEmpty(connectionString))
{
    throw new System.Exception("Connection string 'DefaultConnection' not found.");
}

builder.Services.AddInfrastructure(connectionString);

// Registro de serviços específicos
builder.Services.AddHttpClient<BatatasFritas.API.Services.IInfinitePayService, BatatasFritas.API.Services.InfinitePayService>(client =>
{
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBlazorClient",
        policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();
app.UseCors("AllowBlazorClient");
app.UseAuthorization();
app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
    var produtoRepo = scope.ServiceProvider.GetRequiredService<IRepository<BatatasFritas.Domain.Entities.Produto>>();
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
}

app.Run();
