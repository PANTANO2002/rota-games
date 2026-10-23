using RotaGame.Windows;

namespace RotaGame.App;

internal enum ConnectionSection { Account, InviteFriend, AcceptInvite, AuthorizeBridge }

internal sealed class ConnectionPage : DashboardPanel
{
    private readonly IConnectionSession _session;
    private readonly Action<string> _openUrl;
    private readonly IInviteClipboard _clipboard;
    private readonly Label _message, _connectionState, _machine, _bridge, _peers, _inviteState, _approvalState;
    private readonly DashboardButton _login, _install, _refresh, _invite, _approve, _openInvite, _joinNetwork, _prepare, _manualApproval, _checkApproval;
    private readonly TextBox _inviteLink = new();
    private readonly TextBox _ownerInvite = new();
    private readonly DashboardButton _createInvite, _pasteOwnerInvite, _copyOwnerInvite, _pasteAcceptInvite;
    private readonly Label _recognition;
    private readonly DashboardScrollHost _scroller = new(false) { Dock = DockStyle.Fill };
    private readonly Dictionary<ConnectionSection, (DashboardTableLayout Body, int[] MinimumHeights, DashboardButton Tab)> _sections = [];
    private readonly System.Windows.Forms.Timer _poll = new() { Interval = 3000 };
    private readonly CancellationTokenSource _lifetime = new();
    private TailStatus? _status;
    private bool _busy, _reading, _offering, _connectionLocked, _inviteOpened, _sizingSections;
    private ConnectionSection _section;
    internal bool Busy => _busy;
    public event EventHandler? BackRequested;
    public event EventHandler? BusyChanged;
    public event EventHandler? PrepareBridgeRequested;
    public event Action<TailStatus?>? StatusChanged;

    public ConnectionPage(IConnectionSession? session = null, Action<string>? openUrl = null, IInviteClipboard? clipboard = null)
    {
        _session = session ?? new ConnectionSession(); _openUrl = openUrl ?? SetupForm.OpenUrl; _clipboard = clipboard ?? new InviteClipboard();
        Dock = DockStyle.Fill; BackColor = DashboardTheme.Canvas; Padding = new Padding(24, 15, 24, 16);
        var root = new DashboardTableLayout { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Margin = Padding.Empty, BackColor = BackColor };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        foreach (var row in new[] { 78, 57, 0, 46 }) root.RowStyles.Add(row == 0 ? new RowStyle(SizeType.Percent, 100) : new RowStyle(SizeType.Absolute, row));
        Controls.Add(root);
        var heading = new DashboardPanel { Dock = DockStyle.Fill, BackColor = BackColor };
        var backHolder = new DashboardPanel { Dock = DockStyle.Right, Width = 183, Padding = new Padding(0, 14, 0, 0), BackColor = BackColor };
        var back = Button("Voltar aos jogos"); back.Dock = DockStyle.Top; back.Click += (_, _) => BackRequested?.Invoke(this, EventArgs.Empty); backHolder.Controls.Add(back);
        var title = TextLabel("Conta e conexão", 26, DashboardTheme.Ink); title.Dock = DockStyle.Top; title.Height = 43;
        var subtitle = TextLabel("Conecte os dois PCs e acompanhe cada etapa por aqui.", 11, DashboardTheme.Muted); subtitle.Dock = DockStyle.Fill;
        heading.Controls.Add(subtitle); heading.Controls.Add(title); heading.Controls.Add(backHolder); root.Controls.Add(heading, 0, 0);
        var tabs = new DashboardFlowLayout { Dock = DockStyle.Fill, WrapContents = false, BackColor = BackColor, Margin = Padding.Empty };
        root.Controls.Add(tabs, 0, 1); root.Controls.Add(_scroller, 0, 2);
        _message = TextLabel("Conexão privada fornecida pelo Tailscale. As autorizações oficiais abrem no seu navegador.", 9.5f, DashboardTheme.Muted);
        _message.Dock = DockStyle.Fill; root.Controls.Add(_message, 0, 3);
        var account = AddSection(ConnectionSection.Account, "Minha conta", 158, [174, 140, 127, 100]);
        var owner = AddSection(ConnectionSection.InviteFriend, "Convidar amigo", 177, [126, 190, 185]);
        var accept = AddSection(ConnectionSection.AcceptInvite, "Aceitar convite", 177, [94, 178, 137, 121]);
        var authorize = AddSection(ConnectionSection.AuthorizeBridge, "Autorizar ponte", 177, [94, 123, 183, 116]);

        var entry = Card("Tudo começa pela sua conta", "Entre com Google ou outro provedor disponível no Tailscale.\nA entrada é oficial; o RotaGame detecta a conexão ao terminar.", 18, 53);
        _login = Button("Entrar ou criar conta", true); _login.Width = 220;
        _login.Click += async (_, _) => await RunAsync(() => _session.LoginAsync(Progress(), BrowserProgress(), _lifetime.Token));
        _install = Button("Instalar conexão"); _install.Width = 175; _install.Click += async (_, _) => await RunAsync(() => _session.InstallAsync(Progress(), _lifetime.Token));
        _refresh = Button("Atualizar status"); _refresh.Width = 165; _refresh.Click += async (_, _) => await RefreshStatusAsync();
        AddActions(entry, _login, _install, _refresh); account.Controls.Add(entry, 0, 0);
        var status = Surface(); var statusTitle = TextLabel("ESTE COMPUTADOR", 10, DashboardTheme.Muted); statusTitle.Dock = DockStyle.Top; statusTitle.Height = 25;
        _connectionState = TextLabel("Verificando conexão…", 19, DashboardTheme.Green); _connectionState.Dock = DockStyle.Top; _connectionState.Height = 30;
        _machine = TextLabel("PC: " + Environment.MachineName, 10.5f, DashboardTheme.Ink); _machine.Dock = DockStyle.Top; _machine.Height = 25;
        _bridge = TextLabel("", 10, DashboardTheme.Muted); _bridge.Dock = DockStyle.Fill;
        status.Controls.Add(_bridge); status.Controls.Add(_machine); status.Controls.Add(_connectionState); status.Controls.Add(statusTitle); account.Controls.Add(status, 0, 1);
        var pairing = Card("Dois computadores, uma conexão", "Você convida em Cascavel. Seu amigo aceita no Acre. Depois, autorize o PC que vai oferecer a ponte.", 16, 29);
        _invite = Button("Convidar meu amigo"); _invite.Width = 230; _invite.Click += (_, _) => SelectSection(ConnectionSection.InviteFriend);
        _approve = Button("Configurar autorização"); _approve.Width = 220; _approve.Click += (_, _) => SelectSection(ConnectionSection.AuthorizeBridge);
        AddActions(pairing, _invite, _approve); account.Controls.Add(pairing, 0, 2);
        var peers = Surface(); var peersTitle = TextLabel("Computadores encontrados", 16, DashboardTheme.Ink); peersTitle.Dock = DockStyle.Top; peersTitle.Height = 32;
        _peers = TextLabel("Entre para conferir os computadores disponíveis.", 10.5f, DashboardTheme.Muted); _peers.Dock = DockStyle.Fill;
        peers.Controls.Add(_peers); peers.Controls.Add(peersTitle); account.Controls.Add(peers, 0, 3);

        var recognized = Surface(); var recognizedTitle = TextLabel("Se os PCs já se encontram, pule o convite", 18, DashboardTheme.Ink); recognizedTitle.Dock = DockStyle.Top; recognizedTitle.Height = 34;
        _recognition = TextLabel("Entre na conta para buscar os computadores da sua rede.", 11, DashboardTheme.Muted); _recognition.Dock = DockStyle.Fill;
        recognized.Controls.Add(_recognition); recognized.Controls.Add(recognizedTitle); owner.Controls.Add(recognized, 0, 0);
        var generate = Card("01  ·  De onde vem o link?", "No PC de Cascavel, abra a página oficial pelo botão abaixo. Em Users, selecione:\nInvite external users → Copy invite link → Member → Generate & copy invite link.\nO link será copiado pelo Tailscale. Volte ao RotaGame e use Colar link na etapa 02.", 18, 77);
        _createInvite = Button("Abrir criação do convite", true); _createInvite.Width = 260; _createInvite.Click += (_, _) => OpenOfficial("https://login.tailscale.com/admin/users");
        AddActions(generate, _createInvite); owner.Controls.Add(generate, 0, 1);
        var share = Card("02  ·  Copie e envie ao seu amigo", "Cole o convite gerado para deixá-lo acessível aqui. Seu amigo usa Aceitar convite no PC do Acre.\nO link fica apenas nesta sessão; abrir esta aba não cria um convite novo.", 18, 54);
        var ownerInput = new DashboardSurface { BackColor = DashboardTheme.Raised, Padding = new Padding(12, 10, 12, 8) };
        _ownerInvite.BorderStyle = BorderStyle.None; _ownerInvite.Dock = DockStyle.Fill; _ownerInvite.BackColor = DashboardTheme.Raised; _ownerInvite.ForeColor = DashboardTheme.Ink; _ownerInvite.Font = FontAssets.Ui(11);
        _ownerInvite.PlaceholderText = "O link aparece aqui depois de usar Colar link"; _ownerInvite.AccessibleName = "Convite para enviar ao amigo";
        ownerInput.Controls.Add(_ownerInvite); share.AddField(ownerInput, 43);
        _pasteOwnerInvite = Button("Colar link"); _pasteOwnerInvite.Width = 155; _pasteOwnerInvite.Click += (_, _) => ClipboardAction(() => _ownerInvite.Text = _clipboard.Read().Trim());
        _copyOwnerInvite = Button("Copiar convite para enviar", true); _copyOwnerInvite.Width = 260; _copyOwnerInvite.Click += (_, _) => CopyOwnerInvite();
        AddActions(share, _pasteOwnerInvite, _copyOwnerInvite); owner.Controls.Add(share, 0, 2);

        accept.Controls.Add(new PairingBanner(false) { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 12) }, 0, 0);
        var acceptCard = Card("01  ·  Recebeu um convite?", "No PC do Acre, cole o link enviado pelo seu amigo. Abra e aceite usando sua própria conta.", 18, 29);
        var input = new DashboardSurface { Dock = DockStyle.Top, Height = 43, BackColor = DashboardTheme.Raised, Padding = new Padding(12, 10, 12, 8), Margin = Padding.Empty };
        _inviteLink.BorderStyle = BorderStyle.None; _inviteLink.Dock = DockStyle.Fill; _inviteLink.BackColor = DashboardTheme.Raised; _inviteLink.ForeColor = DashboardTheme.Ink;
        _inviteLink.Font = FontAssets.Ui(11); _inviteLink.PlaceholderText = "Cole o link de convite do Tailscale"; _inviteLink.AccessibleName = "Link de convite";
        input.Controls.Add(_inviteLink); acceptCard.AddField(input, 43);
        _openInvite = Button("Abrir e aceitar convite", true); _openInvite.Width = 240; _openInvite.Click += (_, _) => OpenInvite();
        _pasteAcceptInvite = Button("Colar convite recebido"); _pasteAcceptInvite.Width = 230; _pasteAcceptInvite.Click += (_, _) => ClipboardAction(() => _inviteLink.Text = _clipboard.Read().Trim());
        AddActions(acceptCard, _pasteAcceptInvite, _openInvite); accept.Controls.Add(acceptCard, 0, 1);
        var join = Card("02  ·  Conecte este PC à rede convidada", "Depois de aceitar, entre e selecione a rede do seu amigo na autorização oficial.", 18, 31);
        _joinNetwork = Button("Já aceitei · conectar este PC", true); _joinNetwork.Width = 280;
        _joinNetwork.Click += async (_, _) => await RunAsync(() => _session.LoginAsync(Progress(), BrowserProgress(), _lifetime.Token, chooseNetwork: true));
        AddActions(join, _joinNetwork); accept.Controls.Add(join, 0, 2);
        var inviteStatus = Surface(); var foundTitle = TextLabel("03  ·  Confira a ponte", 18, DashboardTheme.Ink); foundTitle.Dock = DockStyle.Top; foundTitle.Height = 32;
        _inviteState = TextLabel("Aguardando conexão deste computador.", 11, DashboardTheme.Muted); _inviteState.Dock = DockStyle.Fill;
        inviteStatus.Controls.Add(_inviteState); inviteStatus.Controls.Add(foundTitle); accept.Controls.Add(inviteStatus, 0, 3);

        authorize.Controls.Add(new PairingBanner(true) { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 12) }, 0, 0);
        var prepare = Card("01  ·  Ofereça o PC de Cascavel", "Faça esta etapa no computador que ficará ligado para servir de ponte.", 18, 28);
        _prepare = Button("Preparar este PC como ponte", true); _prepare.Width = 280; _prepare.Click += (_, _) => PrepareBridgeRequested?.Invoke(this, EventArgs.Empty);
        AddActions(prepare, _prepare); authorize.Controls.Add(prepare, 0, 1);
        var permission = Card("02  ·  Autorize a ponte, se necessário", "Se a rede já permite a aprovação automática, o estado abaixo muda sozinho.\nCaso continue pendente, abra a autorização oficial, encontre este PC e escolha:\nEdit route settings / Edit exit node → Use as exit node → Save.", 18, 73);
        _manualApproval = Button("Abrir autorização oficial", true); _manualApproval.Width = 255; _manualApproval.Click += (_, _) => OpenOfficial("https://login.tailscale.com/admin/machines");
        _checkApproval = Button("Verificar autorização"); _checkApproval.Width = 215; _checkApproval.Click += async (_, _) => await RefreshStatusAsync();
        AddActions(permission, _manualApproval, _checkApproval); authorize.Controls.Add(permission, 0, 2);
        var approval = Surface(); var approvalTitle = TextLabel("03  ·  Estado da ponte", 18, DashboardTheme.Ink); approvalTitle.Dock = DockStyle.Top; approvalTitle.Height = 32;
        _approvalState = TextLabel("Verificando este computador…", 11, DashboardTheme.Muted); _approvalState.Dock = DockStyle.Fill;
        approval.Controls.Add(_approvalState); approval.Controls.Add(approvalTitle); authorize.Controls.Add(approval, 0, 3);
        _scroller.SizeChanged += (_, _) => UpdateExtent();
        _scroller.ViewportChanged += (_, _) => UpdateExtent();
        _poll.Tick += async (_, _) => { if (Visible && !_busy) await RefreshStatusAsync(); };
        VisibleChanged += async (_, _) => { if (Visible) { _poll.Start(); await RefreshStatusAsync(); } else _poll.Stop(); };
        SelectSection(ConnectionSection.Account);

        DashboardTableLayout AddSection(ConnectionSection section, string name, int width, int[] heights)
        {
            var body = new DashboardTableLayout { ColumnCount = 1, RowCount = heights.Length + 1, BackColor = BackColor, Margin = Padding.Empty };
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            foreach (var h in heights) body.RowStyles.Add(new RowStyle(SizeType.Absolute, h));
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var tab = Button(name); tab.Width = width; tab.Height = 42; tab.Click += (_, _) => SelectSection(section); tabs.Controls.Add(tab);
            _sections.Add(section, (body, heights, tab)); return body;
        }
    }

    internal void SelectSection(ConnectionSection section)
    {
        _section = section;
        foreach (var item in _sections) { item.Value.Tab.SelectedStyle = item.Key == section; item.Value.Tab.Invalidate(); }
        _scroller.SetContent(_sections[section].Body); _scroller.ScrollPosition = 0; UpdateExtent(); Invalidate(true);
    }
    private void UpdateExtent()
    {
        if (_sizingSections || !_sections.TryGetValue(_section, out var section)) return; _sizingSections = true;
        try
        {
            var height = 0; var scale = DeviceDpi / 96f;
            for (var row = 0; row < section.MinimumHeights.Length; row++)
            {
                var control = section.Body.GetControlFromPosition(0, row);
                var needed = control is ConnectionCard card ? card.PreferredHeight(Math.Max(1, _scroller.ViewportSize.Width)) + card.Margin.Vertical : 0;
                var rowHeight = Math.Max((int)(section.MinimumHeights[row] * scale), needed);
                if (Math.Abs(section.Body.RowStyles[row].Height - rowHeight) > .5) section.Body.RowStyles[row].Height = rowHeight;
                height += rowHeight;
            }
            _scroller.SetExtent(Math.Max(_scroller.ClientSize.Height, height));
        }
        finally { _sizingSections = false; }
    }
    internal void SetOffering(bool offering, bool locked = false) { _offering = offering; _connectionLocked = offering || locked; UpdateState(); }

    internal async Task RefreshStatusAsync()
    {
        if (_reading || IsDisposed) return; _reading = true;
        try { _status = await _session.ReadAsync(_lifetime.Token); if (!IsDisposed) { UpdateState(); StatusChanged?.Invoke(_status); } }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception ex) { if (!IsDisposed) _message.Text = "Não foi possível consultar a conexão: " + ex.Message; }
        finally { _reading = false; }
    }
    private void UpdateState()
    {
        var connected = _status?.State == "Running";
        var name = string.IsNullOrWhiteSpace(_status?.SelfName) ? Environment.MachineName : _status.SelfName;
        _connectionState.Text = connected ? "Conta conectada" : _status is null ? "Componente ainda não instalado" : "Entre para conectar este PC";
        _connectionState.ForeColor = connected ? DashboardTheme.Green : DashboardTheme.Muted;
        _machine.Text = "PC: " + name;
        _bridge.Text = _status?.SelfApproved == true ? "Permissão de ponte aprovada" : "Convite e ponte: acompanhe pelas abas acima.";
        var online = _status?.Peers.Where(p => p.Online).ToArray() ?? [];
        _peers.Text = online.Length == 0 ? "Nenhum outro computador conectado foi encontrado." : string.Join("   ·   ", online.Select(p => p.Name + (p.Offered ? " (ponte)" : "")));
        var bridges = online.Where(p => p.Offered).ToArray();
        _recognition.Text = !connected ? "Entre na conta para buscar os computadores da sua rede." : online.Length == 0
            ? "Nenhum outro PC online nesta rede. Se seu amigo usa outra conta/rede, gere o convite abaixo."
            : "PCs encontrados automaticamente: " + string.Join(" · ", online.Select(p => p.Name)) + "\nSe o PC do seu amigo está nesta lista, não é necessário outro convite. Confira Autorizar ponte.";
        _inviteState.Text = !connected ? "Aguardando conexão deste computador." : bridges.Length == 0
            ? "Conta conectada. Aguardando uma ponte autorizada na mesma rede.\nSe já aceitou o convite, confirme que escolheu a rede do seu amigo."
            : "Ponte encontrada automaticamente: " + string.Join(" · ", bridges.Select(p => p.Name)) + "\nVocê já pode pular o convite. Volte aos jogos e clique em Ativar para medir os caminhos.";
        _inviteState.ForeColor = bridges.Length > 0 ? DashboardTheme.Green : DashboardTheme.Muted;
        _approvalState.Text = !connected ? "Entre na sua conta antes de preparar este PC." : _status?.SelfApproved == true
            ? name + " · permissão aprovada" + (_offering ? " · ponte ativa\nSeu amigo já pode voltar aos jogos e medir a conexão." : "\nClique em Preparar este PC como ponte para deixá-la disponível.")
            : name + (_offering ? " · ponte oferecida, autorização pendente\nConclua a etapa 02 com a conta que administra a rede." : " · ainda não está autorizado\nPrepare a ponte e confira a autorização na etapa 02.");
        _approvalState.ForeColor = _status?.SelfApproved == true ? DashboardTheme.Green : DashboardTheme.Muted;
        _login.Enabled = _joinNetwork.Enabled = !_busy && !_connectionLocked;
        _install.Enabled = !_busy && !_connectionLocked && !_session.Installed;
        _inviteLink.Enabled = _openInvite.Enabled = !_busy && !_connectionLocked;
        _refresh.Enabled = _checkApproval.Enabled = !_busy;
        _invite.Enabled = _approve.Enabled = _manualApproval.Enabled = !_busy && connected;
        _prepare.Enabled = !_busy && connected && !_connectionLocked;
        _prepare.Text = _offering ? "Ponte oferecida neste PC" : "Preparar este PC como ponte";
        _createInvite.Enabled = !_busy && connected;
        _pasteOwnerInvite.Enabled = _copyOwnerInvite.Enabled = !_busy;
        _pasteAcceptInvite.Enabled = !_busy && !_connectionLocked;
    }
    internal static bool IsInviteUrl(string value) => Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) && uri.Scheme == "https"
        && uri.IsDefaultPort && uri.UserInfo.Length == 0 && uri.Host is "login.tailscale.com" or "console.tailscale.com"
        && uri.AbsolutePath.StartsWith("/uinv/", StringComparison.Ordinal) && uri.AbsolutePath.Length > 6;
    private void OpenInvite()
    {
        if (!IsInviteUrl(_inviteLink.Text)) { _message.Text = "Cole um convite de usuário válido: https://login.tailscale.com/uinv/… (gerado em Users → Invite users)."; return; }
        if (OpenOfficial(_inviteLink.Text.Trim())) { _inviteOpened = true; _message.Text = "Convite aberto. Aceite na página oficial e depois use Já aceitei · conectar este PC."; }
    }
    private bool OpenOfficial(string url)
    {
        try { _openUrl(url); return true; }
        catch (Exception ex) { _message.Text = "Não foi possível abrir a página oficial: " + ex.Message; return false; }
    }
    private void CopyOwnerInvite()
    {
        if (!IsInviteUrl(_ownerInvite.Text)) { _message.Text = "Gere o convite na etapa 01 e cole o link aqui antes de copiar."; return; }
        ClipboardAction(() => { _clipboard.Write(_ownerInvite.Text.Trim()); _message.Text = "Convite copiado. Envie ao amigo; ele pode usar Colar convite recebido na aba Aceitar convite."; });
    }
    private void ClipboardAction(Action action)
    {
        try { action(); }
        catch (Exception) { _message.Text = "Não consegui acessar a área de transferência. Você também pode usar Ctrl+C e Ctrl+V no campo do link."; }
    }
    private IProgress<string> Progress() => new Progress<string>(text => { if (!IsDisposed) _message.Text = text; });
    private IProgress<string> BrowserProgress() => new Progress<string>(url => { if (!IsDisposed) OpenOfficial(url); });
    private async Task RunAsync(Func<Task> operation)
    {
        if (_busy || _connectionLocked) return; _busy = true; UpdateState(); BusyChanged?.Invoke(this, EventArgs.Empty);
        try { await operation(); }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception ex) { if (!IsDisposed) _message.Text = ex.Message; }
        finally { _busy = false; if (!IsDisposed) { UpdateState(); BusyChanged?.Invoke(this, EventArgs.Empty); await RefreshStatusAsync(); } }
    }
    private static DashboardSurface Surface() => new() { Dock = DockStyle.Fill, Padding = new Padding(20, 10, 20, 10), Margin = new Padding(0, 0, 0, 12) };
    private static ConnectionCard Card(string title, string description, float titleSize, int descriptionHeight) => new(title, description, titleSize) { Dock = DockStyle.Fill };
    private static void AddActions(ConnectionCard card, params DashboardButton[] buttons) => card.AddActions(buttons);
    private static DashboardButton Button(string text, bool primary = false) => new() { Text = text, Height = 40, BackColor = primary ? DashboardTheme.Accent : DashboardTheme.Raised, ForeColor = DashboardTheme.Ink, Font = FontAssets.Ui(10.5f), Margin = new Padding(0, 0, 10, 0) };
    private static Label TextLabel(string text, float size, Color color) => new() { Text = text, Font = size >= 16 ? FontAssets.Title(size) : FontAssets.Ui(size), ForeColor = color, BackColor = Color.Transparent, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft };
    protected override void Dispose(bool disposing)
    {
        if (disposing) { _poll.Stop(); _poll.Dispose(); _lifetime.Cancel(); _lifetime.Dispose(); foreach (var section in _sections.Values) if (!section.Body.IsDisposed && section.Body.Parent is null) section.Body.Dispose(); }
        base.Dispose(disposing);
    }

    internal async Task RunSmokeAsync(string directory)
    {
        Directory.CreateDirectory(directory);
        await RefreshStatusAsync(); if (!_connectionState.Text.Contains("não instalado")) throw new InvalidOperationException("Estado inicial incorreto.");
        await RunAsync(() => _session.InstallAsync(Progress(), _lifetime.Token));
        await RunAsync(() => _session.LoginAsync(Progress(), BrowserProgress(), _lifetime.Token)); await Task.Delay(80);
        if (!_connectionState.Text.Contains("conectada") || !_peers.Text.Contains("PC do amigo")) throw new InvalidOperationException("Entrada não atualizou a conta.");
        Capture("interface-account-test.png"); _invite.PerformClick(); _createInvite.PerformClick();
        _ownerInvite.Text = "https://login.tailscale.com/uinv/interface-test-only"; _copyOwnerInvite.PerformClick();
        if (_clipboard.Read() != _ownerInvite.Text || !_recognition.Text.Contains("não é necessário outro convite")) throw new InvalidOperationException("O convite não ficou fácil de copiar ou o PC existente não foi reconhecido.");
        Capture("interface-create-invite-test.png");
        SelectSection(ConnectionSection.AcceptInvite);
        foreach (var bad in new[] { "http://login.tailscale.com/uinv/no", "https://login.tailscale.com.evil.test/uinv/no", "https://google.com/", "https://login.tailscale.com:444/uinv/no", "https://login.tailscale.com/admin/invite/device-only", "https://login.tailscale.com/uinv/", "https://login.tailscale.com/admin/machines" })
            if (IsInviteUrl(bad)) throw new InvalidOperationException("Convite indevido aceito pelo campo.");
        _pasteAcceptInvite.PerformClick();
        if (_inviteLink.Text != _ownerInvite.Text) throw new InvalidOperationException("Colar convite recebido não recuperou o link.");
        _openInvite.PerformClick();
        if (!_inviteOpened) throw new InvalidOperationException("O convite válido não abriu.");
        await RunAsync(() => _session.LoginAsync(Progress(), BrowserProgress(), _lifetime.Token, chooseNetwork: true)); await Task.Delay(80);
        if (_session is not SmokeConnectionSession fake || fake.NetworkChoices != 1) throw new InvalidOperationException("A escolha da rede convidada foi ignorada.");
        if (!_inviteState.Text.Contains("Ponte encontrada")) throw new InvalidOperationException("A ponte disponível não apareceu.");
        Capture("interface-invite-test.png");
        SelectSection(ConnectionSection.AuthorizeBridge); _manualApproval.PerformClick();
        if (_approvalState.Text.Contains("aprovada")) throw new InvalidOperationException("Abrir o site simulou uma aprovação.");
        SetOffering(true);
        if (_login.Enabled || _joinNetwork.Enabled || _prepare.Enabled || !_approvalState.Text.Contains("pendente")) throw new InvalidOperationException("Bloqueio da ponte ativa incorreto.");
        Capture("interface-authorization-pending-test.png");
        fake.Approved = true; await RefreshStatusAsync();
        if (!_approvalState.Text.Contains("permissão aprovada") || !_approvalState.Text.Contains("ponte ativa")) throw new InvalidOperationException("A aprovação real do serviço não atualizou a tela.");
        Capture("interface-authorization-approved-test.png"); SetOffering(false);
        foreach (var section in _sections.Keys)
        {
            SelectSection(section); PerformLayout(); await Task.Delay(35);
            foreach (var card in _sections[section].Body.Controls.OfType<ConnectionCard>()) card.CheckTextAndActions();
        }
        File.WriteAllText(Path.Combine(directory, "account-smoke.txt"), "PASS: quatro abas com texto e botões sem sobreposição, instalação/entrada controladas, criação orientada e cópia/colagem do convite com clipboard isolado, reconhecimento de PC existente, escolha explícita da rede, ponte encontrada e aprovação confirmada pelo serviço. Apenas dados de teste; nenhum site ou conta real foi utilizado.\nFontes incorporadas: " + FontAssets.BodyFamily.Name + " / " + FontAssets.DisplayFamily.Name + ".\n");
        SelectSection(ConnectionSection.Account);
        void Capture(string name) { using var bitmap = new Bitmap(Width, Height); DrawToBitmap(bitmap, ClientRectangle); bitmap.Save(Path.Combine(directory, name)); }
    }
}

internal sealed class PairingBanner(bool host) : DashboardSurface
{
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e); var g = e.Graphics; var scale = DeviceDpi / 96f;
        var state = g.Save(); g.ScaleTransform(scale, scale); var width = Width / scale;
        BrandAssets.Draw(g, new RectangleF(18, 13, 52, 52));
        DashboardTheme.Text(g, host ? "SUA PONTE EM CASCAVEL" : "SEU CONVITE PARA CONECTAR", new RectangleF(90, 8, width - 115, 31), 18, DashboardTheme.Ink, FontStyle.Bold);
        DashboardTheme.Text(g, host ? "Acre  →  túnel privado  →  este PC  →  jogo" : "PC no Acre  →  rede do seu amigo  →  ponte em Cascavel", new RectangleF(91, 40, width - 115, 25), 10.5f, DashboardTheme.Muted);
        g.Restore(state);
    }
}
