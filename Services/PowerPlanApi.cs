using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Assistools.Services;

/// <summary>
/// Accès direct à powrprof.dll — gère les plans d'alimentation par GUID,
/// sans parser de texte localisé.
/// </summary>
internal static class PowerPlanApi
{
    // GUIDs Windows officiels (fixes sur toutes les machines)
    public static readonly Guid PowerSaver      = new("a1841308-3541-4fab-bc81-f71556f20b4a");
    public static readonly Guid Balanced        = new("381b4222-f694-41f0-9685-ff5bb260df2e");
    public static readonly Guid HighPerformance = new("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");
    public static readonly Guid UltimatePerf    = new("e9a42b02-d5df-448d-aa00-03f14749eb61");

    [DllImport("powrprof.dll")]
    private static extern uint PowerSetActiveScheme(IntPtr UserRootPowerKey, ref Guid SchemeGuid);

    [DllImport("powrprof.dll")]
    private static extern uint PowerGetActiveScheme(IntPtr UserRootPowerKey, out IntPtr ActivePolicyGuid);

    [DllImport("powrprof.dll")]
    private static extern uint PowerDuplicateScheme(IntPtr RootPowerKey, ref Guid SourceSchemeGuid, ref IntPtr DestinationSchemeGuid);

    [DllImport("powrprof.dll")]
    private static extern uint PowerEnumerate(
        IntPtr RootPowerKey, IntPtr SchemeGuid, IntPtr SubGroupOfPowerSettingsGuid,
        uint AccessFlags, uint Index, ref Guid Buffer, ref uint BufferSize);

    [DllImport("powrprof.dll", CharSet = CharSet.Unicode)]
    private static extern uint PowerReadFriendlyName(
        IntPtr RootPowerKey, ref Guid SchemeGuid, IntPtr SubGroupOfPowerSettingsGuid,
        IntPtr PowerSettingGuid, StringBuilder? Buffer, ref uint BufferSize);

    [DllImport("powrprof.dll")]
    private static extern uint PowerDeleteScheme(IntPtr RootPowerKey, ref Guid SchemeGuid);

    private const uint ACCESS_SCHEME = 16;

    public static Guid GetActive()
    {
        if (PowerGetActiveScheme(IntPtr.Zero, out var ptr) != 0 || ptr == IntPtr.Zero)
            return Balanced;
        try { return (Guid)Marshal.PtrToStructure(ptr, typeof(Guid))!; }
        finally { Marshal.FreeHGlobal(ptr); }
    }

    public static bool SetActive(Guid scheme)
        => PowerSetActiveScheme(IntPtr.Zero, ref scheme) == 0;

    public static List<Guid> ListSchemes()
    {
        var list = new List<Guid>();
        uint i = 0;
        while (true)
        {
            var buf = Guid.Empty;
            uint size = (uint)Marshal.SizeOf(typeof(Guid));
            if (PowerEnumerate(IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, ACCESS_SCHEME, i, ref buf, ref size) != 0)
                break;
            list.Add(buf);
            i++;
        }
        return list;
    }

    public static string GetFriendlyName(Guid scheme)
    {
        uint size = 0;
        PowerReadFriendlyName(IntPtr.Zero, ref scheme, IntPtr.Zero, IntPtr.Zero, null, ref size);
        if (size == 0) return "";
        var sb = new StringBuilder((int)size);
        if (PowerReadFriendlyName(IntPtr.Zero, ref scheme, IntPtr.Zero, IntPtr.Zero, sb, ref size) != 0)
            return "";
        return sb.ToString();
    }

    public static Guid DuplicateScheme(Guid source)
    {
        IntPtr dest = IntPtr.Zero;
        if (PowerDuplicateScheme(IntPtr.Zero, ref source, ref dest) != 0 || dest == IntPtr.Zero)
            return Guid.Empty;
        try { return (Guid)Marshal.PtrToStructure(dest, typeof(Guid))!; }
        finally { Marshal.FreeHGlobal(dest); }
    }

    public static bool DeleteScheme(Guid scheme)
        => PowerDeleteScheme(IntPtr.Zero, ref scheme) == 0;

    /// <summary>
    /// Trouve ou crée un plan correspondant au type demandé.
    /// 1. GUID standard si présent
    /// 2. GUID alternatif (Ultimate pour Performance) si présent
    /// 3. Une copie existante reconnaissable par nom
    /// 4. Sinon, crée une copie depuis le GUID standard
    /// Si plusieurs copies existent, supprime les doublons et n'en garde qu'une.
    /// </summary>
    public static Guid GetOrCreateScheme(SchemeKind kind)
    {
        var (standard, alt, keywords) = kind switch
        {
            SchemeKind.PowerSaver      => (PowerSaver,      Guid.Empty,    new[] { "économie", "economie", "saver", "battery" }),
            SchemeKind.Balanced        => (Balanced,        Guid.Empty,    new[] { "équilibré", "equilibre", "normal", "balanced" }),
            SchemeKind.HighPerformance => (HighPerformance, UltimatePerf,  new[] { "performance", "élevé", "eleve", "high", "ultimate" }),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

        var schemes = ListSchemes();

        // 1. GUID standard / alt
        if (alt != Guid.Empty && schemes.Contains(alt)) return alt;
        if (schemes.Contains(standard)) return standard;

        // 2. Chercher des copies par nom
        var matches = new List<Guid>();
        foreach (var g in schemes)
        {
            if (g == PowerSaver || g == Balanced || g == HighPerformance || g == UltimatePerf) continue;
            var name = GetFriendlyName(g).ToLowerInvariant();
            foreach (var kw in keywords)
            {
                if (name.Contains(kw)) { matches.Add(g); break; }
            }
        }

        if (matches.Count >= 1)
        {
            // Garder la première, supprimer les doublons
            for (int i = 1; i < matches.Count; i++)
                DeleteScheme(matches[i]);
            return matches[0];
        }

        // 3. Créer une copie depuis le GUID standard
        return DuplicateScheme(standard);
    }

    public enum SchemeKind { PowerSaver, Balanced, HighPerformance }
}
