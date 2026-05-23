using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Assistools.Services;

namespace Assistools.Pages;

public sealed partial class PageAuto : Page
{
    private enum EtapeEtat { Pending, Active, Done, Error, Skipped }

    private sealed class Etape
    {
        public required string Glyph { get; init; }
        public required string Nom { get; init; }
        public required string Duree { get; init; }
        public required Action<Action<string>, CancellationToken> Action { get; init; }
        public bool Selectionne { get; set; } = true;
        public EtapeEtat Etat { get; set; } = EtapeEtat.Pending;
        public FrameworkElement? Container { get; set; }
        public FontIcon? IconStatus { get; set; }
        public ProgressRing? Spinner { get; set; }
        public CheckBox? Check { get; set; }
    }

    private readonly List<Etape> _etapes;
    private CancellationTokenSource? _maintenanceCts;
    private DateTime _debut;
    private bool _enMaintenance;

    public PageAuto()
    {
        InitializeComponent();
        _etapes =
        [
            new() { Glyph = "", Nom = "Nettoyage",           Duree = "2 à 5 minutes",         Action = (log, _) => NettoyageService.LancerNettoyageDisque(log) },
            new() { Glyph = "", Nom = "Réparation",          Duree = "15 minutes à 1 heure",  Action = (log, _) => ReparationService.LancerReparationSysteme(log) },
            new() { Glyph = "", Nom = "Mises à jour",        Duree = "10 minutes à 2 heures", Action = (log, _) => MajWindowsService.LancerMiseAJour(log) },
            new() { Glyph = "", Nom = "Sécurité",            Duree = "10 à 30 minutes",       Action = (log, _) => SecuriteService.MiseAJourEtAnalyse(log) },
            new() { Glyph = "", Nom = "Optimisation disque", Duree = "5 à 30 minutes",        Action = (log, _) => PerformanceService.OptimiserDisques(log) },
        ];
        BuildEtapes();

        Loaded += async (_, _) =>
        {
            TaskManager.OnStatusChanged += UpdateBusyUi;
            UpdateBusyUi();
            _ = ChargerInfoCpuAsync();
            await ChargerHealthScore();
        };
        Unloaded += (_, _) => TaskManager.OnStatusChanged -= UpdateBusyUi;
    }

    private async Task ChargerInfoCpuAsync()
    {
        var (nom, detail) = await Task.Run(HealthCheckService.ObtenirInfoCpu);
        ValCpu.Text    = nom;
        DetailCpu.Text = detail;
        _ = ChargerMetriquesExtras();
    }

    private async Task ChargerMetriquesExtras()
    {
        var ramTask  = Task.Run(HealthCheckService.ObtenirInfoRam);
        var diskTask = Task.Run(HealthCheckService.ObtenirTypeDisque);
        await Task.WhenAll(ramTask, diskTask);

        var (speedMhz, ddrType) = ramTask.Result;
        string typeDisque = diskTask.Result;

        DispatcherQueue.TryEnqueue(() =>
        {
            if (speedMhz > 0 || ddrType.Length > 0)
            {
                DetailMemoire2.Text = ddrType.Length > 0 && speedMhz > 0 ? $"{ddrType} · {speedMhz} MHz"
                                    : ddrType.Length > 0 ? ddrType
                                    : $"{speedMhz} MHz";
            }

            if (typeDisque.Length > 0)
                DetailDisque2.Text = typeDisque;
        });
    }

    private async Task ChargerHealthScore()
    {
        ScoreText.Text = "--";
        ScoreLabel.Text = "Analyse…";

        var report = await Task.Run(HealthCheckService.Calculer);
        AppliquerHealthReport(report);
    }

    private void AppliquerHealthReport(HealthReport r)
    {
        ScoreText.Text = r.Score.ToString();
        ScoreLabel.Text = r.Score switch
        {
            >= 80 => "Très bon",
            >= 60 => "Correct",
            >= 40 => "À améliorer",
            _     => "Critique",
        };

        var brandBlue = Windows.UI.Color.FromArgb(0xFF, 0x00, 0x69, 0xE2);
        var scoreColor = r.Score switch
        {
            >= 80 => brandBlue,
            >= 60 => Microsoft.UI.Colors.DodgerBlue,
            >= 40 => Microsoft.UI.Colors.Orange,
            _     => Microsoft.UI.Colors.OrangeRed,
        };
        ScoreCircle.Stroke = new SolidColorBrush(scoreColor);
        double circ = 2 * Math.PI * 60;
        double visible = circ * r.Score / 100.0;
        ScoreCircle.StrokeDashArray = [visible, circ];

        AppliquerCard(r.Disque,      IconDisque,  ValDisque,  DetailDisque, ProgressDisque);
        AppliquerCard(r.Memoire,     IconMemoire, ValMemoire, DetailMemoire, ProgressMemoire);
        AppliquerCard(r.Maintenance, IconMaint,   ValMaint,   DetailMaint);
    }

    private static void AppliquerCard(HealthMetric m, FontIcon icon, TextBlock value, TextBlock detail, ProgressBar? progressBar = null)
    {
        value.Text = m.Valeur;
        detail.Text = m.Detail;
        icon.Glyph = m.Glyph;
        var color = m.Statut switch
        {
            HealthStatus.Good     => Windows.UI.Color.FromArgb(0xFF, 0x00, 0x69, 0xE2),
            HealthStatus.Warning  => Microsoft.UI.Colors.Orange,
            HealthStatus.Critical => Microsoft.UI.Colors.OrangeRed,
            _ => Microsoft.UI.Colors.Gray,
        };
        icon.Foreground = new SolidColorBrush(color);
        if (progressBar != null)
        {
            progressBar.Value = m.ValeurPourcentage;
            progressBar.Foreground = new SolidColorBrush(color);
        }
    }

    private async void BtnActualiser_Click(object sender, RoutedEventArgs e)
    {
        BtnActualiser.IsEnabled = false;
        await ChargerHealthScore();
        _ = ChargerMetriquesExtras();
        BtnActualiser.IsEnabled = true;
    }

    private void UpdateBusyUi()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (!_enMaintenance)
            {
                BtnDemarrer.IsEnabled = TaskManager.Status == JobStatus.Idle;
                if (TaskManager.Status == JobStatus.Running)
                {
                    InfoStatus.Message = $"⚙ Tâche en cours : {TaskManager.CurrentLabel}, voir le Journal";
                    InfoStatus.Severity = InfoBarSeverity.Informational;
                    InfoStatus.IsOpen = true;
                }
                else
                {
                    InfoStatus.IsOpen = false;
                }
            }
        });
    }

    private void BuildEtapes()
    {
        EtapesPanel.Children.Clear();
        foreach (var etape in _etapes)
        {
            var grid = new Grid
            {
                Padding = new Thickness(12, 8, 12, 8),
                CornerRadius = new CornerRadius(4),
                Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"]
            };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var checkBox = new CheckBox
            {
                IsChecked = etape.Selectionne,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            checkBox.Checked   += (s, e) => { etape.Selectionne = true; };
            checkBox.Unchecked += (s, e) => { etape.Selectionne = false; };
            Grid.SetColumn(checkBox, 0);
            grid.Children.Add(checkBox);
            etape.Check = checkBox;

            var fontIcon = new FontIcon
            {
                Glyph = etape.Glyph,
                FontSize = 16,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (Brush)Application.Current.Resources["AccentTextFillColorPrimaryBrush"]
            };
            Grid.SetColumn(fontIcon, 1);
            grid.Children.Add(fontIcon);

            var infoStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            infoStack.Children.Add(new TextBlock
            {
                Text = etape.Nom,
                Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"],
                FontSize = 13
            });
            infoStack.Children.Add(new TextBlock
            {
                Text = $"Durée estimée : {etape.Duree}",
                FontSize = 11,
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
            });
            Grid.SetColumn(infoStack, 2);
            grid.Children.Add(infoStack);

            var statusGrid = new Grid { Width = 20, Height = 20, VerticalAlignment = VerticalAlignment.Center };
            var iconStatus = new FontIcon
            {
                FontSize = 14,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = Visibility.Collapsed
            };
            var spinner = new ProgressRing
            {
                Width = 14,
                Height = 14,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = Visibility.Collapsed
            };
            statusGrid.Children.Add(iconStatus);
            statusGrid.Children.Add(spinner);
            Grid.SetColumn(statusGrid, 3);
            grid.Children.Add(statusGrid);

            etape.Container = grid;
            etape.IconStatus = iconStatus;
            etape.Spinner = spinner;

            EtapesPanel.Children.Add(grid);
        }
    }

    private void SetEtapeEtat(Etape e, EtapeEtat etat)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            e.Etat = etat;
            if (e.Container == null || e.IconStatus == null || e.Spinner == null) return;

            e.Container.Opacity = etat == EtapeEtat.Skipped ? 0.35 : 1.0;

            switch (etat)
            {
                case EtapeEtat.Pending:
                    e.IconStatus.Visibility = Visibility.Collapsed;
                    e.Spinner.IsActive = false;
                    e.Spinner.Visibility = Visibility.Collapsed;
                    break;
                case EtapeEtat.Active:
                    e.IconStatus.Visibility = Visibility.Collapsed;
                    e.Spinner.IsActive = true;
                    e.Spinner.Visibility = Visibility.Visible;
                    break;
                case EtapeEtat.Done:
                    e.IconStatus.Glyph = "";
                    e.IconStatus.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x00, 0x69, 0xE2));
                    e.IconStatus.Visibility = Visibility.Visible;
                    e.Spinner.IsActive = false;
                    e.Spinner.Visibility = Visibility.Collapsed;
                    break;
                case EtapeEtat.Error:
                    e.IconStatus.Glyph = "";
                    e.IconStatus.Foreground = new SolidColorBrush(Microsoft.UI.Colors.OrangeRed);
                    e.IconStatus.Visibility = Visibility.Visible;
                    e.Spinner.IsActive = false;
                    e.Spinner.Visibility = Visibility.Collapsed;
                    break;
                case EtapeEtat.Skipped:
                    e.IconStatus.Glyph = "";
                    e.IconStatus.Foreground = (Brush)Application.Current.Resources["TextFillColorDisabledBrush"];
                    e.IconStatus.Visibility = Visibility.Visible;
                    break;
            }
        });
    }

    private async void BtnDemarrer_Click(object sender, RoutedEventArgs e)
    {
        if (_enMaintenance) return;
        if (TaskManager.Status != JobStatus.Idle)
        {
            await new ContentDialog
            {
                Title = "Une opération est déjà en cours",
                Content = $"Attendez la fin de : {TaskManager.CurrentLabel}",
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot,
            }.ShowAsync();
            return;
        }

        bool anySelected = false;
        foreach (var etape in _etapes)
        {
            if (etape.Selectionne) anySelected = true;
        }

        if (!anySelected)
        {
            await new ContentDialog
            {
                Title = "Aucune opération sélectionnée",
                Content = "Veuillez sélectionner au moins une opération de maintenance à exécuter.",
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot,
            }.ShowAsync();
            return;
        }

        for (int i = 0; i < _etapes.Count; i++)
            SetEtapeEtat(_etapes[i], _etapes[i].Selectionne ? EtapeEtat.Pending : EtapeEtat.Skipped);

        _enMaintenance = true;
        _maintenanceCts = new CancellationTokenSource();
        _debut = DateTime.Now;

        BtnDemarrer.IsEnabled = false;
        BtnDemarrerText.Text = "Maintenance en cours…";

        foreach (var etape in _etapes)
        {
            if (etape.Check != null) etape.Check.IsEnabled = false;
        }
        CbRestore.IsEnabled = false;

        BtnAnnuler.Visibility = Visibility.Visible;
        BtnAnnuler.IsEnabled = true;
        BtnAnnuler.Content = "Annuler";
        ProgressGlobal.Visibility = Visibility.Visible;
        ProgressGlobal.Maximum = _etapes.Count;
        ProgressGlobal.Value = 0;

        _creerPointRestauration = CbRestore.IsChecked == true;
        if (_creerPointRestauration)
        {
            SetStatus("Création d'un point de restauration…", InfoBarSeverity.Informational);
            await TaskManager.RunAsync("Point de restauration",
                (log, _) =>
                {
                    RestaurationService.NettoyerVieuxPoints(3, log);
                    RestaurationService.CreerPoint($"Assistools — Maintenance {DateTime.Now:yyyy-MM-dd HH:mm}", log);
                });
        }

        int reussies = 0, echecs = 0, sautees = 0;
        int idx = 0;

        foreach (var etape in _etapes)
        {
            idx++;
            if (!etape.Selectionne) { sautees++; continue; }
            if (_maintenanceCts.Token.IsCancellationRequested)
            {
                SetEtapeEtat(etape, EtapeEtat.Skipped);
                sautees++;
                continue;
            }

            SetStatus($"[{idx}/{_etapes.Count}] {etape.Nom} ({etape.Duree})…", InfoBarSeverity.Informational);
            SetEtapeEtat(etape, EtapeEtat.Active);

            try
            {
                await TaskManager.RunAsync(etape.Nom, etape.Action);
                SetEtapeEtat(etape, EtapeEtat.Done);
                reussies++;
            }
            catch (OperationCanceledException)
            {
                SetEtapeEtat(etape, EtapeEtat.Skipped);
                sautees++;
            }
            catch
            {
                SetEtapeEtat(etape, EtapeEtat.Error);
                echecs++;
            }

            DispatcherQueue.TryEnqueue(() => ProgressGlobal.Value = idx);
        }

        var duree = DateTime.Now - _debut;
        if (reussies > 0) HealthCheckService.EnregistrerMaintenance();
        SetStatus($"Maintenance terminée : {reussies} réussie(s), {echecs} échec(s), {sautees} sautée(s), {FormatDuree(duree)}",
            echecs == 0 ? InfoBarSeverity.Success : InfoBarSeverity.Warning);

        _ = ChargerHealthScore();

        BtnDemarrerText.Text = "Lancer la maintenance";
        BtnDemarrer.IsEnabled = true;

        foreach (var etape in _etapes)
        {
            if (etape.Check != null) etape.Check.IsEnabled = true;
        }
        CbRestore.IsEnabled = true;

        BtnAnnuler.Visibility = Visibility.Collapsed;
        ProgressGlobal.Visibility = Visibility.Collapsed;
        _enMaintenance = false;

        await AfficherResumeFinal(reussies, echecs, sautees, duree);
    }

    private void BtnAnnuler_Click(object sender, RoutedEventArgs e)
    {
        _maintenanceCts?.Cancel();
        TaskManager.Cancel();
        BtnAnnuler.IsEnabled = false;
        BtnAnnuler.Content = "Arrêt…";
        SetStatus("Annulation après l'étape en cours…", InfoBarSeverity.Warning);
    }

    private bool _creerPointRestauration = true;

    private async Task AfficherResumeFinal(int reussies, int echecs, int sautees, TimeSpan duree)
    {
        var sp = new StackPanel { Spacing = 12 };

        var headerBox = new Border
        {
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14, 12, 14, 12),
            Background = (Brush)Application.Current.Resources[
                echecs == 0 ? "SystemFillColorSuccessBackgroundBrush" : "SystemFillColorCautionBackgroundBrush"],
        };
        var headerStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        headerStack.Children.Add(new FontIcon
        {
            Glyph = echecs == 0 ? "" : "",
            FontSize = 22,
            Foreground = new SolidColorBrush(echecs == 0 ? Microsoft.UI.Colors.Green : Microsoft.UI.Colors.DarkOrange),
            VerticalAlignment = VerticalAlignment.Center,
        });
        headerStack.Children.Add(new StackPanel
        {
            Spacing = 2,
            Children =
            {
                new TextBlock
                {
                    Text = echecs == 0 ? "Votre ordinateur a été optimisé en toute sécurité" : "Maintenance terminée avec quelques erreurs",
                    Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"],
                },
                new TextBlock
                {
                    Text = $"Durée : {FormatDuree(duree)} · {reussies} réussie(s)" +
                           (echecs > 0 ? $" · {echecs} échec(s)" : "") +
                           (sautees > 0 ? $" · {sautees} sautée(s)" : ""),
                    FontSize = 12,
                    Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                },
            },
        });
        headerBox.Child = headerStack;
        sp.Children.Add(headerBox);

        var rapport = NettoyageService.DernierRapport;
        if (rapport != null && rapport.Categories.Count > 0)
        {
            sp.Children.Add(new TextBlock
            {
                Text = $"Espace libéré : {NettoyageService.FormatTaille(rapport.TotalOctets)} · {rapport.TotalFichiers} fichier(s)",
                Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"],
                Margin = new Thickness(0, 4, 0, 0),
            });

            var detailStack = new StackPanel { Spacing = 6 };
            foreach (var cat in rapport.Categories)
            {
                if (cat.FichiersSupprimes == 0 && cat.OctetsLiberes == 0) continue;

                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var nameText = new TextBlock { Text = cat.Nom, FontSize = 13 };
                Grid.SetColumn(nameText, 0);

                var valueText = new TextBlock
                {
                    Text = NettoyageService.FormatTaille(cat.OctetsLiberes),
                    FontSize = 13,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Foreground = (Brush)Application.Current.Resources["AccentTextFillColorPrimaryBrush"],
                };
                Grid.SetColumn(valueText, 1);

                grid.Children.Add(nameText);
                grid.Children.Add(valueText);
                detailStack.Children.Add(grid);
            }

            sp.Children.Add(new ScrollViewer
            {
                MaxHeight = 220,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = detailStack,
            });
        }

        sp.Children.Add(new InfoBar
        {
            IsOpen = true,
            IsClosable = false,
            Severity = InfoBarSeverity.Success,
            Title = "Tout est prêt",
            Message = "Redémarrez maintenant pour appliquer toutes les améliorations.",
            Margin = new Thickness(0, 4, 0, 0),
        });

        sp.Children.Add(new TextBlock
        {
            Text = "Le détail complet est disponible dans l'historique.",
            FontSize = 11,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
        });

        var dialog = new ContentDialog
        {
            Title = "Maintenance terminée",
            Content = sp,
            PrimaryButtonText = "Redémarrer maintenant",
            SecondaryButtonText = "Voir l'historique",
            CloseButtonText = "Plus tard",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.XamlRoot,
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            DemanderRedemarrage();
        }
        else if (result == ContentDialogResult.Secondary)
        {
            if (((App)Application.Current).MainWindow?.Content is NavigationView nav)
            {
                foreach (var item in nav.FooterMenuItems)
                {
                    if (item is NavigationViewItem nvi && nvi.Tag?.ToString() == "Logs")
                    {
                        nav.SelectedItem = nvi;
                        break;
                    }
                }
            }
        }
    }

    private static void DemanderRedemarrage()
    {
        try
        {
            using var p = new System.Diagnostics.Process();
            p.StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "shutdown.exe",
                Arguments = "/r /t 30 /c \"Redémarrage planifié par Assistools dans 30 secondes. Vous pouvez l'annuler avec : shutdown /a\"",
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            p.Start();
        }
        catch { }
    }

    private void SetStatus(string message, InfoBarSeverity severity)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            InfoStatus.Message = message;
            InfoStatus.Severity = severity;
            InfoStatus.IsOpen = true;
        });
    }

    private static string FormatDuree(TimeSpan d)
    {
        if (d.TotalHours >= 1) return $"{(int)d.TotalHours} h {d.Minutes} min";
        if (d.TotalMinutes >= 1) return $"{(int)d.TotalMinutes} min {d.Seconds} s";
        return $"{d.Seconds} s";
    }
}
