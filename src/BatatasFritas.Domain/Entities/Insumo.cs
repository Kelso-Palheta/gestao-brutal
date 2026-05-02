namespace BatatasFritas.Domain.Entities;

/// <summary>Insumo/ingrediente controlado no estoque.</summary>
public class Insumo : EntityBase
{
    public virtual string Nome { get; protected set; } = string.Empty;
    public virtual string Unidade { get; protected set; } = "un"; // kg, L, un, g
    public virtual decimal EstoqueAtual { get; set; } = 0;
    public virtual decimal EstoqueMinimo { get; protected set; } = 0;
    public virtual decimal CustoPorUnidade { get; protected set; } = 0;
    public virtual bool Ativo { get; set; } = true;

    protected Insumo() { } // NHibernate

    public Insumo(string nome, string unidade, decimal estoqueMinimo, decimal custoPorUnidade)
    {
        Nome = nome;
        Unidade = unidade;
        EstoqueMinimo = estoqueMinimo;
        CustoPorUnidade = custoPorUnidade;
    }

    public virtual void Atualizar(string nome, string unidade, decimal estoqueMinimo, decimal custoPorUnidade)
    {
        Nome = nome;
        Unidade = unidade;
        EstoqueMinimo = estoqueMinimo;
        CustoPorUnidade = custoPorUnidade;
    }

    public virtual bool AbaixoDoMinimo => EstoqueAtual <= EstoqueMinimo;
    public virtual bool EstoqueNegativo => EstoqueAtual < 0;

    public virtual void AjustarEstoque(decimal quantidade) => EstoqueAtual += quantidade;
}
