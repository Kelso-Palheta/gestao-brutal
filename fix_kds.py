import re

with open("src/BatatasFritas.Web/Pages/KdsMonitor.razor", "r") as f:
    content = f.read()

# 1. ADD RENDER FRAGMENTS
render_fragments = """
    private RenderFragment ExibirBadgesPedido(PedidoDetalheDto p) => __builder =>
    {
        var badge = BadgePagamento(p);
        <span style="@badge.estilo">@badge.texto</span>
        <span style="background:#333; color:#fff; padding:2px 8px; border-radius:4px; font-size:0.75rem; font-weight:bold;">@NomeMetodo(p.MetodoPagamento)</span>
        @if (p.TipoAtendimento == TipoAtendimento.Totem)
        {
            <span style="background:#7c3aed; color:#fff; padding:2px 8px; border-radius:4px; font-size:0.75rem; font-weight:bold;">📱 TOTEM</span>
        }
        else if (p.TipoAtendimento == TipoAtendimento.Delivery)
        {
            <span style="background:#e67e22; color:#fff; padding:2px 8px; border-radius:4px; font-size:0.75rem; font-weight:bold;">🛵 DELIVERY</span>
        }
        else if (p.TipoAtendimento == TipoAtendimento.Balcao)
        {
            <span style="background:#2980b9; color:#fff; padding:2px 8px; border-radius:4px; font-size:0.75rem; font-weight:bold;">💼 BALCÃO</span>
        }
    };

    private RenderFragment ExibirBotoesPagamento(PedidoDetalheDto p) => __builder =>
    {
        @if (p.StatusPagamento != StatusPagamento.Aprovado && p.StatusPagamento != StatusPagamento.Presencial)
        {
            <button class="kds-btn" style="background:#f39c12; color:#fff; flex: 0 0 auto; border-color:#d68910;" @onclick="() => MarcarPago(p.Id)" title="Marcar como Pago">
                ⚠️ Pendente
            </button>
        }
        else
        {
            <button class="kds-btn" style="background:#27ae60; color:#fff; flex: 0 0 auto; border-color:#229954;" @onclick="() => DesfazerPagamento(p.Id)" title="Desfazer Pagamento">
                ✅ Pago
            </button>
        }
    };
"""
content = content.replace("private string GetHorarioDecorrido(DateTime dataPedido)", render_fragments + "\n    private string GetHorarioDecorrido(DateTime dataPedido)")

# 2. REPLACE BADGES BLOCK
badge_pattern = r"\{\s*var\s+badgePag.*?\}\s*<span style=\"@badgePag.*?</span>\s*<span.*?NomeMetodo.*?</span>(?:\s*@if\s*\(p\.TipoAtendimento\s*==\s*TipoAtendimento\.Totem\)\s*\{\s*<span.*?TOTEM</span>\s*\})?"
content = re.sub(badge_pattern, "@ExibirBadgesPedido(p)", content, flags=re.DOTALL)

# 3. REPLACE PAYMENT BUTTONS BLOCK
# It's an @if block with p.StatusPagamento and two buttons.
payment_btns_pattern = r"@if\s*\([^\{]+StatusPagamento\s*!=\s*StatusPagamento\.Aprovado[^\{]+\{\s*<button[^>]+MarcarPago\([^\}]+\}\s*else\s*\{\s*<button[^>]+DesfazerPagamento[^\}]+\}"
content = re.sub(payment_btns_pattern, "@ExibirBotoesPagamento(p)", content, flags=re.DOTALL)

# 4. ADD SIGNALR HUD
hud_html = """
<div style="display:flex; justify-content:flex-end; padding:0 10px 5px 0;">
    <span style="font-size:0.8rem; padding:4px 8px; border-radius:4px; font-weight:bold; @(signalrStatus == "Conectado" ? "background:#27ae60;color:#fff;" : "background:#c0392b;color:#fff;")">
        SignalR: @signalrStatus
    </span>
</div>
<div class="kds-kanban">
"""
content = content.replace('<div class="kds-kanban">', hud_html)

# Add signalrStatus variable
content = content.replace("private HubConnection? _hubConnection;", "private HubConnection? _hubConnection;\n    private string signalrStatus = \"Conectando...\";")

# Update ConectarSignalR
conectar_old = """        _hubConnection = new HubConnectionBuilder()"""
conectar_new = """        _hubConnection = new HubConnectionBuilder()"""
# Wait, actually let's hook onto the events
connection_hooks = """
        _hubConnection.Closed += (ex) => {
            signalrStatus = "Desconectado!";
            InvokeAsync(StateHasChanged);
            return Task.CompletedTask;
        };
        _hubConnection.Reconnecting += (ex) => {
            signalrStatus = "Reconectando...";
            InvokeAsync(StateHasChanged);
            return Task.CompletedTask;
        };
        _hubConnection.Reconnected += (id) => {
            signalrStatus = "Conectado";
            InvokeAsync(StateHasChanged);
            return Task.CompletedTask;
        };
"""
# Replace _hubConnection.StartAsync() to add try/catch
try_catch_old = """        try
        {
            await _hubConnection.StartAsync();
        }
        catch
        {
            // Falha ao conectar — o KDS continua funcional (sem push em tempo real)
        }"""
try_catch_new = """        try
        {
            await _hubConnection.StartAsync();
            signalrStatus = "Conectado";
        }
        catch (Exception ex)
        {
            signalrStatus = "Erro de Conexão";
            Console.WriteLine(ex.Message);
        }"""
content = content.replace(try_catch_old, try_catch_new)
content = content.replace("        _hubConnection.On<int>(\"NovoPedido\",", connection_hooks + "\n        _hubConnection.On<int>(\"NovoPedido\",")


with open("src/BatatasFritas.Web/Pages/KdsMonitor.razor", "w") as f:
    f.write(content)

print("KdsMonitor.razor patched!")
