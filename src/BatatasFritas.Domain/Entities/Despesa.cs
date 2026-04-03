namespace BatatasFritas.Domain.Entities;

using System;

public class Despesa : EntityBase
{
    public virtual string Descricao { get; protected set; } = string.Empty;
    public virtual decimal Valor { get; protected set; }
    public virtual DateTime DataRegistro { get; protected set; }
    public virtual string Categoria { get; protected set; } = string.Empty; // "Funcionario", "Imposto", "Energia/Agua", "Outros"
    public virtual string? Observacao { get; protected set; }

    protected Despesa() { }

    public Despesa(string descricao, decimal valor, DateTime dataRegistro, string categoria, string? observacao = null)
    {
        Descricao = descricao;
        Valor = valor;
        DataRegistro = dataRegistro;
        Categoria = categoria;
        Observacao = observacao;
    }

    public virtual void Atualizar(string descricao, decimal valor, DateTime dataRegistro, string categoria, string? observacao = null)
    {
        Descricao = descricao;
        Valor = valor;
        DataRegistro = dataRegistro;
        Categoria = categoria;
        Observacao = observacao;
    }
}
