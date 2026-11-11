using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using QuickText.App.Interop;

namespace QuickText.App.Ui;

/// <summary>
/// File/folder pickers that run the common dialog in a <b>separate process</b>.
/// In-process Open/Save (WPF or WinForms, even with AutoUpgradeEnabled=false) still loads
/// Explorer shell extensions into QuickText — WER BEX64 / 0xc0000409 with NutstoreShell.dll
/// (and friends) in the module list, the moment the picker opens. Hosting the dialog in
/// powershell.exe keeps a shellext crash from taking the tray app down with it.
/// Hooks are still suspended while we wait so keystrokes in the picker don't fire expansions.
/// </summary>
internal static class ShellFileDialogs
{
    // Window owner is kept on the API so call sites stay unchanged; cross-process dialogs
    // cannot be true owned windows, so we only AllowSetForegroundWindow the helper.
    public static string? OpenFile(Window owner, string filter)
    {
        _ = owner;
        return RunOutOfProcess("open", filter, fileName: null);
    }

    public static string? SaveFile(Window owner, string filter, string fileName)
    {
        _ = owner;
        return RunOutOfProcess("save", filter, fileName);
    }

    public static string? OpenFolder(Window owner)
    {
        _ = owner;
        return RunOutOfProcess("folder", filter: null, fileName: null);
    }

    private static string? RunOutOfProcess(string mode, string? filter, string? fileName)
    {
        return App.WithShellDialog(() =>
        {
            var id = Guid.NewGuid().ToString("N");
            var outFile = Path.Combine(Path.GetTempPath(), "QuickText-pick-" + id + ".txt");
            var script = Path.Combine(Path.GetTempPath(), "QuickText-pick-" + id + ".ps1");
            try
            {
                File.WriteAllText(script, BuildScript(mode, filter, fileName, outFile), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-NoProfile -STA -ExecutionPolicy Bypass -File \"" + script + "\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                using var p = Process.Start(psi);
                if (p == null) return null;
                // Let the picker take foreground (otherwise it can open behind the manager).
                try { NativeMethods.AllowSetForegroundWindow((uint)p.Id); } catch { }
                p.WaitForExit();
                if (!File.Exists(outFile)) return null;
                var path = File.ReadAllText(outFile).Trim();
                return path.Length > 0 ? path : null;
            }
            catch (Exception ex)
            {
                Core.Log.Error("shell file dialog", ex);
                return null;
            }
            finally
            {
                try { File.Delete(script); } catch { }
                try { File.Delete(outFile); } catch { }
            }
        });
    }

    /// <summary>Build a tiny STA script. Values are single-quote-escaped so a filter cannot break out.</summary>
    private static string BuildScript(string mode, string? filter, string? fileName, string outFile)
    {
        var outLit = PsLiteral(outFile);
        return mode switch
        {
            "open" => $$"""
                Add-Type -AssemblyName System.Windows.Forms
                $d = New-Object System.Windows.Forms.OpenFileDialog
                $d.Filter = {{PsLiteral(filter ?? "All|*.*")}}
                $d.AutoUpgradeEnabled = $false
                $d.Multiselect = $false
                $d.CheckFileExists = $true
                if ($d.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) {
                  [System.IO.File]::WriteAllText({{outLit}}, $d.FileName)
                }
                """,
            "save" => $$"""
                Add-Type -AssemblyName System.Windows.Forms
                $d = New-Object System.Windows.Forms.SaveFileDialog
                $d.Filter = {{PsLiteral(filter ?? "All|*.*")}}
                $d.FileName = {{PsLiteral(fileName ?? "")}}
                $d.AutoUpgradeEnabled = $false
                $d.OverwritePrompt = $true
                if ($d.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) {
                  [System.IO.File]::WriteAllText({{outLit}}, $d.FileName)
                }
                """,
            "folder" => $$"""
                Add-Type -AssemblyName System.Windows.Forms
                $d = New-Object System.Windows.Forms.FolderBrowserDialog
                $d.ShowNewFolderButton = $true
                if ($d.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) {
                  [System.IO.File]::WriteAllText({{outLit}}, $d.SelectedPath)
                }
                """,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
        };
    }

    private static string PsLiteral(string value) =>
        "'" + (value ?? "").Replace("'", "''", StringComparison.Ordinal) + "'";
}
