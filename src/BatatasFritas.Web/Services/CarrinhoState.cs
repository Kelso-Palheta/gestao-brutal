using BatatasFritas.Shared.DTOs;
using System.Collections.Generic;
using System.Linq;

namespace BatatasFritas.Web.Services;

public class CarrinhoState
{
    private List<NovoItemPedidoDto> _itens = new();
    
    public IReadOnlyList<NovoItemPedidoDto> Itens => _itens.AsReadOnly();
    
    // ======================================
    // Checkout Form Preservation Draft
    // ======================================
    public string TempNome { get; set; } = "";
    public string TempTelefone { get; set; } = "";
    public string TempRuaAvenida { get; set; } = "";
    public string TempNumeroLocal { get; set; } = "";
    public string TempComplementoLocal { get; set; } = "";
    public string TempBairro { get; set; } = "";
    public string TempPagamento { get; set; } = "";
    public string TempTroco { get; set; } = "";
    // ======================================

    public event Action? OnChange;

    public void AdicionarItem(ProdutoDto produto, int quantidade = 1)
    {
        var itemExistente = _itens.FirstOrDefault(i => i.ProdutoId == produto.Id && i.Observacao == "");
        if (itemExistente != null)
        {
            itemExistente.Quantidade += quantidade;
        }
        else
        {
            _itens.Add(new NovoItemPedidoDto
            {
                ProdutoId = produto.Id,
                NomeProduto = produto.Nome,
                CategoriaId = produto.CategoriaId,
                Quantidade = quantidade,
                PrecoUnitario = produto.PrecoBase,
                Observacao = ""
            });
        }
        NotifyStateChanged();
    }

    public void AdicionarItemComOpcoes(ProdutoDto produto, int quantidade, decimal precoUnitario, string observacao)
    {
        var itemExistente = _itens.FirstOrDefault(i => i.ProdutoId == produto.Id && i.Observacao == observacao && i.PrecoUnitario == precoUnitario);
        if (itemExistente != null)
        {
            itemExistente.Quantidade += quantidade;
        }
        else
        {
            _itens.Add(new NovoItemPedidoDto
            {
                ProdutoId = produto.Id,
                NomeProduto = produto.Nome,
                CategoriaId = produto.CategoriaId,
                Quantidade = quantidade,
                PrecoUnitario = precoUnitario,
                Observacao = observacao
            });
        }
        NotifyStateChanged();
    }

    public void RemoverItem(int produtoId)
    {
        var item = _itens.FirstOrDefault(i => i.ProdutoId == produtoId);
        if (item != null)
        {
            _itens.Remove(item);
            NotifyStateChanged();
        }
    }

    public void LimparCarrinho()
    {
        _itens.Clear();
        
        // Clean Form State too
        TempNome = TempTelefone = TempRuaAvenida = TempNumeroLocal = TempComplementoLocal = "";
        TempBairro = TempPagamento = TempTroco = "";

        NotifyStateChanged();
    }

    public decimal Subtotal => _itens.Sum(i => i.PrecoUnitario * i.Quantidade);

    private void NotifyStateChanged() => OnChange?.Invoke();
}
