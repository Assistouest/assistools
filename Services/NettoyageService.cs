using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace Assistools.Services;

public record CategorieRapport(string Nom, int FichiersSupprimes, long OctetsLiberes, int Echecs);

public record RapportNettoyage(List<CategorieRapport> Categories)
{
    public int TotalFichiers => Sum(c => c.FichiersSupprimes);
    public long TotalOctets  => SumL(c => c.OctetsLiberes);
    public int TotalEchecs   => Sum(c => c.Echecs);

    private int Sum(Func<CategorieRapport, int> f) { int s = 0; foreach (var c in Categories) s += f(c); return s; }
    private long SumL(Func<CategorieRapport, long> f) { long s = 0; foreach (var c in Categories) s += f(c); return s; }
}

public static class NettoyageService
{
    [Flags]
    private enum RecycleFlags : uint
    {
        SHERB_NOCONFIRMATION = 0x00000001,
        SHERB_NOPROGRESSUI   = 0x00000002,
        SHERB_NOSOUND        = 0x00000004,
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHEmptyRecycleBin(IntPtr hwnd, string? pszRootPath, RecycleFlags dwFlags);

    /// <summary>Dernier rapport généré, accessible par la page Auto pour le dialog résumé.</summary>
    public static RapportNettoyage? DernierRapport { get; private set; }

    public static void LancerNettoyageDisque(Action<string>? log = null)
    {
        var local   = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var windir  = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var progdat = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var tempUsr = Path.GetTempPath();

        var rapport = new List<CategorieRapport>();

        rapport.Add(NettoyerDossier("Fichiers temporaires",         tempUsr, log));
        rapport.Add(NettoyerDossier("Cache Internet",               Path.Combine(local, @"Microsoft\Windows\INetCache"), log));
        rapport.Add(ViderCorbeille("Corbeille", log));
        rapport.Add(NettoyerDossier("Prefetch",                     Path.Combine(windir, "Prefetch"), log));
        rapport.Add(NettoyerDossier("Cache notifications",          Path.Combine(local, @"Microsoft\Windows\Notifications"), log));
        rapport.Add(NettoyerDossier("Cache Windows Defender",       Path.Combine(progdat, @"Microsoft\Windows Defender\Scans\History\Service"), log));
        rapport.Add(NettoyerDossier("Temp Windows",                 Path.Combine(windir, "Temp"), log));
        rapport.Add(NettoyerDossier("Cache mises à jour Windows",   Path.Combine(windir, @"SoftwareDistribution\Download"), log));
        rapport.Add(NettoyerVignettes("Cache vignettes", local, log));
        rapport.Add(NettoyerDossier("Temp applications",            Path.Combine(local, "Temp"), log));

        DernierRapport = new RapportNettoyage(rapport);
        MaintenanceStore.EnregistrerNettoyage(DernierRapport);

        log?.Invoke($"--- Nettoyage terminé : {DernierRapport.TotalFichiers} fichier(s), {FormatTaille(DernierRapport.TotalOctets)} libéré(s) ---");
    }

    private static CategorieRapport NettoyerDossier(string nom, string chemin, Action<string>? log)
    {
        log?.Invoke($"[{nom}] {chemin}");
        if (!Directory.Exists(chemin))
        {
            log?.Invoke("  (dossier inexistant)");
            return new(nom, 0, 0, 0);
        }

        int ok = 0, err = 0;
        long octets = 0;

        foreach (var fichier in EnumerateSafely(chemin, "*", SearchOption.AllDirectories))
        {
            try
            {
                var info = new FileInfo(fichier);
                octets += info.Length;
                File.SetAttributes(fichier, FileAttributes.Normal);
                File.Delete(fichier);
                ok++;
            }
            catch { err++; }
        }

        try
        {
            foreach (var d in Directory.EnumerateDirectories(chemin, "*", SearchOption.AllDirectories))
            {
                try { Directory.Delete(d, true); } catch { }
            }
        }
        catch { }

        log?.Invoke($"  ✓ {ok} fichier(s), {FormatTaille(octets)}{(err > 0 ? $", {err} échec(s)" : "")}");
        return new(nom, ok, octets, err);
    }

    private static CategorieRapport NettoyerVignettes(string nom, string local, Action<string>? log)
    {
        var explorer = Path.Combine(local, @"Microsoft\Windows\Explorer");
        log?.Invoke($"[{nom}] {explorer}");
        if (!Directory.Exists(explorer)) { log?.Invoke("  (dossier inexistant)"); return new(nom, 0, 0, 0); }

        int ok = 0;
        long octets = 0;
        foreach (var pattern in new[] { "ThumbCache_*.db", "IconCache_*.db" })
        {
            foreach (var f in EnumerateSafely(explorer, pattern, SearchOption.TopDirectoryOnly))
            {
                try
                {
                    octets += new FileInfo(f).Length;
                    File.SetAttributes(f, FileAttributes.Normal);
                    File.Delete(f);
                    ok++;
                }
                catch { }
            }
        }
        log?.Invoke($"  ✓ {ok} fichier(s), {FormatTaille(octets)}");
        return new(nom, ok, octets, 0);
    }

    private static CategorieRapport ViderCorbeille(string nom, Action<string>? log)
    {
        log?.Invoke($"[{nom}]");
        // On lit la taille avant via SHQueryRecycleBin pour pouvoir reporter, mais
        // pour rester simple on log juste le résultat de l'opération.
        var hr = SHEmptyRecycleBin(IntPtr.Zero, null,
            RecycleFlags.SHERB_NOCONFIRMATION |
            RecycleFlags.SHERB_NOPROGRESSUI |
            RecycleFlags.SHERB_NOSOUND);
        if (hr == 0) { log?.Invoke("  ✓ Vidée"); return new(nom, 1, 0, 0); }
        if (hr == -2147418113) { log?.Invoke("  (déjà vide)"); return new(nom, 0, 0, 0); }
        log?.Invoke($"  (code {hr:X})");
        return new(nom, 0, 0, 1);
    }

    private static IEnumerable<string> EnumerateSafely(string root, string pattern, SearchOption option)
    {
        var stack = new Stack<string>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var dir = stack.Pop();
            string[] files;
            try { files = Directory.GetFiles(dir, pattern); } catch { continue; }
            foreach (var f in files) yield return f;
            if (option == SearchOption.AllDirectories)
            {
                string[] subs;
                try { subs = Directory.GetDirectories(dir); } catch { continue; }
                foreach (var s in subs) stack.Push(s);
            }
        }
    }

    public static string FormatTaille(long octets)
    {
        if (octets < 1024) return $"{octets} o";
        if (octets < 1024 * 1024) return $"{octets / 1024.0:F1} Ko";
        if (octets < 1024L * 1024 * 1024) return $"{octets / (1024.0 * 1024):F1} Mo";
        return $"{octets / (1024.0 * 1024 * 1024):F2} Go";
    }
}
