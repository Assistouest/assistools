using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Assistools.Services;

// ── RECORDS (extensibles — anciens champs préservés pour rétro-compat avec PageRapport) ──
public record OsInfo(string Name, string Version, string Arch,
    string Build = "", string Edition = "", string InstallDate = "",
    string LastBootTime = "", string Language = "", string TimeZone = "",
    string Uptime = "", string ComputerName = "", string Domain = "",
    string CurrentUser = "");

public record CpuInfo(string Name, int Cores, int Threads, int MaxSpeed,
    string Manufacturer = "", int CurrentSpeed = 0, string Socket = "",
    long L2CacheKB = 0, long L3CacheKB = 0, bool VirtualizationEnabled = false,
    string Architecture = "", double TemperatureC = 0, double LoadPercent = 0,
    double PowerW = 0);

public record MotherboardInfo(string Manufacturer, string Product,
    string Version = "", string SerialNumber = "", double TemperatureC = 0);

public record BiosInfo(string Manufacturer = "", string Version = "",
    string ReleaseDate = "", string SerialNumber = "", string SmbiosVersion = "",
    bool SecureBootEnabled = false, bool TpmPresent = false, bool TpmReady = false,
    string TpmVersion = "", string Mode = "");

public record RamModule(string Manufacturer, int Speed, int ConfiguredClockSpeed,
    long CapacityBytes, string DeviceLocator, int SMBIOSMemoryType,
    string PartNumber = "", string SerialNumber = "", int FormFactor = 0);

public record RamInfo(int TotalSlots, int UsedSlots, List<RamModule> Modules,
    long TotalBytes = 0);

public record GpuInfo(string Name, long VramBytes,
    string DriverVersion = "", string DriverDate = "", string CurrentResolution = "",
    double TemperatureC = 0, double LoadPercent = 0, string Vendor = "",
    long VramUsedBytes = 0);

public record DisplayInfo(string Name = "", int Width = 0, int Height = 0,
    int RefreshRateHz = 0, bool IsPrimary = false);

public record DiskInfo(string Model, string MediaType, long SizeBytes,
    string BusType = "", string SerialNumber = "", string FirmwareVersion = "",
    string HealthStatus = "", double TemperatureC = 0, int WearPercent = 0,
    long PowerOnHours = 0);

public record VolumeInfo(string DriveLetter, string Label, long SizeBytes, long FreeBytes,
    string FileSystem = "", string BitlockerStatus = "");

public record NetworkAdapter(string Name = "", string Description = "",
    string MacAddress = "", string IpAddress = "", string Gateway = "",
    string DnsServers = "", string LinkSpeed = "", string Status = "",
    string ConnectionType = "", string Ssid = "");

public record BatteryInfo(string Name = "", long DesignCapacityMwh = 0,
    long FullChargeCapacityMwh = 0, int CycleCount = 0, int HealthPercent = 0,
    string ChargeStatus = "", int EstimatedRuntimeMin = 0);

public record SecurityInfo(bool DefenderEnabled = false, bool RealtimeProtection = false,
    bool AntivirusUpToDate = false, string DefenderEngineVersion = "",
    string SignaturesLastUpdated = "", string LastFullScan = "", string LastQuickScan = "",
    bool FirewallDomain = false, bool FirewallPrivate = false, bool FirewallPublic = false,
    bool SecureBootEnabled = false, string TpmStatus = "", bool UacEnabled = false,
    string ThirdPartyAntivirus = "");

public record OnboardComponent(string Name = "", string Category = "", string Manufacturer = "",
    string DriverVersion = "", string PnpDeviceId = "", string Status = "");

public record MaintenanceHistory(
    string DerniereMaintenance = "",
    long OctetsLiberes = 0,
    int FichiersSupprimes = 0,
    bool ReparationSfc = false,
    bool ReparationDism = false,
    string ScanAntivirus = "",
    List<string>? Operations = null);

public record InstalledApp(string Name, string Version = "", string Publisher = "",
    string InstallDate = "");

public record WindowsUpdateInfo(string Title = "", string KbArticle = "",
    string InstalledOn = "");

public record PerformanceSnapshot(double CpuUsagePercent = 0, double RamUsagePercent = 0,
    long RamUsedBytes = 0, long RamTotalBytes = 0, int ProcessCount = 0,
    int ThreadCount = 0, int HandleCount = 0);

public record SystemReport(
    OsInfo OS,
    CpuInfo CPU,
    MotherboardInfo Motherboard,
    RamInfo RAM,
    List<GpuInfo> GPUs,
    List<DiskInfo> Disks,
    List<VolumeInfo> Volumes,
    BiosInfo? Bios = null,
    List<DisplayInfo>? Displays = null,
    List<NetworkAdapter>? Network = null,
    BatteryInfo? Battery = null,
    SecurityInfo? Security = null,
    List<InstalledApp>? InstalledApps = null,
    List<WindowsUpdateInfo>? RecentUpdates = null,
    PerformanceSnapshot? Performance = null,
    List<OnboardComponent>? OnboardComponents = null,
    MaintenanceHistory? Maintenance = null,
    string GeneratedAt = "");

public static class RapportService
{
    public static string ObtenirDdrType(RamModule module)
    {
        switch (module.SMBIOSMemoryType)
        {
            case 20: return "DDR";
            case 21: return "DDR2";
            case 22: return "DDR2 FB-DIMM";
            case 24: return "DDR3";
            case 26: return "DDR4";
            case 30: return "LPDDR3";
            case 31: return "LPDDR4";
            case 34: return "DDR5";
            case 35: return "LPDDR5";
        }
        int speed = module.ConfiguredClockSpeed > 0 ? module.ConfiguredClockSpeed : module.Speed;
        if (speed >= 4800) return "DDR5";
        if (speed >= 2133) return "DDR4";
        if (speed >= 1066) return "DDR3";
        if (speed >= 533) return "DDR2";
        return "DDR / Autre";
    }

    public static string FormaterTaille(long octets)
    {
        if (octets <= 0) return "0 Mo";
        double go = octets / (1024.0 * 1024.0 * 1024.0);
        if (go >= 1.0) return $"{go:F1} Go";
        double mo = octets / (1024.0 * 1024.0);
        return $"{mo:F0} Mo";
    }

    public static async Task<SystemReport?> GenererRapportAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                // 1) Lecture capteurs hardware via LibreHardwareMonitor
                HardwareReadings readings;
                try { readings = HardwareMonitor.Read(); }
                catch (Exception ex) { LogStore.Append($"[RAPPORT] HardwareMonitor échec : {ex.Message}"); readings = new HardwareReadings(); }

                // 2) Collecte WMI / PowerShell
                LogStore.Append("[RAPPORT] Collecte des informations système…");
                string jsonOutput = RunPowerShellCapture(PowerShellCollectorScript);

                if (string.IsNullOrWhiteSpace(jsonOutput))
                {
                    LogStore.Append("[RAPPORT] ERREUR : aucune sortie reçue de PowerShell.");
                    return null;
                }

                SystemReport? report;
                try
                {
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
                            | System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals,
                    };
                    report = JsonSerializer.Deserialize<SystemReport>(jsonOutput, options);
                }
                catch (Exception ex)
                {
                    LogStore.Append($"[RAPPORT] ERREUR de désérialisation JSON : {ex.Message}");
                    // Sauve le JSON brut pour diagnostic
                    string debugPath = Path.Combine(Path.GetTempPath(), $"assistools_report_debug_{DateTime.Now:yyyyMMdd_HHmmss}.json");
                    try { File.WriteAllText(debugPath, jsonOutput, Encoding.UTF8); LogStore.Append($"[RAPPORT] JSON brut sauvé : {debugPath}"); } catch { }
                    return null;
                }

                if (report == null)
                {
                    LogStore.Append("[RAPPORT] ERREUR : désérialisation a retourné null.");
                    return null;
                }

                // 3) Fusion capteurs
                report = FusionnerCapteurs(report, readings);

                // 4) Historique maintenance
                var maint = new MaintenanceHistory(
                    DerniereMaintenance: MaintenanceStore.DerniereMaintenance?.ToString("dd/MM/yyyy à HH:mm") ?? "",
                    OctetsLiberes: MaintenanceStore.OctetsLiberes,
                    FichiersSupprimes: MaintenanceStore.FichiersSupprimes,
                    ReparationSfc: MaintenanceStore.ReparationSfc,
                    ReparationDism: MaintenanceStore.ReparationDism,
                    ScanAntivirus: MaintenanceStore.ScanAntivirus ?? "",
                    Operations: MaintenanceStore.OperationsEffectuees.Count > 0
                        ? new List<string>(MaintenanceStore.OperationsEffectuees)
                        : null);

                LogStore.Append("[RAPPORT] Rapport généré avec succès.");
                return report with
                {
                    Maintenance = maint,
                    GeneratedAt = DateTime.Now.ToString("dd/MM/yyyy à HH:mm:ss")
                };
            }
            catch (Exception ex)
            {
                LogStore.Append($"[RAPPORT] Erreur fatale : {ex.GetType().Name} — {ex.Message}");
                Debug.WriteLine($"[RapportService] Erreur génération : {ex}");
                return null;
            }
        });
    }

    private static SystemReport FusionnerCapteurs(SystemReport report, HardwareReadings r)
    {
        var cpu = report.CPU with
        {
            TemperatureC = r.CpuTemperatureC ?? 0,
            LoadPercent = r.CpuLoadPercent ?? report.CPU.LoadPercent,
            PowerW = r.CpuPowerW ?? 0,
        };

        var mb = report.Motherboard with
        {
            TemperatureC = r.MotherboardTemperatureC ?? 0,
        };

        var gpus = new List<GpuInfo>();
        foreach (var g in report.GPUs)
        {
            double temp = 0; double load = 0; long vramUsed = 0;
            foreach (var kv in r.GpuTemperatures)
                if (g.Name.Contains(kv.Key, StringComparison.OrdinalIgnoreCase) ||
                    kv.Key.Contains(g.Name, StringComparison.OrdinalIgnoreCase))
                    temp = kv.Value;
            foreach (var kv in r.GpuLoads)
                if (g.Name.Contains(kv.Key, StringComparison.OrdinalIgnoreCase) ||
                    kv.Key.Contains(g.Name, StringComparison.OrdinalIgnoreCase))
                    load = kv.Value;
            foreach (var kv in r.GpuVramUsedBytes)
                if (g.Name.Contains(kv.Key, StringComparison.OrdinalIgnoreCase) ||
                    kv.Key.Contains(g.Name, StringComparison.OrdinalIgnoreCase))
                    vramUsed = kv.Value;
            gpus.Add(g with { TemperatureC = temp, LoadPercent = load, VramUsedBytes = vramUsed });
        }

        var disks = new List<DiskInfo>();
        foreach (var d in report.Disks)
        {
            double temp = 0; int wear = d.WearPercent;
            foreach (var kv in r.StorageTemperatures)
                if (d.Model.Contains(kv.Key, StringComparison.OrdinalIgnoreCase) ||
                    kv.Key.Contains(d.Model, StringComparison.OrdinalIgnoreCase))
                    temp = kv.Value;
            foreach (var kv in r.StorageWearLevels)
                if (d.Model.Contains(kv.Key, StringComparison.OrdinalIgnoreCase) ||
                    kv.Key.Contains(d.Model, StringComparison.OrdinalIgnoreCase))
                    wear = kv.Value;
            disks.Add(d with { TemperatureC = temp, WearPercent = wear });
        }

        return report with { CPU = cpu, Motherboard = mb, GPUs = gpus, Disks = disks };
    }

    // ─────────────────────────────────────────────────────────────────────────
    // SCRIPT POWERSHELL DE COLLECTE (sortie JSON unique)
    // ─────────────────────────────────────────────────────────────────────────
    private const string PowerShellCollectorScript = @"
$ErrorActionPreference = 'SilentlyContinue'

$cpu = Get-CimInstance Win32_Processor | Select-Object -First 1
$os  = Get-CimInstance Win32_OperatingSystem | Select-Object -First 1
$cs  = Get-CimInstance Win32_ComputerSystem | Select-Object -First 1
$bb  = Get-CimInstance Win32_BaseBoard | Select-Object -First 1
$bios = Get-CimInstance Win32_BIOS | Select-Object -First 1

# OS détaillé + uptime
$uptime = (Get-Date) - $os.LastBootUpTime
$uptimeStr = ('{0} j {1:D2}h {2:D2}m' -f [int]$uptime.Days, [int]$uptime.Hours, [int]$uptime.Minutes)
$tz = (Get-TimeZone).DisplayName
$installDate = $os.InstallDate.ToString('dd/MM/yyyy HH:mm')
$lastBoot = $os.LastBootUpTime.ToString('dd/MM/yyyy HH:mm')

# Secure Boot
$secureBoot = $false
try { $secureBoot = [bool](Confirm-SecureBootUEFI -ErrorAction Stop) } catch { $secureBoot = $false }

# Mode BIOS (UEFI / Legacy)
$biosMode = 'Inconnu'
try {
    $env = (Get-CimInstance -Namespace 'root\cimv2\security\microsofttpm' -ClassName Win32_Tpm -ErrorAction Stop)
    $biosMode = if ($secureBoot) { 'UEFI' } else { 'UEFI (sans Secure Boot)' }
} catch {
    $biosMode = if ($secureBoot) { 'UEFI' } else { 'Legacy / CSM' }
}

# TPM
$tpmPresent = $false; $tpmReady = $false; $tpmVer = ''
try {
    $tpm = Get-Tpm -ErrorAction Stop
    $tpmPresent = [bool]$tpm.TpmPresent
    $tpmReady   = [bool]$tpm.TpmReady
    $tpmVer     = [string]$tpm.ManufacturerVersionFull20
} catch { }

# RAM
$ramModules = @(Get-CimInstance Win32_PhysicalMemory | ForEach-Object {
    [PSCustomObject]@{
        Manufacturer = if ($_.Manufacturer) { $_.Manufacturer.Trim() } else { 'Inconnu' }
        Speed = [int]$_.Speed
        ConfiguredClockSpeed = [int]$_.ConfiguredClockSpeed
        CapacityBytes = [long]$_.Capacity
        DeviceLocator = if ($_.DeviceLocator) { $_.DeviceLocator.Trim() } else { 'Slot' }
        SMBIOSMemoryType = [int]$_.SMBIOSMemoryType
        PartNumber   = if ($_.PartNumber) { $_.PartNumber.Trim() } else { '' }
        SerialNumber = if ($_.SerialNumber) { $_.SerialNumber.Trim() } else { '' }
        FormFactor   = [int]$_.FormFactor
    }
})

$ramArray = Get-CimInstance Win32_PhysicalMemoryArray | Select-Object -First 1
$totalSlots = if ($ramArray -and $ramArray.MemoryDevices) { $ramArray.MemoryDevices } else { $ramModules.Count }
$usedSlots = $ramModules.Count
if ($totalSlots -lt $usedSlots) { $totalSlots = $usedSlots }
$totalBytes = 0
foreach ($m in $ramModules) { $totalBytes += $m.CapacityBytes }

# GPU
$gpus = @(Get-CimInstance Win32_VideoController | Where-Object { $_.Name -notlike '*Remote*' } | ForEach-Object {
    [PSCustomObject]@{
        Name = $_.Name
        VramBytes = [long]($_.AdapterRAM)
        DriverVersion = [string]$_.DriverVersion
        DriverDate = if ($_.DriverDate) { $_.DriverDate.ToString('dd/MM/yyyy') } else { '' }
        CurrentResolution = if ($_.CurrentHorizontalResolution -and $_.CurrentVerticalResolution) {
            '{0}x{1} @ {2} Hz' -f $_.CurrentHorizontalResolution, $_.CurrentVerticalResolution, $_.CurrentRefreshRate
        } else { '' }
        Vendor = [string]$_.AdapterCompatibility
    }
})

# Écrans
$displays = @()
try {
    $monitors = Get-CimInstance -Namespace 'root\wmi' -ClassName WmiMonitorBasicDisplayParams -ErrorAction Stop
    $displays = @($monitors | ForEach-Object {
        [PSCustomObject]@{
            Name = ''
            Width = 0
            Height = 0
            RefreshRateHz = 0
            IsPrimary = $false
        }
    })
} catch { }

# Disques physiques + SMART
$disks = @()
try {
    $physicalDisks = Get-PhysicalDisk -ErrorAction Stop
    foreach ($pd in $physicalDisks) {
        $temp = 0; $wear = 0; $hours = 0
        try {
            $rel = Get-StorageReliabilityCounter -PhysicalDisk $pd -ErrorAction Stop
            if ($rel) {
                if ($rel.Temperature -gt 0) { $temp = $rel.Temperature }
                if ($rel.Wear -gt 0)        { $wear = $rel.Wear }
                if ($rel.PowerOnHours -gt 0){ $hours = $rel.PowerOnHours }
            }
        } catch { }
        $disks += [PSCustomObject]@{
            Model = $pd.FriendlyName
            MediaType = $pd.MediaType.ToString()
            SizeBytes = [long]$pd.Size
            BusType = $pd.BusType.ToString()
            SerialNumber = [string]$pd.SerialNumber
            FirmwareVersion = [string]$pd.FirmwareVersion
            HealthStatus = $pd.HealthStatus.ToString()
            TemperatureC = [double]$temp
            WearPercent = [int]$wear
            PowerOnHours = [long]$hours
        }
    }
} catch {
    $disks = @(Get-CimInstance Win32_DiskDrive | ForEach-Object {
        [PSCustomObject]@{
            Model = $_.Model
            MediaType = if ($_.MediaType -like '*fixed*') { 'SSD' } else { 'HDD' }
            SizeBytes = [long]$_.Size
            BusType = [string]$_.InterfaceType
            SerialNumber = [string]$_.SerialNumber
            FirmwareVersion = [string]$_.FirmwareRevision
            HealthStatus = if ($_.Status) { $_.Status } else { 'Inconnu' }
            TemperatureC = 0
            WearPercent = 0
            PowerOnHours = 0
        }
    })
}

# Volumes + BitLocker
$volumes = @()
try {
    $vols = Get-Volume -ErrorAction Stop | Where-Object DriveLetter -ne $null
    foreach ($v in $vols) {
        $bl = 'Non chiffré'
        try {
            $bv = Get-BitLockerVolume -MountPoint ($v.DriveLetter.ToString() + ':') -ErrorAction Stop
            if ($bv) {
                $bl = if ($bv.ProtectionStatus -eq 'On') { 'Chiffré (BitLocker actif)' }
                      elseif ($bv.EncryptionPercentage -gt 0) { 'Chiffrement en cours' }
                      else { 'BitLocker disponible (inactif)' }
            }
        } catch { }
        $volumes += [PSCustomObject]@{
            DriveLetter = $v.DriveLetter.ToString()
            Label = if ($v.FileSystemLabel) { $v.FileSystemLabel } else { '' }
            SizeBytes = [long]$v.Size
            FreeBytes = [long]$v.SizeRemaining
            FileSystem = [string]$v.FileSystem
            BitlockerStatus = $bl
        }
    }
} catch {
    $volumes = @(Get-CimInstance Win32_LogicalDisk | Where-Object DriveType -eq 3 | ForEach-Object {
        [PSCustomObject]@{
            DriveLetter = $_.DeviceID.Replace(':', '')
            Label = if ($_.VolumeName) { $_.VolumeName } else { '' }
            SizeBytes = [long]$_.Size
            FreeBytes = [long]$_.FreeSpace
            FileSystem = [string]$_.FileSystem
            BitlockerStatus = 'Inconnu'
        }
    })
}

# Réseau
$network = @()
try {
    $adapters = Get-NetAdapter -ErrorAction Stop | Where-Object Status -eq 'Up'
    foreach ($a in $adapters) {
        $ip = ''; $gw = ''; $dns = ''
        try {
            $cfg = Get-NetIPConfiguration -InterfaceIndex $a.InterfaceIndex -ErrorAction Stop
            if ($cfg.IPv4Address) { $ip = ($cfg.IPv4Address.IPAddress -join ', ') }
            if ($cfg.IPv4DefaultGateway) { $gw = ($cfg.IPv4DefaultGateway.NextHop -join ', ') }
            if ($cfg.DNSServer) { $dns = (($cfg.DNSServer | Where-Object AddressFamily -eq 2).ServerAddresses -join ', ') }
        } catch { }
        $ssid = ''
        if ($a.MediaType -like '*802.11*' -or $a.Name -like '*Wi-Fi*' -or $a.Name -like '*Wireless*') {
            try {
                $netsh = (netsh wlan show interfaces) 2>$null
                $ssidLine = $netsh | Where-Object { $_ -match '^\s*SSID\s*:' } | Select-Object -First 1
                if ($ssidLine) { $ssid = ($ssidLine -split ':\s*', 2)[1] }
            } catch { }
        }
        $network += [PSCustomObject]@{
            Name = $a.Name
            Description = $a.InterfaceDescription
            MacAddress = $a.MacAddress
            IpAddress = $ip
            Gateway = $gw
            DnsServers = $dns
            LinkSpeed = $a.LinkSpeed
            Status = $a.Status.ToString()
            ConnectionType = $a.MediaType
            Ssid = $ssid
        }
    }
} catch { }

# Composants intégrés carte mère (réseau, son, Bluetooth)
$onboard = @()
try {
    # Fonction helper : version driver via Win32_PnPSignedDriver (sans filtre WMI risqué)
    $allPnpDrivers = @(Get-CimInstance Win32_PnPSignedDriver -ErrorAction SilentlyContinue)

    # Adaptateurs réseau physiques (pas les virtuels ROOT\*)
    $netAdapters = @(Get-CimInstance Win32_NetworkAdapter -ErrorAction SilentlyContinue |
        Where-Object { $_.PhysicalAdapter -eq $true -and $_.PNPDeviceID -notlike 'ROOT\*' })
    foreach ($na in $netAdapters) {
        $drvVer = ''
        $pnpId = [string]$na.PNPDeviceID
        $matched = $allPnpDrivers | Where-Object { $_.DeviceID -eq $pnpId } | Select-Object -First 1
        if ($matched) { $drvVer = [string]$matched.DriverVersion }
        $cat = if ($na.Name -like '*Wi-Fi*' -or $na.Name -like '*Wireless*' -or $na.Name -like '*WiFi*' -or $na.Name -like '*WLAN*') { 'Wi-Fi' }
               elseif ($na.Name -like '*Bluetooth*') { 'Bluetooth' }
               else { 'Ethernet' }
        $onboard += [PSCustomObject]@{
            Name         = [string]$na.Name
            Category     = $cat
            Manufacturer = [string]$na.Manufacturer
            DriverVersion = $drvVer
            PnpDeviceId  = $pnpId
            Status       = if ($na.NetEnabled) { 'Actif' } else { 'Désactivé' }
        }
    }

    # Son intégré
    $sounds = @(Get-CimInstance Win32_SoundDevice -ErrorAction SilentlyContinue |
        Where-Object { $_.PNPDeviceID -notlike 'ROOT\*' })
    foreach ($sd in $sounds) {
        $drvVer = ''
        $pnpId = [string]$sd.PNPDeviceID
        $matched = $allPnpDrivers | Where-Object { $_.DeviceID -eq $pnpId } | Select-Object -First 1
        if ($matched) { $drvVer = [string]$matched.DriverVersion }
        $onboard += [PSCustomObject]@{
            Name         = [string]$sd.Name
            Category     = 'Audio'
            Manufacturer = [string]$sd.Manufacturer
            DriverVersion = $drvVer
            PnpDeviceId  = $pnpId
            Status       = if ($sd.Status) { [string]$sd.Status } else { 'OK' }
        }
    }

    # Bluetooth (appareils PnP non déjà capturés)
    $btDevices = @(Get-CimInstance Win32_PnPEntity -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -like '*Bluetooth*' -and $_.PNPDeviceID -notlike 'ROOT\*' -and $_.PNPClass -notin @('Mouse','Keyboard','HIDClass') } |
        Select-Object -First 4)
    $existingIds = $onboard | ForEach-Object { $_.PnpDeviceId }
    foreach ($bt in $btDevices) {
        $pnpId = [string]$bt.PNPDeviceID
        if ($existingIds -contains $pnpId) { continue }
        $drvVer = ''
        $matched = $allPnpDrivers | Where-Object { $_.DeviceID -eq $pnpId } | Select-Object -First 1
        if ($matched) { $drvVer = [string]$matched.DriverVersion }
        $onboard += [PSCustomObject]@{
            Name         = [string]$bt.Name
            Category     = 'Bluetooth'
            Manufacturer = [string]$bt.Manufacturer
            DriverVersion = $drvVer
            PnpDeviceId  = $pnpId
            Status       = if ($bt.Status) { [string]$bt.Status } else { 'OK' }
        }
    }
} catch { }

# Batterie
$battery = $null
try {
    $bat = Get-CimInstance Win32_Battery -ErrorAction Stop | Select-Object -First 1
    if ($bat) {
        $designCap = 0; $fullCap = 0; $cycles = 0
        try { $stat = Get-CimInstance -Namespace 'root\wmi' -ClassName BatteryStaticData -ErrorAction SilentlyContinue | Select-Object -First 1; if ($stat) { $designCap = [long]$stat.DesignedCapacity } } catch { }
        try { $fcc  = Get-CimInstance -Namespace 'root\wmi' -ClassName BatteryFullChargedCapacity -ErrorAction SilentlyContinue | Select-Object -First 1; if ($fcc) { $fullCap = [long]$fcc.FullChargedCapacity } } catch { }
        try { $cyc  = Get-CimInstance -Namespace 'root\wmi' -ClassName BatteryCycleCount -ErrorAction SilentlyContinue | Select-Object -First 1; if ($cyc) { $cycles = [int]$cyc.CycleCount } } catch { }
        $health = 0
        if ($designCap -gt 0 -and $fullCap -gt 0) { $health = [int](($fullCap / $designCap) * 100) }
        $statusMap = @{1='Décharge';2='Sur secteur';3='Pleine';4='Faible';5='Critique';6='Charge';7='Charge et faible';8='Charge et haute';9='Charge et critique';10='Inconnu';11='Partielle'}
        $stStr = $statusMap[[int]$bat.BatteryStatus]; if (-not $stStr) { $stStr = 'Inconnu' }
        $battery = [PSCustomObject]@{
            Name = [string]$bat.Name
            DesignCapacityMwh = [long]$designCap
            FullChargeCapacityMwh = [long]$fullCap
            CycleCount = [int]$cycles
            HealthPercent = [int]$health
            ChargeStatus = [string]$stStr
            EstimatedRuntimeMin = [int]$bat.EstimatedRunTime
        }
    }
} catch { }

# Sécurité Windows : Defender + Firewall
$sec = [PSCustomObject]@{
    DefenderEnabled = $false
    RealtimeProtection = $false
    AntivirusUpToDate = $false
    DefenderEngineVersion = ''
    SignaturesLastUpdated = ''
    LastFullScan = ''
    LastQuickScan = ''
    FirewallDomain = $false
    FirewallPrivate = $false
    FirewallPublic = $false
    SecureBootEnabled = $secureBoot
    TpmStatus = if ($tpmReady) { 'Activé et prêt' } elseif ($tpmPresent) { 'Présent (non prêt)' } else { 'Absent' }
    UacEnabled = $false
    ThirdPartyAntivirus = ''
}
try {
    $mp = Get-MpComputerStatus -ErrorAction Stop
    $sec.DefenderEnabled = [bool]$mp.AntivirusEnabled
    $sec.RealtimeProtection = [bool]$mp.RealTimeProtectionEnabled
    $sec.AntivirusUpToDate = [bool]($mp.AntivirusSignatureLastUpdated -and ((Get-Date) - $mp.AntivirusSignatureLastUpdated).TotalDays -lt 7)
    $sec.DefenderEngineVersion = [string]$mp.AMEngineVersion
    if ($mp.AntivirusSignatureLastUpdated) { $sec.SignaturesLastUpdated = $mp.AntivirusSignatureLastUpdated.ToString('dd/MM/yyyy HH:mm') }
    if ($mp.FullScanEndTime)  { $sec.LastFullScan  = $mp.FullScanEndTime.ToString('dd/MM/yyyy HH:mm') }
    if ($mp.QuickScanEndTime) { $sec.LastQuickScan = $mp.QuickScanEndTime.ToString('dd/MM/yyyy HH:mm') }
} catch { }
try {
    $fw = Get-NetFirewallProfile -ErrorAction Stop
    $sec.FirewallDomain  = [bool](($fw | Where-Object Name -eq 'Domain').Enabled)
    $sec.FirewallPrivate = [bool](($fw | Where-Object Name -eq 'Private').Enabled)
    $sec.FirewallPublic  = [bool](($fw | Where-Object Name -eq 'Public').Enabled)
} catch { }
try {
    $uac = Get-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System' -Name EnableLUA -ErrorAction Stop
    $sec.UacEnabled = [bool]$uac.EnableLUA
} catch { }
try {
    $av = Get-CimInstance -Namespace 'root\SecurityCenter2' -ClassName AntiVirusProduct -ErrorAction Stop
    $third = $av | Where-Object { $_.displayName -ne 'Windows Defender' -and $_.displayName -notlike '*Microsoft Defender*' } | Select-Object -First 1
    if ($third) { $sec.ThirdPartyAntivirus = $third.displayName }
} catch { }

# Mises à jour récentes (10 dernières)
$updates = @()
try {
    $hot = Get-HotFix -ErrorAction Stop | Sort-Object InstalledOn -Descending | Select-Object -First 10
    foreach ($h in $hot) {
        $updates += [PSCustomObject]@{
            Title = [string]$h.Description
            KbArticle = [string]$h.HotFixID
            InstalledOn = if ($h.InstalledOn) { $h.InstalledOn.ToString('dd/MM/yyyy') } else { '' }
        }
    }
} catch { }

# Applications installées (résumé)
$apps = @()
try {
    $paths = @(
        'HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\*',
        'HKLM:\Software\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*'
    )
    $apps = @(Get-ItemProperty $paths -ErrorAction SilentlyContinue |
        Where-Object { $_.DisplayName -and -not $_.SystemComponent } |
        Select-Object -Unique DisplayName, DisplayVersion, Publisher, InstallDate |
        Sort-Object DisplayName | ForEach-Object {
            [PSCustomObject]@{
                Name = $_.DisplayName
                Version = [string]$_.DisplayVersion
                Publisher = [string]$_.Publisher
                InstallDate = [string]$_.InstallDate
            }
        })
} catch { }

# Performance snapshot
$perfCpu = 0.0; $perfRamPct = 0.0; $perfRamUsed = 0; $perfRamTotal = 0
$perfProc = 0; $perfThread = 0; $perfHandle = 0
try {
    $cpuLoadAvg = (Get-CimInstance Win32_Processor -ErrorAction SilentlyContinue | Measure-Object -Property LoadPercentage -Average).Average
    if ($cpuLoadAvg) { $perfCpu = [double]$cpuLoadAvg }
    $perfRamTotal = [long]($os.TotalVisibleMemorySize * 1024)
    $perfRamUsed  = [long](($os.TotalVisibleMemorySize - $os.FreePhysicalMemory) * 1024)
    if ($perfRamTotal -gt 0) { $perfRamPct = [math]::Round(($perfRamUsed / $perfRamTotal) * 100, 1) }
    $procs = @(Get-Process -ErrorAction SilentlyContinue)
    $perfProc = [int]$procs.Count
    $threadSum = 0
    foreach ($pr in $procs) { try { $threadSum += [int]$pr.Threads.Count } catch { } }
    $perfThread = [int]$threadSum
    $handleSum = ($procs | Measure-Object -Property HandleCount -Sum -ErrorAction SilentlyContinue).Sum
    if ($handleSum) { $perfHandle = [int]$handleSum }
} catch { }
$perf = [PSCustomObject]@{
    CpuUsagePercent = [double]$perfCpu
    RamUsagePercent = [double]$perfRamPct
    RamUsedBytes = [long]$perfRamUsed
    RamTotalBytes = [long]$perfRamTotal
    ProcessCount = [int]$perfProc
    ThreadCount = [int]$perfThread
    HandleCount = [int]$perfHandle
}

# Identité utilisateur
$user = ''
try { $user = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name } catch { }

# Assemblage final
$result = [PSCustomObject]@{
    OS = @{
        Name = $os.Caption
        Version = $os.Version
        Build = $os.BuildNumber
        Edition = $os.Caption
        Arch = $os.OSArchitecture
        InstallDate = $installDate
        LastBootTime = $lastBoot
        Language = $os.OSLanguage.ToString()
        TimeZone = $tz
        Uptime = $uptimeStr
        ComputerName = $env:COMPUTERNAME
        Domain = $cs.Domain
        CurrentUser = $user
    }
    CPU = @{
        Name = if ($cpu -and $cpu.Name) { $cpu.Name.Trim() } else { 'Inconnu' }
        Manufacturer = [string]$cpu.Manufacturer
        Cores = [int]$cpu.NumberOfCores
        Threads = [int]$cpu.NumberOfLogicalProcessors
        MaxSpeed = [int]$cpu.MaxClockSpeed
        CurrentSpeed = [int]$cpu.CurrentClockSpeed
        Socket = [string]$cpu.SocketDesignation
        L2CacheKB = [long]$cpu.L2CacheSize
        L3CacheKB = [long]$cpu.L3CacheSize
        VirtualizationEnabled = [bool]$cpu.VirtualizationFirmwareEnabled
        Architecture = switch ([int]$cpu.Architecture) {
            0 {'x86'} 1 {'MIPS'} 2 {'Alpha'} 3 {'PowerPC'} 5 {'ARM'} 6 {'ia64'} 9 {'x64'} 12 {'ARM64'} default {'Inconnu'}
        }
    }
    Motherboard = @{
        Manufacturer = if ($bb -and $bb.Manufacturer) { $bb.Manufacturer.Trim() } else { 'Inconnu' }
        Product = if ($bb -and $bb.Product) { $bb.Product.Trim() } else { 'Inconnu' }
        Version = if ($bb -and $bb.Version) { $bb.Version.Trim() } else { '' }
        SerialNumber = if ($bb -and $bb.SerialNumber) { $bb.SerialNumber.Trim() } else { '' }
    }
    Bios = @{
        Manufacturer = [string]$bios.Manufacturer
        Version = [string]$bios.SMBIOSBIOSVersion
        ReleaseDate = if ($bios.ReleaseDate) { $bios.ReleaseDate.ToString('dd/MM/yyyy') } else { '' }
        SerialNumber = [string]$bios.SerialNumber
        SmbiosVersion = ('{0}.{1}' -f $bios.SMBIOSMajorVersion, $bios.SMBIOSMinorVersion)
        SecureBootEnabled = [bool]$secureBoot
        TpmPresent = [bool]$tpmPresent
        TpmReady = [bool]$tpmReady
        TpmVersion = [string]$tpmVer
        Mode = $biosMode
    }
    RAM = @{
        TotalSlots = [int]$totalSlots
        UsedSlots = [int]$usedSlots
        TotalBytes = [long]$totalBytes
        Modules = $ramModules
    }
    GPUs = $gpus
    Displays = $displays
    Disks = $disks
    Volumes = $volumes
    Network = $network
    Battery = $battery
    Security = $sec
    InstalledApps = $apps
    RecentUpdates = $updates
    Performance = $perf
    OnboardComponents = $onboard
}

$result | ConvertTo-Json -Depth 6 -Compress
";

    // ─────────────────────────────────────────────────────────────────────────
    // GÉNÉRATION HTML — Rapport qualité professionnelle
    // ─────────────────────────────────────────────────────────────────────────
    public static string GenererHtml(SystemReport report)
    {
        var sb = new StringBuilder();

        // Calculs préalables RAM
        long totalRamBytes = report.RAM.TotalBytes;
        string ddrType = "DDR"; int ramSpeed = 0;
        if (totalRamBytes == 0)
        {
            foreach (var m in report.RAM.Modules)
            {
                totalRamBytes += m.CapacityBytes;
                ddrType = ObtenirDdrType(m);
                ramSpeed = m.ConfiguredClockSpeed > 0 ? m.ConfiguredClockSpeed : m.Speed;
            }
        }
        else if (report.RAM.Modules.Count > 0)
        {
            ddrType = ObtenirDdrType(report.RAM.Modules[0]);
            var firstMod = report.RAM.Modules[0];
            ramSpeed = firstMod.ConfiguredClockSpeed > 0 ? firstMod.ConfiguredClockSpeed : firstMod.Speed;
        }

        // Helper : n'émet une ligne kv que si la valeur est renseignée
        string KvRow(string label, string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "";
            return $"<div class='k'>{label}</div><div class='v'>{Escape(value)}</div>";
        }

        // ── HTML : HEADER + CSS ───────────────────────────────────────────────
        sb.Append($@"<!DOCTYPE html>
<html lang='fr'>
<head>
<meta charset='utf-8'>
<meta name='viewport' content='width=device-width,initial-scale=1'>
<title>Rapport Système — {Escape(report.OS.ComputerName)} — Assistools</title>
<style>
@page {{ margin: 14mm 12mm; }}
* {{ box-sizing: border-box; margin: 0; padding: 0; }}
body {{ font-family: 'Segoe UI', system-ui, sans-serif; color: #0f172a; background: #f1f5f9; font-size: 13px; line-height: 1.55; }}
.page {{ max-width: 960px; margin: 0 auto; background: #fff; padding: 0 0 40px 0; box-shadow: 0 0 32px rgba(0,0,0,.08); }}

/* ── BANDEAU HAUT ── */
.topbar {{ background: #0069E2; height: 6px; }}
.header-wrap {{ background: #fff; padding: 28px 40px 20px; border-bottom: 1px solid #e2e8f0; }}
.header-logo {{ display: flex; align-items: center; gap: 16px; margin-bottom: 18px; }}
.logo-icon {{ width: 44px; height: 44px; background: #0069E2; border-radius: 10px; display: flex; align-items: center; justify-content: center; }}
.logo-icon svg {{ width: 26px; height: 26px; fill: white; }}
.logo-text {{ line-height: 1.2; }}
.logo-text .brand {{ font-size: 20px; font-weight: 800; color: #0069E2; letter-spacing: -0.5px; }}
.logo-text .tagline {{ font-size: 11px; color: #64748b; text-transform: uppercase; letter-spacing: 0.8px; font-weight: 500; }}
.header-title {{ font-size: 24px; font-weight: 700; color: #0f172a; margin-bottom: 6px; }}
.header-sub {{ font-size: 13px; color: #64748b; }}
.header-meta {{ display: flex; flex-wrap: wrap; gap: 0; margin-top: 18px; border: 1px solid #e2e8f0; border-radius: 8px; overflow: hidden; }}
.header-meta-item {{ flex: 1 1 160px; padding: 10px 16px; border-right: 1px solid #e2e8f0; }}
.header-meta-item:last-child {{ border-right: none; }}
.header-meta-item .label {{ font-size: 10px; text-transform: uppercase; letter-spacing: 0.5px; color: #94a3b8; font-weight: 600; margin-bottom: 3px; }}
.header-meta-item .value {{ font-size: 13px; font-weight: 700; color: #0f172a; }}

/* ── BODY ── */
.body {{ padding: 0 40px; }}
h2.section {{ font-size: 15px; font-weight: 700; color: #0069E2; border-bottom: 2px solid #e2e8f0; padding-bottom: 7px; margin: 28px 0 14px; display: flex; align-items: center; gap: 8px; page-break-after: avoid; }}
h2.section::before {{ content: ''; width: 4px; height: 17px; background: #0069E2; border-radius: 2px; flex-shrink: 0; }}

/* ── TABLE OF CONTENTS ── */
.toc {{ background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 8px; padding: 16px 24px; margin: 24px 0; }}
.toc-title {{ font-size: 11px; font-weight: 700; text-transform: uppercase; letter-spacing: 0.6px; color: #64748b; margin-bottom: 10px; }}
.toc-grid {{ display: grid; grid-template-columns: repeat(3, 1fr); gap: 2px 20px; }}
.toc-item {{ font-size: 12px; color: #334155; padding: 3px 0; }}
.toc-num {{ color: #94a3b8; font-size: 10px; margin-right: 4px; }}

/* ── CARDS ── */
.cards {{ display: grid; gap: 10px; margin-bottom: 14px; }}
.cards-2 {{ grid-template-columns: repeat(2,1fr); }}
.cards-3 {{ grid-template-columns: repeat(3,1fr); }}
.card {{ border: 1px solid #e2e8f0; background: #f8fafc; border-radius: 8px; padding: 13px 15px; }}
.card-label {{ font-size: 10px; color: #64748b; text-transform: uppercase; letter-spacing: 0.4px; font-weight: 600; margin-bottom: 4px; }}
.card-value {{ font-size: 16px; font-weight: 800; color: #0f172a; }}
.card-desc {{ font-size: 11px; color: #64748b; margin-top: 3px; }}
.card.ok {{ background: #f0fdf4; border-color: #bbf7d0; }} .card.ok .card-value {{ color: #15803d; }}
.card.warn {{ background: #fffbeb; border-color: #fde68a; }} .card.warn .card-value {{ color: #b45309; }}
.card.alert {{ background: #fef2f2; border-color: #fecaca; }} .card.alert .card-value {{ color: #b91c1c; }}

/* ── KEY–VALUE ── */
.kv {{ display: grid; grid-template-columns: 210px 1fr; gap: 5px 16px; padding: 12px 16px; background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 8px; margin-bottom: 12px; font-size: 12px; }}
.kv .k {{ color: #64748b; font-weight: 500; align-self: start; padding-top: 1px; }}
.kv .v {{ font-weight: 600; color: #0f172a; word-break: break-word; }}

/* ── TABLES ── */
table {{ width: 100%; border-collapse: collapse; margin-bottom: 14px; font-size: 12px; page-break-inside: auto; }}
th {{ background: #f1f5f9; color: #475569; font-weight: 700; border-bottom: 2px solid #cbd5e1; font-size: 10px; text-transform: uppercase; letter-spacing: 0.4px; padding: 7px 10px; text-align: left; }}
td {{ border-bottom: 1px solid #eef0f2; padding: 7px 10px; vertical-align: top; }}
tr:nth-child(even) td {{ background: #fafbfc; }}

/* ── BADGES ── */
.badge {{ display: inline-block; padding: 2px 7px; border-radius: 10px; font-size: 10px; font-weight: 700; letter-spacing: 0.2px; white-space: nowrap; }}
.badge-ok {{ background: #dcfce7; color: #15803d; }}
.badge-ko {{ background: #fee2e2; color: #b91c1c; }}
.badge-warn {{ background: #fef3c7; color: #b45309; }}
.badge-neu {{ background: #f1f5f9; color: #475569; }}

/* ── PROGRESS BARS ── */
.bar {{ height: 5px; background: #e2e8f0; border-radius: 3px; overflow: hidden; margin-top: 3px; }}
.bar > span {{ display: block; height: 100%; border-radius: 3px; background: #0069E2; }}
.bar.warn > span {{ background: #f59e0b; }}
.bar.alert > span {{ background: #ef4444; }}

/* ── FOOTER ── */
.footer {{ margin: 36px 40px 0; padding-top: 16px; border-top: 1px solid #e2e8f0; display: flex; justify-content: space-between; align-items: center; font-size: 10px; color: #94a3b8; }}
.footer-brand {{ font-weight: 700; color: #0069E2; font-size: 11px; }}
</style>
</head>
<body>
<div class='page'>
<div class='topbar'></div>
<div class='header-wrap'>
  <div class='header-logo'>
    <div class='logo-icon'>
      <svg viewBox='0 0 24 24' xmlns='http://www.w3.org/2000/svg'>
        <path d='M12 2a10 10 0 1 0 0 20A10 10 0 0 0 12 2zm1 15h-2v-6h2v6zm0-8h-2V7h2v2z'/>
      </svg>
    </div>
    <div class='logo-text'>
      <div class='brand'>Assistools</div>
      <div class='tagline'>Diagnostic technique professionnel</div>
    </div>
  </div>
  <div class='header-title'>Rapport d'analyse système</div>
  <div class='header-sub'>Fiche technique complète générée automatiquement par Assistools · Assistouest Informatique</div>
  <div class='header-meta'>
    <div class='header-meta-item'><div class='label'>Généré le</div><div class='value'>{Escape(report.GeneratedAt)}</div></div>
    <div class='header-meta-item'><div class='label'>Ordinateur</div><div class='value'>{Escape(report.OS.ComputerName)}</div></div>
    <div class='header-meta-item'><div class='label'>Utilisateur</div><div class='value'>{Escape(report.OS.CurrentUser?.Split('\\') is string[] p && p.Length > 1 ? p[1] : report.OS.CurrentUser)}</div></div>
    <div class='header-meta-item'><div class='label'>Domaine / Groupe</div><div class='value'>{Escape(string.IsNullOrEmpty(report.OS.Domain) ? "WORKGROUP" : report.OS.Domain)}</div></div>
    <div class='header-meta-item'><div class='label'>Système</div><div class='value'>{Escape(report.OS.Name)}</div></div>
  </div>
</div>

<div class='body'>

<div class='toc'>
  <div class='toc-title'>Sommaire</div>
  <div class='toc-grid'>
    <div class='toc-item'><span class='toc-num'>01.</span>Système d'exploitation</div>
    <div class='toc-item'><span class='toc-num'>02.</span>Sécurité Windows</div>
    <div class='toc-item'><span class='toc-num'>03.</span>Processeur (CPU)</div>
    <div class='toc-item'><span class='toc-num'>04.</span>Carte mère &amp; BIOS/UEFI</div>
    <div class='toc-item'><span class='toc-num'>05.</span>Mémoire RAM</div>
    <div class='toc-item'><span class='toc-num'>06.</span>Cartes graphiques</div>
    <div class='toc-item'><span class='toc-num'>07.</span>Disques &amp; stockage</div>
    <div class='toc-item'><span class='toc-num'>08.</span>Volumes &amp; partitions</div>
    <div class='toc-item'><span class='toc-num'>09.</span>Réseau</div>
    <div class='toc-item'><span class='toc-num'>10.</span>Composants intégrés</div>
    <div class='toc-item'><span class='toc-num'>11.</span>Mises à jour Windows</div>
    <div class='toc-item'><span class='toc-num'>12.</span>Applications installées</div>
  </div>
</div>");

        // ── SECTION 1 — OS ────────────────────────────────────────────────────
        sb.Append($@"
<h2 class='section'>01. Système d'exploitation</h2>
<div class='kv'>
<div class='k'>Édition</div><div class='v'>{Escape(report.OS.Edition ?? report.OS.Name)}</div>
<div class='k'>Version / Build</div><div class='v'>{Escape(report.OS.Version)} (build {Escape(report.OS.Build)})</div>
<div class='k'>Architecture</div><div class='v'>{Escape(report.OS.Arch)}</div>
{KvRow("Date d'installation", report.OS.InstallDate)}
{KvRow("Dernier démarrage", report.OS.LastBootTime)}
{KvRow("Temps de fonctionnement", report.OS.Uptime)}
{KvRow("Fuseau horaire", report.OS.TimeZone)}
<div class='k'>Nom de l'ordinateur</div><div class='v'>{Escape(report.OS.ComputerName)}</div>
{KvRow("Utilisateur actif", report.OS.CurrentUser)}
</div>");

        // ── SECTION 2 — SÉCURITÉ ─────────────────────────────────────────────
        var s = report.Security ?? new SecurityInfo();
        bool allFw = s.FirewallDomain && s.FirewallPrivate && s.FirewallPublic;
        bool anyFw = s.FirewallDomain || s.FirewallPrivate || s.FirewallPublic;
        sb.Append($@"
<h2 class='section'>02. Sécurité Windows</h2>
<div class='cards cards-3'>
<div class='card {(s.DefenderEnabled ? "ok" : "alert")}'>
  <div class='card-label'>Windows Defender</div>
  <div class='card-value'>{(s.DefenderEnabled ? "Activé" : "Désactivé")}</div>
  <div class='card-desc'>{(s.RealtimeProtection ? "Protection temps réel active" : "Protection temps réel inactive")}</div>
</div>
<div class='card {(allFw ? "ok" : anyFw ? "warn" : "alert")}'>
  <div class='card-label'>Pare-feu Windows</div>
  <div class='card-value'>{(allFw ? "Actif partout" : anyFw ? "Partiel" : "Désactivé")}</div>
  <div class='card-desc'>Domaine {Badge(s.FirewallDomain)} Privé {Badge(s.FirewallPrivate)} Public {Badge(s.FirewallPublic)}</div>
</div>
<div class='card {(s.UacEnabled ? "ok" : "alert")}'>
  <div class='card-label'>UAC</div>
  <div class='card-value'>{(s.UacEnabled ? "Activé" : "Désactivé")}</div>
  <div class='card-desc'>Contrôle de compte utilisateur</div>
</div>
<div class='card {(s.SecureBootEnabled ? "ok" : "warn")}'>
  <div class='card-label'>Secure Boot</div>
  <div class='card-value'>{(s.SecureBootEnabled ? "Activé" : "Désactivé")}</div>
  <div class='card-desc'>Mode : {Escape(report.Bios?.Mode ?? "—")}</div>
</div>
<div class='card {(s.TpmStatus.Contains("Activé", StringComparison.OrdinalIgnoreCase) ? "ok" : "warn")}'>
  <div class='card-label'>Module TPM</div>
  <div class='card-value'>{Escape(s.TpmStatus)}</div>
  <div class='card-desc'>{Escape(report.Bios?.TpmVersion?.Trim('\0'))}</div>
</div>
<div class='card {(s.AntivirusUpToDate ? "ok" : "warn")}'>
  <div class='card-label'>Définitions Defender</div>
  <div class='card-value'>{(s.AntivirusUpToDate ? "À jour" : "Obsolètes")}</div>
  <div class='card-desc'>Mise à jour : {Escape(s.SignaturesLastUpdated)}</div>
</div>
</div>
<div class='kv'>
{KvRow("Version moteur Defender", s.DefenderEngineVersion)}
{KvRow("Dernière analyse rapide", string.IsNullOrEmpty(s.LastQuickScan) ? "Jamais réalisée" : s.LastQuickScan)}
{KvRow("Dernière analyse complète", string.IsNullOrEmpty(s.LastFullScan) ? "Jamais réalisée" : s.LastFullScan)}
{KvRow("Antivirus tiers détecté", string.IsNullOrEmpty(s.ThirdPartyAntivirus) ? null : s.ThirdPartyAntivirus)}
</div>");

        // ── SECTION 3 — CPU ───────────────────────────────────────────────────
        var c = report.CPU;
        sb.Append($@"
<h2 class='section'>03. Processeur (CPU)</h2>
<div class='kv'>
<div class='k'>Modèle</div><div class='v'>{Escape(c.Name)}</div>
{KvRow("Fabricant", c.Manufacturer)}
{KvRow("Architecture", c.Architecture)}
<div class='k'>Cœurs / Threads</div><div class='v'>{c.Cores} cœurs physiques / {c.Threads} threads logiques</div>
<div class='k'>Fréquence max</div><div class='v'>{c.MaxSpeed} MHz</div>
{KvRow("Socket", c.Socket)}
{(c.L2CacheKB > 0 || c.L3CacheKB > 0 ? $"<div class='k'>Cache L2 / L3</div><div class='v'>{(c.L2CacheKB > 0 ? $"{c.L2CacheKB} Ko" : "—")} / {(c.L3CacheKB > 0 ? $"{c.L3CacheKB} Ko" : "—")}</div>" : "")}
<div class='k'>Virtualisation (VT-x / AMD-V)</div><div class='v'>{(c.VirtualizationEnabled ? "Activée dans le firmware" : "Désactivée ou non supportée")}</div>
</div>");

        // ── SECTION 4 — CARTE MÈRE / BIOS ────────────────────────────────────
        var b = report.Bios ?? new BiosInfo();
        sb.Append($@"
<h2 class='section'>04. Carte mère &amp; BIOS/UEFI</h2>
<div class='kv'>
<div class='k'>Fabricant</div><div class='v'>{Escape(report.Motherboard.Manufacturer)}</div>
<div class='k'>Modèle</div><div class='v'>{Escape(report.Motherboard.Product)}</div>
{KvRow("Version", report.Motherboard.Version)}
{KvRow("N° de série", report.Motherboard.SerialNumber)}
<div class='k'>BIOS — Fabricant / Version</div><div class='v'>{Escape(b.Manufacturer)} · {Escape(b.Version)}</div>
{KvRow("BIOS — Date", b.ReleaseDate)}
{KvRow("BIOS — N° de série", b.SerialNumber)}
<div class='k'>Mode démarrage</div><div class='v'>{Escape(b.Mode)}</div>
{KvRow("Version SMBIOS", b.SmbiosVersion)}
<div class='k'>TPM</div><div class='v'>{(b.TpmReady ? "Activé et prêt" : b.TpmPresent ? "Présent (non configuré)" : "Absent")}{(string.IsNullOrWhiteSpace(b.TpmVersion?.Trim('\0')) ? "" : $" · v{Escape(b.TpmVersion.Trim('\0'))}")}</div>
<div class='k'>Secure Boot</div><div class='v'>{(b.SecureBootEnabled ? "Activé" : "Désactivé")}</div>
</div>");

        // ── SECTION 5 — RAM ───────────────────────────────────────────────────
        sb.Append($@"
<h2 class='section'>05. Mémoire RAM</h2>
<div class='cards cards-3'>
<div class='card'><div class='card-label'>Total installé</div><div class='card-value'>{FormaterTaille(totalRamBytes)}</div><div class='card-desc'>{ddrType} @ {ramSpeed} MHz</div></div>
<div class='card'><div class='card-label'>Emplacements occupés</div><div class='card-value'>{report.RAM.UsedSlots} / {report.RAM.TotalSlots}</div><div class='card-desc'>slots utilisés sur disponibles</div></div>
<div class='card'><div class='card-label'>Type</div><div class='card-value'>{ddrType}</div><div class='card-desc'>Fréquence configurée : {ramSpeed} MHz</div></div>
</div>
<table>
<thead><tr><th>Emplacement</th><th>Fabricant</th><th>Réf. produit</th><th>N° de série</th><th>Capacité</th><th>Vitesse</th><th>Type</th></tr></thead>
<tbody>");
        foreach (var m in report.RAM.Modules)
        {
            int sp = m.ConfiguredClockSpeed > 0 ? m.ConfiguredClockSpeed : m.Speed;
            sb.Append($"<tr><td>{Escape(m.DeviceLocator)}</td><td>{Escape(m.Manufacturer)}</td><td>{Escape(m.PartNumber)}</td><td>{Escape(m.SerialNumber)}</td><td>{FormaterTaille(m.CapacityBytes)}</td><td>{sp} MHz</td><td>{ObtenirDdrType(m)}</td></tr>");
        }
        sb.Append("</tbody></table>");

        // ── SECTION 6 — GPU ───────────────────────────────────────────────────
        sb.Append("<h2 class='section'>06. Cartes graphiques (GPU)</h2><table><thead><tr><th>Modèle</th><th>Fabricant</th><th>VRAM totale</th><th>Résolution active</th><th>Pilote</th><th>Date pilote</th></tr></thead><tbody>");
        foreach (var g in report.GPUs)
        {
            string vram = g.VramBytes > 0 ? FormaterTaille(g.VramBytes) : "Partagée";
            sb.Append($"<tr><td>{Escape(g.Name)}</td><td>{Escape(g.Vendor)}</td><td>{vram}</td><td>{(string.IsNullOrEmpty(g.CurrentResolution) ? "—" : Escape(g.CurrentResolution))}</td><td>{Escape(g.DriverVersion)}</td><td>{Escape(g.DriverDate)}</td></tr>");
        }
        sb.Append("</tbody></table>");

        // ── SECTION 7 — DISQUES ───────────────────────────────────────────────
        sb.Append("<h2 class='section'>07. Disques &amp; stockage (SMART)</h2><table><thead><tr><th>Modèle</th><th>Type</th><th>Bus</th><th>Capacité</th><th>N° série</th><th>Firmware</th><th>Santé</th><th>Heures</th></tr></thead><tbody>");
        foreach (var d in report.Disks)
        {
            string hStr = d.HealthStatus ?? "";
            string hb = hStr.Equals("Healthy", StringComparison.OrdinalIgnoreCase) || hStr.Equals("OK", StringComparison.OrdinalIgnoreCase)
                ? "<span class='badge badge-ok'>OK</span>"
                : $"<span class='badge badge-warn'>{Escape(hStr)}</span>";
            string hours = d.PowerOnHours > 0 ? $"{d.PowerOnHours:N0} h" : "—";
            sb.Append($"<tr><td>{Escape(d.Model)}</td><td>{Escape(d.MediaType)}</td><td>{Escape(d.BusType)}</td><td>{FormaterTaille(d.SizeBytes)}</td><td><code style='font-size:10px'>{Escape(d.SerialNumber)}</code></td><td>{Escape(d.FirmwareVersion)}</td><td>{hb}</td><td>{hours}</td></tr>");
        }
        sb.Append("</tbody></table>");

        // ── SECTION 8 — VOLUMES ───────────────────────────────────────────────
        sb.Append("<h2 class='section'>08. Volumes &amp; partitions</h2><table><thead><tr><th>Lecteur</th><th>Nom de volume</th><th>FS</th><th>Taille</th><th>Libre</th><th>Utilisation</th><th>BitLocker</th></tr></thead><tbody>");
        foreach (var v in report.Volumes)
        {
            double pct = v.SizeBytes > 0 ? (1.0 - ((double)v.FreeBytes / v.SizeBytes)) * 100.0 : 0;
            string barClass = pct > 90 ? "alert" : pct > 75 ? "warn" : "";
            string blBadge = v.BitlockerStatus.Contains("actif", StringComparison.OrdinalIgnoreCase)
                ? "<span class='badge badge-ok'>Chiffré</span>"
                : v.BitlockerStatus.Contains("inactif", StringComparison.OrdinalIgnoreCase)
                    ? "<span class='badge badge-neu'>Disponible</span>"
                    : $"<span class='badge badge-warn'>{Escape(v.BitlockerStatus)}</span>";
            sb.Append($"<tr><td><b>{Escape(v.DriveLetter)}:</b></td><td>{Escape(v.Label)}</td><td>{Escape(v.FileSystem)}</td><td>{FormaterTaille(v.SizeBytes)}</td><td>{FormaterTaille(v.FreeBytes)}</td><td><div class='bar {barClass}'><span style='width:{pct:F0}%'></span></div><span style='font-size:11px'>{pct:F1} %</span></td><td>{blBadge}</td></tr>");
        }
        sb.Append("</tbody></table>");

        // ── SECTION 9 — RÉSEAU ────────────────────────────────────────────────
        sb.Append("<h2 class='section'>09. Réseau</h2>");
        if (report.Network != null && report.Network.Count > 0)
        {
            sb.Append("<table><thead><tr><th>Nom</th><th>Description</th><th>Adresse MAC</th><th>IP</th><th>Passerelle</th><th>DNS</th><th>Débit</th></tr></thead><tbody>");
            foreach (var n in report.Network)
            {
                string nameCell = Escape(n.Name);
                if (!string.IsNullOrEmpty(n.Ssid)) nameCell += $"<br><span style='font-size:10px;color:#64748b'>SSID : {Escape(n.Ssid)}</span>";
                sb.Append($"<tr><td>{nameCell}</td><td style='font-size:11px'>{Escape(n.Description)}</td><td><code style='font-size:10px'>{Escape(n.MacAddress)}</code></td><td>{Escape(n.IpAddress)}</td><td>{Escape(n.Gateway)}</td><td style='font-size:11px'>{Escape(n.DnsServers)}</td><td>{Escape(n.LinkSpeed)}</td></tr>");
            }
            sb.Append("</tbody></table>");
        }
        else sb.Append("<p style='color:#64748b;font-size:12px;margin-bottom:14px'>Aucune connexion réseau active détectée.</p>");

        // ── SECTION 10 — COMPOSANTS INTÉGRÉS ─────────────────────────────────
        if (report.OnboardComponents != null && report.OnboardComponents.Count > 0)
        {
            sb.Append("<h2 class='section'>10. Composants intégrés</h2>");
            sb.Append("<table><thead><tr><th>Catégorie</th><th>Composant</th><th>Fabricant</th><th>Version driver</th><th>PnP Device ID</th><th>État</th></tr></thead><tbody>");
            foreach (var ob in report.OnboardComponents)
            {
                string catBadge = ob.Category switch
                {
                    "Wi-Fi" => "<span class='badge badge-ok'>Wi-Fi</span>",
                    "Ethernet" => "<span class='badge badge-neu'>Ethernet</span>",
                    "Bluetooth" => "<span class='badge' style='background:#ede9fe;color:#6d28d9'>Bluetooth</span>",
                    "Audio" => "<span class='badge' style='background:#fce7f3;color:#be185d'>Audio</span>",
                    _ => $"<span class='badge badge-neu'>{Escape(ob.Category)}</span>"
                };
                string statusBadge = ob.Status.Equals("Actif", StringComparison.OrdinalIgnoreCase) || ob.Status.Equals("OK", StringComparison.OrdinalIgnoreCase)
                    ? "<span class='badge badge-ok'>OK</span>"
                    : $"<span class='badge badge-warn'>{Escape(ob.Status)}</span>";
                sb.Append($"<tr><td>{catBadge}</td><td>{Escape(ob.Name)}</td><td style='font-size:11px'>{Escape(ob.Manufacturer)}</td><td style='font-size:11px'>{Escape(ob.DriverVersion)}</td><td style='font-size:10px;color:#64748b'><code>{Escape(ob.PnpDeviceId)}</code></td><td>{statusBadge}</td></tr>");
            }
            sb.Append("</tbody></table>");
        }

        // ── SECTION 11 — BATTERIE (si présente) ──────────────────────────────
        if (report.Battery != null && !string.IsNullOrEmpty(report.Battery.Name))
        {
            var bat = report.Battery;
            string healthClass = bat.HealthPercent >= 80 ? "ok" : bat.HealthPercent >= 60 ? "warn" : "alert";
            sb.Append($@"
<h2 class='section'>10. Batterie</h2>
<div class='cards cards-3'>
<div class='card {healthClass}'><div class='card-label'>État de santé</div><div class='card-value'>{bat.HealthPercent} %</div><div class='card-desc'>Cycles de charge : {(bat.CycleCount > 0 ? bat.CycleCount.ToString() : "—")}</div></div>
<div class='card'><div class='card-label'>Capacité réelle</div><div class='card-value'>{bat.FullChargeCapacityMwh:N0} mWh</div><div class='card-desc'>Capacité de conception : {bat.DesignCapacityMwh:N0} mWh</div></div>
<div class='card'><div class='card-label'>État de charge</div><div class='card-value'>{Escape(bat.ChargeStatus)}</div><div class='card-desc'>Autonomie estimée : {(bat.EstimatedRuntimeMin > 0 && bat.EstimatedRuntimeMin < 1000 ? $"{bat.EstimatedRuntimeMin} min" : "—")}</div></div>
</div>");
        }

        // ── SECTION MAINTENANCE ────────────────────────────────────────────────
        int secNum = (report.Battery != null && !string.IsNullOrEmpty(report.Battery?.Name)) ? 11 : 10;
        if (report.OnboardComponents != null && report.OnboardComponents.Count > 0) secNum++;
        var mh = report.Maintenance;
        bool hasMaint = mh != null && (!string.IsNullOrEmpty(mh.DerniereMaintenance) || mh.OctetsLiberes > 0 || mh.Operations?.Count > 0);
        if (hasMaint)
        {
            sb.Append($"<h2 class='section'>{secNum:D2}. Maintenance</h2>");

            // Cartes résumé
            string maintClass = string.IsNullOrEmpty(mh!.DerniereMaintenance) ? "warn" : "ok";
            string cleanDesc = mh.OctetsLiberes > 0
                ? $"{mh.FichiersSupprimes} fichier(s) · {NettoyageService.FormatTaille(mh.OctetsLiberes)} libéré(s)"
                : "Aucun nettoyage cette session";
            sb.Append($@"<div class='cards cards-3'>
<div class='card {maintClass}'>
  <div class='card-label'>Dernière maintenance</div>
  <div class='card-value'>{Escape(string.IsNullOrEmpty(mh.DerniereMaintenance) ? "Jamais" : mh.DerniereMaintenance)}</div>
</div>
<div class='card {(mh.OctetsLiberes > 0 ? "ok" : "")}'>
  <div class='card-label'>Nettoyage disque</div>
  <div class='card-value'>{(mh.OctetsLiberes > 0 ? NettoyageService.FormatTaille(mh.OctetsLiberes) : "—")}</div>
  <div class='card-desc'>{cleanDesc}</div>
</div>
<div class='card {(mh.ReparationSfc || mh.ReparationDism ? "ok" : "")}'>
  <div class='card-label'>Réparation système</div>
  <div class='card-value'>{((mh.ReparationSfc || mh.ReparationDism) ? "Effectuée" : "Non réalisée")}</div>
  <div class='card-desc'>{(mh.ReparationSfc ? "SFC " : "")}{(mh.ReparationDism ? "DISM" : "")}</div>
</div>
</div>");

            // Scan antivirus si présent
            if (!string.IsNullOrEmpty(mh.ScanAntivirus))
                sb.Append($"<div class='kv'><div class='k'>Scan antivirus</div><div class='v'>{Escape(mh.ScanAntivirus)}</div></div>");

            // Liste des opérations
            if (mh.Operations != null && mh.Operations.Count > 0)
            {
                sb.Append("<table><thead><tr><th>#</th><th>Opération effectuée</th></tr></thead><tbody>");
                int idx = 1;
                foreach (var op in mh.Operations)
                    sb.Append($"<tr><td style='color:#94a3b8;font-size:11px'>{idx++}</td><td>{Escape(op)}</td></tr>");
                sb.Append("</tbody></table>");
            }
            secNum++;
        }

        // ── SECTION MISES À JOUR ───────────────────────────────────────────────
        sb.Append($"<h2 class='section'>{secNum:D2}. Mises à jour Windows récentes</h2>");
        if (report.RecentUpdates != null && report.RecentUpdates.Count > 0)
        {
            sb.Append("<table><thead><tr><th>KB</th><th>Description</th><th>Date d'installation</th></tr></thead><tbody>");
            foreach (var u in report.RecentUpdates)
                sb.Append($"<tr><td><span class='badge badge-neu'>{Escape(u.KbArticle)}</span></td><td>{Escape(u.Title)}</td><td>{Escape(u.InstalledOn)}</td></tr>");
            sb.Append("</tbody></table>");
        }
        else sb.Append("<p style='color:#64748b;font-size:12px;margin-bottom:14px'>Aucune mise à jour récente détectée.</p>");

        // ── SECTION 11/12 — APPLICATIONS ──────────────────────────────────────
        secNum++;
        if (report.InstalledApps != null && report.InstalledApps.Count > 0)
        {
            sb.Append($"<h2 class='section'>{secNum:D2}. Applications installées <span style='font-size:13px;font-weight:500;color:#64748b'>({report.InstalledApps.Count})</span></h2>");
            sb.Append("<table><thead><tr><th>Application</th><th>Version</th><th>Éditeur</th><th>Installée le</th></tr></thead><tbody>");
            foreach (var a in report.InstalledApps)
                sb.Append($"<tr><td>{Escape(a.Name)}</td><td style='font-size:11px;color:#64748b'>{Escape(a.Version)}</td><td style='font-size:11px;color:#64748b'>{Escape(a.Publisher)}</td><td style='font-size:11px;color:#94a3b8'>{Escape(a.InstallDate)}</td></tr>");
            sb.Append("</tbody></table>");
        }

        // ── FOOTER ─────────────────────────────────────────────────────────────
        sb.Append($@"
</div>
<div class='footer'>
  <div><span class='footer-brand'>Assistools</span> · Assistouest Informatique · Rapport généré le {Escape(report.GeneratedAt)}</div>
  <div>© 2026 Assistouest Informatique — Document confidentiel à usage technique</div>
</div>
</div>
</body></html>");

        return sb.ToString();
    }

    private static string Escape(string? s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return WebUtility.HtmlEncode(s);
    }

    private static string Badge(bool ok) =>
        ok ? "<span class='badge badge-ok'>ON</span>" : "<span class='badge badge-ko'>OFF</span>";

    private static string RunPowerShellCapture(string script)
    {
        // Préfixe : force UTF-8 en sortie pour préserver accents et caractères spéciaux dans le JSON
        const string utf8Prefix = "[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new()\r\n$OutputEncoding = [System.Text.UTF8Encoding]::new()\r\n";
        var tmp = Path.GetTempFileName() + ".ps1";
        File.WriteAllText(tmp, utf8Prefix + script, new UTF8Encoding(true));
        try
        {
            var psi = new ProcessStartInfo("powershell.exe",
                $"-NonInteractive -NoProfile -ExecutionPolicy Bypass -File \"{tmp}\"")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };
            using var proc = Process.Start(psi)!;
            var output = proc.StandardOutput.ReadToEnd();
            var err = proc.StandardError.ReadToEnd();
            proc.WaitForExit(60_000);
            if (!string.IsNullOrWhiteSpace(err))
            {
                LogStore.Append($"[RAPPORT] PowerShell stderr : {err.Substring(0, Math.Min(500, err.Length))}");
            }
            return output;
        }
        finally { try { File.Delete(tmp); } catch { } }
    }
}
