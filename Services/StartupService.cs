using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace Assistools.Services;

public static class StartupService
{
    // ── PROGRAMMES AU DÉMARRAGE ──────────────────────────────────────────────

    public record StartupItem(
        string Name,
        string Command,
        string Location,
        bool IsEnabled
    );

    private static readonly (RegistryKey Hive, string Path, string Label)[] StartupKeys =
    [
        (Registry.CurrentUser,  @"Software\Microsoft\Windows\CurrentVersion\Run",     "HKCU\\Run"),
        (Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run",     "HKLM\\Run"),
        (Registry.CurrentUser,  @"Software\Microsoft\Windows\CurrentVersion\RunOnce", "HKCU\\RunOnce"),
        (Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce", "HKLM\\RunOnce"),
    ];

    private const string ApprovedRunHkcu = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
    private const string ApprovedRunHklm = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";

    public static List<StartupItem> ObtenirDemarrage()
    {
        var items = new List<StartupItem>();
        foreach (var (hive, path, label) in StartupKeys)
        {
            try
            {
                using var key = hive.OpenSubKey(path);
                if (key == null) continue;
                foreach (var name in key.GetValueNames())
                {
                    var cmd = key.GetValue(name)?.ToString() ?? "";
                    bool enabled = EstActif(hive, name);
                    items.Add(new StartupItem(name, cmd, label, enabled));
                }
            }
            catch { }
        }
        return items;
    }

    private static bool EstActif(RegistryKey hive, string name)
    {
        try
        {
            var approvedPath = hive == Registry.CurrentUser ? ApprovedRunHkcu : ApprovedRunHklm;
            using var key = hive.OpenSubKey(approvedPath);
            if (key?.GetValue(name) is byte[] data && data.Length >= 4)
                return data[0] != 0x02;
        }
        catch { }
        return true;
    }

    public static void DefinirDemarrage(StartupItem item, bool activer)
    {
        try
        {
            var hive = item.Location.StartsWith("HKCU") ? Registry.CurrentUser : Registry.LocalMachine;
            var approvedPath = hive == Registry.CurrentUser ? ApprovedRunHkcu : ApprovedRunHklm;
            using var key = hive.CreateSubKey(approvedPath, writable: true);
            if (key == null) return;
            var data = new byte[12];
            if (!activer) data[0] = 0x02;
            key.SetValue(item.Name, data, RegistryValueKind.Binary);
        }
        catch { }
    }

    // ── SERVICES TIERS ───────────────────────────────────────────────────────

    public record ServiceItem(
        string Name,
        string DisplayName,
        string StartMode,   // Auto, Manual, Disabled
        string State,       // Running, Stopped
        string PathName,
        bool IsWindows
    );

    public static List<ServiceItem> ObtenirServices()
    {
        var items = new List<ServiceItem>();
        try
        {
            // Lire la liste via sc.exe query type= all
            var scriptOutput = RunPowerShellCapture(@"
Get-WmiObject Win32_Service | Select-Object Name, DisplayName, StartMode, State, PathName |
    ForEach-Object { ""$($_.Name)`t$($_.DisplayName)`t$($_.StartMode)`t$($_.State)`t$($_.PathName)"" }
");
            foreach (var line in scriptOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = line.Split('\t');
                if (parts.Length < 5) continue;
                var name        = parts[0].Trim();
                var displayName = parts[1].Trim();
                var startMode   = parts[2].Trim();
                var state       = parts[3].Trim();
                var path        = parts[4].Trim();
                bool isWindows  = EstServiceWindows(path);
                items.Add(new ServiceItem(name, displayName, startMode, state, path, isWindows));
            }
        }
        catch { }

        items.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase));
        return items;
    }

    private static bool EstServiceWindows(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return true;
        var p = path.TrimStart('"').ToLowerInvariant();
        return p.Contains(@"\windows\system32\") ||
               p.Contains(@"\windows\syswow64\") ||
               p.Contains(@"\windows\servicing\") ||
               p.Contains(@"\windows\winsxs\") ||
               p.Contains(@"\microsoft\windows defender\");
    }

    public static void DefinirService(ServiceItem service, string startMode)
    {
        // startMode : "Auto" | "Manual" | "Disabled"
        var scMode = startMode switch
        {
            "Auto"     => "auto",
            "Manual"   => "demand",
            "Disabled" => "disabled",
            _          => "demand",
        };

        RunProcess("sc.exe", $"config \"{service.Name}\" start= {scMode}");

        if (startMode == "Disabled" && service.State == "Running")
            RunProcess("sc.exe", $"stop \"{service.Name}\"");
        else if (startMode == "Auto" && service.State != "Running")
            RunProcess("sc.exe", $"start \"{service.Name}\"");
    }

    // ── HELPERS ──────────────────────────────────────────────────────────────

    private static string RunPowerShellCapture(string script)
    {
        var tmp = Path.GetTempFileName() + ".ps1";
        File.WriteAllText(tmp, script, new System.Text.UTF8Encoding(true));
        try
        {
            var psi = new ProcessStartInfo("powershell.exe",
                $"-NonInteractive -NoProfile -ExecutionPolicy Bypass -File \"{tmp}\"")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var proc = Process.Start(psi)!;
            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(30_000);
            return output;
        }
        finally { try { File.Delete(tmp); } catch { } }
    }

    private static void RunProcess(string exe, string args)
    {
        try
        {
            var psi = new ProcessStartInfo(exe, args)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var p = Process.Start(psi);
            p?.WaitForExit(10_000);
        }
        catch { }
    }
}
