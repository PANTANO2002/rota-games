using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using RotaGame.Windows;

namespace RotaGame.App;

internal sealed partial class SimpleForm
{
    // Capture the pixels actually presented by Windows. DrawToBitmap alone repaints
    // the controls and cannot expose stale pixels left behind by a resize.
    internal async Task RunRenderingSmokeAsync(string directory)
    {
        Directory.CreateDirectory(directory); _gameWatcher.Stop(); _tips.Active = false; TopMost = true;
        var log = new List<string>();
        try
        {
            WindowState = FormWindowState.Normal; Show(); Activate();
            var area = Screen.FromControl(this).WorkingArea; Location = area.Location;
            foreach (var scenario in new[] { "empty-resize", "wrapped-resize", "scroll-resize", "account-resize", "create-invite-resize", "invite-resize", "authorization-resize" })
            {
                ShowDashboard();
                ApplyLibrary(scenario == "empty-resize" ? [] : Enumerable.Range(1, scenario == "wrapped-resize" ? 4 : 9)
                    .Select(i => new GameProfile("render-" + i, "Validação de layout " + i, ["NotAGame"], Environment.ProcessPath)).ToArray());
                if (scenario is "account-resize" or "create-invite-resize" or "invite-resize" or "authorization-resize") ShowConnectionPage(scenario == "create-invite-resize" ? ConnectionSection.InviteFriend : scenario == "invite-resize" ? ConnectionSection.AcceptInvite : scenario == "authorization-resize" ? ConnectionSection.AuthorizeBridge : ConnectionSection.Account);
                for (var i = 0; i < 9; i++)
                {
                    ClientSize = (i % 3) switch { 0 => new Size(1080, 720), 1 => new Size(1340, 800), _ => new Size(Math.Min(1600, area.Width), Math.Min(860, area.Height)) };
                    await Task.Delay(45);
                }
                WindowState = FormWindowState.Maximized; await Task.Delay(120); WindowState = FormWindowState.Normal;
                ClientSize = new Size(Math.Min(1600, area.Width), Math.Min(860, area.Height)); await Task.Delay(150);
                if (scenario == "scroll-resize") { _pageScroll.ScrollPosition = 150; await Task.Delay(80); _pageScroll.ScrollPosition = 0; await Task.Delay(100); }
                using var presented = CapturePresented(); presented.Save(Path.Combine(directory, scenario + "-presented.png"));
                Invalidate(true); Refresh(); await Task.Delay(120);
                using var refreshed = CapturePresented(); refreshed.Save(Path.Combine(directory, scenario + "-refreshed.png"));
                var difference = PixelDifference(presented, refreshed);
                log.Add($"{scenario}: {difference:P4} presented pixels differed after forced repaint; {presented.Width}x{presented.Height}");
                File.WriteAllLines(Path.Combine(directory, "rendering-smoke.txt"), log);
                if (difference > .003) throw new InvalidOperationException("Restaram pixels antigos após redimensionar: " + log[^1]);
            }
            var chrome = Descendants(this).OfType<DashboardWindowButton>().ToArray();
            chrome.Single(b => b.Action == DashboardWindowAction.Minimize).PerformClick(); await Task.Delay(80);
            if (WindowState != FormWindowState.Minimized) throw new InvalidOperationException("O botão de minimizar não minimizou.");
            WindowState = FormWindowState.Normal; await Task.Delay(80);
            var maximize = chrome.Single(b => b.Action == DashboardWindowAction.Maximize);
            maximize.PerformClick(); if (WindowState != FormWindowState.Maximized) throw new InvalidOperationException("O botão de maximizar falhou.");
            maximize.PerformClick(); if (WindowState != FormWindowState.Normal) throw new InvalidOperationException("O botão de restaurar falhou.");
            log.Add("PASS: native presented pixels agree with a full repaint after 63 size changes, seven maximize/restore cycles, four account tabs and scrolling; real minimize/maximize/restore buttons work. Tooltips disabled only during pixel comparison. No network changed.");
            File.WriteAllLines(Path.Combine(directory, "rendering-smoke.txt"), log);
        }
        finally { TopMost = false; }
    }

    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls) { yield return child; foreach (var nested in Descendants(child)) yield return nested; }
    }

    private Bitmap CapturePresented()
    {
        if (WindowState != FormWindowState.Normal || ClientSize.Width < 1080) throw new InvalidOperationException("A janela não está disponível para captura.");
        var bitmap = new Bitmap(ClientSize.Width, ClientSize.Height, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap); graphics.CopyFromScreen(PointToScreen(Point.Empty), Point.Empty, ClientSize, CopyPixelOperation.SourceCopy);
        return bitmap;
    }

    private static double PixelDifference(Bitmap a, Bitmap b)
    {
        if (a.Size != b.Size) return 1;
        var bounds = new Rectangle(Point.Empty, a.Size);
        var aData = a.LockBits(bounds, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        var bData = b.LockBits(bounds, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var first = new byte[aData.Stride * a.Height]; var second = new byte[bData.Stride * b.Height];
            Marshal.Copy(aData.Scan0, first, 0, first.Length); Marshal.Copy(bData.Scan0, second, 0, second.Length); var different = 0;
            for (var i = 0; i < first.Length; i += 4)
                if (Math.Abs(first[i] - second[i]) > 8 || Math.Abs(first[i + 1] - second[i + 1]) > 8 || Math.Abs(first[i + 2] - second[i + 2]) > 8) different++;
            return different / (double)(a.Width * a.Height);
        }
        finally { a.UnlockBits(aData); b.UnlockBits(bData); }
    }
}

internal sealed class SmokeConnectionSession : IConnectionSession
{
    public bool Installed { get; private set; }
    private bool _connected;
    public bool Approved { get; set; }
    public int NetworkChoices { get; private set; }
    public Task<RotaGame.Windows.TailStatus?> ReadAsync(CancellationToken ct) => Task.FromResult<RotaGame.Windows.TailStatus?>(!Installed ? null :
        new(_connected ? "Running" : "NeedsLogin", "", true, "Validação local (teste)", Approved, _connected ? [new("100.64.0.2", "PC do amigo (teste)", true, true, "direct")] : []));
    public Task InstallAsync(IProgress<string> progress, CancellationToken ct) { Installed = true; progress.Report("Instalação controlada para validação da interface."); return Task.CompletedTask; }
    public async Task LoginAsync(IProgress<string> progress, IProgress<string> openUrl, CancellationToken ct, bool chooseNetwork = false)
    { if (chooseNetwork) NetworkChoices++; openUrl.Report("https://login.tailscale.com/a/test-only"); await Task.Delay(40, ct); _connected = true; progress.Report("Teste da interface · nenhuma conta real foi conectada."); }
}
