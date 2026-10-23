namespace RotaGame.App;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        Velopack.VelopackApp.Build().SetAutoApplyOnStartup(false).Run();
        ApplicationConfiguration.Initialize();
        Application.SetDefaultFont(FontAssets.Ui(10));
        if (args.Length == 2 && args[0] == "--paths-smoke")
        {
            var directory = Path.GetFullPath(args[1]); Directory.CreateDirectory(directory); var routes = new PathSmokeRoutes(); var meter = new PathSmokeMeter();
            using var paths = new SimpleForm(routes, meter, new SmokeStore(), new RotaGame.Windows.GameLibrary(Path.Combine(directory, "games.json"), () => []));
            paths.Shown += async (_, _) =>
            {
                try { await paths.RunPathSmokeAsync(directory, routes, meter); }
                catch (Exception ex) { File.WriteAllText(Path.Combine(directory, "paths-error.txt"), ex.ToString()); Environment.ExitCode = 1; }
                finally { paths.Close(); }
            };
            Application.Run(paths); return;
        }
        if (args.Length == 2 && args[0] == "--devices-smoke")
        {
            var directory = Path.GetFullPath(args[1]); Directory.CreateDirectory(directory);
            using var devices = new SimpleForm(new SmokeRoutes(), new SmokeMeter(), new SmokeStore(), new RotaGame.Windows.GameLibrary(Path.Combine(directory, "games.json"), () => []), deviceStore: new RotaGame.Core.DeviceProfileStore(Path.Combine(directory, "device.json")));
            devices.Shown += async (_, _) =>
            {
                try { await devices.RunDeviceSmokeAsync(directory); }
                catch (Exception ex) { File.WriteAllText(Path.Combine(directory, "devices-error.txt"), ex.ToString()); Environment.ExitCode = 1; }
                finally { devices.Close(); }
            };
            Application.Run(devices); return;
        }
        if (args.Length == 2 && args[0] == "--update-integration-complete")
        {
            File.WriteAllText(Path.GetFullPath(args[1]), "PASS: reiniciado após atualização real; versão=" + Velopack.Locators.VelopackLocator.Current.CurrentlyInstalledVersion + "; assinatura=PANT_CORE; arquivo=" + Environment.ProcessPath);
            return;
        }
        if (args.Length == 3 && args[0] == "--update-integration")
        {
            var marker = Path.GetFullPath(args[2]);
            try
            {
                var manager = new Velopack.UpdateManager(new Velopack.Sources.SimpleFileSource(new DirectoryInfo(Path.GetFullPath(args[1]))), new Velopack.UpdateOptions { ExplicitChannel = "win" });
                if (!manager.IsInstalled || !manager.IsPortable) throw new InvalidOperationException("Este teste exige o pacote portátil completo em uma pasta isolada.");
                var flow = new UpdateWorkflow(new AppUpdates(manager, ["--update-integration-complete", marker]), () =>
                {
                    File.WriteAllText(marker + ".prepared", "Preparação confirmada antes de reiniciar; teste portátil sem rotas ou instalação no perfil do usuário."); return Task.FromResult(true);
                });
                if (!flow.CheckAsync(true, default).GetAwaiter().GetResult()) throw new InvalidOperationException(flow.Message);
            }
            catch (Exception ex) { File.WriteAllText(marker + ".error", ex.ToString()); Environment.ExitCode = 1; }
            return;
        }
        if (args.Length == 3 && args[0] == "--update-package-smoke")
        {
            try { UpdatesSmoke.PackageAsync(Path.GetFullPath(args[1]), Path.GetFullPath(args[2])).GetAwaiter().GetResult(); }
            catch (Exception ex) { Directory.CreateDirectory(args[2]); File.WriteAllText(Path.Combine(args[2], "updates-package-error.txt"), ex.ToString()); Environment.ExitCode = 1; }
            return;
        }
        if (args.Length == 2 && args[0] == "--update-smoke")
        {
            var directory = Path.GetFullPath(args[1]);
            using var updateSmoke = new SimpleForm(new SmokeRoutes(), new SmokeMeter(), new SmokeStore(), new RotaGame.Windows.GameLibrary(Path.Combine(directory, "updates-catalog.json"), () => []), new SmokeConnectionSession(), _ => { });
            updateSmoke.Shown += async (_, _) =>
            {
                try { await updateSmoke.RunUpdateSmokeAsync(directory); }
                catch (Exception ex) { Directory.CreateDirectory(directory); File.WriteAllText(Path.Combine(directory, "updates-error.txt"), ex.ToString()); Environment.ExitCode = 1; }
                finally { updateSmoke.Close(); }
            };
            Application.Run(updateSmoke); return;
        }
        if (args.Length == 2 && args[0] == "--diagram-smoke")
        {
            var directory = Path.GetFullPath(args[1]); Directory.CreateDirectory(directory);
            var diagramForm = new SimpleForm(new SmokeRoutes(), new SmokeMeter(), new SmokeStore(), new RotaGame.Windows.GameLibrary(Path.Combine(directory, "diagram-catalog.json"), () => []), new SmokeConnectionSession(), _ => { });
            diagramForm.Shown += async (_, _) =>
            {
                try { await diagramForm.RunDiagramSmokeAsync(directory); }
                catch (Exception ex) { File.WriteAllText(Path.Combine(directory, "diagram-error.txt"), ex.ToString()); Environment.ExitCode = 1; }
                finally { diagramForm.Close(); }
            };
            Application.Run(diagramForm); return;
        }
        if (args.Length == 2 && args[0] == "--intro-smoke")
        {
            var directory = Path.GetFullPath(args[1]); Directory.CreateDirectory(directory);
            var watch = System.Diagnostics.Stopwatch.StartNew();
            using var intro = new IntroForm(async ct =>
            {
                await Task.Delay(450, ct);
                return await BuildMainAsync(new RotaGame.Windows.GameLibrary(Path.Combine(directory, "intro-catalog.json"), () => []), ct, true);
            });
            try
            {
                var result = intro.ShowDialog(); var elapsed = watch.Elapsed;
                if (result != DialogResult.OK || intro.LoadedForm is null || elapsed.TotalSeconds is < 3 or > 5) throw new InvalidOperationException("Duração ou carregamento inicial incorreto: " + elapsed.TotalSeconds);
                intro.LoadedForm.Dispose(); IntroArtwork.ExportFrames(Path.Combine(directory, "intro-frames")); BrandAssets.Export(Path.Combine(directory, "branding"));
                File.WriteAllText(Path.Combine(directory, "intro-smoke.txt"), $"PASS: logo vetorial animada e painel preparado durante a introdução; duração real {elapsed.TotalSeconds:0.000} s (alvo 4 s); 50 quadros de prévia. Nenhuma conexão foi alterada.\n");
            }
            catch (Exception ex) { File.WriteAllText(Path.Combine(directory, "intro-error.txt"), ex.ToString()); Environment.ExitCode = 1; }
            return;
        }
        if (args.Length == 2 && args[0] == "--account-smoke")
        {
            var links = new List<string>();
            var account = new SimpleForm(new SmokeRoutes(), new SmokeMeter(), new SmokeStore(), new RotaGame.Windows.GameLibrary(Path.Combine(Path.GetFullPath(args[1]), "account-catalog.json"), () => []), new SmokeConnectionSession(), links.Add, inviteClipboard: new MemoryInviteClipboard());
            account.Shown += async (_, _) =>
            {
                try
                {
                    await account.RunAccountSmokeAsync(Path.GetFullPath(args[1]));
                    if (links.Count != 4 || links.Any(url => new Uri(url).Host is not ("login.tailscale.com" or "console.tailscale.com"))) throw new InvalidOperationException("Os links não seguiram o fluxo oficial: " + links.Count);
                }
                catch (Exception ex) { Directory.CreateDirectory(args[1]); File.WriteAllText(Path.Combine(args[1], "account-error.txt"), ex.ToString()); Environment.ExitCode = 1; }
                finally { account.Close(); }
            };
            Application.Run(account); return;
        }
        if (args.Length == 2 && args[0] == "--render-smoke")
        {
            var render = new SimpleForm(new SmokeRoutes(), new SmokeMeter(), new SmokeStore(), new RotaGame.Windows.GameLibrary(Path.Combine(Path.GetFullPath(args[1]), "render-catalog.json"), () => []), new SmokeConnectionSession(), _ => { });
            render.Shown += async (_, _) =>
            {
                try { await render.RunRenderingSmokeAsync(Path.GetFullPath(args[1])); }
                catch (Exception ex) { Directory.CreateDirectory(args[1]); File.WriteAllText(Path.Combine(args[1], "rendering-error.txt"), ex.ToString()); Environment.ExitCode = 1; }
                finally { render.Close(); }
            };
            Application.Run(render); return;
        }
        if (args.Length == 2 && args[0] == "--simple-smoke")
        {
            var smoke = new SimpleForm(new SmokeRoutes(), new SmokeMeter(), new SmokeStore(), new RotaGame.Windows.GameLibrary(Path.Combine(Path.GetFullPath(args[1]), "catalog-smoke.json"), () => []));
            smoke.Shown += async (_, _) =>
            {
                try { await smoke.RunSmokeAsync(Path.GetFullPath(args[1])); }
                catch (Exception ex) { Directory.CreateDirectory(args[1]); File.WriteAllText(Path.Combine(args[1], "simple-smoke-error.txt"), ex.ToString()); Environment.ExitCode = 1; }
                finally { smoke.Close(); }
            };
            Application.Run(smoke); return;
        }
        if (!args.Contains("--ui-smoke") && !args.Contains("--advanced"))
        {
            SimpleForm? start;
            var updates = new AppUpdates(); IntroForm? opening = null;
            using (opening = new IntroForm(async ct =>
            {
                var main = await LoadMainAsync(ct);
                var workflow = new UpdateWorkflow(updates, main.PrepareForUpdateAsync);
                workflow.Changed += (_, _) => opening?.SetStartupStatus(workflow.Message);
                if (await workflow.CheckAsync(true, ct)) { main.Dispose(); return null; }
                ct.ThrowIfCancellationRequested(); main.ConfigureUpdates(updates, workflow.Message); opening?.SetStartupStatus(null); return main;
            })) { opening.ShowDialog(); start = opening.LoadedForm; }
            if (start is not null) Application.Run(start);
            return;
        }
        var form = new MainForm();
        if (args.Length == 2 && args[0] == "--ui-smoke")
            form.Shown += async (_, _) =>
            {
                try { await form.RunSmokeAsync(Path.GetFullPath(args[1])); }
                catch (Exception ex)
                {
                    Directory.CreateDirectory(args[1]);
                    File.WriteAllText(Path.Combine(args[1], "ui-smoke-error.txt"), ex.ToString());
                    Environment.ExitCode = 1;
                }
                finally { form.Close(); }
            };
        Application.Run(form);
    }

    private static Task<SimpleForm> LoadMainAsync(CancellationToken ct)
    {
        var library = new RotaGame.Windows.GameLibrary(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RotaGame", "games.json"));
        return BuildMainAsync(library, ct);
    }

    private static async Task<SimpleForm> BuildMainAsync(RotaGame.Windows.GameLibrary library, CancellationToken ct, bool controlled = false)
    {
        var profiles = await Task.Run(library.Load, ct); ct.ThrowIfCancellationRequested();
        return controlled ? new SimpleForm(new SmokeRoutes(), new SmokeMeter(), new SmokeStore(), library, startupProfiles: profiles)
            : new SimpleForm(library: library, startupProfiles: profiles);
    }
}
