using System;

namespace BatatasFritas.Domain.Entities;

public enum TipoTransacaoCashback
{
    Entrada = 1, // Ganhou cashback de uma compra
    Saida = 2    // Usou cashback para descontar valor de uma nova compra
}

/// <summary>
/// Histórico de entradas e saídas de saldo na carteira de Cashback.
/// </summary>
public class TransacaoCashback : EntityBase
{
    public virtual CarteiraCashback Carteira { get; set; }
    public virtual decimal Valor { get; set; }
    public virtual TipoTransacaoCashback Tipo { get; set; }
    public virtual string Motivo { get; set; } = string.Empty;
    
    // Se a transação estiver atrelada a um pedido específico do sistema
    public virtual int? PedidoReferenciaId { get; set; }
    
    public virtual DateTime DataHora { get; set; } = DateTime.UtcNow;

    protected TransacaoCashback() { } // NHibernate

    public TransacaoCashback(CarteiraCashback carteira, decimal valor, TipoTransacaoCashback tipo, string motivo, int? pedidoId = null)
    {
        Carteira = carteira;
        Valor = valor;
        Tipo = tipo;
        Motivo = motivo;
        PedidoReferenciaId = pedidoId;
    }
}
