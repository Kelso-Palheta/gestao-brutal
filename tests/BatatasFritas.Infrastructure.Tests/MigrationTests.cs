using System;
using System.Collections.Generic;
using FluentAssertions;
using FluentMigrator.Runner;
using BatatasFritas.Infrastructure.Migrations;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BatatasFritas.Infrastructure.Tests;

public class MigrationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly IServiceProvider _serviceProvider;

    public MigrationTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:;Mode=Memory;Cache=Shared");
        _connection.Open();

        var services = new ServiceCollection();
        services.AddFluentMigratorCore()
            .ConfigureRunner(rb => rb
                .AddSQLite()
                .WithGlobalConnectionString(_connection.ConnectionString)
                .ScanIn(typeof(V001__CreateBairros).Assembly).For.Migrations())
            .AddLogging(lb => lb.AddFluentMigratorConsole());

        _serviceProvider = services.BuildServiceProvider(false);
    }

    [Fact]
    public void MigrateUp_DeveCriarTodasAs12Tabelas()
    {
        using var scope = _serviceProvider.CreateScope();
        var runner = scope.ServiceProvider.GetRequiredService<IMigrationRunner>();

        runner.MigrateUp();

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table';";
        using var reader = cmd.ExecuteReader();

        var tables = new List<string>();
        while (reader.Read()) tables.Add(reader.GetString(0));

        tables.Should().Contain("bairros");
        tables.Should().Contain("configuracoes");
        tables.Should().Contain("complementos");
        tables.Should().Contain("insumos");
        tables.Should().Contain("carteiras_cashback");
        tables.Should().Contain("produtos");
        tables.Should().Contain("despesas");
        tables.Should().Contain("pedidos");
        tables.Should().Contain("itens_pedido");
        tables.Should().Contain("itens_receita");
        tables.Should().Contain("movimentacoes_estoque");
        tables.Should().Contain("transacoes_cashback");
        tables.Should().Contain("VersionInfo");
    }

    [Fact]
    public void MigrateUp_Idempotente_NaoFalhaAoRodarDuasVezes()
    {
        using var scope1 = _serviceProvider.CreateScope();
        scope1.ServiceProvider.GetRequiredService<IMigrationRunner>().MigrateUp();

        using var scope2 = _serviceProvider.CreateScope();
        var act = () => scope2.ServiceProvider.GetRequiredService<IMigrationRunner>().MigrateUp();

        act.Should().NotThrow();
    }

    public void Dispose()
    {
        _connection.Close();
        _connection.Dispose();
    }
}
