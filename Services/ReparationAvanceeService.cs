using System;
using System.IO;

namespace Assistools.Services;

/// <summary>
/// Réparations système avancées : reset Windows Update, réseau, WMI, CHKDSK.
/// </summary>
public static class ReparationAvanceeService
{
    /// <summary>
    /// Reset complet des composants Windows Update (utile quand les MAJ sont bloquées).
    /// Arrête les services → renomme SoftwareDistribution + catroot2 → redémarre.
    /// </summary>
    public static void ResetWindowsUpdate(Action<string>? log = null)
    {
        log?.Invoke("--- Reset Windows Update ---");

        var windir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var sd  = Path.Combine(windir, "SoftwareDistribution");
        var cr2 = Path.Combine(windir, "System32", "catroot2");

        string[] services    = ["wuauserv", "cryptsvc", "bits", "msiserver", "AppIDSvc"];
        string[] aRedemarrer = ["msiserver", "bits", "cryptsvc", "wuauserv"]; // AppIDSvc redémarre seul

        foreach (var svc in services)
            ArreterService(svc, log);

        // Renommer en .bak (sécurité, Windows recrée les dossiers)
        RenommerEnBak(sd, log);
        RenommerEnBak(cr2, log, optionnel: true); // catroot2 peut rester verrouillé — non bloquant

        foreach (var svc in aRedemarrer)
            DemarrerService(svc, log);

        log?.Invoke("--- Windows Update réinitialisé ---");
    }

    private static void ArreterService(string nom, Action<string>? log)
    {
        log?.Invoke($"Arrêt service {nom}…");
        // Stop-Service -Force gère les dépendants sans prompt interactif
        ProcessRunner.RunPowerShell(
            $"Stop-Service -Name '{nom}' -Force -ErrorAction SilentlyContinue; " +
            $"$s = Get-Service -Name '{nom}' -ErrorAction SilentlyContinue; " +
            $"if ($s.Status -eq 'Stopped') {{ Write-Output '  OK' }} else {{ Write-Output \"  Statut : $($s.Status)\" }}",
            log);
    }

    private static void DemarrerService(string nom, Action<string>? log)
    {
        log?.Invoke($"Démarrage service {nom}…");
        ProcessRunner.RunPowerShell(
            $"Start-Service -Name '{nom}' -ErrorAction SilentlyContinue; " +
            $"$s = Get-Service -Name '{nom}' -ErrorAction SilentlyContinue; " +
            $"if ($s.Status -eq 'Running') {{ Write-Output '  OK' }} else {{ Write-Output \"  Statut : $($s.Status)\" }}",
            log);
    }

    private static void RenommerEnBak(string chemin, Action<string>? log, bool optionnel = false)
    {
        if (!Directory.Exists(chemin)) { log?.Invoke($"  (absent : {chemin})"); return; }
        var bak = chemin + ".bak";
        try
        {
            if (Directory.Exists(bak))
            {
                log?.Invoke($"Suppression ancien backup {bak}…");
                Directory.Delete(bak, recursive: true);
            }
            Directory.Move(chemin, bak);
            log?.Invoke($"✓ {chemin} renomme en .bak");
        }
        catch (Exception ex)
        {
            if (optionnel)
                log?.Invoke($"  (ignoré : {Path.GetFileName(chemin)} verrouillé — {ex.Message})");
            else
                log?.Invoke($"[ERREUR] {chemin} : {ex.Message}");
        }
    }

    /// <summary>
    /// Reset complet du réseau : Winsock + TCP/IP + IPv6 + DNS cache + Firewall.
    /// Un redémarrage est requis ensuite.
    /// </summary>
    public static void ResetReseau(Action<string>? log = null)
    {
        log?.Invoke("--- Reset complet du réseau ---");

        log?.Invoke("Winsock reset…");
        ProcessRunner.RunExe("netsh", "winsock reset", log);

        log?.Invoke("TCP/IP reset…");
        ProcessRunner.RunExe("netsh", "int ip reset", log);

        log?.Invoke("IPv6 reset…");
        ProcessRunner.RunExe("netsh", "interface ipv6 reset", log);

        log?.Invoke("Libération IP…");
        ProcessRunner.RunExe("ipconfig", "/release", log);

        log?.Invoke("Renouvellement IP…");
        ProcessRunner.RunExe("ipconfig", "/renew", log);

        log?.Invoke("Vidage cache DNS…");
        ProcessRunner.RunExe("ipconfig", "/flushdns", log);

        log?.Invoke("Réinitialisation du pare-feu…");
        ProcessRunner.RunExe("netsh", "advfirewall reset", log);

        log?.Invoke("--- Réseau réinitialisé — REDÉMARRAGE RECOMMANDÉ ---");
    }

    /// <summary>
    /// Vérifie l'intégrité du référentiel WMI et le répare si nécessaire (salvage).
    /// </summary>
    public static void ReparerWmi(Action<string>? log = null)
    {
        log?.Invoke("--- Vérification du référentiel WMI ---");

        string verifyOutput = "";
        ProcessRunner.RunExe("winmgmt", "/verifyrepository", line =>
        {
            log?.Invoke(line);
            verifyOutput += line;
        });

        if (verifyOutput.Contains("consistent", StringComparison.OrdinalIgnoreCase) ||
            verifyOutput.Contains("cohérent", StringComparison.OrdinalIgnoreCase))
        {
            log?.Invoke("✓ Référentiel WMI cohérent — aucune action nécessaire.");
            return;
        }

        log?.Invoke("⚠ Référentiel WMI incohérent — lancement du salvage…");
        ProcessRunner.RunExe("winmgmt", "/salvagerepository", log);

        log?.Invoke("Re-vérification…");
        ProcessRunner.RunExe("winmgmt", "/verifyrepository", log);

        log?.Invoke("--- WMI réparé ---");
    }

    /// <summary>
    /// Répare le Microsoft Store en 3 phases progressives :
    /// 1. Vider le cache + wsreset.exe (rapide)
    /// 2. Reset-AppxPackage (thorough)
    /// 3. Re-enregistrer le manifeste AppX (nuclear)
    /// </summary>
    public static void ReparerMicrosoftStore(Action<string>? log = null)
    {
        log?.Invoke("--- Réparation du Microsoft Store ---");

        // Phase 1 : Vider le cache Store
        log?.Invoke("Phase 1 : Nettoyage du cache Store…");
        var cachePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            @"Packages\Microsoft.WindowsStore_8wekyb3d8bbwe\LocalCache");
        if (Directory.Exists(cachePath))
        {
            int deleted = 0, failed = 0;
            foreach (var f in Directory.GetFiles(cachePath, "*", SearchOption.AllDirectories))
            {
                try { File.Delete(f); deleted++; }
                catch { failed++; }
            }
            foreach (var d in Directory.GetDirectories(cachePath))
            {
                try { Directory.Delete(d, recursive: true); deleted++; }
                catch { failed++; }
            }
            log?.Invoke($"✓ Cache vidé ({deleted} élément(s) supprimé(s), {failed} verrouillé(s)).");
        }
        else
        {
            log?.Invoke("(cache absent — ignoré)");
        }

        // Phase 1 (suite) : wsreset.exe
        log?.Invoke("wsreset.exe en cours…");
        ProcessRunner.RunExe("wsreset.exe", "-i", log);
        log?.Invoke("✓ wsreset terminé.");

        // Phase 2 : Reset-AppxPackage
        log?.Invoke("Phase 2 : Reset-AppxPackage…");
        ProcessRunner.RunPowerShell(
            "Get-AppxPackage *WindowsStore* | Reset-AppxPackage",
            log);
        log?.Invoke("✓ Reset-AppxPackage terminé.");

        // Phase 3 : Re-enregistrement du manifeste
        log?.Invoke("Phase 3 : Re-enregistrement du package Store…");
        ProcessRunner.RunPowerShell(
            """
            $pkg = Get-AppxPackage -AllUsers Microsoft.WindowsStore -ErrorAction SilentlyContinue
            if ($pkg) {
                Add-AppxPackage -DisableDevelopmentMode -Register "$($pkg.InstallLocation)\AppXManifest.xml" -ErrorAction SilentlyContinue
                Write-Output "Re-enregistrement effectue."
            } else {
                Write-Output "Package Store introuvable - reinstallation depuis le store peut etre necessaire."
            }
            """,
            log);

        log?.Invoke("--- Microsoft Store réparé — relancez le Store pour vérifier ---");
    }

    /// <summary>
    /// Programme CHKDSK /f /r sur C: au prochain redémarrage.
    /// </summary>
    public static void ProgrammerChkdsk(Action<string>? log = null)
    {
        log?.Invoke("--- Programmation de CHKDSK au prochain démarrage ---");

        // Méthode fiable : echo Y | chkdsk /f /r pour accepter le prompt
        ProcessRunner.RunPowerShell(
            """
            $process = Start-Process -FilePath "cmd.exe" -ArgumentList "/c echo Y | chkdsk C: /f /r" -NoNewWindow -Wait -PassThru
            Write-Output "CHKDSK programme — un redemarrage est necessaire pour l'execution."
            Write-Output "Verifiez avec : chkntfs C:"
            """,
            log);

        ProcessRunner.RunExe("chkntfs", "C:", log);
        log?.Invoke("--- CHKDSK programmé — REDÉMARRAGE NÉCESSAIRE ---");
    }
}
