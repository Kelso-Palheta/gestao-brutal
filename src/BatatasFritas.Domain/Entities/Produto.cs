using BatatasFritas.Shared.Enums;

namespace BatatasFritas.Domain.Entities;

public class Produto : EntityBase
{
    public virtual string Nome { get; protected set; } = string.Empty;
    public virtual string Descricao { get; protected set; } = string.Empty;
    public virtual CategoriaEnum CategoriaId { get; protected set; }
    public virtual decimal PrecoBase { get; protected set; }
    public virtual string ImagemUrl { get; protected set; } = string.Empty;
    public virtual bool Ativo { get; protected set; } = true;
    /// <summary>Posição no cardápio. Menor = aparece primeiro.</summary>
    public virtual int Ordem { get; set; } = 0;

    protected Produto() { } // NHibernate

    public Produto(string nome, string descricao, CategoriaEnum categoria, decimal precoBase, string imagemUrl = "")
    {
        Nome = nome;
        Descricao = descricao;
        CategoriaId = categoria;
        PrecoBase = precoBase;
        ImagemUrl = imagemUrl;
        Ativo = true;
    }

    public virtual void Atualizar(string nome, string descricao, CategoriaEnum categoria, decimal precoBase, string imagemUrl)
    {
        Nome = nome;
        Descricao = descricao;
        CategoriaId = categoria;
        PrecoBase = precoBase;
        ImagemUrl = imagemUrl;
    }

    public virtual void Ativar() => Ativo = true;
    public virtual void Desativar() => Ativo = false;
}

