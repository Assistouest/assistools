using System;
using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI;
using Windows.UI;
using Assistools.Services;

namespace Assistools.Pages;

public sealed partial class PageGameBooster : Page, IDisposable
{
    private readonly GameBoosterService _gameBoosterService;
    private bool _disposed;
    private readonly ObservableCollection<string> _detectedGames = new();

    public PageGameBooster()
    {
        // Utilise le singleton partagé (aussi accessible depuis le menu tray)
        _gameBoosterService = App.GameBooster;

        InitializeComponent();

        _gameBoosterService.OnLog += OnServiceLog;
        _gameBoosterService.OnMonitoringStateChanged += OnMonitoringStateChanged;
        _gameBoosterService.OnGameStarted += OnGameStarted;
        _gameBoosterService.OnGameStopped += OnGameStopped;

        DetectedGamesList.ItemsSource = _detectedGames;

        Loaded += (_, _) => ApplyProfile(GameBoosterProfile.Competitif);
    }

    private void OnServiceLog(object? sender, string log) { }

    private void OnMonitoringStateChanged(object? sender, bool isActive)
    {
        DispatcherQueue.TryEnqueue(UpdateUI);
    }

    private void OnGameStarted(object? sender, string gameName)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (!_detectedGames.Contains(gameName))
                _detectedGames.Add(gameName);
            UpdateMonitoringDetails();
        });
    }

    private void OnGameStopped(object? sender, string gameName)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            _detectedGames.Remove(gameName);
            UpdateMonitoringDetails();
        });
    }

    private bool _syncing = false;

    private void Profile_Checked(object sender, RoutedEventArgs e)
    {
        if (_syncing) return;
        if (sender is not RadioButton btn
            || btn.Tag is not string tagStr
            || !Enum.TryParse<GameBoosterProfile>(tagStr, out var profile))
            return;

        if (_gameBoosterService.IsMonitoring)
        {
            _syncing = true;
            try { SyncProfileButtons(_gameBoosterService.ActiveProfile); }
            finally { _syncing = false; }
            return;
        }

        _syncing = true;
        try { ApplyProfile(profile); }
        catch (Exception ex) { LogStore.Append($"[GameBooster][UI] Profile_Checked: {ex}"); }
        finally { _syncing = false; }
    }

    private void ApplyProfile(GameBoosterProfile profile)
    {
        _gameBoosterService.ActiveProfile = profile;
        SyncProfileButtons(profile);

        bool isCustom = profile == GameBoosterProfile.Custom;

        // Expander lecture seule : visible pour Compétitif / Gamer / Streamer
        if (ProfileDetailsExpander != null)
            ProfileDetailsExpander.Visibility = isCustom ? Visibility.Collapsed : Visibility.Visible;

        // Expander éditable : visible uniquement pour Custom
        if (AdvancedOptionsExpander != null)
            AdvancedOptionsExpander.Visibility = isCustom ? Visibility.Visible : Visibility.Collapsed;

        if (isCustom)
            UpdateAdvancedOptionsFromConfig();
        else
            UpdateProfileDetails(profile);

        UpdateUI();
    }

    private void UpdateProfileDetails(GameBoosterProfile profile)
    {
        if (ProfileInfoStack == null) return;
        ProfileInfoStack.Children.Clear();

        // Titre de l'expander
        if (ProfileDetailsTitle != null)
            ProfileDetailsTitle.Text = profile switch
            {
                GameBoosterProfile.Competitif => "Ce que fait le mode Compétitif",
                GameBoosterProfile.Gamer      => "Ce que fait le mode Gamer",
                GameBoosterProfile.Streamer   => "Ce que fait le mode Streamer",
                _                             => "Détails du profil"
            };

        if (ProfileDetailsIcon != null)
            ProfileDetailsIcon.Glyph = profile switch
            {
                GameBoosterProfile.Competitif => "", // éclair
                GameBoosterProfile.Gamer      => "", // manette
                GameBoosterProfile.Streamer   => "", // caméra
                _                             => ""
            };

        // Données par profil
        var rows = profile switch
        {
            GameBoosterProfile.Competitif => new[]
            {
                ("Plan d'alimentation",     "Ultime Performance",            true,  "Le plan le plus puissant de Windows, normalement caché. Votre PC ne fait aucune économie d'énergie."),
                ("Précision du timer",      "0.5 ms au lieu de 15.6 ms",    true,  "Windows gère le temps 31× plus précisément, vos frames sont plus régulières et votre souris plus réactive."),
                ("Cœurs CPU",               "Tous actifs en permanence",     true,  "Aucun cœur ne se met en veille. Chaque pic de charge est absorbé instantanément."),
                ("Limitation CPU",          "Désactivée",                    true,  "Windows ne ralentit jamais votre processeur, même s'il chauffe un peu."),
                ("Latence réseau",          "Optimisée (5 à 15 ms en moins)", true,  "Vos clics et actions arrivent plus vite sur les serveurs de jeu en multijoueur."),
                ("Bande passante",          "Sans limite",                   true,  "Windows arrête de restreindre votre connexion, tout passe en priorité vers le jeu."),
                ("Priorité CPU du jeu",     "Maximum",                       true,  "Le processeur consacre presque tout son temps à votre jeu, le reste du système attend."),
                ("Priorité GPU",            "Maximale",                      true,  "La carte graphique traite les images du jeu avant toute autre tâche."),
                ("Mémoire système",         "Optimisée",                     true,  "Windows garde ses propres données en RAM pour éviter les micro-ralentissements."),
                ("Game Mode",               "Activé",                        true,  "Windows détecte automatiquement votre jeu et lui donne la priorité sur tout le reste."),
                ("Xbox Game Bar",           "Désactivé",                     true,  "L'overlay Xbox et l'enregistrement automatique sont coupés, zéro perte de FPS."),
                ("Services inutiles",       "22 arrêtés temporairement",     true,  "Impression, indexation, télémétrie, Xbox sync… tout ce qui n'est pas utile au jeu est mis en pause."),
            },
            GameBoosterProfile.Gamer => new[]
            {
                ("Plan d'alimentation",     "Haute Performance",             true,  "Votre PC tourne à pleine puissance, aucune réduction de fréquence CPU ou GPU."),
                ("Précision du timer",      "Défaut Windows",                false, "Précision standard, suffisante pour les jeux AAA et les sessions longues."),
                ("Cœurs CPU",               "Gérés par Windows",             false, "Windows active les cœurs selon les besoins, comportement habituel préservé."),
                ("Limitation CPU",          "Désactivée",                    true,  "Le processeur tourne sans bride pendant toute la session de jeu."),
                ("Latence réseau",          "Optimisée",                     true,  "Les données réseau sont envoyées plus rapidement vers les serveurs multijoueur."),
                ("Bande passante",          "Sans limite",                   true,  "Toute votre connexion est disponible pour le jeu."),
                ("Priorité CPU du jeu",     "Élevée",                        true,  "Le jeu passe devant les tâches d'arrière-plan pour les ressources processeur."),
                ("Priorité GPU",            "Maximale",                      true,  "La carte graphique se concentre sur le rendu du jeu en priorité."),
                ("Game Mode",               "Activé",                        true,  "Windows reconnaît votre jeu et lui alloue plus de ressources automatiquement."),
                ("Xbox Game Bar",           "Désactivé",                     true,  "Pas d'enregistrement en tâche de fond qui grignote vos performances."),
                ("Services inutiles",       "13 arrêtés temporairement",     true,  "Impression, indexation de fichiers, télémétrie… mis en pause le temps de jouer."),
            },
            GameBoosterProfile.Streamer => new[]
            {
                ("Plan d'alimentation",     "Haute Performance",             true,  "Assez de puissance pour faire tourner le jeu et OBS en même temps sans ralentir."),
                ("Précision du timer",      "Défaut Windows",                false, "La précision standard convient bien à l'encodage vidéo, pas besoin d'aller plus loin."),
                ("Cœurs CPU",               "Gérés par Windows",             false, "Windows répartit les cœurs entre le jeu et OBS, on ne force rien pour rester stable."),
                ("Limitation CPU",          "Conservée",                     false, "Gardée active pour qu'OBS ne soit pas pénalisé si le CPU monte en température."),
                ("Latence réseau",          "Non modifiée",                  false, "On ne touche pas au réseau pour que l'upload de votre stream reste stable."),
                ("Bande passante",          "Sans limite",                   true,  "Toute la connexion disponible, jeu en ligne et stream en simultané sans se gêner."),
                ("Priorité CPU du jeu",     "Modérée",                       true,  "Le jeu est prioritaire, mais OBS garde assez de ressources pour encoder sans saccade."),
                ("Priorité GPU",            "Maximale",                      true,  "La carte graphique se concentre sur le rendu, OBS utilise son propre encodeur matériel."),
                ("Game Mode",               "Activé",                        true,  "Windows optimise les ressources du jeu sans couper celles dont OBS a besoin."),
                ("Xbox Game Bar",           "Conservé",                      false, "Certains streamers utilisent ses fonctions de capture, on le laisse disponible."),
                ("Services inutiles",       "9 arrêtés temporairement",      true,  "Seulement les services vraiment inutiles, tout ce qui peut aider OBS est préservé."),
            },
            _ => Array.Empty<(string, string, bool, string)>()
        };

        foreach (var (label, value, enabled, desc) in rows)
        {

            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.Style = (Style)Application.Current.Resources["AppItemRowStyle"];

            var textStack = new StackPanel { Spacing = 2 };
            textStack.Children.Add(new TextBlock
            {
                Text = label,
                Style = (Style)Application.Current.Resources["AppItemTitleStyle"]
            });
            textStack.Children.Add(new TextBlock
            {
                Text = desc,
                Style = (Style)Application.Current.Resources["AppItemDescriptionStyle"],
                TextWrapping = TextWrapping.Wrap
            });
            Grid.SetColumn(textStack, 0);

            var badge = new Border
            {
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 3, 8, 3),
                VerticalAlignment = VerticalAlignment.Center,
                Background = new SolidColorBrush(enabled
                    ? Windows.UI.Color.FromArgb(30, 0, 120, 215)
                    : Windows.UI.Color.FromArgb(20, 128, 128, 128))
            };
            var badgeText = new TextBlock
            {
                Text = value,
                Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                Foreground = new SolidColorBrush(enabled ? Colors.CornflowerBlue : Colors.Gray),
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            };
            badge.Child = badgeText;
            Grid.SetColumn(badge, 1);

            row.Children.Add(textStack);
            row.Children.Add(badge);
            ProfileInfoStack.Children.Add(row);
        }
    }

    private void SyncProfileButtons(GameBoosterProfile profile)
    {
        // BtnGamer    → Compétitif
        // BtnStreamer → Gamer
        // BtnMultitask→ Streamer
        // BtnCustom   → Custom
        _syncing = true;
        try
        {
            if (BtnGamer != null)    BtnGamer.IsChecked    = profile == GameBoosterProfile.Competitif;
            if (BtnStreamer != null) BtnStreamer.IsChecked  = profile == GameBoosterProfile.Gamer;
            if (BtnMultitask != null)BtnMultitask.IsChecked = profile == GameBoosterProfile.Streamer;
            if (BtnCustom != null)   BtnCustom.IsChecked   = profile == GameBoosterProfile.Custom;
        }
        finally { _syncing = false; }
    }

    private void UpdateAdvancedOptionsFromConfig()
    {
        var config = _gameBoosterService.CustomConfig;
        if (config == null) return;

        if (ChkGameMode != null)       ChkGameMode.IsChecked       = config.EnableGameMode;
        if (ChkXboxGameBar != null)    ChkXboxGameBar.IsChecked    = config.DisableXboxGameBar;
        if (ChkHighPriority != null)   ChkHighPriority.IsChecked   = config.SetHighPriority;
        if (ChkServices != null)       ChkServices.IsChecked       = config.ServicesToStop.Count > 0;
        if (ChkRegistry != null)       ChkRegistry.IsChecked       = config.DisableNetworkThrottling;
        if (ChkHighPerfPower != null)  ChkHighPerfPower.IsChecked  = config.PowerPlan != "Balanced";
    }

    private void SaveCustomConfig()
    {
        var config = _gameBoosterService.CustomConfig;
        config.EnableGameMode       = ChkGameMode.IsChecked ?? true;
        config.DisableXboxGameBar   = ChkXboxGameBar.IsChecked ?? true;
        config.SetHighPriority      = ChkHighPriority.IsChecked ?? true;
        config.DisableNetworkThrottling = ChkRegistry.IsChecked ?? true;
        config.DisableNagle         = ChkRegistry.IsChecked ?? true;
        config.PowerPlan            = (ChkHighPerfPower.IsChecked ?? true) ? "HighPerformance" : "Balanced";
    }

    private async void BtnToggle_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_gameBoosterService.ActiveProfile == GameBoosterProfile.Custom)
                SaveCustomConfig();

            if (_gameBoosterService.IsMonitoring)
            {
                await Lancer(BtnToggle, "Arrêt du Booster", (_, _) => _gameBoosterService.StopMonitoring());
            }
            else
            {
                await Lancer(BtnToggle, "Démarrage du Booster", (_, _) => _gameBoosterService.StartMonitoring());
            }
        }
        catch (Exception ex)
        {
            LogStore.Append($"[GameBooster][UI] BtnToggle_Click: {ex}");
        }
    }

    private void UpdateUI()
    {
        bool isActive = _gameBoosterService.IsMonitoring;

        StatusDot.Background = new SolidColorBrush(isActive ? Colors.LimeGreen : Colors.Gray);
        StatusText.Text = isActive ? "Moteur actif" : "Moteur éteint";
        StatusDesc.Text = isActive
            ? $"Le booster est en attente du lancement d'un jeu (Profil: {_gameBoosterService.ActiveProfile})."
            : "Sélectionnez un profil et activez le booster pour optimiser votre session de jeu.";

        BtnToggle.Content = isActive ? "Arrêter le Booster" : "Activer le Booster";

        BtnGamer.IsEnabled = !isActive;
        BtnStreamer.IsEnabled = !isActive;
        BtnMultitask.IsEnabled = !isActive;
        BtnCustom.IsEnabled = !isActive;
        AdvancedOptionsExpander.IsEnabled = !isActive;
        MonitoringSection.Visibility = isActive ? Visibility.Visible : Visibility.Collapsed;

        if (isActive)
            UpdateMonitoringDetails();
        else
            _detectedGames.Clear();
    }

    private void UpdateMonitoringDetails()
    {
        NoGameText.Visibility = _detectedGames.Count > 0 ? Visibility.Collapsed : Visibility.Visible;

        ActiveOptimizationsStack.Children.Clear();

        var cfg = _gameBoosterService.CustomConfig;
        bool isCustom = _gameBoosterService.ActiveProfile == GameBoosterProfile.Custom;

        // Afficher les optimisations réellement appliquées
        var profile = _gameBoosterService.ActiveProfile;
        AddOptimizationItem($"Profil actif : {profile}");

        var planLabel = profile switch
        {
            GameBoosterProfile.Competitif => "Ultime Performance",
            GameBoosterProfile.Gamer      => "Haute Performance",
            GameBoosterProfile.Streamer   => "Haute Performance",
            _                             => isCustom ? cfg.PowerPlan : "?"
        };
        AddOptimizationItem($"Plan d'alimentation : {planLabel}");

        if (profile == GameBoosterProfile.Competitif)
        {
            AddOptimizationItem("Timer résolution : 0.5 ms");
            AddOptimizationItem("Core Parking : désactivé");
            AddOptimizationItem("Power Throttling : désactivé");
            AddOptimizationItem("Nagle Algorithm : désactivé");
        }
        if (profile != GameBoosterProfile.Streamer)
            AddOptimizationItem("Network Throttling : désactivé");

        AddOptimizationItem("Game Mode : activé");
        AddOptimizationItem($"Win32PrioritySeparation : optimisé");
        AddOptimizationItem($"GPU Priority : 8 / CPU : 6");
    }

    private void AddOptimizationItem(string text)
    {
        var stack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        stack.Children.Add(new FontIcon { Glyph = "", FontSize = 12, Foreground = new SolidColorBrush(Colors.LimeGreen), VerticalAlignment = VerticalAlignment.Center });
        stack.Children.Add(new TextBlock { Text = text, Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"] });
        ActiveOptimizationsStack.Children.Add(stack);
    }

    private async System.Threading.Tasks.Task Lancer(Button? btn, string label, Action<Action<string>, System.Threading.CancellationToken> work)
    {
        if (btn != null) ButtonBusyHelper.SetBusy(btn, true);
        try { await TaskManager.RunAsync(label, work); }
        catch { /* erreur déjà journalisée */ }
        finally
        {
            if (btn != null) ButtonBusyHelper.SetBusy(btn, false);
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed) return;
        if (disposing)
        {
            // On ne dispose pas le service (il appartient à App.GameBooster),
            // on se contente de se désabonner des événements.
            _gameBoosterService.OnLog                  -= OnServiceLog;
            _gameBoosterService.OnMonitoringStateChanged -= OnMonitoringStateChanged;
            _gameBoosterService.OnGameStarted          -= OnGameStarted;
            _gameBoosterService.OnGameStopped          -= OnGameStopped;
        }
        _disposed = true;
    }

    ~PageGameBooster()
    {
        Dispose(false);
    }
}
