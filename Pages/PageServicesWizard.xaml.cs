using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Assistools.Services;

namespace Assistools.Pages;

public sealed partial class PageServicesWizard : Page
{
    private List<(WizardCategory Cat, ServiceState[] States)> _categories = new();
    // null = pas de réponse, true = utilise, false = n'utilise pas
    private readonly Dictionary<string, bool?> _answers = new();
    private int _step = 0;

    public PageServicesWizard()
    {
        InitializeComponent();
        Loaded += (_, _) => Init();
    }

    private void Init()
    {
        _categories = ServiceWizardEngine.GetRelevantCategories();
        _answers.Clear();
        foreach (var (cat, _) in _categories) _answers[cat.Key] = null;

        TxtWelcomeCount.Text = _categories.Count == 0
            ? "Tous les services optionnels sont déjà désactivés sur ce PC. Rien à faire."
            : $"{_categories.Count} catégories pertinentes détectées sur ce PC.";

        BtnStart.IsEnabled = _categories.Count > 0;

        RestoreBar.IsOpen = ServiceWizardEngine.HasBackup();
        var backup = ServiceWizardEngine.LoadBackup();
        if (backup != null)
            RestoreBar.Message = $"Sauvegarde du {backup.CreatedAt} : {backup.Services.Count} service(s) modifié(s). Vous pouvez tout restaurer.";

        ShowPanel(PanelWelcome);
    }

    // ── Navigation panels ─────────────────────────────────────────────────────────
    private void ShowPanel(StackPanel toShow)
    {
        PanelWelcome.Visibility = toShow == PanelWelcome ? Visibility.Visible : Visibility.Collapsed;
        PanelQuestion.Visibility = toShow == PanelQuestion ? Visibility.Visible : Visibility.Collapsed;
        PanelSummary.Visibility = toShow == PanelSummary ? Visibility.Visible : Visibility.Collapsed;
        PanelResult.Visibility = toShow == PanelResult ? Visibility.Visible : Visibility.Collapsed;
    }

    private void BtnStart_Click(object sender, RoutedEventArgs e)
    {
        _step = 0;
        RenderStep();
        ShowPanel(PanelQuestion);
    }

    // ── Affichage d'une étape ─────────────────────────────────────────────────────
    private void RenderStep()
    {
        if (_step < 0) _step = 0;
        if (_step >= _categories.Count) { ShowSummary(); return; }

        var (cat, states) = _categories[_step];

        TxtStepIndicator.Text = $"Étape {_step + 1} / {_categories.Count}";
        StepProgress.Value = (double)(_step) / _categories.Count * 100;

        QIcon.Glyph = cat.Icon;
        QText.Text = cat.Question;
        QIfYes.Text = "✓ Si oui : " + cat.IfYesText;
        QIfNo.Text = "✗ Si non : " + cat.IfNoText;

        // Services list (uniquement ceux présents)
        QServicesList.Children.Clear();
        var presentServices = cat.Services
            .Zip(states, (s, st) => (Service: s, State: st))
            .Where(t => t.State.Exists)
            .ToList();

        QServicesHeader.Text = presentServices.Count == 1
            ? "Voir le service concerné"
            : $"Voir les {presentServices.Count} services concernés";

        foreach (var (svc, st) in presentServices)
        {
            var row = new StackPanel { Spacing = 2, Padding = new Thickness(0, 4, 0, 4) };
            row.Children.Add(new TextBlock
            {
                Text = $"{svc.Friendly}  ({svc.Name})",
                Style = (Style)Application.Current.Resources["AppItemTitleStyle"]
            });
            row.Children.Add(new TextBlock
            {
                Text = svc.Description,
                Style = (Style)Application.Current.Resources["AppItemDescriptionStyle"]
            });
            row.Children.Add(new TextBlock
            {
                Text = $"État actuel : {FormatStartType(st.StartType)}",
                Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorTertiaryBrush"]
            });
            QServicesList.Children.Add(row);
        }

        // Visuels boutons selon réponse mémorisée
        bool? prev = _answers[cat.Key];
        BtnYes.Opacity = prev == true ? 1.0 : (prev == false ? 0.5 : 1.0);
        BtnNo.Opacity  = prev == false ? 1.0 : (prev == true ? 0.5 : 1.0);

        BtnPrev.IsEnabled = _step > 0;
    }

    private static string FormatStartType(ServiceStartType t) => t switch
    {
        ServiceStartType.Automatic => "Démarrage automatique",
        ServiceStartType.Manual    => "Démarrage manuel",
        ServiceStartType.Disabled  => "Désactivé",
        ServiceStartType.Boot      => "Démarrage au boot",
        ServiceStartType.System    => "Démarrage système",
        _                          => "Inconnu",
    };

    private void BtnYes_Click(object sender, RoutedEventArgs e)
    {
        if (_step < _categories.Count) _answers[_categories[_step].Cat.Key] = true;
        Next();
    }

    private void BtnNo_Click(object sender, RoutedEventArgs e)
    {
        if (_step < _categories.Count) _answers[_categories[_step].Cat.Key] = false;
        Next();
    }

    private void BtnSkip_Click(object sender, RoutedEventArgs e)
    {
        if (_step < _categories.Count) _answers[_categories[_step].Cat.Key] = null;
        Next();
    }

    private void BtnPrev_Click(object sender, RoutedEventArgs e)
    {
        if (_step > 0) { _step--; RenderStep(); }
    }

    private void Next()
    {
        _step++;
        if (_step >= _categories.Count) ShowSummary();
        else RenderStep();
    }

    // ── Récapitulatif ─────────────────────────────────────────────────────────────
    private List<string> ComputeServicesToDisable()
    {
        var result = new List<string>();
        foreach (var (cat, states) in _categories)
        {
            if (_answers.TryGetValue(cat.Key, out var ans) && ans == false)
            {
                foreach (var (svc, st) in cat.Services.Zip(states, (s, st) => (s, st)))
                {
                    if (st.Exists && st.StartType != ServiceStartType.Disabled)
                        result.Add(svc.Name);
                }
            }
        }
        return result;
    }

    private void ShowSummary()
    {
        var toDisable = ComputeServicesToDisable();
        SummaryList.Children.Clear();

        if (toDisable.Count == 0)
        {
            TxtSummaryCount.Text = "Aucun service à désactiver d'après vos réponses. Vous pouvez revenir en arrière pour ajuster.";
            BtnApply.IsEnabled = false;
        }
        else
        {
            TxtSummaryCount.Text = $"{toDisable.Count} service(s) seront désactivé(s) :";
            BtnApply.IsEnabled = true;

            // Regrouper par catégorie pour la lecture
            foreach (var (cat, states) in _categories)
            {
                if (_answers.TryGetValue(cat.Key, out var ans) && ans == false)
                {
                    var present = cat.Services.Zip(states, (s, st) => (s, st))
                        .Where(t => t.st.Exists && t.st.StartType != ServiceStartType.Disabled)
                        .ToList();

                    if (present.Count == 0) continue;

                    var catBlock = new StackPanel { Spacing = 2, Padding = new Thickness(0, 4, 0, 4) };
                    catBlock.Children.Add(new TextBlock
                    {
                        Text = "• " + cat.Question.TrimEnd('?', ' '),
                        Style = (Style)Application.Current.Resources["AppItemTitleStyle"]
                    });
                    foreach (var (svc, _) in present)
                    {
                        catBlock.Children.Add(new TextBlock
                        {
                            Text = $"    – {svc.Friendly} ({svc.Name})",
                            Style = (Style)Application.Current.Resources["AppItemDescriptionStyle"]
                        });
                    }
                    SummaryList.Children.Add(catBlock);
                }
            }
        }

        ShowPanel(PanelSummary);
    }

    private void BtnSummaryBack_Click(object sender, RoutedEventArgs e)
    {
        _step = Math.Max(0, _categories.Count - 1);
        RenderStep();
        ShowPanel(PanelQuestion);
    }

    // ── Application ───────────────────────────────────────────────────────────────
    private async void BtnApply_Click(object sender, RoutedEventArgs e)
    {
        var toDisable = ComputeServicesToDisable();
        if (toDisable.Count == 0) return;

        BtnApply.IsEnabled = false;
        var sb = new StringBuilder();
        Action<string> log = line =>
        {
            sb.AppendLine(line);
            LogStore.Append("[ServicesWizard] " + line);
        };

        await Task.Run(() => ServiceWizardEngine.Apply(toDisable, log));

        ResultBar.Severity = InfoBarSeverity.Success;
        ResultBar.Title = "Services désactivés";
        ResultBar.Message = $"{toDisable.Count} service(s) traité(s). Vous pouvez tout restaurer si besoin.";
        ResultLog.Text = sb.ToString();

        // Refresh restore bar pour la prochaine visite
        RestoreBar.IsOpen = ServiceWizardEngine.HasBackup();

        ShowPanel(PanelResult);
    }

    // ── Restauration ──────────────────────────────────────────────────────────────
    private async void BtnRestore_Click(object sender, RoutedEventArgs e)
    {
        BtnRestore.IsEnabled = false;
        var sb = new StringBuilder();
        Action<string> log = line =>
        {
            sb.AppendLine(line);
            LogStore.Append("[ServicesWizard][Restore] " + line);
        };

        await Task.Run(() => ServiceWizardEngine.Restore(log));

        ResultBar.Severity = InfoBarSeverity.Success;
        ResultBar.Title = "Services restaurés";
        ResultBar.Message = "L'état initial a été remis en place.";
        ResultLog.Text = sb.ToString();

        RestoreBar.IsOpen = false;
        BtnRestore.IsEnabled = true;
        ShowPanel(PanelResult);

        // Recalcul des catégories pertinentes
        Init();
    }

    private void BtnDone_Click(object sender, RoutedEventArgs e)
    {
        Init();
    }
}
