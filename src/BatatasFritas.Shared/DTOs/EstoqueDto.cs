namespace BatatasFritas.Shared.DTOs;

public class InsumoDto
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Unidade { get; set; } = "un";
    public decimal EstoqueAtual { get; set; }
    public decimal EstoqueMinimo { get; set; }
    public decimal CustoPorUnidade { get; set; }
    public bool Ativo { get; set; } = true;
    public bool AbaixoDoMinimo { get; set; }
    public bool EstoqueNegativo { get; set; }
}

public class MovimentacaoDto
{
    public int Id { get; set; }
    public int InsumoId { get; set; }
    public string InsumoNome { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;   // "Entrada" | "Saida" | "Ajuste"
    public decimal Quantidade { get; set; }
    public decimal ValorUnitario { get; set; }
    public decimal ValorTotal { get; set; }
    public DateTime DataMovimentacao { get; set; }
    public string Motivo { get; set; } = string.Empty;
    public string Fornecedor { get; set; } = string.Empty;
    public string NumeroNF { get; set; } = string.Empty;
}

public class NovaMovimentacaoRequest
{
    public int InsumoId { get; set; }
    public string Tipo { get; set; } = "Entrada"; // Entrada | Saida | Ajuste
    public decimal Quantidade { get; set; }
    public decimal ValorUnitario { get; set; }
    public string Motivo { get; set; } = string.Empty;
    public string Fornecedor { get; set; } = string.Empty;
    public string NumeroNF { get; set; } = string.Empty;
}

public class ItemReceitaDto
{
    public int Id { get; set; }
    public int ProdutoId { get; set; }
    public string ProdutoNome { get; set; } = string.Empty;
    public int InsumoId { get; set; }
    public string InsumoNome { get; set; } = string.Empty;
    public string InsumoUnidade { get; set; } = string.Empty;
    public decimal QuantidadePorUnidade { get; set; }
}

public class NovoItemReceitaDto
{
    public int InsumoId { get; set; }
    public decimal QuantidadePorUnidade { get; set; }
}

public class EstoqueDashboardDto
{
    public int TotalInsumos { get; set; }
    public int InsumosAbaixoMinimo { get; set; }
    public int InsumosNegativos { get; set; }
    public decimal TotalGastosCompras { get; set; }
    public decimal ValorEstoqueAtual { get; set; }
    public List<InsumoDto> AlertasEstoque { get; set; } = new();
    public List<MovimentacaoDto> UltimasMovimentacoes { get; set; } = new();
}

public class AjusteSaldoRequest
{
    public decimal NovoSaldo { get; set; }
    public string Motivo { get; set; } = string.Empty;
}
