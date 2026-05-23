using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Assistools.Services;

/// <summary>
/// Transforme un Button en spinner+texte pendant l'exécution d'une tâche,
/// puis restaure son contenu original. Utilisable pour n'importe quel Button
/// de l'application (compatibilité Content = string OU Content = panel).
/// </summary>
public static class ButtonBusyHelper
{
    private static readonly Dictionary<Button, object?> _originalContent = new();
    private static readonly Dictionary<Button, bool> _originalEnabled = new();

    public static void SetBusy(Button btn, bool busy, string busyText = "En cours…")
    {
        if (busy)
        {
            // Sauvegarder l'état si pas déjà fait (idempotent)
            if (!_originalContent.ContainsKey(btn))
            {
                _originalContent[btn] = btn.Content;
                _originalEnabled[btn] = btn.IsEnabled;
            }

            var sp = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            sp.Children.Add(new ProgressRing
            {
                Width = 14, Height = 14,
                IsActive = true,
                VerticalAlignment = VerticalAlignment.Center,
            });
            sp.Children.Add(new TextBlock
            {
                Text = busyText,
                VerticalAlignment = VerticalAlignment.Center,
            });
            btn.Content = sp;
            btn.IsEnabled = false;
        }
        else
        {
            // Restaurer
            if (_originalContent.TryGetValue(btn, out var content))
            {
                btn.Content = content;
                _originalContent.Remove(btn);
            }
            if (_originalEnabled.TryGetValue(btn, out var enabled))
            {
                btn.IsEnabled = enabled;
                _originalEnabled.Remove(btn);
            }
            else
            {
                btn.IsEnabled = true;
            }
        }
    }
}
