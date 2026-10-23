using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Windows.Forms;

// Small .NET Framework bootstrap: Windows 11 includes this runtime.
// The manifest asks Windows for consent; no UAC setting is changed.
internal static class Bootstrap
{
    [STAThread]
    private static int Main(string[] args)
    {
        Application.EnableVisualStyles();
        string temporary = null;
        try
        {
            temporary = Path.Combine(Path.GetTempPath(), "RotaGame-setup-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporary);
            var setup = Path.Combine(temporary, "RotaGame-Setup-core.exe");
            using (var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("RotaGame.Setup"))
            using (var destination = new FileStream(setup, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                if (resource == null) throw new IOException("Instalador ausente.");
                resource.CopyTo(destination);
            }
            string actual;
            using (var sha = SHA256.Create())
            using (var payload = File.OpenRead(setup)) actual = BitConverter.ToString(sha.ComputeHash(payload)).Replace("-", "");
            if (!String.Equals(actual, PackageInfo.Sha256, StringComparison.OrdinalIgnoreCase)) throw new IOException("Falha na integridade do instalador.");
            // A test can verify the entire embedded payload without installing.
            if (args.Length == 2 && args[0] == "--verify-payload")
            {
                File.WriteAllText(Path.GetFullPath(args[1]), "PASS: instalador incorporado íntegro; PANT_CORE; " + PackageInfo.Version + "; SHA256=" + actual);
                return 0;
            }
            using (var child = Process.Start(new ProcessStartInfo(setup) { UseShellExecute = false }))
            {
                if (child == null) throw new IOException("Não foi possível iniciar a instalação.");
                child.WaitForExit(); return child.ExitCode;
            }
        }
        catch (Exception ex) { MessageBox.Show("Não foi possível instalar o RotaGame.\n" + ex.Message, "RotaGame · PANT_CORE", MessageBoxButtons.OK, MessageBoxIcon.Error); return 1; }
        finally
        {
            // Only the exact, freshly created temporary directory is removed.
            if (temporary != null) try { Directory.Delete(temporary, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
}
