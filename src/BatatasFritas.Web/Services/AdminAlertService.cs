namespace BatatasFritas.Web.Services;

/// <summary>
/// Serviço de alertas globais do AdminPanel.
/// Passado via CascadingValue — não registrado no DI.
/// </summary>
public class AdminAlertService
{
    public string MsgSucesso { get; private set; } = "";
    public string MsgErro    { get; private set; } = "";

    /// <summary>Callback invocado após cada mudança de estado (chama StateHasChanged no shell).</summary>
    public Action? OnMudou { get; set; }

    public void MostrarSucesso(string msg)
    {
        MsgSucesso = msg;
        MsgErro    = "";
        OnMudou?.Invoke();
    }

    public void MostrarErro(string msg)
    {
        MsgErro    = msg;
        MsgSucesso = "";
        OnMudou?.Invoke();
    }

    public void Limpar()
    {
        MsgSucesso = "";
        MsgErro    = "";
        OnMudou?.Invoke();
    }
}
