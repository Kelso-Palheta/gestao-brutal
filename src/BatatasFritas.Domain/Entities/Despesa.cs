namespace BatatasFritas.Domain.Entities;

using System;

public class Despesa : EntityBase
{
    public virtual string Descricao { get; protected set; } = string.Empty;
    public virtual decimal Valor { get; protected set; }
    public virtual DateTime DataRegistro { get; protected set; }
    public virtual string Categoria { get; protected set; } = string.Empty; // "Funcionario", "Imposto", "Energia/Agua", "Outros"

    protected Despesa() { }

    public Despesa(string descricao, decimal valor, DateTime dataRegistro, string categoria)
    {
        Descricao = descricao;
        Valor = valor;
        DataRegistro = dataRegistro;
        Categoria = categoria;
    }

    public virtual void Atualizar(string descricao, decimal valor, DateTime dataRegistro, string categoria)
    {
        Descricao = descricao;
        Valor = valor;
        DataRegistro = dataRegistro;
        Categoria = categoria;
    }
}
