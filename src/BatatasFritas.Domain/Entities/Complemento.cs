namespace BatatasFritas.Domain.Entities;

public class Complemento : EntityBase
{
    public virtual string Nome { get; protected set; } = string.Empty;
    public virtual decimal Preco { get; protected set; }
    public virtual string CategoriaAlvo { get; protected set; } = "Todas";
    public virtual string TipoAcao { get; protected set; } = "AdicionalPago";
    public virtual bool Ativo { get; set; } = true;

    protected Complemento() { }

    public Complemento(string nome, decimal preco, string categoriaAlvo, string tipoAcao)
    {
        Nome = nome;
        Preco = preco;
        CategoriaAlvo = categoriaAlvo;
        TipoAcao = tipoAcao;
    }

    public virtual void Atualizar(string nome, decimal preco, string categoriaAlvo, string tipoAcao)
    {
        Nome = nome;
        Preco = preco;
        CategoriaAlvo = categoriaAlvo;
        TipoAcao = tipoAcao;
    }

    public virtual void Ativar() => Ativo = true;
    public virtual void Desativar() => Ativo = false;
}
