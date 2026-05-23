using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using H.NotifyIcon;
using Assistools.Pages;
using Assistools.Services;

namespace Assistools;

public sealed partial class MainWindow : Window
{
    private const int BannerMinVisibleMs = 5000;

    private DateTime _bannerShownAt = DateTime.MinValue;
    private CancellationTokenSource? _bannerHideCts;
    private TaskbarIcon? _trayIcon;
    private bool _forceClose = false;

    public MainWindow()
    {
        InitializeComponent();
        Title = "Assistools";
        ExtendsContentIntoTitleBar = true;

        AppWindow.SetIcon("Assets\\app.ico");
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1000, 650));

        NavView.SelectedItem = NavView.MenuItems[0];
        ContentFrame.Navigate(typeof(PageAuto));

        // Brancher l'UI globale (badge + bandeau bas) au TaskManager
        TaskManager.OnStatusChanged += UpdateGlobalUi;
        TaskManager.OnLineUpdated += OnGlobalLineUpdated;

        InitTrayIcon();

        // Intercepter la fermeture : minimiser en tray au lieu de quitter
        AppWindow.Closing += OnAppWindowClosing;
    }

    // ── Tray ─────────────────────────────────────────────────────────────────

    private void InitTrayIcon()
    {
        _trayIcon = (TaskbarIcon)RootGrid.Resources["TrayIcon"];

        // Pas de ContextFlyout : on affiche un vrai menu Win32 natif via RightClickCommand.
        _trayIcon.ContextFlyout = null;

        var openCmd  = new TrayCommand(() => DispatcherQueue.TryEnqueue(ShowWindow));
        var rightCmd = new TrayCommand(() => DispatcherQueue.TryEnqueue(ShowNativeTrayMenu));

        _trayIcon.DoubleClickCommand = openCmd;
        _trayIcon.LeftClickCommand   = openCmd;
        _trayIcon.RightClickCommand  = rightCmd;

        _trayIcon.ForceCreate(enablesEfficiencyMode: false);
    }

    private void ShowNativeTrayMenu()
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var booster = App.GameBooster;

        var menu = new TrayNativeMenu();

        // ── Game Booster ─────────────────────────────────────────────────────
        string boosterLabel = booster.IsMonitoring
            ? "Désactiver le booster"
            : $"Activer le booster  ({ProfileShortLabel(booster.ActiveProfile)})";

        menu.AddItem(boosterLabel, () =>
        {
            _ = Task.Run(() =>
            {
                try
                {
                    if (booster.IsMonitoring) booster.StopMonitoring();
                    else                       booster.StartMonitoring();
                }
                catch (Exception ex) { LogStore.Append($"[Tray][GameBooster] {ex.Message}"); }
            });
        });

        menu.AddSubmenu("Profil", sub =>
        {
            foreach (var p in Enum.GetValues<GameBoosterProfile>())
            {
                var captured = p;
                // Le profil Avancé (Custom) doit être configuré dans l'app, pas appliqué depuis le tray
                if (captured == GameBoosterProfile.Custom)
                {
                    sub.AddItem(
                        ProfileLabel(captured) + "…",
                        () => DispatcherQueue.TryEnqueue(() =>
                        {
                            ShowWindow();
                            foreach (var item in NavView.MenuItems)
                            {
                                if (item is NavigationViewItem nvi && nvi.Tag?.ToString() == "GameBooster")
                                {
                                    NavView.SelectedItem = nvi;
                                    break;
                                }
                            }
                        }),
                        isChecked: booster.ActiveProfile == captured);
                }
                else
                {
                    sub.AddItem(
                        ProfileLabel(captured),
                        () => booster.ActiveProfile = captured,
                        isChecked: booster.ActiveProfile == captured);
                }
            }
        }, enabled: !booster.IsMonitoring);

        menu.AddSeparator();

        // ── Maintenance ──────────────────────────────────────────────────────
        bool idle = TaskManager.Status == JobStatus.Idle;
        menu.AddItem("Maintenance automatique", () => _ = App.RunMaintenanceAllAsync(), enabled: idle);

        menu.AddSeparator();

        // ── Navigation ───────────────────────────────────────────────────────
        menu.AddItem("Ouvrir", () => DispatcherQueue.TryEnqueue(ShowWindow), isDefault: true);
        menu.AddSeparator();
        menu.AddItem("Quitter", () => DispatcherQueue.TryEnqueue(ExitApp));

        menu.ShowAt(hwnd);
    }

    // ── Helpers libellés ─────────────────────────────────────────────────────

    private static string ProfileLabel(GameBoosterProfile p) => p switch
    {
        GameBoosterProfile.Competitif => "Compétitif  (Esport / FPS)",
        GameBoosterProfile.Gamer      => "Gamer  (AAA)",
        GameBoosterProfile.Streamer   => "Streamer  (Gaming + OBS)",
        GameBoosterProfile.Custom     => "Personnalisé",
        _                             => p.ToString(),
    };

    private static string ProfileShortLabel(GameBoosterProfile p) => p switch
    {
        GameBoosterProfile.Competitif => "Compétitif",
        GameBoosterProfile.Gamer      => "Gamer",
        GameBoosterProfile.Streamer   => "Streamer",
        GameBoosterProfile.Custom     => "Personnalisé",
        _                             => p.ToString(),
    };

    private void ShowWindow()
    {
        AppWindow.Show();
        this.Activate();
    }

    private void ExitApp()
    {
        _forceClose = true;
        if (App.GameBooster.IsMonitoring)
            App.GameBooster.StopMonitoring();
        App.GameBooster.Dispose();
        _trayIcon?.Dispose();
        TaskManager.OnStatusChanged -= UpdateGlobalUi;
        TaskManager.OnLineUpdated   -= OnGlobalLineUpdated;
        Close();
    }

    private void OnAppWindowClosing(Microsoft.UI.Windowing.AppWindow sender, Microsoft.UI.Windowing.AppWindowClosingEventArgs args)
    {
        if (_forceClose) return;

        // Annuler la fermeture et se replier en tray
        args.Cancel = true;
        this.Hide();
    }

    private void UpdateGlobalUi()
    {
        NavView.DispatcherQueue.TryEnqueue(() =>
        {
            bool busy = TaskManager.Status == JobStatus.Running;

            if (busy)
            {
                // Afficher immédiatement le bandeau et noter l'heure
                LogsBadge.Visibility = Visibility.Visible;
                if (GlobalTaskBar.Visibility != Visibility.Visible)
                {
                    GlobalTaskBar.Visibility = Visibility.Visible;
                    _bannerShownAt = DateTime.UtcNow;
                    // Annuler tout timer de masquage en cours
                    _bannerHideCts?.Cancel();
                    _bannerHideCts = null;
                }
                // Spinner actif pendant l'opération
                GlobalTaskSpinner.IsActive = true;
                GlobalTaskTitle.Text = TaskManager.CurrentLabel + " en cours…";
                GlobalTaskLine.Text = string.IsNullOrEmpty(TaskManager.CurrentLine)
                    ? "Préparation…"
                    : Truncate(TaskManager.CurrentLine);
            }
            else
            {
                // Tâche terminée : arrêter le spinner et afficher "Terminé"
                GlobalTaskSpinner.IsActive = false;
                GlobalTaskTitle.Text = "✓ " + (string.IsNullOrEmpty(TaskManager.CurrentLabel)
                    ? "Opération terminée"
                    : TaskManager.CurrentLabel + " : terminé");
                GlobalTaskLine.Text = "Voir l'historique pour les détails";

                // Calcul du délai restant pour atteindre 5 secondes
                int elapsedMs = (int)(DateTime.UtcNow - _bannerShownAt).TotalMilliseconds;
                int remainingMs = Math.Max(0, BannerMinVisibleMs - elapsedMs);

                if (remainingMs <= 0)
                {
                    // Déjà affiché assez longtemps → cacher tout de suite
                    LogsBadge.Visibility = Visibility.Collapsed;
                    GlobalTaskBar.Visibility = Visibility.Collapsed;
                }
                else
                {
                    // Masquer après le délai restant
                    var cts = new CancellationTokenSource();
                    _bannerHideCts = cts;
                    _ = Task.Delay(remainingMs, cts.Token).ContinueWith(_ =>
                    {
                        if (!cts.IsCancellationRequested)
                        {
                            NavView.DispatcherQueue.TryEnqueue(() =>
                            {
                                LogsBadge.Visibility = Visibility.Collapsed;
                                GlobalTaskBar.Visibility = Visibility.Collapsed;
                            });
                        }
                    }, TaskScheduler.Default);
                }
            }
        });
    }

    private void OnGlobalLineUpdated(string line)
    {
        NavView.DispatcherQueue.TryEnqueue(() =>
        {
            if (TaskManager.Status == JobStatus.Running)
                GlobalTaskLine.Text = Truncate(line);
        });
    }

    private static string Truncate(string s) =>
        s.Length > 130 ? s.Substring(0, 127) + "…" : s;

    // Commande légère pour le tray icon (DoubleClickCommand / LeftClickCommand)
    private sealed class TrayCommand : System.Windows.Input.ICommand
    {
        private readonly Action _action;
        public TrayCommand(Action action) => _action = action;
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _action();
    }

    private void GlobalTaskShowLogs_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in NavView.FooterMenuItems)
        {
            if (item is NavigationViewItem nvi && nvi.Tag?.ToString() == "Logs")
            {
                NavView.SelectedItem = nvi;
                break;
            }
        }
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected)
        {
            ContentFrame.Navigate(typeof(PageSettings));
            return;
        }

        if (args.SelectedItem is NavigationViewItem item)
        {
            ContentFrame.Navigate(item.Tag switch
            {
                "Auto"    => typeof(PageAuto),
                "Avance"  => typeof(PageAvance),
                "Perf"    => typeof(PagePerf),
                "GameBooster" => typeof(PageGameBooster),
                "Privacy" => typeof(PagePrivacy),
                "Debloat"  => typeof(PageDebloat),
                "Startup"  => typeof(PageStartup),
                "ServicesWizard" => typeof(PageServicesWizard),
                "Rapport"  => typeof(PageRapport),
                "Logs"    => typeof(PageLogs),
                "Info"    => typeof(PageInfo),
                _         => typeof(PageAuto),
            });
        }
    }
}
