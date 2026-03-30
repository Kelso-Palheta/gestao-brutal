using System;
using System.Collections.Generic;

namespace BatatasFritas.Domain.Entities;

/// <summary>
/// Representa a carteira virtual de Cashback de um cliente.
/// A chave primária natural para identificar o cliente será o número de telefone (WhatsApp).
/// </summary>
public class CarteiraCashback : EntityBase
{
    public virtual string Telefone { get; set; } = string.Empty;
    public virtual string NomeCliente { get; set; } = string.Empty;
    public virtual decimal SaldoAtual { get; protected set; } = 0m;
    public virtual DateTime CriadoEm { get; protected set; } = DateTime.UtcNow;

    // Relacionamento com o histórico de transações
    public virtual IList<TransacaoCashback> Transacoes { get; protected set; } = new List<TransacaoCashback>();

    protected CarteiraCashback() { } // NHibernate

    public CarteiraCashback(string telefone, string nomeCliente)
    {
        Telefone = telefone;
        NomeCliente = nomeCliente;
    }

    /// <summary>
    /// Adiciona fundos à carteira e registra a transação.
    /// </summary>
    public virtual void AdicionarSaldo(decimal valor, string motivo, int? pedidoId = null)
    {
        if (valor <= 0) throw new ArgumentException("Valor para adicionar deve ser maior que zero.");

        SaldoAtual += valor;
        Transacoes.Add(new TransacaoCashback(this, valor, TipoTransacaoCashback.Entrada, motivo, pedidoId));
    }

    /// <summary>
    /// Deduz fundos da carteira e registra a transação.
    /// </summary>
    public virtual void UsarSaldo(decimal valor, string motivo, int? pedidoId = null)
    {
        if (valor <= 0) throw new ArgumentException("Valor para usar deve ser maior que zero.");
        if (SaldoAtual < valor) throw new InvalidOperationException("Saldo insuficiente na carteira de cashback.");

        SaldoAtual -= valor;
        Transacoes.Add(new TransacaoCashback(this, valor, TipoTransacaoCashback.Saida, motivo, pedidoId));
    }

    /// <summary>
    /// Força um novo valor de saldo (ajuste manual do admin).
    /// </summary>
    public virtual void SetSaldoManual(decimal novoValor, string motivo)
    {
        var diferenca = novoValor - SaldoAtual;
        if (diferenca == 0) return;

        var tipo = diferenca > 0 ? TipoTransacaoCashback.Entrada : TipoTransacaoCashback.Saida;
        SaldoAtual = novoValor;
        Transacoes.Add(new TransacaoCashback(this, Math.Abs(diferenca), tipo, motivo));
    }
}
