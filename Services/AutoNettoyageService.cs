using System;
using Microsoft.Win32;

namespace Assistools.Services;

public static class AutoNettoyageService
{
    private const string RegPath = @"Software\Policies\Microsoft\Windows\StorageSense";

    private static readonly (string Name, int Value)[] Entries =
    {
        ("AllowStorageSenseGlobal", 1),
        ("ConfigStorageSenseGlobalCadence", 1),
        ("ConfigStorageSenseRecycleBinCleanupThreshold", 14),
        ("ConfigStorageSenseDownloadsCleanupThreshold", 0),
        ("AllowStorageSenseTemporaryFilesCleanup", 1),
    };

    public static bool EstActive()
    {
        using var key = Registry.LocalMachine.OpenSubKey(RegPath);
        return key?.GetValue("AllowStorageSenseGlobal") is int v && v == 1;
    }

    public static void Configurer(Action<string>? log = null)
    {
        log?.Invoke("--- Activation Storage Sense ---");
        using var key = Registry.LocalMachine.CreateSubKey(RegPath, writable: true);
        foreach (var (name, value) in Entries)
        {
            key.SetValue(name, value, RegistryValueKind.DWord);
            log?.Invoke($"  SET {name} = {value}");
        }
        log?.Invoke("--- Storage Sense activé ---");
    }

    public static void Reinitialiser(Action<string>? log = null)
    {
        log?.Invoke("--- Désactivation Storage Sense ---");
        using var key = Registry.LocalMachine.OpenSubKey(RegPath, writable: true);
        if (key == null)
        {
            log?.Invoke("  (déjà désactivé)");
            return;
        }
        foreach (var (name, _) in Entries)
        {
            try { key.DeleteValue(name, throwOnMissingValue: false); log?.Invoke($"  DEL {name}"); }
            catch (Exception ex) { log?.Invoke($"  [ERR] {name} : {ex.Message}"); }
        }
        log?.Invoke("--- Storage Sense désactivé ---");
    }
}
