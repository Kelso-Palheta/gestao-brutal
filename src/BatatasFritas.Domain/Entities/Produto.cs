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
    /// <summary>Lista de IDs de complementos delimitados por vírgula. Se vazio, aceita comportamento padrão por categoria.</summary>
    public virtual string ComplementosPermitidos { get; protected set; } = string.Empty;
    public virtual int EstoqueAtual { get; set; } = 0;
    public virtual int EstoqueMinimo { get; set; } = 0;

    protected Produto() { } // NHibernate

    public Produto(string nome, string descricao, CategoriaEnum categoria, decimal precoBase, string imagemUrl = "", string complementosPermitidos = "", int estoqueAtual = 0, int estoqueMinimo = 0)
    {
        Nome = nome;
        Descricao = descricao;
        CategoriaId = categoria;
        PrecoBase = precoBase;
        ImagemUrl = imagemUrl;
        ComplementosPermitidos = complementosPermitidos;
        Ativo = true;
        EstoqueAtual = estoqueAtual;
        EstoqueMinimo = estoqueMinimo;
    }

    public virtual void Atualizar(string nome, string descricao, CategoriaEnum categoria, decimal precoBase, string imagemUrl, string complementosPermitidos = "")
    {
        Nome = nome;
        Descricao = descricao;
        CategoriaId = categoria;
        PrecoBase = precoBase;
        ImagemUrl = imagemUrl;
        ComplementosPermitidos = complementosPermitidos;
    }

    public virtual void Ativar() => Ativo = true;
    public virtual void Desativar() => Ativo = false;
    public virtual void AjustarEstoque(int quantidade) => EstoqueAtual += quantidade;
}

