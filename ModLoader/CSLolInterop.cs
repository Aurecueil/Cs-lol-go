using System.Runtime.InteropServices;

namespace ModManager
{
    internal static class CSLolInterop
    {
        private const string DllName = "cslol-tools/cslol-dll.dll";

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr cslol_init();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        public static extern IntPtr cslol_set_config([MarshalAs(UnmanagedType.LPWStr)] string configPath);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr cslol_set_flags(ulong flags);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr cslol_set_log_level(long level);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern uint cslol_find();

        // The v1.8 runner uses this simplified hook call
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr cslol_hook(uint tid, uint timeoutMs, uint stepMs);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr cslol_log_pull();
    }
}