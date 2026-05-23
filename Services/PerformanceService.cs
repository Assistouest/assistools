using System;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Assistools.Services;

public static class PerformanceService
{
    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    private const string MemMgmtKey = @"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management";

    public enum ModePower { Economie, Equilibre, Performance }

    public static ModePower GetModePowerActuel()
    {
        var active = PowerPlanApi.GetActive();

        if (active == PowerPlanApi.PowerSaver) return ModePower.Economie;
        if (active == PowerPlanApi.Balanced)   return ModePower.Equilibre;
        if (active == PowerPlanApi.HighPerformance ||
            active == PowerPlanApi.UltimatePerf) return ModePower.Performance;

        // Plan custom : on vérifie son nom pour décider
        var name = PowerPlanApi.GetFriendlyName(active).ToLowerInvariant();
        if (name.Contains("performance") || name.Contains("élevé") ||
            name.Contains("eleve") || name.Contains("high"))
            return ModePower.Performance;
        if (name.Contains("économie") || name.Contains("economie") ||
            name.Contains("saver") || name.Contains("battery"))
            return ModePower.Economie;
        return ModePower.Equilibre;
    }

    public static void SetModePower(ModePower mode, Action<string>? log = null)
    {
        var (kind, label) = mode switch
        {
            ModePower.Economie    => (PowerPlanApi.SchemeKind.PowerSaver,      "Économie d'énergie"),
            ModePower.Equilibre   => (PowerPlanApi.SchemeKind.Balanced,        "Équilibré"),
            ModePower.Performance => (PowerPlanApi.SchemeKind.HighPerformance, "Haute performance"),
            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        };

        log?.Invoke($"--- Activation : {label} ---");

        var target = PowerPlanApi.GetOrCreateScheme(kind);
        if (target == Guid.Empty)
        {
            log?.Invoke("[ERR] Plan introuvable et impossible à créer.");
            return;
        }

        if (PowerPlanApi.SetActive(target))
        {
            var friendly = PowerPlanApi.GetFriendlyName(target);
            log?.Invoke($"OK — {friendly} ({target})");
        }
        else
        {
            log?.Invoke($"[ERR] Activation impossible ({target}).");
        }
    }

    public static void OptimiserDisques(Action<string>? log = null)
    {
        log?.Invoke("--- Optimisation des disques (defrag /o) ---");

        // Active TRIM au niveau système
        ProcessRunner.RunExe("fsutil", "behavior set DisableDeleteNotify 0", log);

        // defrag /c = tous les volumes, /o = optimisation auto SSD/HDD, /h = priorité normale, /u = progression, /v = verbose
        log?.Invoke("Lancement : defrag /c /o /h /u /v");
        ProcessRunner.RunExe("defrag", "/c /o /h /u /v", log);

        log?.Invoke("--- Optimisation terminée ---");
    }

    public static void AjusterMemVirtuelle(Action<string>? log = null)
    {
        log?.Invoke("--- Ajustement de la mémoire virtuelle ---");

        var mem = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX)) };
        if (!GlobalMemoryStatusEx(ref mem))
        {
            log?.Invoke("[ERREUR] Lecture de la RAM impossible.");
            return;
        }

        long ramMb   = (long)(mem.ullTotalPhys / (1024UL * 1024UL));
        long initial = (long)(ramMb * 2.5);
        long max     = (long)(ramMb * 3.0);
        log?.Invoke($"RAM détectée : {ramMb} Mo → pagefile {initial} → {max} Mo");

        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(MemMgmtKey, writable: true)
                          ?? throw new InvalidOperationException("Clé registre introuvable");

            key.SetValue("PagingFiles",
                new[] { $@"C:\pagefile.sys {initial} {max}" },
                RegistryValueKind.MultiString);
            key.SetValue("ClearPageFileAtShutdown", 1, RegistryValueKind.DWord);

            log?.Invoke("✓ PagingFiles écrit dans le registre.");
            log?.Invoke("✓ ClearPageFileAtShutdown activé.");
            log?.Invoke("ℹ Un redémarrage est nécessaire pour appliquer les changements.");
        }
        catch (Exception ex)
        {
            log?.Invoke($"[ERREUR] {ex.Message}");
        }
    }

    public static bool EstMemVirtuelleOptimisee()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(MemMgmtKey);
            if (key?.GetValue("PagingFiles") is string[] values && values.Length > 0)
            {
                var v = values[0].Trim();
                // Si la valeur contient des chiffres de taille (ex: "C:\pagefile.sys 4096 8192"), c'est optimisé
                // La valeur Windows par défaut est vide ou "?:\pagefile.sys"
                if (v.StartsWith("?:") || string.IsNullOrWhiteSpace(v)) return false;
                var parts = v.Split(' ');
                return parts.Length >= 3 && int.TryParse(parts[1], out _);
            }
            return false;
        }
        catch { return false; }
    }

    public static void MemVirtuelleParDefaut(Action<string>? log = null)
    {
        log?.Invoke("--- Réinitialisation mémoire virtuelle ---");
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(MemMgmtKey, writable: true)
                          ?? throw new InvalidOperationException("Clé registre introuvable");

            // Valeur vide = gestion automatique par Windows
            key.SetValue("PagingFiles", new[] { "?:\\pagefile.sys" }, RegistryValueKind.MultiString);
            key.SetValue("ClearPageFileAtShutdown", 0, RegistryValueKind.DWord);

            log?.Invoke("✓ Gestion automatique du pagefile restaurée.");
            log?.Invoke("ℹ Un redémarrage est nécessaire pour appliquer les changements.");
        }
        catch (Exception ex)
        {
            log?.Invoke($"[ERREUR] {ex.Message}");
        }
    }
}
