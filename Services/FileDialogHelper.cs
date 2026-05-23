using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Assistools.Services;

public static class FileDialogHelper
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct OPENFILENAME
    {
        public int lStructSize;
        public IntPtr hwndOwner;
        public IntPtr hInstance;
        public IntPtr lpstrFilter;
        public IntPtr lpstrCustomFilter;
        public int nMaxCustFilter;
        public int nFilterIndex;
        public IntPtr lpstrFile;
        public int nMaxFile;
        public IntPtr lpstrFileTitle;
        public int nMaxFileTitle;
        public IntPtr lpstrInitialDir;
        public IntPtr lpstrTitle;
        public int Flags;
        public short nFileOffset;
        public short nFileExtension;
        public IntPtr lpstrDefExt;
        public IntPtr lCustData;
        public IntPtr lpfnHook;
        public IntPtr lpTemplateName;
        public IntPtr pvReserved;
        public int dwReserved;
        public int FlagsEx;
    }

    [DllImport("comdlg32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool GetSaveFileName(ref OPENFILENAME lpofn);

    /// <summary>
    /// Displays a Win32 save file dialog. Works in both elevated (Admin) and non-elevated contexts.
    /// </summary>
    /// <param name="hwndOwner">Window handle of the owner window.</param>
    /// <param name="title">Title of the dialog window.</param>
    /// <param name="defaultFileName">Default suggested file name.</param>
    /// <param name="filter">File filter (e.g. "Document PDF (*.pdf)|*.pdf"). Use '|' to separate parts.</param>
    /// <param name="defaultExt">Default extension without dot (e.g. "pdf").</param>
    /// <returns>The chosen file path, or null if cancelled.</returns>
    public static string? SaveFileDialog(IntPtr hwndOwner, string title, string defaultFileName, string filter, string defaultExt)
    {
        var ofn = new OPENFILENAME();
        ofn.lStructSize = Marshal.SizeOf(ofn);
        ofn.hwndOwner = hwndOwner;
        
        // Format filter: replace '|' with '\0' and append final '\0'
        string formattedFilter = filter.Replace('|', '\0') + "\0";
        IntPtr filterPtr = Marshal.StringToHGlobalUni(formattedFilter);
        ofn.lpstrFilter = filterPtr;
        
        // Allocate buffer for output file path (Unicode uses 2 bytes per char)
        int maxFile = 2048;
        IntPtr fileBufferPtr = Marshal.AllocHGlobal(maxFile * 2);
        
        // Zero out the buffer
        byte[] zeroBuffer = new byte[maxFile * 2];
        Marshal.Copy(zeroBuffer, 0, fileBufferPtr, zeroBuffer.Length);
        
        // Copy the default filename to the start of the buffer
        byte[] defaultNameBytes = Encoding.Unicode.GetBytes(defaultFileName);
        Marshal.Copy(defaultNameBytes, 0, fileBufferPtr, Math.Min(defaultNameBytes.Length, zeroBuffer.Length));
        
        ofn.lpstrFile = fileBufferPtr;
        ofn.nMaxFile = maxFile;
        
        // Allocate title
        IntPtr titlePtr = Marshal.StringToHGlobalUni(title);
        ofn.lpstrTitle = titlePtr;
        
        // Allocate default extension
        IntPtr defExtPtr = Marshal.StringToHGlobalUni(defaultExt);
        ofn.lpstrDefExt = defExtPtr;
        
        // OFN_PATHMUSTEXIST (0x00000800) | OFN_OVERWRITEPROMPT (0x00000002) | OFN_EXPLORER (0x00080000)
        ofn.Flags = 0x00000800 | 0x00000002 | 0x00080000;

        string? result = null;
        try
        {
            if (GetSaveFileName(ref ofn))
            {
                result = Marshal.PtrToStringUni(ofn.lpstrFile);
            }
        }
        finally
        {
            // Free all allocated unmanaged memory
            if (filterPtr != IntPtr.Zero) Marshal.FreeHGlobal(filterPtr);
            if (fileBufferPtr != IntPtr.Zero) Marshal.FreeHGlobal(fileBufferPtr);
            if (titlePtr != IntPtr.Zero) Marshal.FreeHGlobal(titlePtr);
            if (defExtPtr != IntPtr.Zero) Marshal.FreeHGlobal(defExtPtr);
        }

        return result;
    }
}
