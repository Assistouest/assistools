using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Assistools.Services;

public static class ProcessRunner
{
    // cmd /u redirige stdout/stderr en UTF-16 LE — évite les mojibake avec SFC, DISM, etc.
    private static readonly Encoding CmdUnicodeEncoding = Encoding.Unicode; // UTF-16 LE

    public static void RunPowerShell(string script, Action<string>? log = null)
    {
        // Écrire le script dans un fichier temporaire .ps1 — plus fiable que stdin
        var tmp = Path.Combine(Path.GetTempPath(), $"assistools_{Guid.NewGuid():N}.ps1");
        try
        {
            File.WriteAllText(tmp, script, new UTF8Encoding(true)); // BOM requis pour PowerShell 5.1
            Run("powershell.exe",
                $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"{tmp}\"",
                null, log, Encoding.UTF8);
        }
        finally
        {
            try { File.Delete(tmp); } catch { }
        }
    }

    public static void RunExe(string exe, string args, Action<string>? log = null)
    {
        // On passe par "cmd /u /c exe args" : /u force la sortie en UTF-16 LE,
        // ce qui règle les mojibake de SFC, DISM et autres outils système.
        Run("cmd.exe", $"/u /c \"{exe}\" {args}", null, log, CmdUnicodeEncoding);
    }

    private static void Run(string exe, string args, string? stdin, Action<string>? log, Encoding encoding)
    {
        using var p = new Process();
        p.StartInfo = new ProcessStartInfo
        {
            FileName = exe,
            Arguments = args,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = log != null,
            RedirectStandardError  = log != null,
            RedirectStandardInput  = stdin != null,
            StandardOutputEncoding = log != null ? encoding : null,
            StandardErrorEncoding  = log != null ? encoding : null,
        };

        if (log != null)
        {
            p.OutputDataReceived += (_, e) => { if (e.Data != null) log(e.Data); };
            p.ErrorDataReceived  += (_, e) => { if (e.Data != null) log("[ERR] " + e.Data); };
        }

        p.Start();

        if (stdin != null)
        {
            p.StandardInput.Write(stdin);
            p.StandardInput.Close();
        }

        if (log != null)
        {
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
        }

        p.WaitForExit();
    }
}
