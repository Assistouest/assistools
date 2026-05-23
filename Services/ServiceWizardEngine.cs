using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Win32;

namespace Assistools.Services;

public enum ServiceStartType { Boot = 0, System = 1, Automatic = 2, Manual = 3, Disabled = 4, Unknown = 99 }

public sealed record ServiceState(string Name, bool Exists, ServiceStartType StartType);

public sealed class ServiceBackupEntry
{
    public string Name { get; set; } = "";
    public int StartType { get; set; }   // valeur Registry: 2=Auto, 3=Manual, 4=Disabled
}

public sealed class ServiceBackup
{
    public string CreatedAt { get; set; } = "";
    public List<ServiceBackupEntry> Services { get; set; } = new();
}

public static class ServiceWizardEngine
{
    private static readonly string BackupDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Assistools");

    private static readonly string BackupPath = Path.Combine(BackupDir, "service-wizard-backup.json");

    // Lit l'état d'un service via le registre — pas besoin d'admin pour lire.
    public static ServiceState GetState(string name)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Services\{name}");
            if (key == null) return new ServiceState(name, false, ServiceStartType.Unknown);

            int start = (key.GetValue("Start") as int?) ?? 99;
            var startType = Enum.IsDefined(typeof(ServiceStartType), start)
                ? (ServiceStartType)start
                : ServiceStartType.Unknown;

            return new ServiceState(name, true, startType);
        }
        catch
        {
            return new ServiceState(name, false, ServiceStartType.Unknown);
        }
    }

    // Une catégorie est "pertinente" si au moins un de ses services existe et n'est pas déjà désactivé.
    public static List<(WizardCategory Cat, ServiceState[] States)> GetRelevantCategories()
    {
        var result = new List<(WizardCategory, ServiceState[])>();
        foreach (var cat in ServiceWizardCatalog.All)
        {
            var states = cat.Services.Select(s => GetState(s.Name)).ToArray();
            bool relevant = states.Any(s => s.Exists && s.StartType != ServiceStartType.Disabled);
            if (relevant) result.Add((cat, states));
        }
        return result;
    }

    public static bool HasBackup() => File.Exists(BackupPath);

    public static ServiceBackup? LoadBackup()
    {
        try
        {
            if (!File.Exists(BackupPath)) return null;
            return JsonSerializer.Deserialize<ServiceBackup>(File.ReadAllText(BackupPath));
        }
        catch { return null; }
    }

    // ── Appliquer : capture état initial puis désactive ────────────────────────────
    public static void Apply(IEnumerable<string> serviceNames, Action<string>? log)
    {
        Directory.CreateDirectory(BackupDir);

        // Fusionner avec backup existant pour ne pas écraser l'état initial
        var existing = LoadBackup() ?? new ServiceBackup { CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm") };
        var known = existing.Services.Select(s => s.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        int done = 0, skipped = 0, errors = 0;

        foreach (var name in serviceNames.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var state = GetState(name);
            if (!state.Exists)
            {
                log?.Invoke($"  ⊘ {name} : absent du système, ignoré.");
                skipped++;
                continue;
            }
            if (state.StartType == ServiceStartType.Disabled)
            {
                log?.Invoke($"  ⊘ {name} : déjà désactivé, ignoré.");
                skipped++;
                continue;
            }

            if (!known.Contains(name))
            {
                existing.Services.Add(new ServiceBackupEntry
                {
                    Name = name,
                    StartType = (int)state.StartType,
                });
                known.Add(name);
            }

            try
            {
                // Arrêt si actuellement en route (sc.exe stop fait rien si déjà arrêté)
                ProcessRunner.RunExe("sc.exe", $"stop \"{name}\"", log: null);
                ProcessRunner.RunExe("sc.exe", $"config \"{name}\" start= disabled", log: null);
                log?.Invoke($"  ✓ {name} désactivé.");
                done++;
            }
            catch (Exception ex)
            {
                log?.Invoke($"  ✗ {name} : erreur — {ex.Message}");
                errors++;
            }
        }

        try
        {
            File.WriteAllText(BackupPath,
                JsonSerializer.Serialize(existing, new JsonSerializerOptions { WriteIndented = true }));
            log?.Invoke($"Sauvegarde enregistrée : {BackupPath}");
        }
        catch (Exception ex)
        {
            log?.Invoke($"⚠ Sauvegarde impossible : {ex.Message}");
        }

        log?.Invoke($"Résultat : {done} désactivé(s), {skipped} ignoré(s), {errors} erreur(s).");
    }

    // ── Restaurer depuis le backup ─────────────────────────────────────────────────
    public static void Restore(Action<string>? log)
    {
        var backup = LoadBackup();
        if (backup == null || backup.Services.Count == 0)
        {
            log?.Invoke("Aucune sauvegarde à restaurer.");
            return;
        }

        log?.Invoke($"Restauration de {backup.Services.Count} service(s) (sauvegarde du {backup.CreatedAt}).");

        int done = 0, errors = 0;

        foreach (var entry in backup.Services)
        {
            try
            {
                string startArg = entry.StartType switch
                {
                    2 => "auto",
                    3 => "demand",   // manual
                    4 => "disabled",
                    _ => "demand",
                };

                ProcessRunner.RunExe("sc.exe", $"config \"{entry.Name}\" start= {startArg}", log: null);

                // Si le service était en démarrage automatique, on tente de le redémarrer
                if (entry.StartType == 2)
                    ProcessRunner.RunExe("sc.exe", $"start \"{entry.Name}\"", log: null);

                log?.Invoke($"  ✓ {entry.Name} restauré ({startArg}).");
                done++;
            }
            catch (Exception ex)
            {
                log?.Invoke($"  ✗ {entry.Name} : {ex.Message}");
                errors++;
            }
        }

        try { File.Delete(BackupPath); } catch { }
        log?.Invoke($"Restauration terminée : {done} OK, {errors} erreur(s).");
    }
}
