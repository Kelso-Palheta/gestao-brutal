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

    public async void MostrarSucesso(string msg)
    {
        MsgSucesso = msg;
        MsgErro    = "";
        OnMudou?.Invoke();

        // Auto-hide após 4 segundos
        await Task.Delay(4000);
        if (MsgSucesso == msg) Limpar();
    }

    public async void MostrarErro(string msg)
    {
        MsgErro    = msg;
        MsgSucesso = "";
        OnMudou?.Invoke();

        // Auto-hide após 4 segundos
        await Task.Delay(4000);
        if (MsgErro == msg) Limpar();
    }

    public void Limpar()
    {
        MsgSucesso = "";
        MsgErro    = "";
        OnMudou?.Invoke();
    }
}
