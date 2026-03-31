namespace BatatasFritas.Domain.Entities;

public class Bairro : EntityBase
{
    public virtual string Nome { get; protected set; } = string.Empty;
    public virtual decimal TaxaEntrega { get; protected set; }
    public virtual int OrdemExibicao { get; set; } = 0;

    protected Bairro() { } // Para o NHibernate

    public Bairro(string nome, decimal taxaEntrega)
    {
        Nome = nome;
        TaxaEntrega = taxaEntrega;
    }

    public virtual void AtualizarTaxa(decimal novaTaxa)
    {
        TaxaEntrega = novaTaxa;
    }

    public virtual void Atualizar(string nome, decimal taxaEntrega)
    {
        Nome = nome;
        TaxaEntrega = taxaEntrega;
    }
}

