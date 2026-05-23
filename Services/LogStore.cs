using System;
using System.Collections.Generic;
using System.Text;

namespace Assistools.Services;

/// <summary>
/// Journal global thread-safe — accumule toutes les lignes de logs de la session.
/// Notifié à chaque ajout via OnLineAdded.
/// </summary>
public static class LogStore
{
    private static readonly object _lock = new();
    private static readonly List<string> _lines = new();

    public static event Action<string>? OnLineAdded;
    public static event Action? OnCleared;

    public static void Append(string line)
    {
        var stamp = DateTime.Now.ToString("HH:mm:ss");
        var formatted = $"[{stamp}] {line}";
        lock (_lock) _lines.Add(formatted);
        OnLineAdded?.Invoke(formatted);
    }

    public static IReadOnlyList<string> Snapshot()
    {
        lock (_lock) return _lines.ToArray();
    }

    public static void Clear()
    {
        lock (_lock) _lines.Clear();
        OnCleared?.Invoke();
    }

    public static string Export()
    {
        var sb = new StringBuilder();
        lock (_lock)
        {
            foreach (var l in _lines) sb.AppendLine(l);
        }
        return sb.ToString();
    }
}
