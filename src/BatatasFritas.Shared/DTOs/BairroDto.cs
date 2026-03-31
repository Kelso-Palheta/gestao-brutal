namespace BatatasFritas.Shared.DTOs;

public class BairroDto
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public decimal TaxaEntrega { get; set; }
    public int OrdemExibicao { get; set; } = 0;
}

public record ReordenarBairroItem(int Id, int Ordem);
