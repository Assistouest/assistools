using System.Threading;
using Assistools.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Assistools.Pages;

public sealed partial class PagePrivacy : Page
{
    private bool _initDone;

    public PagePrivacy()
    {
        InitializeComponent();
        Loaded += (_, _) => ChargerEtats();
    }

    private void ChargerEtats()
    {
        ToggleCopilot.IsOn        = PrivacyService.EstCopilotDesactive();
        ToggleRecall.IsOn         = PrivacyService.EstRecallDesactive();
        ToggleIaEdge.IsOn         = PrivacyService.EstIaEdgeDesactivee();
        ToggleIaOffice.IsOn       = PrivacyService.EstIaOfficeDesactivee();
        ToggleTelemetrie.IsOn     = PrivacyService.EstTelemetrieDesactivee();
        TogglePublicites.IsOn     = PrivacyService.EstPublicitesDesactivees();
        ToggleBing.IsOn           = PrivacyService.EstBingRechercheDesactive();
        ToggleSuggestions.IsOn    = PrivacyService.EstSuggestionsDestarrerDesactivees();
        ToggleLocalisation.IsOn   = PrivacyService.EstLocalisationDesactivee();
        ToggleCamera.IsOn         = PrivacyService.EstCameraDesactivee();
        ToggleMicrophone.IsOn     = PrivacyService.EstMicrophoneDesactive();
        ToggleVoix.IsOn           = PrivacyService.EstVoixDesactivee();
        ToggleActivite.IsOn       = PrivacyService.EstHistoriqueActiviteDesactive();
        ToggleFeedback.IsOn       = PrivacyService.EstFeedbackDesactive();
        ToggleFindMyDevice.IsOn   = PrivacyService.EstFindMyDeviceDesactive();
        _initDone = true;
    }

    private void ToggleCopilot_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_initDone) return;
        Appliquer(ToggleCopilot.IsOn
            ? "Désactivation de Windows Copilot"
            : "Réactivation de Windows Copilot",
            ToggleCopilot.IsOn
                ? (log, _) => PrivacyService.DesactiverCopilot(log)
                : (log, _) => PrivacyService.ReactiverCopilot(log));
    }

    private void ToggleRecall_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_initDone) return;
        Appliquer(ToggleRecall.IsOn
            ? "Désactivation de Windows Recall"
            : "Réactivation de Windows Recall",
            ToggleRecall.IsOn
                ? (log, _) => PrivacyService.DesactiverRecall(log)
                : (log, _) => PrivacyService.ReactiverRecall(log));
    }

    private void ToggleIaEdge_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_initDone) return;
        Appliquer(ToggleIaEdge.IsOn
            ? "Désactivation de l'IA dans Edge"
            : "Réactivation de l'IA dans Edge",
            ToggleIaEdge.IsOn
                ? (log, _) => PrivacyService.DesactiverIaEdge(log)
                : (log, _) => PrivacyService.ReactiverIaEdge(log));
    }

    private void ToggleIaOffice_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_initDone) return;
        Appliquer(ToggleIaOffice.IsOn
            ? "Désactivation de Copilot dans Office"
            : "Réactivation de Copilot dans Office",
            ToggleIaOffice.IsOn
                ? (log, _) => PrivacyService.DesactiverIaOffice(log)
                : (log, _) => PrivacyService.ReactiverIaOffice(log));
    }

    private void ToggleTelemetrie_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_initDone) return;
        Appliquer(ToggleTelemetrie.IsOn
            ? "Désactivation de la télémétrie"
            : "Réactivation de la télémétrie",
            ToggleTelemetrie.IsOn
                ? (log, _) => PrivacyService.DesactiverTelemetrie(log)
                : (log, _) => PrivacyService.ReactiverTelemetrie(log));
    }

    private void TogglePublicites_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_initDone) return;
        Appliquer(TogglePublicites.IsOn
            ? "Désactivation des publicités personnalisées"
            : "Réactivation des publicités personnalisées",
            TogglePublicites.IsOn
                ? (log, _) => PrivacyService.DesactiverPublicites(log)
                : (log, _) => PrivacyService.ReactiverPublicites(log));
    }

    private void ToggleBing_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_initDone) return;
        Appliquer(ToggleBing.IsOn
            ? "Désactivation de Bing dans la recherche"
            : "Réactivation de Bing dans la recherche",
            ToggleBing.IsOn
                ? (log, _) => PrivacyService.DesactiverBingRecherche(log)
                : (log, _) => PrivacyService.ReactiverBingRecherche(log));
    }

    private void ToggleSuggestions_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_initDone) return;
        Appliquer(ToggleSuggestions.IsOn
            ? "Désactivation des suggestions Démarrer"
            : "Réactivation des suggestions Démarrer",
            ToggleSuggestions.IsOn
                ? (log, _) => PrivacyService.DesactiverSuggestionsDemarrer(log)
                : (log, _) => PrivacyService.ReactiverSuggestionsDemarrer(log));
    }

    private void ToggleLocalisation_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_initDone) return;
        Appliquer(ToggleLocalisation.IsOn ? "Désactivation de la localisation" : "Réactivation de la localisation",
            ToggleLocalisation.IsOn ? (log, _) => PrivacyService.DesactiverLocalisation(log) : (log, _) => PrivacyService.ReactiverLocalisation(log));
    }

    private void ToggleCamera_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_initDone) return;
        Appliquer(ToggleCamera.IsOn ? "Désactivation de l'accès caméra" : "Réactivation de l'accès caméra",
            ToggleCamera.IsOn ? (log, _) => PrivacyService.DesactiverCamera(log) : (log, _) => PrivacyService.ReactiverCamera(log));
    }

    private void ToggleMicrophone_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_initDone) return;
        Appliquer(ToggleMicrophone.IsOn ? "Désactivation de l'accès microphone" : "Réactivation de l'accès microphone",
            ToggleMicrophone.IsOn ? (log, _) => PrivacyService.DesactiverMicrophone(log) : (log, _) => PrivacyService.ReactiverMicrophone(log));
    }

    private void ToggleVoix_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_initDone) return;
        Appliquer(ToggleVoix.IsOn ? "Désactivation de la reconnaissance vocale" : "Réactivation de la reconnaissance vocale",
            ToggleVoix.IsOn ? (log, _) => PrivacyService.DesactiverVoix(log) : (log, _) => PrivacyService.ReactiverVoix(log));
    }

    private void ToggleActivite_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_initDone) return;
        Appliquer(ToggleActivite.IsOn ? "Désactivation de l'historique d'activité" : "Réactivation de l'historique d'activité",
            ToggleActivite.IsOn ? (log, _) => PrivacyService.DesactiverHistoriqueActivite(log) : (log, _) => PrivacyService.ReactiverHistoriqueActivite(log));
    }

    private void ToggleFeedback_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_initDone) return;
        Appliquer(ToggleFeedback.IsOn ? "Désactivation du feedback Microsoft" : "Réactivation du feedback Microsoft",
            ToggleFeedback.IsOn ? (log, _) => PrivacyService.DesactiverFeedback(log) : (log, _) => PrivacyService.ReactiverFeedback(log));
    }

    private void ToggleFindMyDevice_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_initDone) return;
        Appliquer(ToggleFindMyDevice.IsOn ? "Désactivation de Localiser mon appareil" : "Réactivation de Localiser mon appareil",
            ToggleFindMyDevice.IsOn ? (log, _) => PrivacyService.DesactiverFindMyDevice(log) : (log, _) => PrivacyService.ReactiverFindMyDevice(log));
    }

    private void Appliquer(string label, System.Action<System.Action<string>, CancellationToken> work)
    {
        InfoStatus.Severity = InfoBarSeverity.Informational;
        InfoStatus.Message = $"{label}… Voir l'historique pour les détails.";
        _ = TaskManager.RunAsync(label, work);
    }
}
