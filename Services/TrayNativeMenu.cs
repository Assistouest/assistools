using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Assistools.Services;

// Menu contextuel Win32 natif (TrackPopupMenuEx) pour la zone de notification.
// Look 100% identique aux autres apps tray de Windows.
public sealed class TrayNativeMenu
{
    private const uint MF_STRING    = 0x0000;
    private const uint MF_SEPARATOR = 0x0800;
    private const uint MF_POPUP     = 0x0010;
    private const uint MF_GRAYED    = 0x0001;
    private const uint MF_CHECKED   = 0x0008;
    private const uint MF_DEFAULT   = 0x1000;

    private const uint TPM_LEFTALIGN   = 0x0000;
    private const uint TPM_RIGHTBUTTON = 0x0002;
    private const uint TPM_RETURNCMD   = 0x0100;
    private const uint TPM_NONOTIFY    = 0x0080;

    private readonly IntPtr _hMenu;
    private readonly Dictionary<int, Action> _actions = new();
    private readonly List<TrayNativeMenu> _subMenus = new();
    private int _nextId = 1;
    private int? _defaultId;

    public TrayNativeMenu() { _hMenu = CreatePopupMenu(); }

    public TrayNativeMenu AddItem(string text, Action onClick, bool enabled = true, bool isChecked = false, bool isDefault = false)
    {
        int id = _nextId++;
        uint flags = MF_STRING;
        if (!enabled)  flags |= MF_GRAYED;
        if (isChecked) flags |= MF_CHECKED;
        if (isDefault) { flags |= MF_DEFAULT; _defaultId = id; }
        AppendMenuW(_hMenu, flags, (UIntPtr)id, text);
        _actions[id] = onClick;
        return this;
    }

    public TrayNativeMenu AddSeparator()
    {
        AppendMenuW(_hMenu, MF_SEPARATOR, UIntPtr.Zero, null);
        return this;
    }

    public TrayNativeMenu AddSubmenu(string text, Action<TrayNativeMenu> build, bool enabled = true)
    {
        var sub = new TrayNativeMenu();
        build(sub);
        uint flags = MF_POPUP | MF_STRING;
        if (!enabled) flags |= MF_GRAYED;
        AppendMenuW(_hMenu, flags, (UIntPtr)(long)sub._hMenu, text);
        _subMenus.Add(sub);
        return this;
    }

    public void ShowAt(IntPtr ownerHwnd)
    {
        if (!GetCursorPos(out var pt)) return;

        // Indispensable : sinon le menu peut ne pas se fermer au clic en dehors.
        SetForegroundWindow(ownerHwnd);

        int cmd = TrackPopupMenuEx(
            _hMenu,
            TPM_LEFTALIGN | TPM_RIGHTBUTTON | TPM_RETURNCMD,
            pt.X, pt.Y,
            ownerHwnd,
            IntPtr.Zero);

        // Astuce documentée Microsoft : poste un message bidon pour purger.
        PostMessage(ownerHwnd, 0x0000, IntPtr.Zero, IntPtr.Zero);

        Destroy();

        if (cmd != 0 && _actions.TryGetValue(cmd, out var action))
        {
            try { action(); } catch (Exception ex) { LogStore.Append($"[Tray][NativeMenu] {ex.Message}"); }
        }
    }

    private void Destroy()
    {
        foreach (var s in _subMenus) s.Destroy();
        if (_hMenu != IntPtr.Zero) DestroyMenu(_hMenu);
    }

    [StructLayout(LayoutKind.Sequential)] private struct POINT { public int X; public int Y; }

    [DllImport("user32.dll")] private static extern IntPtr CreatePopupMenu();
    [DllImport("user32.dll")] private static extern bool DestroyMenu(IntPtr hMenu);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool AppendMenuW(IntPtr hMenu, uint uFlags, UIntPtr uIDNewItem, string? lpNewItem);
    [DllImport("user32.dll")] private static extern int TrackPopupMenuEx(IntPtr hMenu, uint uFlags, int x, int y, IntPtr hWnd, IntPtr lptpm);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT lpPoint);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
}
