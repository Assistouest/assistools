using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace Assistools.Services;

// ─────────────────────────────────────────────────────────────────────────────
// ENUMS & CONFIG
// ─────────────────────────────────────────────────────────────────────────────

public enum GameBoosterProfile
{
    Competitif,  // Extrême : esport / compétitif, latence zéro, tout sacrifié
    Gamer,       // Équilibré gaming : AAA, puissance + stabilité
    Streamer,    // Gaming + stream OBS : puissance sans casser l'encodage
    Custom       // Manuel : contrôle total
}

public class GameBoosterProfileConfig
{
    // Power
    public string PowerPlan           { get; set; } = "HighPerformance";
    public bool   DisablePowerThrottling { get; set; }
    public bool   DisableCoreParking  { get; set; }

    // CPU Scheduling
    public int  Win32PrioritySeparation { get; set; } = 0x26;
    public int  SystemResponsiveness   { get; set; } = 10;

    // Network
    public bool DisableNagle           { get; set; }
    public bool DisableNetworkThrottling { get; set; }

    // GPU / Multimedia
    public int    GpuPriority          { get; set; } = 8;
    public int    CpuSchedulerPriority { get; set; } = 6;
    public string SchedulingCategory   { get; set; } = "High";

    // Memory
    public bool DisablePagingExecutive { get; set; }

    // Features
    public bool EnableGameMode         { get; set; } = true;
    public bool DisableXboxGameBar     { get; set; } = true;
    public bool SetTimerResolution     { get; set; }
    public bool SetHighPriority        { get; set; } = true;

    // Services
    public List<string> ServicesToStop { get; set; } = new();
}

// ─────────────────────────────────────────────────────────────────────────────
// SERVICE
// ─────────────────────────────────────────────────────────────────────────────

public class GameBoosterService : IDisposable
{
    // ── P/Invoke ──────────────────────────────────────────────────────────────
    [DllImport("ntdll.dll")] private static extern int NtSetTimerResolution(int desired, bool set, out int current);
    [DllImport("ntdll.dll")] private static extern int NtQueryTimerResolution(out int min, out int max, out int current);

    // ── Constantes ────────────────────────────────────────────────────────────
    private const string GuidUltimate    = "e9a42b02-d5df-448d-aa00-03f14749eb61";
    private const string GuidHighPerf    = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";
    private const string GuidBalanced    = "381b4222-f694-41f0-9685-ff5bb260df2e";

    private const string RegMultimedia   = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile";
    private const string RegGames        = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games";
    private const string RegPriority     = @"SYSTEM\CurrentControlSet\Control\PriorityControl";
    private const string RegPowerThrot   = @"SYSTEM\CurrentControlSet\Control\Power\PowerThrottling";
    private const string RegMemMgmt      = @"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management";
    private const string RegNetIfaces    = @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces";
    private const string RegGameBar      = @"Software\Microsoft\GameBar";
    private const string RegGameDvr      = @"Software\Microsoft\Windows\CurrentVersion\GameDVR";

    // ── Profils prédéfinis ────────────────────────────────────────────────────
    private static readonly Dictionary<GameBoosterProfile, GameBoosterProfileConfig> Profiles = new()
    {
        // ── COMPÉTITIF EXTRÊME ────────────────────────────────────────────────
        // Tout sacrifié pour les FPS et la latence minimale.
        // Utilisé par les pros esport : CS2, Valorant, Apex, CoD.
        [GameBoosterProfile.Competitif] = new()
        {
            PowerPlan                = "UltimatePerformance",
            DisablePowerThrottling   = true,
            DisableCoreParking       = true,
            Win32PrioritySeparation  = 0x26,   // boost max foreground + quantum court
            SystemResponsiveness     = 0,       // 100% CPU pour le jeu
            DisableNagle             = true,    // -5 à -15ms réseau
            DisableNetworkThrottling = true,
            GpuPriority              = 8,
            CpuSchedulerPriority     = 6,
            SchedulingCategory       = "High",
            DisablePagingExecutive   = true,    // kernel en RAM
            EnableGameMode           = true,
            DisableXboxGameBar       = true,
            SetTimerResolution       = true,    // 0.5ms timer
            SetHighPriority          = true,
            ServicesToStop           = new()
            {
                "SysMain",         // SuperFetch — inutile si SSD NVMe
                "WSearch",         // Indexation — pics CPU
                "Spooler",         // Print spooler
                "Fax",
                "RemoteRegistry",
                "TabletInputService",
                "WerSvc",          // Windows Error Reporting
                "MapsBroker",
                "lfsvc",           // Geolocation
                "DiagTrack",       // Telemetry Microsoft
                "RetailDemo",
                "WMPNetworkSvc",
                "OneSyncSvc",
                "XblGameSave",     // Xbox save sync
                "XblAuthManager",
                "XboxNetApiSvc",
                "WbioSrvc",        // Biometrics
                "PhoneSvc",
                "CDPSvc",          // Connected Devices Platform
                "PcaSvc",          // Program Compatibility Assistant
                "WpnService",      // Push notifications
                "SCardSvr",        // Smart Card
                "SEMgrSvc",        // Payments NFC
            }
        },

        // ── GAMER ─────────────────────────────────────────────────────────────
        // Optimisation gaming sérieuse sans sacrifier la stabilité.
        // Idéal : AAA (Cyberpunk, Elden Ring), sessions longues.
        [GameBoosterProfile.Gamer] = new()
        {
            PowerPlan                = "HighPerformance",
            DisablePowerThrottling   = true,
            DisableCoreParking       = false,
            Win32PrioritySeparation  = 0x26,
            SystemResponsiveness     = 10,
            DisableNagle             = true,
            DisableNetworkThrottling = true,
            GpuPriority              = 8,
            CpuSchedulerPriority     = 6,
            SchedulingCategory       = "High",
            DisablePagingExecutive   = false,
            EnableGameMode           = true,
            DisableXboxGameBar       = true,
            SetTimerResolution       = false,
            SetHighPriority          = true,
            ServicesToStop           = new()
            {
                "SysMain",
                "WSearch",
                "Spooler",
                "Fax",
                "RemoteRegistry",
                "TabletInputService",
                "WerSvc",
                "MapsBroker",
                "lfsvc",
                "DiagTrack",
                "RetailDemo",
                "WMPNetworkSvc",
                "OneSyncSvc",
            }
        },

        // ── STREAMER ─────────────────────────────────────────────────────────
        // Gaming + encodage OBS simultané.
        // Préserve les ressources pour l'encodage x264/NVENC, réseau stable.
        [GameBoosterProfile.Streamer] = new()
        {
            PowerPlan                = "HighPerformance",
            DisablePowerThrottling   = false,
            DisableCoreParking       = false,
            Win32PrioritySeparation  = 0x16,   // boost modéré — laisse du CPU à OBS
            SystemResponsiveness     = 20,     // 80% jeu / 20% background (OBS)
            DisableNagle             = false,  // réseau stable pour l'upload stream
            DisableNetworkThrottling = true,
            GpuPriority              = 8,
            CpuSchedulerPriority     = 6,
            SchedulingCategory       = "High",
            DisablePagingExecutive   = false,
            EnableGameMode           = true,
            DisableXboxGameBar       = false,  // peut être utile pour certains capteurs
            SetTimerResolution       = false,
            SetHighPriority          = true,
            ServicesToStop           = new()
            {
                "Fax",
                "RemoteRegistry",
                "TabletInputService",
                "MapsBroker",
                "lfsvc",
                "DiagTrack",
                "RetailDemo",
                "SCardSvr",
                "SEMgrSvc",
            }
        },

        // Custom : géré dynamiquement depuis _customConfig
    };

    // ── State ─────────────────────────────────────────────────────────────────
    private bool              _isMonitoring;
    private bool              _disposed;
    private GameBoosterProfile _activeProfile = GameBoosterProfile.Gamer;
    private GameBoosterProfileConfig _customConfig;

    // Sauvegardes pour la restauration
    private Guid?                          _originalPowerPlan;
    private int                            _originalTimerResolution;
    private readonly Dictionary<string, object?> _registrySaves = new();
    private readonly Dictionary<string, (object? ack, object? nodelay)> _nagleSaves = new();
    private readonly Dictionary<string, ServiceState> _serviceSaves = new();
    private readonly Dictionary<int, GameProcessInfo> _activeGames = new();

    private ManagementEventWatcher? _startWatcher;
    private ManagementEventWatcher? _stopWatcher;

    private readonly List<string> _gameProcessNames = new()
    {
        "cs2", "csgo", "valorant", "leagueoflegends", "dota2", "apex",
        "fortnite", "battlefront2", "codmw", "codwarzone", "overwatch",
        "overwatch2", "r5apex", "eldenring", "cyberpunk2077", "witcher3",
        "rdr2", "gtav", "gta5", "sekiro", "darksouls3", "hogwartslegacy",
        "starfield", "baldursgate3", "bg3", "helldivers2", "palworld",
        "steam", "epicgameslauncher", "origin", "upc", "battle.net",
    };

    // ── Ctor ──────────────────────────────────────────────────────────────────
    public GameBoosterService()
    {
        _customConfig = BuildDefaultCustomConfig();
    }

    private static GameBoosterProfileConfig BuildDefaultCustomConfig() => new()
    {
        PowerPlan                = "HighPerformance",
        DisablePowerThrottling   = true,
        DisableCoreParking       = false,
        Win32PrioritySeparation  = 0x26,
        SystemResponsiveness     = 10,
        DisableNagle             = true,
        DisableNetworkThrottling = true,
        GpuPriority              = 8,
        CpuSchedulerPriority     = 6,
        SchedulingCategory       = "High",
        DisablePagingExecutive   = false,
        EnableGameMode           = true,
        DisableXboxGameBar       = true,
        SetTimerResolution       = false,
        SetHighPriority          = true,
        ServicesToStop           = new(Profiles[GameBoosterProfile.Gamer].ServicesToStop),
    };

    // ── Propriétés publiques ──────────────────────────────────────────────────
    public bool IsMonitoring => _isMonitoring;
    public GameBoosterProfile ActiveProfile
    {
        get => _activeProfile;
        set
        {
            if (_isMonitoring)
            {
                Log("[WARN] Changement de profil ignoré : booster actif.");
                return;
            }
            _activeProfile = value;
        }
    }
    public GameBoosterProfileConfig CustomConfig => _customConfig;
    public List<string> GameProcessNames => _gameProcessNames;

    public event EventHandler<string>? OnLog;
    public event EventHandler<bool>?   OnMonitoringStateChanged;
    public event EventHandler<string>? OnGameStarted;
    public event EventHandler<string>? OnGameStopped;

    // ── Start / Stop ──────────────────────────────────────────────────────────
    public void StartMonitoring()
    {
        if (_isMonitoring) return;

        try
        {
            var cfg = GetConfig();
            Log($"━━ Démarrage GameBooster — Profil : {_activeProfile} ━━");

            _originalPowerPlan = PowerPlanApi.GetActive();
            Log($"  Plan original sauvegardé : {PowerPlanApi.GetFriendlyName(_originalPowerPlan!.Value)}");

            ApplyPowerPlan(cfg);
            ApplyRegistryTweaks(cfg);
            ApplyNetworkTweaks(cfg);
            ApplyTimerResolution(cfg);
            ApplyServices(cfg);
            ApplyGameModeFeatures(cfg);

            StartProcessMonitoring();

            _isMonitoring = true;
            OnMonitoringStateChanged?.Invoke(this, true);
            Log($"━━ ✓ GameBooster actif ━━");
        }
        catch (UnauthorizedAccessException ex)
        {
            Log($"[ERREUR] Accès refusé — lancez l'application en administrateur. {ex.Message}");
            throw;
        }
        catch (Exception ex)
        {
            Log($"[ERREUR] Démarrage : {ex.Message}");
            throw;
        }
    }

    public void StopMonitoring()
    {
        if (!_isMonitoring) return;

        try
        {
            Log("━━ Arrêt GameBooster — Restauration du système ━━");

            StopProcessMonitoring();

            foreach (var kv in _activeGames)
                RestoreGameProcess(kv.Value);
            _activeGames.Clear();

            RestoreServices();
            RestoreRegistryTweaks();
            RestoreNetworkTweaks();
            RestoreTimerResolution();

            if (_originalPowerPlan.HasValue)
            {
                PowerPlanApi.SetActive(_originalPowerPlan.Value);
                Log($"  ✓ Plan d'alimentation restauré : {PowerPlanApi.GetFriendlyName(_originalPowerPlan.Value)}");
            }

            _isMonitoring = false;
            OnMonitoringStateChanged?.Invoke(this, false);
            Log("━━ ✓ Système restauré ━━");
        }
        catch (Exception ex)
        {
            Log($"[ERREUR] Arrêt : {ex.Message}");
        }
    }

    // ── Application des tweaks ────────────────────────────────────────────────

    private void ApplyPowerPlan(GameBoosterProfileConfig cfg)
    {
        try
        {
            if (cfg.PowerPlan == "UltimatePerformance")
            {
                var ultimateGuid = new Guid(GuidUltimate);
                if (!PowerPlanApi.SetActive(ultimateGuid))
                {
                    // Créer le plan Ultimate Performance (masqué par défaut sur Windows)
                    Log("  Plan Ultime absent — création en cours...");
                    var output = RunProcess("powercfg", $"/duplicatescheme {GuidUltimate}");
                    // powercfg retourne : "Power Scheme GUID: <new-guid>  (Ultimate Performance)"
                    var match = System.Text.RegularExpressions.Regex.Match(
                        output, @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}");
                    if (match.Success && Guid.TryParse(match.Value, out var newGuid))
                    {
                        PowerPlanApi.SetActive(newGuid);
                        Log($"  ✓ Plan Ultime Performance créé et activé ({newGuid})");
                    }
                    else
                    {
                        Log($"  [WARN] Plan Ultime impossible à créer (output: {output.Trim()}) → High Performance");
                        PowerPlanApi.SetActive(new Guid(GuidHighPerf));
                    }
                }
                else
                {
                    Log("  ✓ Plan Ultime Performance activé");
                }
            }
            else
            {
                var guid = cfg.PowerPlan == "HighPerformance"
                    ? new Guid(GuidHighPerf)
                    : new Guid(GuidBalanced);
                PowerPlanApi.SetActive(guid);
                Log($"  ✓ Plan d'alimentation : {cfg.PowerPlan}");
            }
        }
        catch (Exception ex) { Log($"  [WARN] Plan d'alimentation : {ex.Message}"); }
    }

    private void ApplyRegistryTweaks(GameBoosterProfileConfig cfg)
    {
        try
        {
            // ── Multimedia SystemProfile ──────────────────────────────────────
            using var mm = Registry.LocalMachine.OpenSubKey(RegMultimedia, true);
            if (mm != null)
            {
                SaveAndSet(mm, "SystemResponsiveness",   cfg.SystemResponsiveness);
                SaveAndSet(mm, "NetworkThrottlingIndex", cfg.DisableNetworkThrottling ? unchecked((int)0xFFFFFFFF) : 10);
                Log($"  ✓ SystemResponsiveness = {cfg.SystemResponsiveness}");
                Log($"  ✓ NetworkThrottlingIndex = {(cfg.DisableNetworkThrottling ? "désactivé" : "10")}");
            }

            // ── Tasks\Games ───────────────────────────────────────────────────
            using var games = Registry.LocalMachine.CreateSubKey(RegGames);
            if (games != null)
            {
                SaveAndSet(games, "GPU Priority",         cfg.GpuPriority);
                SaveAndSet(games, "Priority",             cfg.CpuSchedulerPriority);
                SaveAndSet(games, "Scheduling Category",  cfg.SchedulingCategory);
                SaveAndSet(games, "SFIO Priority",        "High");
                SaveAndSet(games, "Background Only",      "False");
                SaveAndSet(games, "Background Priority",  1);
                Log($"  ✓ GPU Priority={cfg.GpuPriority}, CPU Priority={cfg.CpuSchedulerPriority}, Scheduling={cfg.SchedulingCategory}");
            }

            // ── Win32PrioritySeparation ───────────────────────────────────────
            using var prio = Registry.LocalMachine.OpenSubKey(RegPriority, true);
            if (prio != null)
            {
                SaveAndSet(prio, "Win32PrioritySeparation", cfg.Win32PrioritySeparation);
                Log($"  ✓ Win32PrioritySeparation = 0x{cfg.Win32PrioritySeparation:X2}");
            }

            // ── Power Throttling ──────────────────────────────────────────────
            if (cfg.DisablePowerThrottling)
            {
                using var pt = Registry.LocalMachine.CreateSubKey(RegPowerThrot);
                if (pt != null)
                {
                    SaveAndSet(pt, "PowerThrottlingOff", 1);
                    Log("  ✓ Power Throttling désactivé");
                }
            }

            // ── Memory Management ─────────────────────────────────────────────
            using var mem = Registry.LocalMachine.OpenSubKey(RegMemMgmt, true);
            if (mem != null)
            {
                if (cfg.DisablePagingExecutive)
                {
                    SaveAndSet(mem, "DisablePagingExecutive", 1);
                    Log("  ✓ Kernel en RAM (DisablePagingExecutive=1)");
                }
                // Désactiver LargeSystemCache pour que la RAM aille aux apps
                SaveAndSet(mem, "LargeSystemCache", 0);
            }

            // ── Game Mode & Xbox DVR ──────────────────────────────────────────
            using var gameBar = Registry.CurrentUser.OpenSubKey(RegGameBar, true);
            if (gameBar != null && cfg.EnableGameMode)
            {
                SaveAndSet(gameBar, "AllowAutoGameMode",   1);
                SaveAndSet(gameBar, "AutoGameModeEnabled", 1);
                Log("  ✓ Game Mode activé");
            }

            if (cfg.DisableXboxGameBar)
            {
                using var dvr = Registry.CurrentUser.OpenSubKey(RegGameDvr, true);
                if (dvr != null)
                {
                    SaveAndSet(dvr, "AppCaptureEnabled", 0);
                    Log("  ✓ Xbox Game Bar/DVR désactivé");
                }
            }

            // ── Core Parking ──────────────────────────────────────────────────
            if (cfg.DisableCoreParking)
            {
                // Lire la valeur actuelle avant modification
                var currentMin = ReadCoreParkingMin();
                _registrySaves["__CoreParkingMin"] = currentMin;
                ApplyCoreParking(minPercent: 100);
                Log($"  ✓ Core Parking désactivé (valeur originale sauvegardée : {currentMin}%)");
            }
        }
        catch (Exception ex) { Log($"  [WARN] Registry tweaks : {ex.Message}"); }
    }

    private void ApplyNetworkTweaks(GameBoosterProfileConfig cfg)
    {
        if (!cfg.DisableNagle) return;
        try
        {
            using var ifaces = Registry.LocalMachine.OpenSubKey(RegNetIfaces, true);
            if (ifaces == null) return;

            int count = 0;
            foreach (var name in ifaces.GetSubKeyNames())
            {
                using var key = ifaces.OpenSubKey(name, true);
                if (key == null) continue;
                _nagleSaves[name] = (key.GetValue("TcpAckFrequency"), key.GetValue("TcpNoDelay"));
                key.SetValue("TcpAckFrequency", 1, RegistryValueKind.DWord);
                key.SetValue("TcpNoDelay",      1, RegistryValueKind.DWord);
                count++;
            }
            Log($"  ✓ Algorithme de Nagle désactivé ({count} interfaces)");
        }
        catch (Exception ex) { Log($"  [WARN] Nagle : {ex.Message}"); }
    }

    private void ApplyTimerResolution(GameBoosterProfileConfig cfg)
    {
        if (!cfg.SetTimerResolution) return;
        try
        {
            NtQueryTimerResolution(out _, out _, out _originalTimerResolution);
            NtSetTimerResolution(5000, true, out _); // 0.5 ms
            Log("  ✓ Résolution timer : 0.5 ms (précision maximale)");
        }
        catch (Exception ex) { Log($"  [WARN] Timer resolution : {ex.Message}"); }
    }

    private void ApplyServices(GameBoosterProfileConfig cfg)
    {
        if (cfg.ServicesToStop.Count == 0) return;

        int stopped = 0;
        foreach (var svc in cfg.ServicesToStop)
        {
            try
            {
                var (running, startMode) = GetServiceState(svc);
                _serviceSaves[svc] = new ServiceState(svc, running, startMode);
                if (running)
                {
                    StopService(svc);
                    stopped++;
                }
            }
            catch (Exception ex) { Log($"  [WARN] Service {svc} : {ex.Message}"); }
        }
        Log($"  ✓ Services arrêtés : {stopped}/{cfg.ServicesToStop.Count}");
    }

    private void ApplyGameModeFeatures(GameBoosterProfileConfig cfg)
    {
        // HAGS (Hardware-Accelerated GPU Scheduling) nécessite un redémarrage
        // pour prendre effet — on ne l'applique pas ici.
    }

    // ── Restauration ─────────────────────────────────────────────────────────

    private void RestoreRegistryTweaks()
    {
        try
        {
            using var mm      = Registry.LocalMachine.OpenSubKey(RegMultimedia, true);
            using var games   = Registry.LocalMachine.OpenSubKey(RegGames, true);
            using var prio    = Registry.LocalMachine.OpenSubKey(RegPriority, true);
            using var pt      = Registry.LocalMachine.OpenSubKey(RegPowerThrot, true);
            using var mem     = Registry.LocalMachine.OpenSubKey(RegMemMgmt, true);
            using var gameBar = Registry.CurrentUser.OpenSubKey(RegGameBar, true);
            using var dvr     = Registry.CurrentUser.OpenSubKey(RegGameDvr, true);
            using var gfx     = (RegistryKey?)null; // HAGS retiré (nécessite redémarrage)

            RestoreValue(mm,      "SystemResponsiveness");
            RestoreValue(mm,      "NetworkThrottlingIndex");
            RestoreValue(games,   "GPU Priority");
            RestoreValue(games,   "Priority");
            RestoreValue(games,   "Scheduling Category");
            RestoreValue(games,   "SFIO Priority");
            RestoreValue(games,   "Background Only");
            RestoreValue(games,   "Background Priority");
            RestoreValue(prio,    "Win32PrioritySeparation");
            RestoreValue(pt,      "PowerThrottlingOff");
            RestoreValue(mem,     "DisablePagingExecutive");
            RestoreValue(mem,     "LargeSystemCache");
            RestoreValue(gameBar, "AllowAutoGameMode");
            RestoreValue(gameBar, "AutoGameModeEnabled");
            RestoreValue(dvr,     "AppCaptureEnabled");
            // HAGS non appliqué — pas de restauration nécessaire

            // Restaurer Core Parking
            if (_registrySaves.TryGetValue("__CoreParkingMin", out var cpMin))
            {
                ApplyCoreParking(Convert.ToInt32(cpMin ?? 0));
                _registrySaves.Remove("__CoreParkingMin");
                Log($"  ✓ Core Parking restauré ({cpMin}%)");
            }

            Log("  ✓ Paramètres registre restaurés");
        }
        catch (Exception ex) { Log($"  [WARN] RestoreRegistry : {ex.Message}"); }
    }

    private void RestoreNetworkTweaks()
    {
        try
        {
            using var ifaces = Registry.LocalMachine.OpenSubKey(RegNetIfaces, true);
            if (ifaces == null) return;

            foreach (var kv in _nagleSaves)
            {
                using var key = ifaces.OpenSubKey(kv.Key, true);
                if (key == null) continue;
                if (kv.Value.ack == null) key.DeleteValue("TcpAckFrequency", false);
                else key.SetValue("TcpAckFrequency", kv.Value.ack, RegistryValueKind.DWord);
                if (kv.Value.nodelay == null) key.DeleteValue("TcpNoDelay", false);
                else key.SetValue("TcpNoDelay", kv.Value.nodelay, RegistryValueKind.DWord);
            }
            _nagleSaves.Clear();
            if (_nagleSaves.Count == 0) Log("  ✓ Algorithme de Nagle restauré");
        }
        catch (Exception ex) { Log($"  [WARN] RestoreNagle : {ex.Message}"); }
    }

    private void RestoreTimerResolution()
    {
        try
        {
            if (_originalTimerResolution > 0)
            {
                NtSetTimerResolution(_originalTimerResolution, true, out _);
                _originalTimerResolution = 0;
                Log("  ✓ Résolution timer restaurée");
            }
        }
        catch (Exception ex) { Log($"  [WARN] RestoreTimer : {ex.Message}"); }
    }

    private void RestoreServices()
    {
        int restarted = 0;
        foreach (var kv in _serviceSaves)
        {
            try
            {
                if (kv.Value.WasRunning) { StartService(kv.Key); restarted++; }
            }
            catch (Exception ex) { Log($"  [WARN] Restart {kv.Key} : {ex.Message}"); }
        }
        _serviceSaves.Clear();
        Log($"  ✓ Services restaurés : {restarted}");
    }

    // ── Monitoring processus ──────────────────────────────────────────────────

    private void StartProcessMonitoring()
    {
        try
        {
            _startWatcher = new ManagementEventWatcher(new WqlEventQuery("SELECT * FROM Win32_ProcessStartTrace"));
            _startWatcher.EventArrived += OnProcessStarted;
            _startWatcher.Start();

            _stopWatcher = new ManagementEventWatcher(new WqlEventQuery("SELECT * FROM Win32_ProcessStopTrace"));
            _stopWatcher.EventArrived += OnProcessStopped;
            _stopWatcher.Start();

            Log("  ✓ Surveillance des processus active");
        }
        catch (Exception ex) { Log($"  [WARN] Process monitor : {ex.Message}"); }
    }

    private void StopProcessMonitoring()
    {
        try { _startWatcher?.Stop(); } catch { }
        try { _stopWatcher?.Stop();  } catch { }
    }

    private void OnProcessStarted(object sender, EventArrivedEventArgs e)
    {
        try
        {
            var name = e.NewEvent.Properties["ProcessName"].Value?.ToString()?.ToLowerInvariant() ?? "";
            var pid  = Convert.ToInt32(e.NewEvent.Properties["ProcessID"].Value);
            if (!IsGameProcess(name)) return;

            Log($"  🎮 Jeu détecté : {name} (PID {pid})");
            OnGameStarted?.Invoke(this, name);
            Task.Run(() => OptimizeGameProcess(name, pid));
        }
        catch { }
    }

    private void OnProcessStopped(object sender, EventArrivedEventArgs e)
    {
        try
        {
            var pid = Convert.ToInt32(e.NewEvent.Properties["ProcessID"].Value);
            if (!_activeGames.TryGetValue(pid, out var info)) return;

            Log($"  🏁 Jeu fermé : {info.Process.ProcessName} (PID {pid})");
            OnGameStopped?.Invoke(this, info.Process.ProcessName);
            RestoreGameProcess(info);
            _activeGames.Remove(pid);
        }
        catch { }
    }

    private bool IsGameProcess(string name)
    {
        name = name.Replace(".exe", "");
        foreach (var g in _gameProcessNames)
            if (name.Contains(g, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private void OptimizeGameProcess(string name, int pid)
    {
        try
        {
            var cfg     = GetConfig();
            var process = Process.GetProcessById(pid);
            var info    = new GameProcessInfo(process);

            if (cfg.SetHighPriority)
            {
                process.PriorityClass = ProcessPriorityClass.High;
                Log($"  ✓ Priorité High → {name}");
            }

            process.EnableRaisingEvents = true;
            process.Exited += (_, _) =>
            {
                if (_activeGames.TryGetValue(pid, out var gi))
                {
                    OnGameStopped?.Invoke(this, gi.Process.ProcessName);
                    RestoreGameProcess(gi);
                    _activeGames.Remove(pid);
                }
            };

            _activeGames[pid] = info;
        }
        catch (ArgumentException) { /* process déjà fermé */ }
        catch (Exception ex) { Log($"  [WARN] OptimizeProcess {name} : {ex.Message}"); }
    }

    private void RestoreGameProcess(GameProcessInfo info)
    {
        try
        {
            if (!info.Process.HasExited)
                info.Process.PriorityClass = info.OriginalPriority;
        }
        catch { }
    }

    // ── Helpers registre ──────────────────────────────────────────────────────

    private void SaveAndSet(RegistryKey key, string name, object value)
    {
        var saveKey = $"{key.Name}\\{name}";
        if (!_registrySaves.ContainsKey(saveKey))
            _registrySaves[saveKey] = key.GetValue(name);

        if (value is string s)
            key.SetValue(name, s, RegistryValueKind.String);
        else if (value is int i)
            key.SetValue(name, i, RegistryValueKind.DWord);
    }

    private void RestoreValue(RegistryKey? key, string name)
    {
        if (key == null) return;
        var saveKey = $"{key.Name}\\{name}";
        if (!_registrySaves.TryGetValue(saveKey, out var orig)) return;

        if (orig == null)
            key.DeleteValue(name, false);
        else if (orig is int i)
            key.SetValue(name, i, RegistryValueKind.DWord);
        else if (orig is string s)
            key.SetValue(name, s, RegistryValueKind.String);

        _registrySaves.Remove(saveKey);
    }

    private void ApplyCoreParking(int minPercent)
    {
        RunProcess("powercfg", $"/setacvalueindex SCHEME_CURRENT SUB_PROCESSOR PROCTHROTTLEMIN {minPercent}");
        RunProcess("powercfg", "/setactive SCHEME_CURRENT");
    }

    private int ReadCoreParkingMin()
    {
        try
        {
            // powercfg /query retourne la valeur actuelle du min processor state
            var output = RunProcess("powercfg", "/query SCHEME_CURRENT SUB_PROCESSOR PROCTHROTTLEMIN");
            var match = System.Text.RegularExpressions.Regex.Match(output, @"Paramètre actuel de l'alimentation secteur:\s*0x([0-9a-fA-F]+)");
            if (!match.Success)
                match = System.Text.RegularExpressions.Regex.Match(output, @"Current AC Power Setting Index:\s*0x([0-9a-fA-F]+)");
            if (match.Success)
                return Convert.ToInt32(match.Groups[1].Value, 16);
        }
        catch { }
        return 0; // valeur par défaut Windows (laisse le parking se gérer)
    }

    // ── Services helpers ──────────────────────────────────────────────────────

    private (bool isRunning, string startMode) GetServiceState(string name)
    {
        using var s = new ManagementObjectSearcher(
            $"SELECT State,StartMode FROM Win32_Service WHERE Name='{name}'");
        foreach (ManagementObject o in s.Get())
            return (o["State"]?.ToString() == "Running", o["StartMode"]?.ToString() ?? "Manual");
        return (false, "Manual");
    }

    private void StopService(string name)
    {
        using var s = new ManagementObjectSearcher($"SELECT * FROM Win32_Service WHERE Name='{name}'");
        foreach (ManagementObject o in s.Get()) o.InvokeMethod("StopService", null);
    }

    private void StartService(string name)
    {
        using var s = new ManagementObjectSearcher($"SELECT * FROM Win32_Service WHERE Name='{name}'");
        foreach (ManagementObject o in s.Get()) o.InvokeMethod("StartService", null);
    }

    private static string RunProcess(string exe, string args)
    {
        using var p = Process.Start(new ProcessStartInfo(exe, args)
        {
            RedirectStandardOutput = true,
            UseShellExecute        = false,
            CreateNoWindow         = true,
        })!;
        p.WaitForExit(5000);
        return p.StandardOutput.ReadToEnd();
    }

    // ── Misc ──────────────────────────────────────────────────────────────────

    private GameBoosterProfileConfig GetConfig() =>
        _activeProfile == GameBoosterProfile.Custom
            ? _customConfig
            : Profiles[_activeProfile];

    private void Log(string msg)
    {
        LogStore.Append($"[GameBooster] {msg}");
        OnLog?.Invoke(this, msg);
    }

    // ── Dispose ───────────────────────────────────────────────────────────────
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        if (disposing)
        {
            StopMonitoring();
            _startWatcher?.Dispose();
            _stopWatcher?.Dispose();
        }
        _disposed = true;
    }

    ~GameBoosterService() => Dispose(false);

    // ── Inner types ───────────────────────────────────────────────────────────
    private class GameProcessInfo
    {
        public Process           Process         { get; }
        public ProcessPriorityClass OriginalPriority { get; }
        public GameProcessInfo(Process p) { Process = p; OriginalPriority = p.PriorityClass; }
    }

    private class ServiceState
    {
        public string Name            { get; }
        public bool   WasRunning      { get; }
        public string OriginalStartup { get; }
        public ServiceState(string n, bool r, string s) { Name = n; WasRunning = r; OriginalStartup = s; }
    }
}
