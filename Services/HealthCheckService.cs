using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Win32;

namespace Assistools.Services;

public enum HealthStatus { Good, Warning, Critical }

public record HealthMetric(string Nom, string Valeur, string Detail, HealthStatus Statut, string Glyph, double ValeurPourcentage = 0);

public record HealthReport(int Score, HealthMetric Disque, HealthMetric Memoire, HealthMetric Maintenance);

public static class HealthCheckService
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

    // NtQuerySystemInformation pour le % CPU (sans PerformanceCounter)
    [StructLayout(LayoutKind.Sequential)]
    private struct SYSTEM_PROCESSOR_PERFORMANCE_INFORMATION
    {
        public long IdleTime;
        public long KernelTime;
        public long UserTime;
        public long DpcTime;
        public long InterruptTime;
        public uint InterruptCount;
    }

    [DllImport("ntdll.dll")]
    private static extern int NtQuerySystemInformation(int SystemInformationClass,
        [Out] SYSTEM_PROCESSOR_PERFORMANCE_INFORMATION[] SystemInformation,
        int SystemInformationLength, out int ReturnLength);

    private const int SystemProcessorPerformanceInformation = 8;

    public static HealthReport Calculer()
    {
        var disque  = AnalyserDisque();
        var memoire = AnalyserMemoire();
        var maint   = AnalyserMaintenance();

        int score = Math.Min(100, Note(disque) + Note(memoire) + Note(maint));
        return new(score, disque, memoire, maint);
    }

    private static int Note(HealthMetric m) => m.Statut switch
    {
        HealthStatus.Good     => 34,
        HealthStatus.Warning  => 17,
        HealthStatus.Critical => 5,
        _ => 0,
    };

    private static HealthMetric AnalyserDisque()
    {
        try
        {
            var sys = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
            var info = new DriveInfo(sys);
            if (!info.IsReady) return new("Disque système", "?", "Non disponible", HealthStatus.Warning, "", 0);

            double libreGo = info.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);
            double totalGo = info.TotalSize / (1024.0 * 1024.0 * 1024.0);
            double pctLibre = 100.0 * info.AvailableFreeSpace / info.TotalSize;

            HealthStatus statut;
            if (pctLibre < 10 || libreGo < 15)
                statut = HealthStatus.Critical;
            else if (pctLibre < 20 || (pctLibre < 30 && libreGo < 50))
                statut = HealthStatus.Warning;
            else
                statut = HealthStatus.Good;

            return new(
                "Espace disque",
                $"{libreGo:F0} Go libres",
                $"sur {totalGo:F0} Go ({pctLibre:F0} %)",
                statut,
                "",
                100.0 - pctLibre);
        }
        catch
        {
            return new("Espace disque", "?", "Erreur de lecture", HealthStatus.Warning, "", 0);
        }
    }

    private static HealthMetric AnalyserMemoire()
    {
        var mem = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX)) };
        if (!GlobalMemoryStatusEx(ref mem))
            return new("Mémoire", "?", "Non disponible", HealthStatus.Warning, "", 0);

        double totalGo = mem.ullTotalPhys / (1024.0 * 1024.0 * 1024.0);
        double utiliseGo = totalGo * mem.dwMemoryLoad / 100.0;
        uint charge = mem.dwMemoryLoad;

        HealthStatus statut;
        if (charge > 90 || (charge > 80 && totalGo <= 4))
            statut = HealthStatus.Critical;
        else if (charge > 80 || (charge > 70 && totalGo <= 8))
            statut = HealthStatus.Warning;
        else
            statut = HealthStatus.Good;

        return new(
            "Mémoire RAM",
            $"{utiliseGo:F1} Go / {totalGo:F0} Go",
            $"{charge} % utilisée",
            statut,
            "",
            charge);
    }

    private static HealthMetric AnalyserMaintenance()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Assistools");
            if (key?.GetValue("LastMaintenance") is string raw && DateTime.TryParse(raw, out var date))
            {
                var jours = (DateTime.Now - date).TotalDays;
                var statut = jours switch
                {
                    <= 30 => HealthStatus.Good,
                    <= 90 => HealthStatus.Warning,
                    _     => HealthStatus.Critical,
                };
                return new(
                    "Maintenance",
                    jours < 1 ? "Aujourd'hui" : $"Il y a {(int)jours} j",
                    $"Dernière exécution : {date:dd/MM/yyyy}",
                    statut,
                    "",
                    0);
            }
            return new("Maintenance", "Jamais", "Lancez une maintenance pour améliorer le score", HealthStatus.Critical, "", 0);
        }
        catch
        {
            return new("Maintenance", "?", "Non disponible", HealthStatus.Warning, "", 0);
        }
    }

    /// <summary>Mesure l'utilisation CPU sur 600ms (deux échantillons NtQuerySystemInformation).</summary>
    public static double ObtenirUsageCpu()
    {
        try
        {
            int count = Environment.ProcessorCount;
            var buf = new SYSTEM_PROCESSOR_PERFORMANCE_INFORMATION[count];
            int size = Marshal.SizeOf<SYSTEM_PROCESSOR_PERFORMANCE_INFORMATION>() * count;

            if (NtQuerySystemInformation(SystemProcessorPerformanceInformation, buf, size, out _) != 0)
                return -1;

            long idle1 = 0, kernel1 = 0, user1 = 0;
            foreach (var p in buf) { idle1 += p.IdleTime; kernel1 += p.KernelTime; user1 += p.UserTime; }

            Thread.Sleep(600);

            if (NtQuerySystemInformation(SystemProcessorPerformanceInformation, buf, size, out _) != 0)
                return -1;

            long idle2 = 0, kernel2 = 0, user2 = 0;
            foreach (var p in buf) { idle2 += p.IdleTime; kernel2 += p.KernelTime; user2 += p.UserTime; }

            long dIdle   = idle2   - idle1;
            long dKernel = kernel2 - kernel1;
            long dUser   = user2   - user1;
            long dTotal  = dKernel + dUser;

            if (dTotal <= 0) return 0;
            return Math.Max(0, Math.Min(100, 100.0 * (dTotal - dIdle) / dTotal));
        }
        catch { return -1; }
    }

    public static (string Nom, string Detail) ObtenirInfoCpu()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"HARDWARE\DESCRIPTION\System\CentralProcessor\0");

            var nom = key?.GetValue("ProcessorNameString") as string ?? "Inconnu";
            int mhzBase  = key?.GetValue("~MHz")    is int b ? b : 0;
            int mhzTurbo = key?.GetValue("~MaxMHz") is int t ? t : 0;
            int logique = Environment.ProcessorCount;

            nom = Regex.Replace(nom.Trim(), @"\s+", " ");
            nom = nom.Replace("(R)", "").Replace("(TM)", "").Replace("  ", " ").Trim();
            var nomCourt = nom.Length > 32 ? nom[..32].TrimEnd() + "…" : nom;

            double ghzBase  = mhzBase  / 1000.0;
            double ghzTurbo = mhzTurbo / 1000.0;

            int physique = ObtenirCoeursPhysiques();
            string coeurs = physique > 0 ? $"{physique}C/{logique}T" : $"{logique}T";

            string detail = mhzTurbo > mhzBase && mhzBase > 0
                ? $"{coeurs} · {ghzBase:F1} / {ghzTurbo:F1} GHz"
                : mhzBase > 0
                    ? $"{coeurs} · {ghzBase:F1} GHz"
                    : coeurs;

            return (nomCourt, detail);
        }
        catch
        {
            return ($"{Environment.ProcessorCount} threads", "");
        }
    }

    private static int ObtenirCoeursPhysiques()
    {
        try
        {
            int count = 0;
            using var key = Registry.LocalMachine.OpenSubKey(
                @"HARDWARE\DESCRIPTION\System\CentralProcessor");
            if (key == null) return 0;
            // Chaque sous-clé = 1 thread logique ; on cherche le nb de cœurs physiques
            // via la clé ProcessorNameString identique → compter les entrées uniques
            // Méthode simple : GetRelationshipInformation via kernel32
            count = GetPhysicalCoreCountViaKernel();
            return count;
        }
        catch { return 0; }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SYSTEM_LOGICAL_PROCESSOR_INFORMATION
    {
        public UIntPtr ProcessorMask;
        public int Relationship; // 0=ProcessorCore, 1=NumaNode, 2=Cache, 3=ProcessorPackage
        // union (16 bytes on x64 : largest member = CACHE_DESCRIPTOR + padding)
        public long Reserved1;
        public long Reserved2;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetLogicalProcessorInformation(
        [Out] SYSTEM_LOGICAL_PROCESSOR_INFORMATION[] buffer, ref uint returnLength);

    private static int GetPhysicalCoreCountViaKernel()
    {
        uint size = 0;
        GetLogicalProcessorInformation(Array.Empty<SYSTEM_LOGICAL_PROCESSOR_INFORMATION>(), ref size);
        int count = (int)(size / Marshal.SizeOf<SYSTEM_LOGICAL_PROCESSOR_INFORMATION>());
        var buf = new SYSTEM_LOGICAL_PROCESSOR_INFORMATION[count];
        if (!GetLogicalProcessorInformation(buf, ref size)) return 0;
        int cores = 0;
        foreach (var info in buf)
            if (info.Relationship == 0) cores++; // RelationProcessorCore
        return cores;
    }

    /// <summary>Retourne la vitesse RAM en MHz et le type DDR via WMI (Get-CimInstance Win32_PhysicalMemory).</summary>
    public static (int SpeedMhz, string Type) ObtenirInfoRam()
    {
        try
        {
            // SMBIOSMemoryType: 24=DDR3, 26=DDR4, 34=DDR5
            var output = new System.Text.StringBuilder();
            using var p = new System.Diagnostics.Process();
            p.StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -NonInteractive -Command " +
                    "\"Get-CimInstance Win32_PhysicalMemory | Select-Object -First 1 Speed,SMBIOSMemoryType | " +
                    "Format-List | Out-String\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
            };
            p.OutputDataReceived += (_, e) => { if (e.Data != null) output.AppendLine(e.Data); };
            p.Start();
            p.BeginOutputReadLine();
            p.WaitForExit(5000);

            var text = output.ToString();
            int speed = 0;
            string ddrType = "";

            foreach (var line in text.Split('\n'))
            {
                if (line.StartsWith("Speed", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = line.Split(':');
                    if (parts.Length > 1) int.TryParse(parts[1].Trim(), out speed);
                }
                if (line.StartsWith("SMBIOSMemoryType", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = line.Split(':');
                    if (parts.Length > 1 && int.TryParse(parts[1].Trim(), out int t))
                    {
                        ddrType = t switch
                        {
                            34 => "DDR5",
                            26 => "DDR4",
                            24 => "DDR3",
                            _  => "",
                        };
                    }
                }
            }

            return (speed, ddrType);
        }
        catch { return (0, ""); }
    }

    /// <summary>Détermine si le disque système est un SSD ou HDD.</summary>
    public static string ObtenirTypeDisque()
    {
        try
        {
            // HKLM\SYSTEM\CurrentControlSet\Services\disk\Enum → clés du disque physique
            // Méthode simple : lire le registre StorAHCI ou NVMe pour SSD NVMe
            // ou vérifier via DeviceIoControl IOCTL_STORAGE_QUERY_PROPERTY
            // Approche registre : chercher "NVMe" ou "SSD" dans le nom du disque
            using var diskEnum = Registry.LocalMachine.OpenSubKey(
                @"SYSTEM\CurrentControlSet\Services\disk\Enum");
            if (diskEnum != null)
            {
                int count = (int)(diskEnum.GetValue("Count") ?? 0);
                for (int i = 0; i < Math.Min(count, 4); i++)
                {
                    var val = diskEnum.GetValue(i.ToString()) as string ?? "";
                    if (val.Contains("NVMe", StringComparison.OrdinalIgnoreCase) ||
                        val.Contains("NVME", StringComparison.OrdinalIgnoreCase))
                        return "NVMe SSD";
                    if (val.Contains("SSD", StringComparison.OrdinalIgnoreCase))
                        return "SSD";
                }
            }

            // Fallback : vérifier via IOCTL si le disque système est SSD
            return DetecterSsdViaIoctl() ? "SSD" : "HDD";
        }
        catch { return ""; }
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern Microsoft.Win32.SafeHandles.SafeFileHandle CreateFile(
        string lpFileName, uint dwDesiredAccess, uint dwShareMode,
        IntPtr lpSecurityAttributes, uint dwCreationDisposition,
        uint dwFlagsAndAttributes, IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool DeviceIoControl(
        Microsoft.Win32.SafeHandles.SafeFileHandle hDevice, uint dwIoControlCode,
        ref STORAGE_PROPERTY_QUERY lpInBuffer, int nInBufferSize,
        out DEVICE_SEEK_PENALTY_DESCRIPTOR lpOutBuffer, int nOutBufferSize,
        out uint lpBytesReturned, IntPtr lpOverlapped);

    [StructLayout(LayoutKind.Sequential)]
    private struct STORAGE_PROPERTY_QUERY
    {
        public uint PropertyId;  // 7 = StorageDeviceSeekPenaltyProperty
        public uint QueryType;   // 0 = PropertyStandardQuery
        public byte AdditionalParameters;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DEVICE_SEEK_PENALTY_DESCRIPTOR
    {
        public uint Version;
        public uint Size;
        [MarshalAs(UnmanagedType.Bool)]
        public bool IncursSeekPenalty; // false = SSD, true = HDD
    }

    private const uint IOCTL_STORAGE_QUERY_PROPERTY = 0x002D1400;

    private static bool DetecterSsdViaIoctl()
    {
        try
        {
            using var handle = CreateFile(@"\\.\PhysicalDrive0",
                0x80000000, // GENERIC_READ
                3,          // FILE_SHARE_READ | FILE_SHARE_WRITE
                IntPtr.Zero, 3, 0, IntPtr.Zero); // OPEN_EXISTING

            if (handle.IsInvalid) return false;

            var query = new STORAGE_PROPERTY_QUERY { PropertyId = 7, QueryType = 0 };
            bool ok = DeviceIoControl(handle, IOCTL_STORAGE_QUERY_PROPERTY,
                ref query, Marshal.SizeOf<STORAGE_PROPERTY_QUERY>(),
                out var desc, Marshal.SizeOf<DEVICE_SEEK_PENALTY_DESCRIPTOR>(),
                out _, IntPtr.Zero);

            return ok && !desc.IncursSeekPenalty;
        }
        catch { return false; }
    }

    public static void EnregistrerMaintenance()
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(@"Software\Assistools");
            key.SetValue("LastMaintenance", DateTime.Now.ToString("o"));
        }
        catch { }
    }
}
