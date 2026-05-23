using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime;
using System.Threading;
using System.Threading.Tasks;

namespace Assistools.Services;

public enum JobStatus { Idle, Running, Cancelling }

/// <summary>
/// Gestionnaire central des tâches longue durée.
/// Une seule tâche à la fois — les autres sont mises en file d'attente.
/// L'UI s'abonne à OnStatusChanged pour griser les boutons quand quelque chose tourne.
/// </summary>
public static class TaskManager
{
    private static readonly object _lock = new();
    private static readonly Queue<QueuedTask> _queue = new();
    private static CancellationTokenSource? _cts;
    private static Task? _runner;

    public static JobStatus Status { get; private set; } = JobStatus.Idle;
    public static string CurrentLabel { get; private set; } = "";
    public static string CurrentLine { get; private set; } = "";

    public static event Action? OnStatusChanged;
    public static event Action<string>? OnLineUpdated;

    private static void EmitLine(string line)
    {
        // Filtrer le bruit (lignes vides, `[ERR]` techniques etc.)
        var trimmed = line?.Trim() ?? "";
        if (string.IsNullOrEmpty(trimmed)) return;

        LogStore.Append(trimmed);
        CurrentLine = trimmed;
        OnLineUpdated?.Invoke(trimmed);
    }

    private sealed record QueuedTask(string Label, Action<Action<string>, CancellationToken> Work, TaskCompletionSource Tcs);

    /// <summary>
    /// Lance une opération en arrière-plan. Si une opération tourne déjà,
    /// la nouvelle est mise en file. Renvoie une Task qui se complète à la fin.
    /// </summary>
    public static Task RunAsync(string label, Action<Action<string>, CancellationToken> work)
    {
        var tcs = new TaskCompletionSource();
        var entry = new QueuedTask(label, work, tcs);

        lock (_lock)
        {
            _queue.Enqueue(entry);
            if (_runner == null || _runner.IsCompleted)
                _runner = Task.Run(ProcessQueue);
        }

        return tcs.Task;
    }

    public static void Cancel()
    {
        lock (_lock)
        {
            if (Status != JobStatus.Running) return;
            Status = JobStatus.Cancelling;
            _cts?.Cancel();
        }
        OnStatusChanged?.Invoke();
        LogStore.Append("[INFO] Annulation demandée…");
    }

    private static async Task ProcessQueue()
    {
        while (true)
        {
            QueuedTask? job;
            lock (_lock)
            {
                if (_queue.Count == 0)
                {
                    Status = JobStatus.Idle;
                    CurrentLabel = "";
                    OnStatusChanged?.Invoke();
                    return;
                }
                job = _queue.Dequeue();
                _cts = new CancellationTokenSource();
                Status = JobStatus.Running;
                CurrentLabel = job.Label;
            }

            OnStatusChanged?.Invoke();
            LogStore.Append($"=== Démarrage : {job.Label} ===");

            var prevPriority = Process.GetCurrentProcess().PriorityClass;
            var prevGc       = GCSettings.LatencyMode;
            try
            {
                Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.AboveNormal;
                if (prevGc != GCLatencyMode.NoGCRegion)
                    GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;

                await Task.Run(() =>
                {
                    Thread.CurrentThread.Priority = ThreadPriority.AboveNormal;
                    job.Work(EmitLine, _cts.Token);
                }, _cts.Token);

                LogStore.Append($"=== Terminé : {job.Label} ===");
                CurrentLine = "Terminé";
                OnLineUpdated?.Invoke(CurrentLine);
                job.Tcs.TrySetResult();
            }
            catch (OperationCanceledException)
            {
                LogStore.Append($"=== Annulé : {job.Label} ===");
                job.Tcs.TrySetCanceled();
            }
            catch (Exception ex)
            {
                LogStore.Append($"[ERREUR] {ex.Message}");
                LogStore.Append($"=== Échec : {job.Label} ===");
                job.Tcs.TrySetException(ex);
            }
            finally
            {
                try { Process.GetCurrentProcess().PriorityClass = prevPriority; } catch { }
                try { GCSettings.LatencyMode = prevGc; } catch { }
            }
        }
    }
}
