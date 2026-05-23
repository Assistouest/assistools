using System;
using System.IO;
using Microsoft.Win32;

namespace Assistools.Services;

/// <summary>
/// Export de la configuration du PC (à archiver avant un gros chantier de réparation).
/// </summary>
public static class ExportService
{
    /// <summary>
    /// Exporte la liste des programmes installés (registre Uninstall) en CSV.
    /// Plus rapide et plus complet que Get-WmiObject Win32_Product.
    /// </summary>
    public static void ExporterProgrammes(string cheminCsv, Action<string>? log = null)
    {
        log?.Invoke($"--- Export des programmes installés vers {cheminCsv} ---");

        using var writer = new StreamWriter(cheminCsv, false, new System.Text.UTF8Encoding(true));
        writer.WriteLine("Nom;Version;Editeur;DateInstallation");

        int count = 0;
        string[] subKeys =
        {
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall",
        };

        foreach (var path in subKeys)
        {
            using var root = Registry.LocalMachine.OpenSubKey(path);
            if (root == null) continue;

            foreach (var name in root.GetSubKeyNames())
            {
                using var sub = root.OpenSubKey(name);
                if (sub == null) continue;

                var displayName = sub.GetValue("DisplayName") as string;
                if (string.IsNullOrWhiteSpace(displayName)) continue;
                if (sub.GetValue("SystemComponent") is int sc && sc == 1) continue;

                var version = sub.GetValue("DisplayVersion") as string ?? "";
                var publisher = sub.GetValue("Publisher") as string ?? "";
                var installDate = sub.GetValue("InstallDate") as string ?? "";

                writer.WriteLine($"{Csv(displayName)};{Csv(version)};{Csv(publisher)};{Csv(installDate)}");
                count++;
            }
        }

        log?.Invoke($"✓ {count} programme(s) exporté(s).");
    }

    /// <summary>
    /// Exporte tous les pilotes tiers du système (DISM /export-drivers).
    /// </summary>
    public static void ExporterDrivers(string dossierDest, Action<string>? log = null)
    {
        log?.Invoke($"--- Export des pilotes vers {dossierDest} ---");
        Directory.CreateDirectory(dossierDest);
        ProcessRunner.RunExe("dism", $"/online /export-driver /destination:\"{dossierDest}\"", log);
        log?.Invoke("--- Export pilotes terminé ---");
    }

    private static string Csv(string s) => s.Replace(';', ',').Replace("\r", "").Replace("\n", " ");
}
