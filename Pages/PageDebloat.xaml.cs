using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Assistools.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Assistools.Pages;

public sealed partial class PageDebloat : Page
{
    private readonly Dictionary<DebloatService.AppItem, CheckBox> _checkboxes = [];
    private bool _enCours;

    public PageDebloat()
    {
        InitializeComponent();
        Loaded += (_, _) => BuildListe();
    }

    private void BuildListe()
    {
        AppsPanel.Children.Clear();
        _checkboxes.Clear();

        var categories = DebloatService.Apps
            .GroupBy(a => a.Categorie)
            .OrderBy(g => g.Key);

        foreach (var groupe in categories)
        {
            var header = new TextBlock
            {
                Text = groupe.Key,
                Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"],
                Margin = new Thickness(0, 8, 0, 4),
            };
            AppsPanel.Children.Add(header);

            var card = new Border
            {
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(1),
                BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            };
            var stack = new StackPanel();

            bool first = true;
            foreach (var app in groupe)
            {
                if (!first)
                    stack.Children.Add(new MenuFlyoutSeparator());
                first = false;

                var grid = new Grid { Padding = new Thickness(16, 12, 16, 12) };
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var cb = new CheckBox
                {
                    IsChecked = app.SelectionneParDefaut,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 12, 0),
                };
                cb.Checked   += (_, _) => MettreAJourCompteur();
                cb.Unchecked += (_, _) => MettreAJourCompteur();
                Grid.SetColumn(cb, 0);
                grid.Children.Add(cb);
                _checkboxes[app] = cb;

                var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 2 };
                info.Children.Add(new TextBlock
                {
                    Text = app.Nom,
                    Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"],
                    FontSize = 13,
                });
                info.Children.Add(new TextBlock
                {
                    Text = app.Description,
                    FontSize = 11,
                    Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                    TextWrapping = TextWrapping.Wrap,
                });
                Grid.SetColumn(info, 1);
                grid.Children.Add(info);

                stack.Children.Add(grid);
            }

            card.Child = stack;
            AppsPanel.Children.Add(card);
        }

        MettreAJourCompteur();
    }

    private void MettreAJourCompteur()
    {
        int n = _checkboxes.Values.Count(cb => cb.IsChecked == true);
        SelectionLabel.Text = n == 0 ? "Aucune application sélectionnée"
                            : n == 1 ? "1 application sélectionnée"
                            : $"{n} applications sélectionnées";
        BtnSupprimer.IsEnabled = n > 0 && !_enCours;
    }

    private void BtnTout_Click(object sender, RoutedEventArgs e)
    {
        foreach (var cb in _checkboxes.Values) cb.IsChecked = true;
    }

    private void BtnRien_Click(object sender, RoutedEventArgs e)
    {
        foreach (var cb in _checkboxes.Values) cb.IsChecked = false;
    }

    private void BtnDefaut_Click(object sender, RoutedEventArgs e)
    {
        foreach (var (app, cb) in _checkboxes)
            cb.IsChecked = app.SelectionneParDefaut;
    }

    private async void BtnSupprimer_Click(object sender, RoutedEventArgs e)
    {
        var selection = _checkboxes
            .Where(kv => kv.Value.IsChecked == true)
            .Select(kv => kv.Key)
            .ToList();

        if (selection.Count == 0) return;

        string liste = string.Join("\n• ", selection.Select(a => a.Nom));
        var dialog = new ContentDialog
        {
            Title = "Confirmer la suppression",
            Content = new ScrollViewer
            {
                MaxHeight = 300,
                Content = new TextBlock
                {
                    Text = $"Les applications suivantes seront supprimées définitivement :\n\n• {liste}\n\nUn point de restauration sera créé automatiquement avant la suppression.",
                    TextWrapping = TextWrapping.Wrap,
                }
            },
            PrimaryButtonText = "Supprimer",
            CloseButtonText = "Annuler",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return;

        _enCours = true;
        BtnSupprimer.IsEnabled = false;
        BtnSupprimerText.Text = "Suppression en cours…";

        var appsToRemove = selection.ToList();
        await TaskManager.RunAsync("Nettoyage des applications", (log, ct) =>
        {
            log("Création d'un point de restauration…");
            RestaurationService.CreerPoint($"Assistools — Debloat {DateTime.Now:yyyy-MM-dd HH:mm}", log);
            log("✓ Point de restauration créé.");

            DebloatService.SupprimerApps(appsToRemove, log);

            log("✓ Suppression terminée. Un redémarrage peut être nécessaire.");
        });

        _enCours = false;
        BtnSupprimerText.Text = "Supprimer la sélection";
        MettreAJourCompteur();
    }
}
