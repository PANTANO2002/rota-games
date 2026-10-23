using RotaGame.Core;
using RotaGame.Windows;

namespace RotaGame.App;

internal enum ConnectionSection { Account, InviteFriend, AcceptInvite, AuthorizeBridge }

internal sealed class ConnectionPage : DashboardPanel
{
    private readonly IConnectionSession _session;
    private readonly Action<string> _openUrl;
    private readonly IInviteClipboard _clipboard;
    private readonly Func<DeviceProfile> _profile;
    private readonly Action<DeviceRole> _chooseRole;
    private readonly DashboardScrollHost _scroll = new(false) { Dock = DockStyle.Fill };
    private readonly DashboardPanel _body = new();
    private readonly Label _summary, _progress, _feedback;
    private readonly System.Windows.Forms.Timer _poll = new() { Interval = 1500 };
    private readonly CancellationTokenSource _lifetime = new();
    private ConnectionCard? _card;
    private DashboardButton? _next;
    private TextBox? _input;
    private DeviceProfile _ownProfile = DeviceProfile.Create();
    private TailStatus? _status;
    private bool _busy, _reading, _offering, _locked, _playerFound;
    private bool _legacyOpened;
    private string _inviteText = "";
    private ConnectionStep? _shownStep;
    private ConnectionStep Step => ConnectionSteps.Next(_profile().Role, _status?.State == "Running", _offering, _status?.SelfApproved == true,
        _profile().Role == DeviceRole.Player ? _status?.Peers.Any(p => p.Online && p.Offered) == true : _playerFound);
    internal bool Busy => _busy;
    public event EventHandler? BackRequested;
    public event EventHandler? BusyChanged;
    public event EventHandler? PrepareBridgeRequested;
    public event Action<TailStatus?>? StatusChanged;

    public ConnectionPage(IConnectionSession? session = null, Action<string>? openUrl = null, IInviteClipboard? clipboard = null,
        Func<DeviceProfile>? profile = null, Action<DeviceRole>? chooseRole = null)
    {
        _session = session ?? new ConnectionSession(); _openUrl = openUrl ?? SetupForm.OpenUrl; _clipboard = clipboard ?? new InviteClipboard();
        _profile = profile ?? (() => _ownProfile); _chooseRole = chooseRole ?? (role => _ownProfile = _ownProfile with { Role = role });
        Dock = DockStyle.Fill; BackColor = DashboardTheme.Canvas; Padding = new Padding(26, 15, 26, 18);
        var layout = new DashboardTableLayout { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Margin = Padding.Empty };
        foreach (var h in new[] { 61, 38, 55, 0, 85 }) layout.RowStyles.Add(h == 0 ? new RowStyle(SizeType.Percent, 100) : new RowStyle(SizeType.Absolute, h));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); Controls.Add(layout);
        var header = new DashboardPanel { Dock = DockStyle.Fill };
        var title = TextLabel("Vamos conectar os dois PCs", 26, DashboardTheme.Ink); title.Dock = DockStyle.Fill;
        var back = Button("Voltar", false, 110); back.Dock = DockStyle.Right; back.Click += (_, _) => BackRequested?.Invoke(this, EventArgs.Empty);
        header.Controls.Add(title); header.Controls.Add(back); layout.Controls.Add(header, 0, 0);
        _progress = TextLabel("", 12, DashboardTheme.Green); _progress.Dock = DockStyle.Fill; layout.Controls.Add(_progress, 0, 1);
        _summary = TextLabel("", 11, DashboardTheme.Ink); _summary.Dock = DockStyle.Fill; layout.Controls.Add(_summary, 0, 2);
        layout.Controls.Add(_scroll, 0, 3); _scroll.SetContent(_body);
        _feedback = TextLabel("A entrada e a permissão usam a página oficial do Tailscale, que fornece o túnel privado.", 10, DashboardTheme.Muted); _feedback.Dock = DockStyle.Fill; layout.Controls.Add(_feedback, 0, 4);
        _scroll.ViewportChanged += (_, _) => ArrangeCard(); _scroll.SizeChanged += (_, _) => ArrangeCard();
        _poll.Tick += async (_, _) => { if (Visible) await RefreshStatusAsync(); };
        VisibleChanged += async (_, _) => { if (Visible) { _poll.Start(); await RefreshStatusAsync(); } else _poll.Stop(); };
        RefreshPresentation();
    }
    internal void SelectSection(ConnectionSection section) => RefreshPresentation();
    internal void SetOffering(bool offering, bool locked = false) { _offering = offering; _locked = locked; RefreshPresentation(); }
    internal void SetPlayerFound(bool found) { if (_playerFound == found) return; _playerFound = found; RefreshPresentation(); }
    internal void RefreshPresentation()
    {
        if (IsDisposed) return; var role = _profile().Role; var step = Step;
        _summary.Text = "Este PC: " + _profile().Name + "   ·   " + (role == DeviceRole.Bridge ? "Ponte · Cascavel" : role == DeviceRole.Player ? "Jogador · Acre" : "Escolha a função abaixo")
            + (_status?.State == "Running" ? "   ·   Login confirmado ✓" : "");
        _progress.Text = step == ConnectionStep.ChooseRole ? "1  IDENTIFICAR ESTE PC" : step == ConnectionStep.Ready ? "3  PRONTO PARA USAR" : "2  CONECTAR · UMA ETAPA POR VEZ";
        if (_shownStep != step) { _shownStep = step; BuildCard(step); _feedback.Text = step == ConnectionStep.Ready ? "Conexão reconhecida. Nas próximas aberturas, sua função já estará salva." : ""; }
        if (_next is not null) _next.Enabled = !_busy && (step != ConnectionStep.StartBridge || !_locked);
        ArrangeCard();
    }
    private void BuildCard(ConnectionStep step)
    {
        if (_input is not null && !_input.IsDisposed) _inviteText = _input.Text;
        _body.Controls.Clear(); _card?.Dispose(); _input = null; _next = null;
        var name = _status?.SelfName ?? _profile().Name;
        var text = step switch
        {
            ConnectionStep.ChooseRole => ("Qual é a função deste PC?", "Escolha uma vez. O RotaGame vai lembrar nas próximas aberturas."),
            ConnectionStep.SignIn => ("Conecte sua conta", "Clique abaixo, entre com Google ou outro provedor e volte ao RotaGame.\nEsta tela avança sozinha quando a conexão for confirmada."),
            ConnectionStep.StartBridge => ("Seu PC vai oferecer a ponte", "Este é o PC de Cascavel. Ele precisa ficar ligado enquanto seu amigo joga.\nO botão abaixo prepara a ponte automaticamente."),
            ConnectionStep.ApproveBridge => ("Falta uma permissão inicial", "Na página que vai abrir, encontre “" + name + "”.\nAbra Edit route settings / Edit exit node, marque Use as exit node e salve.\nO RotaGame confirma a permissão e avança sozinho."),
            ConnectionStep.ShareBridge => ("Envie o acesso à sua ponte", "Na página oficial, encontre “" + name + "” e escolha Share / Compartilhar.\nMarque Allow use as exit node, copie o link e envie ao seu amigo.\nEle aceita na própria conta. Você não precisa convidá-lo para outra rede."),
            ConnectionStep.AcceptBridge => ("Recebeu o link do seu amigo?", "Cole o link abaixo ou use o botão Colar. Depois aceite na página oficial.\nSua ponte aparece aqui automaticamente, sem trocar de rede."),
            _ => (_profile().Role == DeviceRole.Bridge ? "Sua ponte está pronta" : "Ponte encontrada. Pode jogar!",
                _profile().Role == DeviceRole.Bridge ? "Seu amigo foi reconhecido. Deixe este PC ligado.\nO painel mostrará as medições feitas no computador dele."
                : "Encontrada: " + string.Join(" · ", _status!.Peers.Where(p => p.Online && p.Offered).Select(p => p.Name)) + "\nVolte ao painel e clique em Ativar conexão. O programa compara os caminhos.")
        };
        _card = new ConnectionCard(text.Item1, text.Item2, 22); _body.Controls.Add(_card);
        if (step == ConnectionStep.ChooseRole)
        {
            var roles = new DashboardTableLayout { ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
            roles.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); roles.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); roles.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var player = new RoleChoiceButton(DeviceRole.Player) { Dock = DockStyle.Fill, AccessibleName = "Vou jogar neste PC" }; var host = new RoleChoiceButton(DeviceRole.Bridge) { Dock = DockStyle.Fill, AccessibleName = "Este PC é a ponte" };
            player.Click += (_, _) => Choose(DeviceRole.Player); host.Click += (_, _) => Choose(DeviceRole.Bridge);
            roles.Controls.Add(player, 0, 0); roles.Controls.Add(host, 1, 0); _card.AddField(roles, 112);
        }
        else
        {
            if (step == ConnectionStep.AcceptBridge)
            {
                var field = new DashboardSurface { BackColor = DashboardTheme.Raised, Padding = new Padding(12, 12, 12, 9) };
                _input = new TextBox { BorderStyle = BorderStyle.None, BackColor = DashboardTheme.Raised, ForeColor = DashboardTheme.Ink, Font = FontAssets.Ui(11), Dock = DockStyle.Fill,
                    PlaceholderText = "Link recebido do seu amigo", Text = _inviteText, AccessibleName = "Convite da ponte" }; field.Controls.Add(_input); _card.AddField(field, 48);
            }
            var label = step switch { ConnectionStep.SignIn => "Entrar e conectar", ConnectionStep.StartBridge => "Ativar minha ponte", ConnectionStep.ApproveBridge => "Abrir permissão da ponte", ConnectionStep.ShareBridge => "Compartilhar minha ponte", ConnectionStep.AcceptBridge => _legacyOpened ? "Conectar à rede convidada" : "Aceitar convite e conectar", _ => "Voltar ao painel" };
            _next = Button(label, true, 310); _next.Height = 54; _next.Click += async (_, _) => await NextAsync(); _card.AddActions(_next);
            if (step == ConnectionStep.AcceptBridge)
            {
                var paste = Button("Colar convite", false, 180); paste.Height = 54; paste.Click += (_, _) => ClipboardAction(() => { if (_input is not null) _input.Text = _clipboard.Read().Trim(); }); _card.AddActions(paste);
            }
            if (step is ConnectionStep.ApproveBridge or ConnectionStep.ShareBridge)
            {
                var copy = Button("Copiar nome deste PC", false, 250); copy.Height = 54; copy.Click += (_, _) => ClipboardAction(() => { _clipboard.Write(name); _feedback.Text = "Nome copiado: " + name; }); _card.AddActions(copy);
            }
        }
        ArrangeCard();
    }
    private void Choose(DeviceRole role) { _chooseRole(role); RefreshPresentation(); }
    private async Task NextAsync()
    {
        switch (Step)
        {
            case ConnectionStep.SignIn: await RunAsync(() => _session.LoginAsync(Progress(), BrowserProgress(), _lifetime.Token)); break;
            case ConnectionStep.StartBridge: PrepareBridgeRequested?.Invoke(this, EventArgs.Empty); break;
            case ConnectionStep.ApproveBridge:
            case ConnectionStep.ShareBridge: OpenOfficial("https://console.tailscale.com/admin/machines"); break;
            case ConnectionStep.AcceptBridge:
                if (_legacyOpened) { await RunAsync(() => _session.LoginAsync(Progress(), BrowserProgress(), _lifetime.Token, true)); break; }
                var link = _input?.Text.Trim() ?? "";
                if (link.Length == 0) { try { link = _clipboard.Read().Trim(); if (_input is not null) _input.Text = link; } catch { } }
                if (!IsInviteUrl(link)) { _feedback.Text = "Esse link não é um convite válido. Peça ao amigo o link de Compartilhar a ponte."; break; }
                if (OpenOfficial(link))
                {
                    _feedback.Text = "Aceite no navegador. Estou procurando sua ponte automaticamente…";
                    if (ConnectionInvites.IsNetworkInvite(link)) { _legacyOpened = true; if (_next is not null) _next.Text = "Conectar à rede convidada"; }
                }
                break;
            case ConnectionStep.Ready: BackRequested?.Invoke(this, EventArgs.Empty); break;
        }
    }
    private bool _arranging;
    private void ArrangeCard()
    {
        if (_arranging || _card is null || _card.IsDisposed) return; _arranging = true;
        try { var width = Math.Max(1, _scroll.ViewportSize.Width); var height = _card.PreferredHeight(width); _card.SetBounds(0, 0, width, height); _scroll.SetExtent(Math.Max(_scroll.ClientSize.Height, height + 12)); }
        finally { _arranging = false; }
    }
    internal async Task RefreshStatusAsync()
    {
        if (_reading || IsDisposed) return; _reading = true;
        try { var status = await _session.ReadAsync(_lifetime.Token); if (!IsDisposed) { _status = status; RefreshPresentation(); StatusChanged?.Invoke(status); } }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception) { if (!IsDisposed) _feedback.Text = "Ainda não consegui confirmar a conexão. Vou tentar novamente automaticamente."; }
        finally { _reading = false; }
    }
    private async Task RunAsync(Func<Task> operation)
    {
        if (_busy) return; _busy = true; RefreshPresentation(); BusyChanged?.Invoke(this, EventArgs.Empty);
        try { await operation(); }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception ex) { if (!IsDisposed) _feedback.Text = ex.Message; }
        finally { _busy = false; if (!IsDisposed) { await RefreshStatusAsync(); RefreshPresentation(); BusyChanged?.Invoke(this, EventArgs.Empty); } }
    }
    internal static bool IsInviteUrl(string text) => ConnectionInvites.IsValid(text);
    private bool OpenOfficial(string url) { try { _openUrl(url); return true; } catch { _feedback.Text = "Não consegui abrir o navegador. Tente novamente pelo botão."; return false; } }
    private IProgress<string> Progress() => new Progress<string>(s => { if (!IsDisposed) _feedback.Text = s; });
    private IProgress<string> BrowserProgress() => new Progress<string>(s => { if (!IsDisposed) OpenOfficial(s); });
    private void ClipboardAction(Action action) { try { action(); } catch { _feedback.Text = "Não consegui colar. Você pode usar Ctrl+V no campo do convite."; } }
    private static DashboardButton Button(string text, bool primary, int width) => new() { Text = text, Width = width, Height = 44, Font = FontAssets.Ui(11), ForeColor = DashboardTheme.Ink, BackColor = primary ? DashboardTheme.Accent : DashboardTheme.Raised };
    private static Label TextLabel(string text, float size, Color color) => new() { Text = text, Font = size >= 16 ? FontAssets.Title(size) : FontAssets.Ui(size), ForeColor = color, BackColor = Color.Transparent, TextAlign = ContentAlignment.MiddleLeft };
    protected override void Dispose(bool disposing) { if (disposing) { _poll.Stop(); _poll.Dispose(); _lifetime.Cancel(); _lifetime.Dispose(); } base.Dispose(disposing); }

    internal async Task RunSmokeAsync(string directory)
    {
        Directory.CreateDirectory(directory); Capture("wizard-choose-role.png"); Choose(DeviceRole.Bridge); await RefreshStatusAsync();
        if (Step != ConnectionStep.SignIn) throw new InvalidOperationException("Etapa inicial incorreta.");
        await NextAsync(); await Task.Delay(60);
        if (Step != ConnectionStep.StartBridge || !_summary.Text.Contains("Login confirmado")) throw new InvalidOperationException("Login não avançou sozinho.");
        Capture("wizard-login-confirmed.png"); SetOffering(true);
        if (Step != ConnectionStep.ApproveBridge) throw new InvalidOperationException("Permissão não foi solicitada.");
        Capture("wizard-permission.png"); await NextAsync(); var fake = (SmokeConnectionSession)_session; fake.Approved = true; await RefreshStatusAsync();
        if (Step != ConnectionStep.ShareBridge) throw new InvalidOperationException("Permissão não avançou automaticamente.");
        Capture("wizard-share-bridge.png"); await NextAsync(); SetPlayerFound(true);
        if (Step != ConnectionStep.Ready) throw new InvalidOperationException("Jogador não foi reconhecido.");
        Capture("wizard-ready.png"); SetOffering(false); Choose(DeviceRole.Player); fake.PeerAvailable = false; await RefreshStatusAsync();
        if (Step != ConnectionStep.AcceptBridge) throw new InvalidOperationException("Convite não apareceu no jogador.");
        _clipboard.Write("https://console.tailscale.com/admin/invite/interface-only"); await NextAsync(); Capture("wizard-accept.png");
        fake.PeerAvailable = true; await RefreshStatusAsync(); if (Step != ConnectionStep.Ready) throw new InvalidOperationException("Aceite não avançou pelo estado real.");
        foreach (var link in new[] { "https://login.tailscale.com/uinv/test", "https://console.tailscale.com/admin/invite/test", "https://login.tailscale.com/admin/invite/test" }) if (!IsInviteUrl(link)) throw new InvalidOperationException("Convite oficial rejeitado.");
        foreach (var link in new[] { "http://login.tailscale.com/uinv/test", "https://login.tailscale.com.evil/uinv/test", "https://console.tailscale.com/admin/machines" }) if (IsInviteUrl(link)) throw new InvalidOperationException("URL inválida aceita.");
        File.WriteAllText(Path.Combine(directory, "account-smoke.txt"), "PASS: assistente com uma ação por vez; login e autorização avançam pelo estado real, ponte/jogador identificados, convite de compartilhamento sem troca de rede, links oficiais novos e antigos aceitos, links estranhos rejeitados. Apenas dados controlados, sem conta ou rede real.\n");
        void Capture(string file) { _card!.CheckTextAndActions(); using var image = new Bitmap(Width, Height); DrawToBitmap(image, ClientRectangle); image.Save(Path.Combine(directory, file)); }
    }
}
