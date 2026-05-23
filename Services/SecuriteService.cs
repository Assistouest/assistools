using System;
using System.IO;
using Microsoft.Win32;

namespace Assistools.Services;

public static class SecuriteService
{
    private const string DefenderPath = @"C:\Program Files\Windows Defender\MpCmdRun.exe";

    public static void MiseAJourEtAnalyse(Action<string>? log = null)
    {
        log?.Invoke("--- Analyse de sécurité ---");

        if (DefenderEstActif() && File.Exists(DefenderPath))
        {
            log?.Invoke("Windows Defender actif — utilisation directe.");
            log?.Invoke("Mise à jour des définitions...");
            ProcessRunner.RunExe(DefenderPath, "-SignatureUpdate", log);

            log?.Invoke("Analyse rapide...");
            ProcessRunner.RunExe(DefenderPath, "-Scan -ScanType 1", log);
            log?.Invoke("--- Analyse terminée ---");
            return;
        }

        log?.Invoke("Windows Defender désactivé/absent — recherche d'un autre antivirus...");
        UtiliserAntivirusTiers(log);
    }

    /// <summary>
    /// Lit le statut de Defender via le registre (rapide, pas de PowerShell).
    /// </summary>
    private static bool DefenderEstActif()
    {
        try
        {
            // DisableRealtimeMonitoring = 1 → désactivé
            using var key = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows Defender\Real-Time Protection");
            if (key?.GetValue("DisableRealtimeMonitoring") is int v && v == 1)
                return false;
            return true;
        }
        catch { return true; }
    }

    private static void UtiliserAntivirusTiers(Action<string>? log)
    {
        // Cherche dans les chemins standards des AV connus
        var candidats = new (string Nom, string CheminUpdate, string ArgsUpdate, string CheminScan, string ArgsScan)[]
        {
            ("Avast",
                @"C:\Program Files\AVAST Software\Avast\ashUpd.exe",  "vps",
                @"C:\Program Files\AVAST Software\Avast\ashQuick.exe", @"/silent /action:delete C:\"),
            ("AVG",
                @"C:\Program Files\AVG\Antivirus\ashUpd.exe", "vps",
                @"C:\Program Files\AVG\Antivirus\ashQuick.exe", @"/silent /action:delete C:\"),
            ("Bitdefender",
                @"C:\Program Files\Bitdefender\Bitdefender Security\bdcore.exe", "/update",
                @"C:\Program Files\Bitdefender\Bitdefender Security\bdcore.exe", "/scan c:"),
        };

        foreach (var av in candidats)
        {
            if (File.Exists(av.CheminUpdate))
            {
                log?.Invoke($"{av.Nom} détecté — mise à jour des définitions...");
                ProcessRunner.RunExe(av.CheminUpdate, av.ArgsUpdate, log);

                if (File.Exists(av.CheminScan))
                {
                    log?.Invoke($"{av.Nom} — analyse rapide...");
                    ProcessRunner.RunExe(av.CheminScan, av.ArgsScan, log);
                }
                log?.Invoke("--- Analyse terminée ---");
                return;
            }
        }

        log?.Invoke("[INFO] Aucun antivirus pris en charge trouvé sur le système.");
    }
}
