using System;
using System.Threading;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Assistools.Services;

namespace Assistools.Pages;

public sealed partial class PagePerf : Page
{
    private bool _initDone;

    public PagePerf()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        var mode = await System.Threading.Tasks.Task.Run(PerformanceService.GetModePowerActuel);
        _initDone = false;
        switch (mode)
        {
            case PerformanceService.ModePower.Economie:    RbEconomie.IsChecked    = true; break;
            case PerformanceService.ModePower.Performance: RbPerformance.IsChecked = true; break;
            default:                                       RbEquilibre.IsChecked   = true; break;
        }

        var memOptimisee = await System.Threading.Tasks.Task.Run(PerformanceService.EstMemVirtuelleOptimisee);
        ToggleMemVirt.IsOn = memOptimisee;
        UpdateMemVirtLabel(memOptimisee);

        _initDone = true;
    }

    private void PowerMode_Checked(object sender, RoutedEventArgs e)
    {
        if (!_initDone) return;
        if (sender is not RadioButton rb) return;

        var mode = rb.Tag switch
        {
            "Economie"    => PerformanceService.ModePower.Economie,
            "Performance" => PerformanceService.ModePower.Performance,
            _             => PerformanceService.ModePower.Equilibre,
        };
        Lancer(null, $"Plan d'alimentation : {rb.Tag}", (log, _) => PerformanceService.SetModePower(mode, log));
    }

    private void BtnDisques_Click(object sender, RoutedEventArgs e) =>
        Lancer(BtnDisques, "Optimisation des disques", (log, _) => PerformanceService.OptimiserDisques(log));

    private void ToggleMemVirt_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_initDone) return;
        bool optimise = ToggleMemVirt.IsOn;
        UpdateMemVirtLabel(optimise);
        if (optimise)
            Lancer(null, "Optimisation mémoire virtuelle", (log, _) => PerformanceService.AjusterMemVirtuelle(log));
        else
            Lancer(null, "Réinitialisation mémoire virtuelle", (log, _) => PerformanceService.MemVirtuelleParDefaut(log));
    }

    private void UpdateMemVirtLabel(bool optimise)
    {
        MemVirtLabel.Text = optimise ? "Optimisée" : "Gérée par Windows";
        MemVirtDesc.Text  = optimise
            ? "Fichier d'échange dimensionné selon votre RAM."
            : "Windows gère automatiquement la taille du fichier d'échange.";
    }

    private async void Lancer(Button? btn, string label, Action<Action<string>, CancellationToken> work)
    {
        if (btn != null) ButtonBusyHelper.SetBusy(btn, true);
        try { await TaskManager.RunAsync(label, work); }
        catch { /* erreur déjà journalisée */ }
        finally
        {
            if (btn != null) ButtonBusyHelper.SetBusy(btn, false);
        }
    }
}
