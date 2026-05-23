using System;
using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Pickers;
using WinRT.Interop;
using Assistools.Services;

namespace Assistools.Pages;

public sealed partial class PageLogs : Page
{
    public PageLogs()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Charger l'historique existant
        LogsText.Text = string.Join("\n", LogStore.Snapshot());
        ScrollToEnd();

        LogStore.OnLineAdded += HandleLineAdded;
        LogStore.OnCleared   += HandleCleared;
        TaskManager.OnStatusChanged += UpdateStatusBar;
        UpdateStatusBar();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        LogStore.OnLineAdded -= HandleLineAdded;
        LogStore.OnCleared   -= HandleCleared;
        TaskManager.OnStatusChanged -= UpdateStatusBar;
    }

    private void HandleLineAdded(string line)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            LogsText.Text += (LogsText.Text.Length > 0 ? "\n" : "") + line;
            ScrollToEnd();
        });
    }

    private void HandleCleared()
    {
        DispatcherQueue.TryEnqueue(() => LogsText.Text = "");
    }

    private void UpdateStatusBar()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (TaskManager.Status == JobStatus.Running)
            {
                StatusBar.Message = $"⚙ En cours : {TaskManager.CurrentLabel}";
                StatusBar.Severity = InfoBarSeverity.Informational;
                StatusBar.IsOpen = true;
            }
            else if (TaskManager.Status == JobStatus.Cancelling)
            {
                StatusBar.Message = "Annulation en cours…";
                StatusBar.Severity = InfoBarSeverity.Warning;
                StatusBar.IsOpen = true;
            }
            else
            {
                StatusBar.IsOpen = false;
            }
        });
    }

    private void ScrollToEnd()
    {
        LogsScroller.ChangeView(null, LogsScroller.ScrollableHeight, null, disableAnimation: true);
    }

    private void BtnCopier_Click(object sender, RoutedEventArgs e)
    {
        var pkg = new DataPackage();
        pkg.SetText(LogStore.Export());
        Clipboard.SetContent(pkg);
    }

    private async void BtnExporter_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var hwnd = WindowNative.GetWindowHandle(((App)Application.Current).MainWindow);
            string defaultName = $"assistools-logs-{DateTime.Now:yyyyMMdd-HHmmss}.txt";

            // Win32 dialog compatible with running as Administrator
            string? destinationPath = FileDialogHelper.SaveFileDialog(
                hwnd,
                "Exporter l'historique des logs",
                defaultName,
                "Fichier texte (*.txt)|*.txt",
                "txt"
            );

            if (string.IsNullOrEmpty(destinationPath)) return;
            await File.WriteAllTextAsync(destinationPath, LogStore.Export());
        }
        catch (Exception ex)
        {
            LogStore.Append($"[ERREUR export] {ex.GetType().Name}: {ex.Message}");
        }
    }

    private void BtnEffacer_Click(object sender, RoutedEventArgs e) => LogStore.Clear();
}
