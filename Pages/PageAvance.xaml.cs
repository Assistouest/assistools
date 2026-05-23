using System;
using System.IO;
using System.Threading;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using WinRT.Interop;
using Assistools.Services;

namespace Assistools.Pages;

public sealed partial class PageAvance : Page
{
    public PageAvance()
    {
        InitializeComponent();
        ToggleStorageSense.IsOn = AutoNettoyageService.EstActive();

        Loaded += (_, _) =>
        {
            TaskManager.OnStatusChanged += UpdateUi;
            TaskManager.OnLineUpdated += OnLineUpdated;
            UpdateUi();
        };
        Unloaded += (_, _) =>
        {
            TaskManager.OnStatusChanged -= UpdateUi;
            TaskManager.OnLineUpdated -= OnLineUpdated;
        };
    }

    private void OnLineUpdated(string line)
    {
        // Plus besoin : géré globalement par MainWindow.GlobalTaskBar
    }

    private void UpdateUi()
    {
        // Plus de gestion locale d'InfoBar : bandeau global dans MainWindow
        // Les boutons sont gérés par ButtonBusyHelper individuellement
    }

    private void BtnNettoyage_Click(object sender, RoutedEventArgs e) =>
        Lancer(BtnNettoyage, "Nettoyage du disque", (log, _) => NettoyageService.LancerNettoyageDisque(log));

    private void BtnReparation_Click(object sender, RoutedEventArgs e) =>
        Lancer(BtnReparation, "Réparation du système", (log, _) => ReparationService.LancerReparationSysteme(log));

    private void BtnAntivirus_Click(object sender, RoutedEventArgs e) =>
        Lancer(BtnAntivirus, "Analyse antivirus", (log, _) => SecuriteService.MiseAJourEtAnalyse(log));

    private void ToggleStorageSense_Toggled(object sender, RoutedEventArgs e)
    {
        if (ToggleStorageSense.IsOn)
            Lancer(null, "Activation Storage Sense", (log, _) => AutoNettoyageService.Configurer(log));
        else
            Lancer(null, "Désactivation Storage Sense", (log, _) => AutoNettoyageService.Reinitialiser(log));
    }

    private void BtnReparerStore_Click(object sender, RoutedEventArgs e) =>
        Lancer(BtnReparerStore, "Réparation Microsoft Store", (log, _) => ReparationAvanceeService.ReparerMicrosoftStore(log));

    private async void BtnResetUpdate_Click(object sender, RoutedEventArgs e)
    {
        if (!await Confirmer("Réinitialiser Windows Update ?",
            "Cette opération arrête les services Windows Update, renomme les caches puis redémarre les services. " +
            "Aucun risque pour vos données.")) return;
        Lancer(BtnResetUpdate, "Réparation Windows Update", (log, _) => ReparationAvanceeService.ResetWindowsUpdate(log));
    }

    private async void BtnResetReseau_Click(object sender, RoutedEventArgs e)
    {
        if (!await Confirmer("Réinitialiser le réseau ?",
            "Réinitialise Winsock, TCP/IP, DNS et le pare-feu Windows.\n" +
            "⚠ Un redémarrage est nécessaire pour appliquer les changements.")) return;
        Lancer(BtnResetReseau, "Réparation réseau", (log, _) => ReparationAvanceeService.ResetReseau(log));
    }

    private async void BtnReparerWmi_Click(object sender, RoutedEventArgs e)
    {
        if (!await Confirmer("Réparer les services système ?",
            "Vérifie et répare la base WMI si elle est corrompue. Sans risque.")) return;
        Lancer(BtnReparerWmi, "Réparation des services système", (log, _) => ReparationAvanceeService.ReparerWmi(log));
    }

    private async void BtnChkdsk_Click(object sender, RoutedEventArgs e)
    {
        if (!await Confirmer("Programmer CHKDSK ?",
            "CHKDSK /f /r sera exécuté au prochain redémarrage. Cette analyse peut durer plusieurs heures " +
            "et nécessite que le PC reste allumé pendant toute sa durée.")) return;
        Lancer(BtnChkdsk, "Programmation CHKDSK", (log, _) => ReparationAvanceeService.ProgrammerChkdsk(log));
    }

    private void BtnCreerPoint_Click(object sender, RoutedEventArgs e)
    {
        Lancer(BtnCreerPoint, "Point de restauration", (log, _) =>
        {
            RestaurationService.NettoyerVieuxPoints(3, log);
            RestaurationService.CreerPoint($"Assistools — {DateTime.Now:yyyy-MM-dd HH:mm}", log);
        });
    }

    private async void BtnExportProgs_Click(object sender, RoutedEventArgs e)
    {
        var hwnd = WindowNative.GetWindowHandle(((App)Application.Current).MainWindow);
        var picker = new FileSavePicker();
        InitializeWithWindow.Initialize(picker, hwnd);
        picker.SuggestedFileName = $"programmes-{DateTime.Now:yyyyMMdd}";
        picker.DefaultFileExtension = ".csv";
        picker.FileTypeChoices.Add("Fichier CSV", new System.Collections.Generic.List<string> { ".csv" });
        var file = await picker.PickSaveFileAsync();
        if (file == null) return;
        var path = file.Path;
        Lancer(BtnExportProgs, "Export programmes", (log, _) => ExportService.ExporterProgrammes(path, log));
    }

    private async void BtnExportDrivers_Click(object sender, RoutedEventArgs e)
    {
        var hwnd = WindowNative.GetWindowHandle(((App)Application.Current).MainWindow);
        var picker = new FolderPicker();
        InitializeWithWindow.Initialize(picker, hwnd);
        picker.FileTypeFilter.Add("*");
        var folder = await picker.PickSingleFolderAsync();
        if (folder == null) return;
        var path = folder.Path;
        Lancer(BtnExportDrivers, "Export pilotes", (log, _) => ExportService.ExporterDrivers(path, log));
    }

    private async System.Threading.Tasks.Task<bool> Confirmer(string titre, string message)
    {
        var dialog = new ContentDialog
        {
            Title = titre,
            Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
            PrimaryButtonText = "Continuer",
            CloseButtonText = "Annuler",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.XamlRoot,
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
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
