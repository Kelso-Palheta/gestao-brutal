using System;

namespace BatatasFritas.Shared.DTOs
{
    /// <summary>
    /// DTO usado para exibir pedidos no dashboard.
    /// </summary>
    public class ListaPedidosDto
    {
        public int Id { get; set; }
        public string NomeCliente { get; set; } = string.Empty;
        public string TelefoneCliente { get; set; } = string.Empty;
        public decimal ValorTotal { get; set; }
        public DateTime DataHoraPedido { get; set; }
        public decimal ValorCashbackUsado { get; set; }
    }
}