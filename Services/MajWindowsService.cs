using System;
using System.Diagnostics;
using System.Threading;

namespace Assistools.Services;

public static class MajWindowsService
{
    public static void LancerMiseAJour(Action<string>? log = null)
    {
        log?.Invoke("--- Windows Update ---");

        // Vérifier et démarrer le service wuauserv si nécessaire
        AssurerServiceActif(log);

        try
        {
            RechercherEtInstaller(log);
        }
        catch (Exception ex)
        {
            log?.Invoke($"[ERREUR WUApi] {ex.Message}");
            log?.Invoke("Basculement vers le client USO...");
            FallbackUsoClient(log);
        }
    }

    private static void AssurerServiceActif(Action<string>? log)
    {
        try
        {
            var windir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            var sc = System.IO.Path.Combine(windir, "system32", "sc.exe");

            // Vérifier l'état
            using var query = Process.Start(new ProcessStartInfo
            {
                FileName = sc,
                Arguments = "query wuauserv",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
            });
            var output = query?.StandardOutput.ReadToEnd() ?? "";
            query?.WaitForExit();

            if (output.Contains("STOPPED", StringComparison.OrdinalIgnoreCase))
            {
                log?.Invoke("Service Windows Update arrêté — tentative de démarrage...");
                using var start = Process.Start(new ProcessStartInfo
                {
                    FileName = sc,
                    Arguments = "start wuauserv",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                });
                start?.WaitForExit();
                Thread.Sleep(3000);
                log?.Invoke("Service Windows Update démarré.");
            }
            else
            {
                log?.Invoke("Service Windows Update actif.");
            }
        }
        catch { }
    }

    private static void RechercherEtInstaller(Action<string>? log)
    {
        var sessionType = Type.GetTypeFromProgID("Microsoft.Update.Session")
            ?? throw new InvalidOperationException("WUApi non disponible sur ce système.");

        dynamic session = Activator.CreateInstance(sessionType)!;
        session.ClientApplicationID = "Assistools";

        log?.Invoke("Recherche des mises à jour disponibles...");
        dynamic searcher = session.CreateUpdateSearcher();
        dynamic searchResult = searcher.Search("IsInstalled=0 AND Type='Software' AND IsHidden=0");

        int count = searchResult.Updates.Count;
        if (count == 0)
        {
            log?.Invoke("Votre système est à jour — aucune mise à jour disponible.");
            return;
        }

        log?.Invoke($"{count} mise(s) à jour disponible(s) :");
        var updatesToDownloadType = Type.GetTypeFromProgID("Microsoft.Update.UpdateColl")!;
        dynamic updatesToDownload = Activator.CreateInstance(updatesToDownloadType)!;

        for (int i = 0; i < count; i++)
        {
            dynamic update = searchResult.Updates.Item(i);
            log?.Invoke($"  • {update.Title}");
            if (!update.EulaAccepted)
            {
                try { update.AcceptEula(); } catch { }
            }
            updatesToDownload.Add(update);
        }

        log?.Invoke("Téléchargement en cours...");
        dynamic downloader = session.CreateUpdateDownloader();
        downloader.Updates = updatesToDownload;
        downloader.Download();
        log?.Invoke("Téléchargement terminé.");

        dynamic updatesToInstall = Activator.CreateInstance(updatesToDownloadType)!;
        for (int i = 0; i < count; i++)
        {
            dynamic update = searchResult.Updates.Item(i);
            if (update.IsDownloaded) updatesToInstall.Add(update);
        }

        if (updatesToInstall.Count == 0)
        {
            log?.Invoke("Aucune mise à jour n'a pu être téléchargée.");
            return;
        }

        log?.Invoke($"Installation de {updatesToInstall.Count} mise(s) à jour...");
        dynamic installer = session.CreateUpdateInstaller();
        installer.Updates = updatesToInstall;
        dynamic installResult = installer.Install();

        int ok = 0, err = 0, reboot = 0;
        for (int i = 0; i < updatesToInstall.Count; i++)
        {
            dynamic r = installResult.GetUpdateResult(i);
            int code = r.ResultCode; // 2=Succeeded, 3=SucceededWithErrors, 4=Failed, 5=Aborted
            if (code is 2 or 3) ok++;
            else if (code is 4 or 5) err++;
            if (r.RebootRequired == true) reboot++;
        }

        log?.Invoke($"{ok} mise(s) à jour installée(s)" +
                    (err > 0 ? $", {err} échec(s)" : "") +
                    (reboot > 0 ? $" — {reboot} nécessitent un redémarrage" : "") + ".");
        log?.Invoke("--- Windows Update terminé ---");
    }

    private static void FallbackUsoClient(Action<string>? log)
    {
        var windir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var uso = System.IO.Path.Combine(windir, "system32", "usoclient.exe");

        foreach (var cmd in new[] { "StartScan", "StartDownload", "StartInstall" })
        {
            try
            {
                log?.Invoke($"usoclient {cmd}...");
                using var p = Process.Start(new ProcessStartInfo
                {
                    FileName = uso,
                    Arguments = cmd,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                });
                p?.WaitForExit(60_000);
            }
            catch (Exception ex)
            {
                log?.Invoke($"[ERREUR] usoclient {cmd} : {ex.Message}");
            }
        }
        log?.Invoke("Demande de mise à jour envoyée à Windows.");
    }
}
