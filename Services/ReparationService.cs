using System;

namespace Assistools.Services;

public static class ReparationService
{
    public static void LancerReparationSysteme(Action<string>? log = null)
    {
        string[][] commandes =
        [
            ["DISM", "/Online", "/Cleanup-Image", "/CheckHealth"],
            ["DISM", "/Online", "/Cleanup-Image", "/ScanHealth"],
            ["DISM", "/Online", "/Cleanup-Image", "/RestoreHealth"],
            ["DISM", "/Online", "/Cleanup-Image", "/StartComponentCleanup"],
            ["sfc", "/scannow"],
        ];

        foreach (var cmd in commandes)
        {
            log?.Invoke($"--- {cmd[0]} {string.Join(" ", cmd[1..])} ---");
            ProcessRunner.RunExe(cmd[0], string.Join(" ", cmd[1..]), log);
        }
        log?.Invoke("--- Réparation terminée ---");
        MaintenanceStore.EnregistrerReparation(sfc: true, dism: true);
    }
}
