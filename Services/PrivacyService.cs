using System;
using System.Diagnostics;
using Microsoft.Win32;

namespace Assistools.Services;

/// <summary>
/// Actions de confidentialité : désactiver Copilot, Recall, télémétrie, Bing, publicités, IA Edge/Office.
/// Chaque méthode est idempotente et peut être appelée plusieurs fois sans effet de bord.
/// Toutes les actions HKLM nécessitent des droits admin (l'app doit être lancée en admin).
/// </summary>
public static class PrivacyService
{
    // ── COPILOT WINDOWS ──────────────────────────────────────────────────────

    public static void DesactiverCopilot(Action<string>? log = null)
    {
        log?.Invoke("Désactivation de Windows Copilot…");
        SetDword(Registry.LocalMachine,  @"SOFTWARE\Policies\Microsoft\Windows\WindowsCopilot", "TurnOffWindowsCopilot", 1);
        SetDword(Registry.CurrentUser,   @"Software\Policies\Microsoft\Windows\WindowsCopilot",  "TurnOffWindowsCopilot", 1);
        SetDword(Registry.LocalMachine,  @"SOFTWARE\Microsoft\Windows\Shell\Copilot",            "IsCopilotAvailable",    0);
        SetString(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\Shell\Copilot",            "CopilotDisabledReason", "FeatureIsDisabled");
        SetDword(Registry.LocalMachine,  @"SOFTWARE\Microsoft\Windows\Shell\Copilot\BingChat",   "IsUserEligible",        0);
        SetDword(Registry.CurrentUser,   @"Software\Microsoft\Windows\Explorer\Advanced",        "ShowCopilotButton",     0);
        // Supprimer le package Appx Copilot s'il est présent
        ProcessRunner.RunPowerShell(
            "Get-AppxPackage -Name 'Microsoft.Copilot' -ErrorAction SilentlyContinue | Remove-AppxPackage -ErrorAction SilentlyContinue",
            log);
        log?.Invoke("✓ Copilot désactivé.");
    }

    public static void ReactiverCopilot(Action<string>? log = null)
    {
        log?.Invoke("Réactivation de Windows Copilot…");
        DeleteValue(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\WindowsCopilot", "TurnOffWindowsCopilot");
        DeleteValue(Registry.CurrentUser,  @"Software\Policies\Microsoft\Windows\WindowsCopilot",  "TurnOffWindowsCopilot");
        SetDword(Registry.LocalMachine,    @"SOFTWARE\Microsoft\Windows\Shell\Copilot",            "IsCopilotAvailable", 1);
        SetDword(Registry.CurrentUser,     @"Software\Microsoft\Windows\Explorer\Advanced",        "ShowCopilotButton",  1);
        log?.Invoke("✓ Copilot réactivé.");
    }

    public static bool EstCopilotDesactive()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\WindowsCopilot");
            return key?.GetValue("TurnOffWindowsCopilot") is int v && v == 1;
        }
        catch { return false; }
    }

    // ── WINDOWS RECALL ───────────────────────────────────────────────────────

    public static void DesactiverRecall(Action<string>? log = null)
    {
        log?.Invoke("Désactivation de Windows Recall…");
        SetDword(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\WindowsAI", "AllowRecallEnablement", 0);
        SetDword(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\WindowsAI", "DisableAIDataAnalysis", 1);
        SetDword(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\WindowsAI", "TurnOffSavingSnapshots", 1);
        log?.Invoke("✓ Windows Recall désactivé.");
    }

    public static void ReactiverRecall(Action<string>? log = null)
    {
        log?.Invoke("Réactivation de Windows Recall…");
        DeleteValue(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\WindowsAI", "AllowRecallEnablement");
        DeleteValue(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\WindowsAI", "DisableAIDataAnalysis");
        DeleteValue(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\WindowsAI", "TurnOffSavingSnapshots");
        log?.Invoke("✓ Windows Recall réactivé.");
    }

    public static bool EstRecallDesactive()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\WindowsAI");
            return key?.GetValue("TurnOffSavingSnapshots") is int v && v == 1;
        }
        catch { return false; }
    }

    // ── TÉLÉMÉTRIE ───────────────────────────────────────────────────────────

    public static void DesactiverTelemetrie(Action<string>? log = null)
    {
        log?.Invoke("Désactivation de la télémétrie Windows…");
        SetDword(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "AllowTelemetry", 0);
        SetDword(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection", "AllowTelemetry", 0);
        // Désactiver le service DiagTrack
        ProcessRunner.RunPowerShell(
            "Stop-Service -Name 'DiagTrack' -Force -ErrorAction SilentlyContinue; " +
            "Set-Service -Name 'DiagTrack' -StartupType Disabled -ErrorAction SilentlyContinue; " +
            "Stop-Service -Name 'dmwappushservice' -Force -ErrorAction SilentlyContinue; " +
            "Set-Service -Name 'dmwappushservice' -StartupType Disabled -ErrorAction SilentlyContinue",
            log);
        log?.Invoke("✓ Télémétrie désactivée.");
    }

    public static void ReactiverTelemetrie(Action<string>? log = null)
    {
        log?.Invoke("Réactivation de la télémétrie Windows…");
        SetDword(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "AllowTelemetry", 1);
        ProcessRunner.RunPowerShell(
            "Set-Service -Name 'DiagTrack' -StartupType Automatic -ErrorAction SilentlyContinue; " +
            "Start-Service -Name 'DiagTrack' -ErrorAction SilentlyContinue",
            log);
        log?.Invoke("✓ Télémétrie réactivée.");
    }

    public static bool EstTelemetrieDesactivee()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\DataCollection");
            return key?.GetValue("AllowTelemetry") is int v && v == 0;
        }
        catch { return false; }
    }

    // ── BING DANS RECHERCHE WINDOWS ──────────────────────────────────────────

    public static void DesactiverBingRecherche(Action<string>? log = null)
    {
        log?.Invoke("Désactivation de Bing dans la recherche Windows…");
        SetDword(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Search",  "BingSearchEnabled",         0);
        SetDword(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Search",  "CortanaConsent",            0);
        SetDword(Registry.CurrentUser, @"Software\Policies\Microsoft\Windows\Explorer",      "DisableSearchBoxSuggestions", 1);
        log?.Invoke("✓ Bing dans la recherche désactivé.");
    }

    public static void ReactiverBingRecherche(Action<string>? log = null)
    {
        log?.Invoke("Réactivation de Bing dans la recherche Windows…");
        SetDword(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Search", "BingSearchEnabled", 1);
        DeleteValue(Registry.CurrentUser, @"Software\Policies\Microsoft\Windows\Explorer",  "DisableSearchBoxSuggestions");
        log?.Invoke("✓ Bing dans la recherche réactivé.");
    }

    public static bool EstBingRechercheDesactive()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Search");
            return key?.GetValue("BingSearchEnabled") is int v && v == 0;
        }
        catch { return false; }
    }

    // ── PUBLICITÉS PERSONNALISÉES ────────────────────────────────────────────

    public static void DesactiverPublicites(Action<string>? log = null)
    {
        log?.Invoke("Désactivation des publicités personnalisées…");
        SetDword(Registry.CurrentUser,   @"Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled",              0);
        SetDword(Registry.LocalMachine,  @"SOFTWARE\Policies\Microsoft\Windows\AdvertisingInfo",       "DisabledByGroupPolicy", 1);
        SetDword(Registry.CurrentUser,   @"Software\Microsoft\Windows\CurrentVersion\Privacy",         "TailoredExperiencesWithDiagnosticDataEnabled", 0);
        log?.Invoke("✓ Publicités personnalisées désactivées.");
    }

    public static void ReactiverPublicites(Action<string>? log = null)
    {
        log?.Invoke("Réactivation des publicités personnalisées…");
        SetDword(Registry.CurrentUser,  @"Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled", 1);
        DeleteValue(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\AdvertisingInfo",    "DisabledByGroupPolicy");
        log?.Invoke("✓ Publicités personnalisées réactivées.");
    }

    public static bool EstPublicitesDesactivees()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo");
            return key?.GetValue("Enabled") is int v && v == 0;
        }
        catch { return false; }
    }

    // ── SUGGESTIONS DANS LE MENU DÉMARRER ────────────────────────────────────

    public static void DesactiverSuggestionsDemarrer(Action<string>? log = null)
    {
        log?.Invoke("Désactivation des suggestions dans le menu Démarrer…");
        SetDword(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SystemPaneSuggestionsEnabled",      0);
        SetDword(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-338388Enabled",   0);
        SetDword(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-338389Enabled",   0);
        SetDword(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-353698Enabled",   0);
        SetDword(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SoftLandingEnabled",               0);
        SetDword(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "RotatingLockScreenOverlayEnabled",  0);
        log?.Invoke("✓ Suggestions du menu Démarrer désactivées.");
    }

    public static void ReactiverSuggestionsDemarrer(Action<string>? log = null)
    {
        log?.Invoke("Réactivation des suggestions dans le menu Démarrer…");
        SetDword(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SystemPaneSuggestionsEnabled",    1);
        SetDword(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-338388Enabled", 1);
        SetDword(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-338389Enabled", 1);
        SetDword(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-353698Enabled", 1);
        log?.Invoke("✓ Suggestions du menu Démarrer réactivées.");
    }

    public static bool EstSuggestionsDestarrerDesactivees()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager");
            return key?.GetValue("SystemPaneSuggestionsEnabled") is int v && v == 0;
        }
        catch { return false; }
    }

    // ── IA EDGE ──────────────────────────────────────────────────────────────

    public static void DesactiverIaEdge(Action<string>? log = null)
    {
        log?.Invoke("Désactivation des fonctions IA dans Microsoft Edge…");
        var edgePath = @"SOFTWARE\Policies\Microsoft\Edge";
        SetDword(Registry.LocalMachine, edgePath, "CopilotCDPPageContext",              0);
        SetDword(Registry.LocalMachine, edgePath, "CopilotPageContext",                 0);
        SetDword(Registry.LocalMachine, edgePath, "HubsSidebarEnabled",                0);
        SetDword(Registry.LocalMachine, edgePath, "EdgeEntraCopilotChatIconEnabled",    0);
        SetDword(Registry.LocalMachine, edgePath, "EdgeHistoryAISearchEnabled",         0);
        SetDword(Registry.LocalMachine, edgePath, "ComposeInlineEnabled",               0);
        SetDword(Registry.LocalMachine, edgePath, "BuiltInAIAPIsEnabled",               0);
        log?.Invoke("✓ IA Edge désactivée.");
    }

    public static void ReactiverIaEdge(Action<string>? log = null)
    {
        log?.Invoke("Réactivation des fonctions IA dans Microsoft Edge…");
        var edgePath = @"SOFTWARE\Policies\Microsoft\Edge";
        DeleteValue(Registry.LocalMachine, edgePath, "CopilotCDPPageContext");
        DeleteValue(Registry.LocalMachine, edgePath, "CopilotPageContext");
        DeleteValue(Registry.LocalMachine, edgePath, "HubsSidebarEnabled");
        DeleteValue(Registry.LocalMachine, edgePath, "EdgeEntraCopilotChatIconEnabled");
        DeleteValue(Registry.LocalMachine, edgePath, "EdgeHistoryAISearchEnabled");
        DeleteValue(Registry.LocalMachine, edgePath, "ComposeInlineEnabled");
        DeleteValue(Registry.LocalMachine, edgePath, "BuiltInAIAPIsEnabled");
        log?.Invoke("✓ IA Edge réactivée.");
    }

    public static bool EstIaEdgeDesactivee()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Edge");
            return key?.GetValue("BuiltInAIAPIsEnabled") is int v && v == 0;
        }
        catch { return false; }
    }

    // ── IA OFFICE ────────────────────────────────────────────────────────────

    public static void DesactiverIaOffice(Action<string>? log = null)
    {
        log?.Invoke("Désactivation de Copilot dans Microsoft Office…");
        SetDword(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\office\16.0\common\ai\training\general", "disabletraining", 1);
        SetDword(Registry.CurrentUser,  @"Software\Microsoft\Office\16.0\Word\Options",   "EnableCopilot", 0);
        SetDword(Registry.CurrentUser,  @"Software\Microsoft\Office\16.0\Excel\Options",  "EnableCopilot", 0);
        SetDword(Registry.CurrentUser,  @"Software\Policies\Microsoft\office\16.0\common\privacy", "controllerconnectedservicesenabled", 2);
        log?.Invoke("✓ Copilot Office désactivé.");
    }

    public static void ReactiverIaOffice(Action<string>? log = null)
    {
        log?.Invoke("Réactivation de Copilot dans Microsoft Office…");
        DeleteValue(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\office\16.0\common\ai\training\general", "disabletraining");
        DeleteValue(Registry.CurrentUser,  @"Software\Microsoft\Office\16.0\Word\Options",  "EnableCopilot");
        DeleteValue(Registry.CurrentUser,  @"Software\Microsoft\Office\16.0\Excel\Options", "EnableCopilot");
        log?.Invoke("✓ Copilot Office réactivé.");
    }

    public static bool EstIaOfficeDesactivee()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Office\16.0\Word\Options");
            return key?.GetValue("EnableCopilot") is int v && v == 0;
        }
        catch { return false; }
    }

    // ── LOCALISATION ─────────────────────────────────────────────────────────

    public static void DesactiverLocalisation(Action<string>? log = null)
    {
        log?.Invoke("Désactivation de la localisation…");
        SetDword(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\LocationAndSensors", "DisableLocation", 1);
        SetString(Registry.CurrentUser,  @"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\location", "Value", "Deny");
        SetString(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\location", "Value", "Deny");
        log?.Invoke("✓ Localisation désactivée.");
    }

    public static void ReactiverLocalisation(Action<string>? log = null)
    {
        log?.Invoke("Réactivation de la localisation…");
        DeleteValue(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\LocationAndSensors", "DisableLocation");
        SetString(Registry.CurrentUser,  @"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\location", "Value", "Allow");
        SetString(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\location", "Value", "Allow");
        log?.Invoke("✓ Localisation réactivée.");
    }

    public static bool EstLocalisationDesactivee()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\LocationAndSensors");
            if (key?.GetValue("DisableLocation") is int v && v == 1) return true;
            using var cap = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\location");
            return cap?.GetValue("Value") is string s && s == "Deny";
        }
        catch { return false; }
    }

    // ── CAMÉRA ───────────────────────────────────────────────────────────────

    public static void DesactiverCamera(Action<string>? log = null)
    {
        log?.Invoke("Désactivation de l'accès à la caméra…");
        SetString(Registry.CurrentUser,  @"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\webcam", "Value", "Deny");
        SetString(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\webcam", "Value", "Deny");
        log?.Invoke("✓ Accès caméra désactivé.");
    }

    public static void ReactiverCamera(Action<string>? log = null)
    {
        log?.Invoke("Réactivation de l'accès à la caméra…");
        SetString(Registry.CurrentUser,  @"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\webcam", "Value", "Allow");
        SetString(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\webcam", "Value", "Allow");
        log?.Invoke("✓ Accès caméra réactivé.");
    }

    public static bool EstCameraDesactivee()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\webcam");
            return key?.GetValue("Value") is string s && s == "Deny";
        }
        catch { return false; }
    }

    // ── MICROPHONE ───────────────────────────────────────────────────────────

    public static void DesactiverMicrophone(Action<string>? log = null)
    {
        log?.Invoke("Désactivation de l'accès au microphone…");
        SetString(Registry.CurrentUser,  @"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone", "Value", "Deny");
        SetString(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone", "Value", "Deny");
        log?.Invoke("✓ Accès microphone désactivé.");
    }

    public static void ReactiverMicrophone(Action<string>? log = null)
    {
        log?.Invoke("Réactivation de l'accès au microphone…");
        SetString(Registry.CurrentUser,  @"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone", "Value", "Allow");
        SetString(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone", "Value", "Allow");
        log?.Invoke("✓ Accès microphone réactivé.");
    }

    public static bool EstMicrophoneDesactive()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone");
            return key?.GetValue("Value") is string s && s == "Deny";
        }
        catch { return false; }
    }

    // ── RECONNAISSANCE VOCALE EN LIGNE ────────────────────────────────────────

    public static void DesactiverVoix(Action<string>? log = null)
    {
        log?.Invoke("Désactivation de la reconnaissance vocale en ligne…");
        SetDword(Registry.CurrentUser, @"Software\Microsoft\Speech_OneCore\Settings\OnlineSpeechPrivacy", "HasAccepted", 0);
        SetDword(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\InputPersonalization", "AllowInputPersonalization", 0);
        log?.Invoke("✓ Reconnaissance vocale en ligne désactivée.");
    }

    public static void ReactiverVoix(Action<string>? log = null)
    {
        log?.Invoke("Réactivation de la reconnaissance vocale en ligne…");
        DeleteValue(Registry.CurrentUser, @"Software\Microsoft\Speech_OneCore\Settings\OnlineSpeechPrivacy", "HasAccepted");
        DeleteValue(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\InputPersonalization", "AllowInputPersonalization");
        log?.Invoke("✓ Reconnaissance vocale en ligne réactivée.");
    }

    public static bool EstVoixDesactivee()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Speech_OneCore\Settings\OnlineSpeechPrivacy");
            return key?.GetValue("HasAccepted") is int v && v == 0;
        }
        catch { return false; }
    }

    // ── HISTORIQUE D'ACTIVITÉ ─────────────────────────────────────────────────

    public static void DesactiverHistoriqueActivite(Action<string>? log = null)
    {
        log?.Invoke("Désactivation de l'historique d'activité…");
        SetDword(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\System", "PublishUserActivities", 0);
        SetDword(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\System", "UploadUserActivities", 0);
        SetDword(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\System", "EnableActivityFeed", 0);
        log?.Invoke("✓ Historique d'activité désactivé.");
    }

    public static void ReactiverHistoriqueActivite(Action<string>? log = null)
    {
        log?.Invoke("Réactivation de l'historique d'activité…");
        DeleteValue(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\System", "PublishUserActivities");
        DeleteValue(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\System", "UploadUserActivities");
        DeleteValue(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\System", "EnableActivityFeed");
        log?.Invoke("✓ Historique d'activité réactivé.");
    }

    public static bool EstHistoriqueActiviteDesactive()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\System");
            return key?.GetValue("PublishUserActivities") is int v && v == 0;
        }
        catch { return false; }
    }

    // ── RETOURS D'EXPÉRIENCE (FEEDBACK) ──────────────────────────────────────

    public static void DesactiverFeedback(Action<string>? log = null)
    {
        log?.Invoke("Désactivation des demandes de feedback Microsoft…");
        SetDword(Registry.CurrentUser, @"SOFTWARE\Microsoft\Siuf\Rules", "NumberOfSIUFInPeriod", 0);
        DeleteValue(Registry.CurrentUser, @"SOFTWARE\Microsoft\Siuf\Rules", "PeriodInNanoSeconds");
        SetDword(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "DoNotShowFeedbackNotifications", 1);
        log?.Invoke("✓ Demandes de feedback désactivées.");
    }

    public static void ReactiverFeedback(Action<string>? log = null)
    {
        log?.Invoke("Réactivation des demandes de feedback Microsoft…");
        DeleteValue(Registry.CurrentUser, @"SOFTWARE\Microsoft\Siuf\Rules", "NumberOfSIUFInPeriod");
        DeleteValue(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "DoNotShowFeedbackNotifications");
        log?.Invoke("✓ Demandes de feedback réactivées.");
    }

    public static bool EstFeedbackDesactive()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Siuf\Rules");
            return key?.GetValue("NumberOfSIUFInPeriod") is int v && v == 0;
        }
        catch { return false; }
    }

    // ── FIND MY DEVICE ────────────────────────────────────────────────────────

    public static void DesactiverFindMyDevice(Action<string>? log = null)
    {
        log?.Invoke("Désactivation de Localiser mon appareil…");
        SetDword(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\FindMyDevice", "AllowFindMyDevice", 0);
        log?.Invoke("✓ Localiser mon appareil désactivé.");
    }

    public static void ReactiverFindMyDevice(Action<string>? log = null)
    {
        log?.Invoke("Réactivation de Localiser mon appareil…");
        DeleteValue(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\FindMyDevice", "AllowFindMyDevice");
        log?.Invoke("✓ Localiser mon appareil réactivé.");
    }

    public static bool EstFindMyDeviceDesactive()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\FindMyDevice");
            return key?.GetValue("AllowFindMyDevice") is int v && v == 0;
        }
        catch { return false; }
    }

    // ── HELPERS ──────────────────────────────────────────────────────────────

    private static void SetDword(RegistryKey hive, string path, string name, int value)
    {
        try
        {
            using var key = hive.CreateSubKey(path, writable: true);
            key.SetValue(name, value, RegistryValueKind.DWord);
        }
        catch { }
    }

    private static void SetString(RegistryKey hive, string path, string name, string value)
    {
        try
        {
            using var key = hive.CreateSubKey(path, writable: true);
            key.SetValue(name, value, RegistryValueKind.String);
        }
        catch { }
    }

    private static void DeleteValue(RegistryKey hive, string path, string name)
    {
        try
        {
            using var key = hive.OpenSubKey(path, writable: true);
            key?.DeleteValue(name, throwOnMissingValue: false);
        }
        catch { }
    }
}
