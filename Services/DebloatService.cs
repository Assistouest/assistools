using System;
using System.Collections.Generic;

namespace Assistools.Services;

/// <summary>
/// Suppression granulaire des applications préinstallées Windows (bloatware).
/// Chaque AppItem représente un package Appx sélectionnable individuellement.
/// </summary>
public static class DebloatService
{
    public record AppItem(
        string Nom,
        string Description,
        string PackagePattern,
        string Categorie,
        bool SelectionneParDefaut = true);

    /// <summary>Liste complète des applications supprimables, par catégorie.</summary>
    public static readonly IReadOnlyList<AppItem> Apps = new List<AppItem>
    {
        // ── IA & Copilot ─────────────────────────────────────────────────────
        new("Copilot",             "Assistant IA Microsoft intégré à Windows",             "Microsoft.Copilot",                    "IA & Copilot",  true),
        new("Cortana",             "Assistant vocal et recherche Microsoft",                "*Microsoft.549981C3F5F10*",             "IA & Copilot",  true),

        // ── Jeux & Xbox ──────────────────────────────────────────────────────
        new("Xbox Game Bar",       "Barre de jeu Xbox (overlay en jeu)",                   "Microsoft.XboxGamingOverlay",           "Jeux & Xbox",   true),
        new("Xbox App",            "Application principale Xbox",                           "Microsoft.XboxApp",                    "Jeux & Xbox",   true),
        new("Xbox Game Overlay",   "Superposition de jeu Xbox",                            "Microsoft.XboxGameOverlay",             "Jeux & Xbox",   true),
        new("Xbox Identity",       "Fournisseur d'identité Xbox",                          "Microsoft.XboxIdentityProvider",        "Jeux & Xbox",   true),
        new("Xbox Speech To Text", "Reconnaissance vocale Xbox",                           "Microsoft.XboxSpeechToTextOverlay",     "Jeux & Xbox",   true),
        new("Solitaire Collection","Jeux de cartes Microsoft",                             "Microsoft.MicrosoftSolitaireCollection", "Jeux & Xbox",  true),
        new("Candy Crush",         "Jeux Candy Crush préinstallés",                        "*king.com*",                           "Jeux & Xbox",   true),

        // ── Réseaux sociaux & Médias ──────────────────────────────────────────
        new("TikTok",              "Réseau social TikTok",                                  "*TikTok*",                             "Réseaux sociaux", true),
        new("Facebook",            "Application Facebook",                                  "*Facebook*",                           "Réseaux sociaux", true),
        new("Instagram",           "Application Instagram",                                 "*Instagram*",                          "Réseaux sociaux", true),
        new("Twitter / X",         "Application Twitter/X",                                 "*Twitter*",                            "Réseaux sociaux", true),
        new("Spotify",             "Application Spotify préinstallée",                      "SpotifyAB.SpotifyMusic",               "Réseaux sociaux", true),

        // ── Actualités & Météo ────────────────────────────────────────────────
        new("Microsoft News",      "Actualités Microsoft",                                  "Microsoft.BingNews",                   "Actualités & Météo", true),
        new("MSN Météo",           "Application Météo Microsoft",                           "Microsoft.BingWeather",                "Actualités & Météo", true),
        new("Microsoft Finance",   "Application Finance Microsoft",                         "Microsoft.BingFinance",                "Actualités & Météo", true),
        new("Microsoft Sports",    "Application Sport Microsoft",                           "Microsoft.BingSports",                 "Actualités & Météo", true),

        // ── Productivité Microsoft ────────────────────────────────────────────
        new("Microsoft Teams",     "Teams préinstallé (version grand public)",              "MicrosoftTeams",                       "Productivité",  true),
        new("Teams (perso)",       "Teams chat personnel intégré à Windows 11",             "MSTeams",                              "Productivité",  true),
        new("Skype",               "Application Skype",                                     "Microsoft.SkypeApp",                   "Productivité",  true),
        new("Office Hub",          "Hub Microsoft 365 (publicité pour Office)",             "Microsoft.MicrosoftOfficeHub",          "Productivité",  true),
        new("To Do",               "Microsoft To Do",                                       "Microsoft.Todos",                      "Productivité",  false),
        new("Sticky Notes",        "Notes adhésives Windows",                               "Microsoft.MicrosoftStickyNotes",        "Productivité",  false),

        // ── Système & OneDrive ────────────────────────────────────────────────
        new("OneDrive",            "Stockage cloud Microsoft (désinstallation complète)",   "OneDrive",                             "Système",       false),
        new("Mixed Reality Portal","Portail de réalité mixte",                             "Microsoft.MixedReality.Portal",         "Système",       true),
        new("3D Viewer",           "Visionneuse d'objets 3D",                              "Microsoft.Microsoft3DViewer",           "Système",       true),
        new("Feedback Hub",        "Hub de commentaires Microsoft (télémétrie)",            "Microsoft.WindowsFeedbackHub",          "Système",       true),
        new("Get Help",            "Application d'aide Microsoft",                         "Microsoft.GetHelp",                    "Système",       true),
        new("Tips",                "Conseils Windows",                                     "Microsoft.Getstarted",                  "Système",       true),
        new("Maps",                "Cartes Windows",                                       "Microsoft.WindowsMaps",                 "Système",       true),
        new("People",              "Application Contacts Windows",                         "Microsoft.People",                     "Système",       true),
        new("Your Phone / Link",   "Lien avec téléphone Android",                         "Microsoft.YourPhone",                   "Système",       false),
        new("Power Automate",      "Automatisation Power Automate Desktop",                "Microsoft.PowerAutomateDesktop",        "Système",       false),

        // ── Multimédia ────────────────────────────────────────────────────────
        new("Films & TV",          "Lecteur vidéo Films et TV Microsoft",                  "Microsoft.ZuneVideo",                   "Multimédia",    false),
        new("Groove Music",        "Lecteur de musique Groove (désactivé par MS)",         "Microsoft.ZuneMusic",                   "Multimédia",    true),
        new("Enregistreur vocal",  "Enregistreur vocal Windows",                          "Microsoft.WindowsSoundRecorder",        "Multimédia",    false),
    };

    /// <summary>Supprime une liste d'apps sélectionnées par l'utilisateur.</summary>
    public static void SupprimerApps(IEnumerable<AppItem> apps, Action<string>? log = null)
    {
        foreach (var app in apps)
        {
            if (app.PackagePattern == "OneDrive")
            {
                SupprimerOneDrive(log);
                continue;
            }

            log?.Invoke($"Suppression de {app.Nom}…");
            ProcessRunner.RunPowerShell(
                $"Get-AppxPackage -Name '{app.PackagePattern}' -AllUsers -ErrorAction SilentlyContinue | " +
                $"Remove-AppxPackage -AllUsers -ErrorAction SilentlyContinue; " +
                $"Get-AppxProvisionedPackage -Online -ErrorAction SilentlyContinue | " +
                $"Where-Object {{ $_.DisplayName -like '{app.PackagePattern}' }} | " +
                $"Remove-AppxProvisionedPackage -Online -ErrorAction SilentlyContinue",
                log);
            log?.Invoke($"✓ {app.Nom} supprimé.");
        }
    }

    private static void SupprimerOneDrive(Action<string>? log = null)
    {
        log?.Invoke("Suppression de OneDrive…");
        ProcessRunner.RunPowerShell(
            """
            $od = "$env:SYSTEMROOT\SysWOW64\OneDriveSetup.exe"
            if (-not (Test-Path $od)) { $od = "$env:SYSTEMROOT\System32\OneDriveSetup.exe" }
            if (Test-Path $od) {
                Stop-Process -Name "OneDrive" -Force -ErrorAction SilentlyContinue
                Start-Sleep -Seconds 2
                & $od /uninstall
                Write-Output "OneDrive desinstalle."
            } else {
                Write-Output "OneDrive introuvable - peut-etre deja supprime."
            }
            # Nettoyer les dossiers residuels
            Remove-Item "$env:USERPROFILE\OneDrive" -Recurse -Force -ErrorAction SilentlyContinue
            Remove-Item "$env:LOCALAPPDATA\Microsoft\OneDrive" -Recurse -Force -ErrorAction SilentlyContinue
            Remove-Item "$env:PROGRAMDATA\Microsoft OneDrive" -Recurse -Force -ErrorAction SilentlyContinue
            # Supprimer l'entree du volet de navigation
            reg delete "HKCR\CLSID\{018D5C66-4533-4307-9B53-224DE2ED1FE6}" /f 2>$null
            reg delete "HKCR\Wow6432Node\CLSID\{018D5C66-4533-4307-9B53-224DE2ED1FE6}" /f 2>$null
            """,
            log);
        log?.Invoke("✓ OneDrive supprimé.");
    }
}
