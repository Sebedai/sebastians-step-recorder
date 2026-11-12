using System.Runtime.InteropServices;

namespace StepRecorder.Native;

public enum RecycleResult { Recycled, NoRecycleBin, Failed }

/// <summary>
/// In den Papierkorb verschieben über IFileOperation – löscht nie endgültig: Würde ein Element nicht im Papierkorb landen
/// (USB-Stick, Netzlaufwerk, zu groß, „Dateien nicht in den Papierkorb verschieben“), bricht die Senke den Vorgang vorher ab.
/// </summary>
public static class RecycleBin
{
    /// <summary>Hat das Laufwerk vermutlich einen Papierkorb? Nur für die Wortwahl von Rückfragen.</summary>
    public static bool LikelyAvailable(string path)
    {
        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(path));
            if (root == null || root.StartsWith(@"\\")) return false;
            if (new DriveInfo(root).DriveType != DriveType.Fixed) return false;
            var info = new SHQUERYRBINFO { cbSize = Marshal.SizeOf<SHQUERYRBINFO>() };
            return SHQueryRecycleBinW(root, ref info) == 0;
        }
        catch { return false; }
    }

    /// <summary>Alle Pfade in einem Vorgang (eine Rückgängig-Einheit im Explorer) in den Papierkorb.</summary>
    public static RecycleResult Delete(IReadOnlyList<string> paths, out string? error)
    {
        error = null;
        var existing = paths.Where(p => File.Exists(p) || Directory.Exists(p)).Select(Path.GetFullPath).ToList();
        if (existing.Count == 0) return RecycleResult.Recycled;
        IFileOperation? op = null;
        var sink = new Sink();
        try
        {
            op = (IFileOperation)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("3ad05575-8857-4850-9277-11b85bdb8e09"))!)!;
            op.SetOperationFlags(FOF_ALLOWUNDO | FOF_NO_UI | FOFX_RECYCLEONDELETE | FOFX_EARLYFAILURE);
            op.Advise(sink, out uint cookie);
            foreach (var p in existing)
            {
                var iid = typeof(IShellItem).GUID;
                int hr = SHCreateItemFromParsingName(p, IntPtr.Zero, ref iid, out var item);
                if (hr != 0) { error = $"0x{hr:X8}"; return RecycleResult.Failed; }
                op.DeleteItem(item, null);
            }
            int perf;
            try { op.PerformOperations(); perf = 0; }
            catch (COMException ex) { perf = ex.HResult; }
            op.Unadvise(cookie);
            if (sink.Vetoed) return RecycleResult.NoRecycleBin;
            if (perf != 0 || sink.Failure != 0) { error = $"0x{(perf != 0 ? perf : sink.Failure):X8}"; return RecycleResult.Failed; }
            // Gegenprobe: alles weg?
            if (existing.Any(p => File.Exists(p) || Directory.Exists(p))) { error = "not removed"; return RecycleResult.Failed; }
            return RecycleResult.Recycled;
        }
        catch (Exception ex) { error = ex.Message; return RecycleResult.Failed; }
        finally { if (op != null) Marshal.ReleaseComObject(op); }
    }

    const uint FOF_ALLOWUNDO = 0x40, FOF_NO_UI = 0x4 | 0x10 | 0x200 | 0x400, FOFX_RECYCLEONDELETE = 0x00080000, FOFX_EARLYFAILURE = 0x00100000;
    const uint TSF_DELETE_RECYCLE_IF_POSSIBLE = 0x80;
    const int E_ABORT = unchecked((int)0x80004004);

    /// <summary>Fortschritts-Senke: Veto vor endgültigem Löschen.</summary>
    sealed class Sink : IFileOperationProgressSink
    {
        public bool Vetoed;
        public int Failure;
        public int StartOperations() => 0;
        public int FinishOperations(int hrResult) => 0;
        public int PreRenameItem(uint dwFlags, IShellItem psiItem, string? pszNewName) => 0;
        public int PostRenameItem(uint dwFlags, IShellItem psiItem, string? pszNewName, int hrRename, IShellItem? psiNewlyCreated) => 0;
        public int PreMoveItem(uint dwFlags, IShellItem psiItem, IShellItem psiDestinationFolder, string? pszNewName) => 0;
        public int PostMoveItem(uint dwFlags, IShellItem psiItem, IShellItem psiDestinationFolder, string? pszNewName, int hrMove, IShellItem? psiNewlyCreated) => 0;
        public int PreCopyItem(uint dwFlags, IShellItem psiItem, IShellItem psiDestinationFolder, string? pszNewName) => 0;
        public int PostCopyItem(uint dwFlags, IShellItem psiItem, IShellItem psiDestinationFolder, string? pszNewName, int hrCopy, IShellItem? psiNewlyCreated) => 0;
        public int PreDeleteItem(uint dwFlags, IShellItem psiItem)
        {
            // Ohne dieses Kennzeichen würde endgültig gelöscht – dann lieber gar nicht
            if ((dwFlags & TSF_DELETE_RECYCLE_IF_POSSIBLE) == 0) { Vetoed = true; return E_ABORT; }
            return 0;
        }
        public int PostDeleteItem(uint dwFlags, IShellItem psiItem, int hrDelete, IShellItem? psiNewlyCreated) { if (hrDelete != 0 && Failure == 0) Failure = hrDelete; return 0; }
        public int PreNewItem(uint dwFlags, IShellItem psiDestinationFolder, string? pszNewName) => 0;
        public int PostNewItem(uint dwFlags, IShellItem psiDestinationFolder, string? pszNewName, string? pszTemplateName, uint dwFileAttributes, int hrNew, IShellItem? psiNewItem) => 0;
        public int UpdateProgress(uint iWorkTotal, uint iWorkSoFar) => 0;
        public int ResetTimer() => 0;
        public int PauseTimer() => 0;
        public int ResumeTimer() => 0;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)] struct SHQUERYRBINFO { public int cbSize; public long i64Size; public long i64NumItems; }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern int SHQueryRecycleBinW(string pszRootPath, ref SHQUERYRBINFO pSHQueryRBInfo);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)] static extern int SHCreateItemFromParsingName(string pszPath, IntPtr pbc, ref Guid riid, out IShellItem ppv);

    [ComImport, Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IShellItem
    {
        void BindToHandler(IntPtr pbc, ref Guid bhid, ref Guid riid, out IntPtr ppv);
        void GetParent(out IShellItem ppsi);
        void GetDisplayName(uint sigdnName, out IntPtr ppszName);
        void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
        void Compare(IShellItem psi, uint hint, out int piOrder);
    }

    [ComImport, Guid("04b0f1a7-9490-44bc-96e1-4296a31252e2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IFileOperationProgressSink
    {
        [PreserveSig] int StartOperations();
        [PreserveSig] int FinishOperations(int hrResult);
        [PreserveSig] int PreRenameItem(uint dwFlags, IShellItem psiItem, [MarshalAs(UnmanagedType.LPWStr)] string? pszNewName);
        [PreserveSig] int PostRenameItem(uint dwFlags, IShellItem psiItem, [MarshalAs(UnmanagedType.LPWStr)] string? pszNewName, int hrRename, IShellItem? psiNewlyCreated);
        [PreserveSig] int PreMoveItem(uint dwFlags, IShellItem psiItem, IShellItem psiDestinationFolder, [MarshalAs(UnmanagedType.LPWStr)] string? pszNewName);
        [PreserveSig] int PostMoveItem(uint dwFlags, IShellItem psiItem, IShellItem psiDestinationFolder, [MarshalAs(UnmanagedType.LPWStr)] string? pszNewName, int hrMove, IShellItem? psiNewlyCreated);
        [PreserveSig] int PreCopyItem(uint dwFlags, IShellItem psiItem, IShellItem psiDestinationFolder, [MarshalAs(UnmanagedType.LPWStr)] string? pszNewName);
        [PreserveSig] int PostCopyItem(uint dwFlags, IShellItem psiItem, IShellItem psiDestinationFolder, [MarshalAs(UnmanagedType.LPWStr)] string? pszNewName, int hrCopy, IShellItem? psiNewlyCreated);
        [PreserveSig] int PreDeleteItem(uint dwFlags, IShellItem psiItem);
        [PreserveSig] int PostDeleteItem(uint dwFlags, IShellItem psiItem, int hrDelete, IShellItem? psiNewlyCreated);
        [PreserveSig] int PreNewItem(uint dwFlags, IShellItem psiDestinationFolder, [MarshalAs(UnmanagedType.LPWStr)] string? pszNewName);
        [PreserveSig] int PostNewItem(uint dwFlags, IShellItem psiDestinationFolder, [MarshalAs(UnmanagedType.LPWStr)] string? pszNewName, [MarshalAs(UnmanagedType.LPWStr)] string? pszTemplateName, uint dwFileAttributes, int hrNew, IShellItem? psiNewItem);
        [PreserveSig] int UpdateProgress(uint iWorkTotal, uint iWorkSoFar);
        [PreserveSig] int ResetTimer();
        [PreserveSig] int PauseTimer();
        [PreserveSig] int ResumeTimer();
    }

    [ComImport, Guid("947aab5f-0a5c-4c13-b4d6-4bf7836fc9f8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IFileOperation
    {
        void Advise(IFileOperationProgressSink pfops, out uint pdwCookie);
        void Unadvise(uint dwCookie);
        void SetOperationFlags(uint dwOperationFlags);
        void SetProgressMessage([MarshalAs(UnmanagedType.LPWStr)] string pszMessage);
        void SetProgressDialog(IntPtr popd);
        void SetProperties(IntPtr pproparray);
        void SetOwnerWindow(IntPtr hwndOwner);
        void ApplyPropertiesToItem(IShellItem psiItem);
        void ApplyPropertiesToItems([MarshalAs(UnmanagedType.IUnknown)] object punkItems);
        void RenameItem(IShellItem psiItem, [MarshalAs(UnmanagedType.LPWStr)] string pszNewName, IFileOperationProgressSink? pfopsItem);
        void RenameItems([MarshalAs(UnmanagedType.IUnknown)] object pUnkItems, [MarshalAs(UnmanagedType.LPWStr)] string pszNewName);
        void MoveItem(IShellItem psiItem, IShellItem psiDestinationFolder, [MarshalAs(UnmanagedType.LPWStr)] string? pszNewName, IFileOperationProgressSink? pfopsItem);
        void MoveItems([MarshalAs(UnmanagedType.IUnknown)] object punkItems, IShellItem psiDestinationFolder);
        void CopyItem(IShellItem psiItem, IShellItem psiDestinationFolder, [MarshalAs(UnmanagedType.LPWStr)] string? pszCopyName, IFileOperationProgressSink? pfopsItem);
        void CopyItems([MarshalAs(UnmanagedType.IUnknown)] object punkItems, IShellItem psiDestinationFolder);
        void DeleteItem(IShellItem psiItem, IFileOperationProgressSink? pfopsItem);
        void DeleteItems([MarshalAs(UnmanagedType.IUnknown)] object punkItems);
        void NewItem(IShellItem psiDestinationFolder, uint dwFileAttributes, [MarshalAs(UnmanagedType.LPWStr)] string pszName, [MarshalAs(UnmanagedType.LPWStr)] string? pszTemplateName, IFileOperationProgressSink? pfopsItem);
        void PerformOperations();
        [return: MarshalAs(UnmanagedType.Bool)] bool GetAnyOperationsAborted();
    }
}
