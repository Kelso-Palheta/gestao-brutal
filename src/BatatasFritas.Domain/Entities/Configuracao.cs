namespace BatatasFritas.Domain.Entities;

/// <summary>
/// Tabela chave-valor de configurações do sistema.
/// Exemplo: Chave = "senha_kds", Valor = hash bcrypt da senha.
/// </summary>
public class Configuracao : EntityBase
{
    public virtual string Chave { get; protected set; } = string.Empty;
    public virtual string Valor { get; set; } = string.Empty;

    protected Configuracao() { } // NHibernate

    public Configuracao(string chave, string valor)
    {
        Chave = chave;
        Valor = valor;
    }
}
