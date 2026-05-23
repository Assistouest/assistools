using System;
using System.Collections.Generic;
using Microsoft.Win32;

namespace Assistools.Services;

/// <summary>
/// Accumule les résultats des opérations de maintenance réalisées pendant la session.
/// Persisté partiellement en registre pour survivre aux redémarrages.
/// </summary>
public static class MaintenanceStore
{
    private const string RegPath = @"Software\Assistools\MaintenanceHistory";

    // ── Résultats de la session en cours ───────────────────────────────────
    public static DateTime? DernierNettoyageDate { get; private set; }
    public static long OctetsLiberes { get; private set; }
    public static int FichiersSupprimes { get; private set; }
    public static bool ReparationSfc { get; private set; }
    public static bool ReparationDism { get; private set; }
    public static DateTime? DerniereScanDate { get; private set; }
    public static string? ScanAntivirus { get; private set; }   // ex: "Aucune menace", "2 menaces supprimées"
    public static List<string> OperationsEffectuees { get; } = new();

    // ── Persistance registre ───────────────────────────────────────────────
    public static DateTime? DerniereMaintenance { get; private set; }

    static MaintenanceStore()
    {
        ChargerDepuisRegistre();
    }

    /// <summary>Appeler après un nettoyage disque terminé.</summary>
    public static void EnregistrerNettoyage(RapportNettoyage rapport)
    {
        DernierNettoyageDate = DateTime.Now;
        OctetsLiberes += rapport.TotalOctets;
        FichiersSupprimes += rapport.TotalFichiers;
        OperationsEffectuees.Add($"Nettoyage disque : {rapport.TotalFichiers} fichier(s), {NettoyageService.FormatTaille(rapport.TotalOctets)} libéré(s)");
        Persister();
    }

    /// <summary>Appeler après une réparation SFC/DISM.</summary>
    public static void EnregistrerReparation(bool sfc, bool dism)
    {
        ReparationSfc = sfc;
        ReparationDism = dism;
        if (sfc || dism)
            OperationsEffectuees.Add($"Réparation système : {(sfc ? "SFC " : "")}{(dism ? "DISM" : "")}".Trim());
        Persister();
    }

    /// <summary>Appeler après un scan antivirus Defender.</summary>
    public static void EnregistrerScanAntivirus(string resultat)
    {
        DerniereScanDate = DateTime.Now;
        ScanAntivirus = resultat;
        OperationsEffectuees.Add($"Scan antivirus : {resultat}");
        Persister();
    }

    /// <summary>Appeler après n'importe quelle opération de maintenance réussie.</summary>
    public static void AjouterOperation(string description)
    {
        if (!string.IsNullOrWhiteSpace(description))
            OperationsEffectuees.Add(description);
        Persister();
    }

    private static void Persister()
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RegPath, writable: true);
            key.SetValue("LastMaintenance", DateTime.Now.ToString("o"));
            if (OctetsLiberes > 0) key.SetValue("LastCleanBytes", OctetsLiberes.ToString());
            if (FichiersSupprimes > 0) key.SetValue("LastCleanFiles", FichiersSupprimes.ToString());
        }
        catch { }

        // Compatibilité HealthCheckService
        HealthCheckService.EnregistrerMaintenance();
        DerniereMaintenance = DateTime.Now;
    }

    private static void ChargerDepuisRegistre()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegPath);
            if (key == null) return;

            if (key.GetValue("LastMaintenance") is string raw && DateTime.TryParse(raw, out var d))
                DerniereMaintenance = d;
            if (key.GetValue("LastCleanBytes") is string ob && long.TryParse(ob, out var b))
                OctetsLiberes = b;
            if (key.GetValue("LastCleanFiles") is string of && int.TryParse(of, out var f))
                FichiersSupprimes = f;
        }
        catch { }
    }
}
