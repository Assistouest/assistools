using System;
using System.Diagnostics;
using Microsoft.Win32;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace Assistools.Services;

public static class NotificationService
{
    private const string AppId       = "Assistouest.Assistools";
    private const string AppName     = "Assistools";
    private const string TaskName    = "AssistoolsMaintenanceReminder";
    private const string RegKey      = @"Software\Assistools";
    private const string RegEnabled  = "ReminderEnabled";

    public const string ArgRemind = "--remind";

    // ── AUMID ────────────────────────────────────────────────────────────────

    /// <summary>Enregistre l'AUMID dans le registre (requis pour les toasts non packagés).</summary>
    public static void EnregistrerAumid()
    {
        try
        {
            var exe = ExePath();
            using var key = Registry.CurrentUser.CreateSubKey(
                $@"Software\Classes\AppUserModelId\{AppId}");
            key.SetValue("DisplayName", AppName);
            if (exe.Length > 0) key.SetValue("IconUri", exe);
        }
        catch { }
    }

    // ── TOAST ────────────────────────────────────────────────────────────────

    /// <summary>Envoie le toast de rappel et se ferme si lancé uniquement pour ça.</summary>
    public static void EnvoyerRappel()
    {
        try
        {
            double jours = ObtenirJoursSansMaintenance();

            string duree = jours >= double.MaxValue / 2
                ? "Vous n'avez jamais effectué de maintenance."
                : $"Il y a {(int)jours} jour{((int)jours > 1 ? "s" : "")} que votre ordinateur n'a pas été entretenu.";

            string xml = $"""
                <toast activationType="protocol" launch="assistools://maintenance">
                  <visual>
                    <binding template="ToastGeneric">
                      <text>Maintenance recommandée 🛡</text>
                      <text>{duree}</text>
                      <text>Ouvrez Assistools pour optimiser votre ordinateur en toute sécurité.</text>
                    </binding>
                  </visual>
                  <actions>
                    <action content="Ouvrir Assistools" arguments="assistools://maintenance" activationType="protocol" />
                    <action content="Plus tard" arguments="dismiss" activationType="system" />
                  </actions>
                </toast>
                """;

            var doc = new XmlDocument();
            doc.LoadXml(xml);
            var notifier = ToastNotificationManager.CreateToastNotifier(AppId);
            var toast    = new ToastNotification(doc) { Tag = "maintenance-reminder" };
            notifier.Show(toast);
        }
        catch { }
    }

    // ── TASK SCHEDULER ───────────────────────────────────────────────────────

    /// <summary>Crée (ou recrée) la tâche planifiée annuelle.</summary>
    public static void CreerTachePlanifiee()
    {
        try
        {
            string exe = ExePath();
            if (exe.Length == 0) return;

            // Date de déclenchement : aujourd'hui + 365 jours, à 10h00
            var trigger = DateTime.Now.AddDays(365).ToString("yyyy-MM-ddT10:00:00");

            // schtasks /create : tâche quotidienne avec intervalle de 365 jours
            // On utilise un trigger de type ONCE avec répétition annuelle via /SC DAILY /MO 365
            string args =
                $"/Create /F /TN \"{TaskName}\" " +
                $"/TR \"\\\"{exe}\\\" {ArgRemind}\" " +
                $"/SC DAILY /MO 365 " +
                $"/ST 10:00 " +
                $"/SD {DateTime.Now.AddDays(365):MM/dd/yyyy} " +
                $"/RL LIMITED";   // pas besoin de privilèges admin

            RunSchtasks(args);
        }
        catch { }
    }

    /// <summary>Supprime la tâche planifiée.</summary>
    public static void SupprimerTachePlanifiee()
    {
        try
        {
            RunSchtasks($"/Delete /F /TN \"{TaskName}\"");
        }
        catch { }
    }

    /// <summary>Vérifie si la tâche planifiée existe déjà.</summary>
    public static bool TacheExiste()
    {
        try
        {
            using var p = new Process();
            p.StartInfo = new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                Arguments = $"/Query /TN \"{TaskName}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            p.Start();
            p.WaitForExit(5000);
            return p.ExitCode == 0;
        }
        catch { return false; }
    }

    // ── PARAMÈTRES ───────────────────────────────────────────────────────────

    public static bool EstRappelActif()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegKey);
            if (key?.GetValue(RegEnabled) is int v) return v != 0;
            return true; // activé par défaut
        }
        catch { return true; }
    }

    public static void DefinirRappelActif(bool actif)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RegKey);
            key.SetValue(RegEnabled, actif ? 1 : 0, RegistryValueKind.DWord);

            if (actif)
                CreerTachePlanifiee();
            else
                SupprimerTachePlanifiee();
        }
        catch { }
    }

    // ── HELPERS ──────────────────────────────────────────────────────────────

    private static double ObtenirJoursSansMaintenance()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegKey);
            if (key?.GetValue("LastMaintenance") is string raw &&
                DateTime.TryParse(raw, out var date))
                return (DateTime.Now - date).TotalDays;
        }
        catch { }
        return double.MaxValue;
    }

    private static string ExePath()
    {
        try { return Process.GetCurrentProcess().MainModule?.FileName ?? ""; }
        catch { return ""; }
    }

    private static void RunSchtasks(string args)
    {
        using var p = new Process();
        p.StartInfo = new ProcessStartInfo
        {
            FileName = "schtasks.exe",
            Arguments = args,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        p.Start();
        p.WaitForExit(10000);
    }
}
