using System;
using System.Threading;
using System.Threading.Tasks;
using Assistools.Services;
using Microsoft.UI.Xaml;

namespace Assistools;

public partial class App : Application
{
    // ── Singleton GameBooster (partagé entre la page et le menu tray) ─────────
    public static GameBoosterService GameBooster { get; } = new GameBoosterService();

    // ── Maintenance complète depuis le tray (toutes étapes, toutes cochées) ───
    public static async Task RunMaintenanceAllAsync()
    {
        if (TaskManager.Status != JobStatus.Idle) return;

        (string name, Action<Action<string>, CancellationToken> action)[] steps =
        [
            ("Nettoyage",           (log, _) => NettoyageService.LancerNettoyageDisque(log)),
            ("Réparation",          (log, _) => ReparationService.LancerReparationSysteme(log)),
            ("Mises à jour",        (log, _) => MajWindowsService.LancerMiseAJour(log)),
            ("Sécurité",            (log, _) => SecuriteService.MiseAJourEtAnalyse(log)),
            ("Optimisation disque", (log, _) => PerformanceService.OptimiserDisques(log)),
        ];

        foreach (var (name, action) in steps)
        {
            try { await TaskManager.RunAsync(name, action); }
            catch { /* exceptions déjà loggées par TaskManager */ }
        }

        HealthCheckService.EnregistrerMaintenance();
    }

    public App()
    {
        InitializeComponent();
        UnhandledException += (s, e) =>
        {
            LogStore.Append($"[App][UnhandledException] {e.Message}\n{e.Exception}");
            e.Handled = true;
        };
    }

    // Kept for diagnostic: any uncaught XAML exception is logged and swallowed
    // instead of triggering the debugger break. Remove once stable.

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Lancé par le Task Scheduler pour envoyer le rappel
        string[] argv = Environment.GetCommandLineArgs();
        bool isRemind = Array.Exists(argv, a =>
            string.Equals(a, NotificationService.ArgRemind, StringComparison.OrdinalIgnoreCase));

        if (isRemind)
        {
            // Envoie le toast et replanifie pour dans 365 jours, puis quitte
            NotificationService.EnvoyerRappel();
            NotificationService.CreerTachePlanifiee();
            Exit();
            return;
        }

        // Démarrage normal
        MainWindow = new MainWindow();
        MainWindow.Activate();

        // Tâches différées après affichage de la fenêtre (ne bloquent pas le démarrage)
        Task.Run(() =>
        {
            NotificationService.EnregistrerAumid();
            if (NotificationService.EstRappelActif() && !NotificationService.TacheExiste())
                NotificationService.CreerTachePlanifiee();
        });
    }

    public MainWindow? MainWindow { get; private set; }
}
