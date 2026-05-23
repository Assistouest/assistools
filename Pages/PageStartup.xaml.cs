using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Assistools.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

// SelectionChangedEventArgs n'est plus utilisé

namespace Assistools.Pages;

public sealed partial class PageStartup : Page
{
    private List<StartupService.StartupItem> _startupItems = [];
    private List<StartupService.ServiceItem> _serviceItems = [];
    private bool _building;

    private bool _showingDemarrage = true;

    public PageStartup()
    {
        InitializeComponent();
        Loaded += (_, _) => _ = ChargerDemarrage();
    }

    private void BtnDemarrage_Click(object sender, RoutedEventArgs e)
    {
        if (_showingDemarrage || _building) return;
        _showingDemarrage = true;
        BtnDemarrage.Style = (Style)Application.Current.Resources["AccentButtonStyle"];
        BtnServices.Style = (Style)Application.Current.Resources["DefaultButtonStyle"];
        _ = ChargerDemarrage();
    }

    private void BtnServices_Click(object sender, RoutedEventArgs e)
    {
        if (!_showingDemarrage || _building) return;
        _showingDemarrage = false;
        BtnServices.Style = (Style)Application.Current.Resources["AccentButtonStyle"];
        BtnDemarrage.Style = (Style)Application.Current.Resources["DefaultButtonStyle"];
        _ = ChargerServices();
    }

    // ── DÉMARRAGE ────────────────────────────────────────────────────────────

    private async Task ChargerDemarrage()
    {
        AfficherChargement("Lecture du démarrage…");
        _startupItems = await Task.Run(StartupService.ObtenirDemarrage);
        ContentPanel.Children.Clear();

        if (_startupItems.Count == 0)
        {
            ContentPanel.Children.Add(new TextBlock
            {
                Text = "Aucun programme au démarrage trouvé.",
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                Margin = new Thickness(0, 16, 0, 0),
            });
        }
        else
        {
            var card = CreerCard();
            var stack = (StackPanel)card.Child;
            bool first = true;

            foreach (var item in _startupItems)
            {
                if (!first) stack.Children.Add(new MenuFlyoutSeparator());
                first = false;
                stack.Children.Add(CreerLigneDemarrage(item));
            }

            ContentPanel.Children.Add(card);
        }

        MasquerChargement();
    }

    private UIElement CreerLigneDemarrage(StartupService.StartupItem item)
    {
        var grid = new Grid { Padding = new Thickness(16, 12, 16, 12) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var info = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        info.Children.Add(new TextBlock
        {
            Text = item.Name,
            Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"],
            FontSize = 13,
        });
        info.Children.Add(new TextBlock
        {
            Text = TronquerCommande(item.Command),
            FontSize = 11,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            TextWrapping = TextWrapping.NoWrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
        });
        info.Children.Add(new TextBlock
        {
            Text = item.Location,
            FontSize = 10,
            Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
        });
        Grid.SetColumn(info, 0);
        grid.Children.Add(info);

        var toggle = new ToggleSwitch
        {
            IsOn = item.IsEnabled,
            VerticalAlignment = VerticalAlignment.Center,
            OffContent = "",
            OnContent = "",
            Tag = item,
        };
        toggle.Toggled += ToggleDemarrage_Toggled;
        Grid.SetColumn(toggle, 1);
        grid.Children.Add(toggle);

        return grid;
    }

    private void ToggleDemarrage_Toggled(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleSwitch ts || ts.Tag is not StartupService.StartupItem item) return;
        bool activer = ts.IsOn; // capturer sur le thread UI avant Task.Run
        string action = activer ? "Activation" : "Désactivation";
        _ = TaskManager.RunAsync($"{action} démarrage : {item.Name}", (log, ct) =>
        {
            log($"{action} du programme au démarrage : {item.Name}");
            log($"Emplacement : {item.Location}");
            StartupService.DefinirDemarrage(item, activer);
            log($"[OK] {item.Name} : démarrage {(activer ? "activé" : "désactivé")}");
        });
    }

    // ── SERVICES ─────────────────────────────────────────────────────────────

    private async Task ChargerServices()
    {
        AfficherChargement("Lecture des services…");
        _serviceItems = await Task.Run(StartupService.ObtenirServices);
        ContentPanel.Children.Clear();

        // Séparer tiers / Windows (on n'affiche que les tiers par défaut)
        var tiers = _serviceItems.FindAll(s => !s.IsWindows);

        if (tiers.Count == 0)
        {
            ContentPanel.Children.Add(new TextBlock
            {
                Text = "Aucun service tiers trouvé.",
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                Margin = new Thickness(0, 16, 0, 0),
            });
        }
        else
        {
            var card = CreerCard();
            var stack = (StackPanel)card.Child;
            bool first = true;

            foreach (var svc in tiers)
            {
                if (!first) stack.Children.Add(new MenuFlyoutSeparator());
                first = false;
                stack.Children.Add(CreerLigneService(svc));
            }

            ContentPanel.Children.Add(card);
        }

        MasquerChargement();
    }

    private UIElement CreerLigneService(StartupService.ServiceItem svc)
    {
        var grid = new Grid { Padding = new Thickness(16, 12, 16, 12) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Badge état
        string etatText = svc.State == "Running" ? "En cours" : "Arrêté";
        var etatBrush = svc.State == "Running"
            ? (Brush)Application.Current.Resources["SystemFillColorSuccessBrush"]
            : (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"];

        var info = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        info.Children.Add(new TextBlock
        {
            Text = svc.DisplayName,
            Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"],
            FontSize = 13,
        });

        var statusRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        statusRow.Children.Add(new TextBlock
        {
            Text = etatText,
            FontSize = 11,
            Foreground = etatBrush,
        });
        statusRow.Children.Add(new TextBlock
        {
            Text = "·",
            FontSize = 11,
            Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
        });
        statusRow.Children.Add(new TextBlock
        {
            Text = svc.StartMode == "Auto" ? "Automatique" : svc.StartMode == "Manual" ? "Manuel" : "Désactivé",
            FontSize = 11,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
        });
        info.Children.Add(statusRow);
        info.Children.Add(new TextBlock
        {
            Text = TronquerCommande(svc.PathName),
            FontSize = 10,
            Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
            TextTrimming = TextTrimming.CharacterEllipsis,
        });

        Grid.SetColumn(info, 0);
        grid.Children.Add(info);

        var toggle = new ToggleSwitch
        {
            IsOn = svc.StartMode != "Disabled",
            VerticalAlignment = VerticalAlignment.Center,
            OffContent = "",
            OnContent = "",
            Tag = svc,
        };
        toggle.Toggled += ToggleService_Toggled;
        Grid.SetColumn(toggle, 1);
        grid.Children.Add(toggle);

        return grid;
    }

    private void ToggleService_Toggled(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleSwitch ts || ts.Tag is not StartupService.ServiceItem svc) return;
        string mode = ts.IsOn ? "Auto" : "Disabled"; // capturer sur le thread UI
        string action = ts.IsOn ? "Activation" : "Désactivation";
        _ = TaskManager.RunAsync($"{action} service : {svc.DisplayName}", (log, ct) =>
        {
            log($"{action} du service : {svc.DisplayName}");
            log($"Mode de démarrage cible : {(mode == "Auto" ? "Automatique" : "Désactivé")}");
            StartupService.DefinirService(svc, mode);
            log($"[OK] {svc.DisplayName} : démarrage {(mode == "Auto" ? "automatique" : "désactivé")}");
        });
    }

    // ── HELPERS UI ────────────────────────────────────────────────────────────

    private static Border CreerCard() => new Border
    {
        CornerRadius = new CornerRadius(8),
        BorderThickness = new Thickness(1),
        BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
        Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
        Child = new StackPanel(),
        Margin = new Thickness(0, 8, 0, 0),
    };

    private static string TronquerCommande(string cmd)
    {
        var s = cmd.Trim('"');
        var idx = s.IndexOf('"');
        if (idx > 0) s = s[..idx];
        return s.Length > 80 ? s[..77] + "…" : s;
    }

    private void AfficherChargement(string texte)
    {
        _building = true;
        LoadingText.Text = texte;
        LoadingOverlay.Visibility = Visibility.Visible;
    }

    private void MasquerChargement()
    {
        LoadingOverlay.Visibility = Visibility.Collapsed;
        _building = false;
    }
}
