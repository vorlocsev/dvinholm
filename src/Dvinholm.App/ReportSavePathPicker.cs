using System.Runtime.InteropServices;

namespace Dvinholm.App;

/// <summary>
/// Win32 Save As dialog that returns a path without materializing a new,
/// empty StorageFile. Windows.Storage.Pickers.FileSavePicker.PickSaveFileAsync
/// creates a zero-length file before our post-picker destination check, so
/// it must not be used for report exports rejected inside the app folder.
/// </summary>
internal static class ReportSavePathPicker
{
    private const int FileBufferCharacters = 32768;
    private const uint OfnOverwritePrompt = 0x00000002;
    private const uint OfnNoChangeDir = 0x00000008;
    private const uint OfnPathMustExist = 0x00000800;
    private const uint OfnNotestFileCreate = 0x00010000;
    private const uint OfnExplorer = 0x00080000;

    internal static string SuggestedName(string basename, string extension)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(basename);
        ArgumentException.ThrowIfNullOrWhiteSpace(extension);
        if (basename.IndexOfAny(['/', '\\', '\0']) >= 0 ||
            extension[0] != '.' ||
            extension.Length < 2 ||
            extension[1..].Any(ch => !char.IsAsciiLetterOrDigit(ch)))
            throw new ArgumentException("Invalid report filename or extension.");

        return basename.EndsWith(extension, StringComparison.OrdinalIgnoreCase)
            ? basename
            : basename + extension;
    }

    /// <returns>Chosen path; null only when the user dismissed Save As.</returns>
    internal static string? PickPath(
        IntPtr ownerHwnd, string suggestedBaseName, string extension, string typeName)
    {
        if (ownerHwnd == IntPtr.Zero)
            throw new ArgumentException("The owner window is not available.", nameof(ownerHwnd));
        ArgumentException.ThrowIfNullOrWhiteSpace(typeName);

        var proposedName = SuggestedName(suggestedBaseName, extension);
        IntPtr fileBuffer = IntPtr.Zero;
        IntPtr filterBuffer = IntPtr.Zero;
        IntPtr extensionBuffer = IntPtr.Zero;
        IntPtr titleBuffer = IntPtr.Zero;
        try
        {
            // OPENFILENAMEW requires a writable, NUL-terminated file-name
            // buffer, not an immutable marshalled .NET string.
            fileBuffer = Marshal.AllocHGlobal(FileBufferCharacters * sizeof(char));
            Marshal.Copy(new char[FileBufferCharacters], 0, fileBuffer, FileBufferCharacters);
            Marshal.Copy(proposedName.ToCharArray(), 0, fileBuffer, proposedName.Length);
            filterBuffer = Marshal.StringToHGlobalUni(
                typeName + " (*" + extension + ")\0*" + extension + "\0\0");
            extensionBuffer = Marshal.StringToHGlobalUni(extension[1..]);
            titleBuffer = Marshal.StringToHGlobalUni("Save Dvinholm report");

            var options = new OpenFileNameW
            {
                StructSize = (uint)Marshal.SizeOf<OpenFileNameW>(),
                Owner = ownerHwnd,
                Filter = filterBuffer,
                FilterIndex = 1,
                File = fileBuffer,
                MaxFileCharacters = FileBufferCharacters,
                Title = titleBuffer,
                DefaultExtension = extensionBuffer,
                Flags = OfnOverwritePrompt | OfnNoChangeDir |
                        OfnPathMustExist | OfnNotestFileCreate | OfnExplorer
            };

            // Unlike WinRT PickSaveFileAsync, the common dialog returns only
            // a filename; Dvinholm can reject it BEFORE creating any file.
            if (GetSaveFileNameW(ref options))
                return Marshal.PtrToStringUni(fileBuffer)
                    ?? throw new IOException("Save As returned an empty filename.");

            var dialogError = CommDlgExtendedError();
            if (dialogError != 0)
                throw new IOException($"Windows Save As dialog failed (0x{dialogError:X}).");
            return null; // Cancel leaves destination untouched.
        }
        finally
        {
            if (fileBuffer != IntPtr.Zero) Marshal.FreeHGlobal(fileBuffer);
            if (filterBuffer != IntPtr.Zero) Marshal.FreeHGlobal(filterBuffer);
            if (extensionBuffer != IntPtr.Zero) Marshal.FreeHGlobal(extensionBuffer);
            if (titleBuffer != IntPtr.Zero) Marshal.FreeHGlobal(titleBuffer);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct OpenFileNameW
    {
        public uint StructSize;
        public IntPtr Owner;
        public IntPtr Instance;
        public IntPtr Filter;
        public IntPtr CustomFilter;
        public uint MaxCustomFilter;
        public uint FilterIndex;
        public IntPtr File;
        public uint MaxFileCharacters;
        public IntPtr FileTitle;
        public uint MaxFileTitleCharacters;
        public IntPtr InitialDirectory;
        public IntPtr Title;
        public uint Flags;
        public ushort FileOffset;
        public ushort FileExtension;
        public IntPtr DefaultExtension;
        public IntPtr CustomData;
        public IntPtr Hook;
        public IntPtr TemplateName;
        public IntPtr Reserved;
        public uint ReservedFlags;
        public uint FlagsEx;
    }

    [DllImport("comdlg32.dll", EntryPoint = "GetSaveFileNameW", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSaveFileNameW(ref OpenFileNameW options);

    [DllImport("comdlg32.dll", EntryPoint = "CommDlgExtendedError", ExactSpelling = true)]
    private static extern uint CommDlgExtendedError();
}
