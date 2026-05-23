using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Win32;

namespace Assistools.Services;

public record PointRestauration(int Sequence, string Description, DateTime Creation);

/// <summary>
/// Gestion des points de restauration système Windows.
/// Utilise PowerShell Checkpoint-Computer et Get-ComputerRestorePoint.
/// </summary>
public static class RestaurationService
{
    /// <summary>
    /// Active la création de points de restauration si désactivée
    /// (par défaut sur les éditions Windows 11 Famille).
    /// </summary>
    private static void EnsureSystemRestoreEnabled(Action<string>? log = null)
    {
        try
        {
            // Limite la fréquence de création à 0 minute (sinon Windows bloque toutes les 24h)
            using var key = Registry.LocalMachine.CreateSubKey(
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore", writable: true);
            key.SetValue("SystemRestorePointCreationFrequency", 0, RegistryValueKind.DWord);
            log?.Invoke("[INFO] Fréquence de restauration réglée à 0 minute.");
        }
        catch (Exception ex)
        {
            log?.Invoke($"[INFO] Impossible d'ajuster la fréquence : {ex.Message}");
        }

        // Active la protection système sur C: si elle ne l'est pas
        ProcessRunner.RunPowerShell(
            "Enable-ComputerRestore -Drive 'C:\\' -ErrorAction SilentlyContinue",
            log);
    }

    public static bool CreerPoint(string description, Action<string>? log = null)
    {
        log?.Invoke($"--- Création point de restauration : « {description} » ---");

        EnsureSystemRestoreEnabled(log);

        bool succes = false;
        var desc = description.Replace("\"", "").Replace("'", "");

        // Le script vérifie aussi explicitement que SystemRestore est actif sur C:
        // et force le contournement de la limite de fréquence de 24h via le registre
        // (SetValue est fait avant, mais on le ré-écrit aussi dans PowerShell par sécurité)
        var script = @"
try {
    # Forcer fréquence à 0 (double sécurité depuis PowerShell)
    Set-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore' `
        -Name 'SystemRestorePointCreationFrequency' -Value 0 -Type DWord -ErrorAction SilentlyContinue

    # Activer la protection sur C: si nécessaire
    Enable-ComputerRestore -Drive 'C:\' -ErrorAction SilentlyContinue

    # Créer le point de restauration
    Checkpoint-Computer -Description """ + desc + @""" -RestorePointType 'MODIFY_SETTINGS' -ErrorAction Stop
    Write-Output 'POINT_OK'
}
catch {
    Write-Output ""POINT_ERR: $_""
}
";

        ProcessRunner.RunPowerShell(script,
            line =>
            {
                log?.Invoke(line);
                if (line.Contains("POINT_OK"))
                {
                    succes = true;
                    log?.Invoke("✓ Point de restauration créé avec succès.");
                }
                else if (line.Contains("POINT_ERR"))
                {
                    log?.Invoke($"[AVERTISSEMENT] Impossible de créer le point de restauration : {line}");
                }
            });

        if (!succes)
            log?.Invoke("[AVERTISSEMENT] Le point de restauration n'a pas été confirmé. La maintenance continue quand même.");

        return succes;
    }

    public static List<PointRestauration> Lister(Action<string>? log = null)
    {
        var list = new List<PointRestauration>();
        var raw = new System.Text.StringBuilder();

        ProcessRunner.RunPowerShell(
            "Get-ComputerRestorePoint | ForEach-Object { \"$($_.SequenceNumber)|$($_.Description)|$($_.CreationTime.ToString('o'))\" }",
            line => raw.AppendLine(line));

        foreach (var line in raw.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Trim().Split('|');
            if (parts.Length >= 3 &&
                int.TryParse(parts[0], out var seq) &&
                DateTime.TryParse(parts[2], out var date))
            {
                list.Add(new PointRestauration(seq, parts[1], date));
            }
        }

        log?.Invoke($"{list.Count} point(s) de restauration trouvé(s).");
        return list;
    }

    /// <summary>
    /// Supprime les vieux points pour ne garder que les <paramref name="nbAGarder"/> plus récents.
    /// </summary>
    public static int NettoyerVieuxPoints(int nbAGarder, Action<string>? log = null)
    {
        var points = Lister(log);
        if (points.Count <= nbAGarder)
        {
            log?.Invoke($"Aucun nettoyage nécessaire ({points.Count} ≤ {nbAGarder}).");
            return 0;
        }

        // vssadmin ne permet pas de supprimer un point spécifique facilement,
        // mais on peut supprimer le plus ancien plusieurs fois
        int aSupprimer = points.Count - nbAGarder;
        log?.Invoke($"Suppression des {aSupprimer} plus ancien(s) point(s) (garde les {nbAGarder} récents)…");

        int supprimes = 0;
        for (int i = 0; i < aSupprimer; i++)
        {
            using var p = new Process();
            p.StartInfo = new ProcessStartInfo
            {
                FileName = "vssadmin.exe",
                Arguments = "delete shadows /for=C: /oldest /quiet",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
            };
            try
            {
                p.Start();
                p.WaitForExit();
                if (p.ExitCode == 0) supprimes++;
            }
            catch (Exception ex)
            {
                log?.Invoke($"[ERREUR] {ex.Message}");
                break;
            }
        }

        log?.Invoke($"✓ {supprimes} point(s) supprimé(s).");
        return supprimes;
    }
}
