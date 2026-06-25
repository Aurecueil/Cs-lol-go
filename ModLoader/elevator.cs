using ModManager;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Windows;

namespace ModLoader
{
    public static class Elevator
    {
        // Native Windows API constants for token elevation checks
        private const int TokenElevation = 20;

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool OpenProcessToken(IntPtr processHandle, uint desiredAccess, out IntPtr tokenHandle);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool GetTokenInformation(IntPtr tokenHandle, int tokenInformationClass, IntPtr tokenInformation, uint tokenInformationLength, out uint returnLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        private const uint TOKEN_QUERY = 0x0008;

        /// <summary>
        /// Checks if the CURRENT running manager application is elevated (Running as Administrator).
        /// Replicates: crate::diagnostics::manager_is_elevated()
        /// </summary>
        public static bool IsManagerElevated()
        {
            try
            {
                using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
                {
                    WindowsPrincipal principal = new WindowsPrincipal(identity);
                    return principal.IsInRole(WindowsBuiltInRole.Administrator);
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Checks if the target process (League of Legends) is configured or currently running as an administrator.
        /// Replicates: crate::diagnostics::league_configured_as_admin()
        /// </summary>
        public static bool IsLeagueRunningAsAdmin()
        {
            // Replace with the exact executable name used by your game client variant if different
            string processName = "League of Legends";
            Process[] processes = Process.GetProcessesByName(processName);

            if (processes.Length == 0)
            {
                // If the process isn't running, we check its file compatibility properties if you have the path, 
                // but checking the active running token is the most reliable way under live conditions:
                return false;
            }

            Process leagueProcess = processes[0];
            IntPtr tokenHandle = IntPtr.Zero;

            try
            {
                // Open the access token associated with the League process
                if (!OpenProcessToken(leagueProcess.Handle, TOKEN_QUERY, out tokenHandle))
                {
                    return false;
                }

                uint returnLength = 0;
                int elevationSize = Marshal.SizeOf(typeof(int));
                IntPtr elevationPtr = Marshal.AllocHGlobal(elevationSize);

                try
                {
                    // Query the token to see if it is an elevated token type
                    if (GetTokenInformation(tokenHandle, TokenElevation, elevationPtr, (uint)elevationSize, out returnLength))
                    {
                        int isElevated = Marshal.ReadInt32(elevationPtr);
                        return isElevated != 0;
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(elevationPtr);
                }
            }
            catch
            {
                // Fallback/Safety catch if process restrictions deny handle access (which often implies it's higher integrity)
                return false;
            }
            finally
            {
                if (tokenHandle != IntPtr.Zero)
                {
                    CloseHandle(tokenHandle);
                }
            }

            return false;
        }

        /// <summary>
        /// Resolves whether the injector host should ask for elevation parameters.
        /// Replicates the evaluation logic from start_patcher_inner()
        /// </summary>
        public static int ShouldElevateInjector()
        {
            bool managerElevated = IsManagerElevated();
            bool leagueAdmin = IsLeagueRunningAsAdmin();

            if (!managerElevated && leagueAdmin)
            {
                CustomMessageBox.Show("League is running as Admin, Please restart cslol-go with admin privilages or remove them from league.", ["Okay"] ,"Privilages Missmatch");
                return 2;
            }
            if (managerElevated && leagueAdmin)
            {
                return 1;
            }

            return 0;
        }
    }
}
