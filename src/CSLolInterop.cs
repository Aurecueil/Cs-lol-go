using System;
using System.Runtime.InteropServices;

public static class CSLolInterop
{
    private const string DllName = "cslol-tools/cslol-dll.dll";

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr cslol_init();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
    public static extern IntPtr cslol_set_config(string prefix);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern uint cslol_find();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr cslol_hook(uint tid, int timeoutMs, int retries);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr cslol_log_pull();
}
