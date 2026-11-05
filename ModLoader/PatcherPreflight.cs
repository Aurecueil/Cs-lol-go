#nullable enable
using System;
using System.Collections.Generic;
using System.Security.Principal;
using Microsoft.Win32;

namespace ModManager
{
    /// <summary>
    /// Guide section 6.1. The DLL lives in a user-writable folder, so a high-integrity host must never load it.
    /// Returns null when it is safe to start a session, otherwise the message to show the user.
    /// </summary>
    public static class PatcherPreflight
    {
        private static readonly string[] LeagueExes =
        {
            "LeagueClient.exe", "League of Legends.exe", "RiotClientServices.exe", "Riot Client.exe"
        };

        private const string LayersPath = @"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers";

        public static string? Check()
        {
            try
            {
                using (var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System"))
                {
                    if (k?.GetValue("EnableLUA") is int lua && lua == 0)
                        return "User Account Control is turned off, so every program runs elevated. Turn UAC on and restart Windows, then try again.";
                }

                using (var id = WindowsIdentity.GetCurrent())
                {
                    if (new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator))
                        return "The app is running as administrator. Close it and start it normally (not \"Run as administrator\").";
                }

                var hits = FindRunAsAdmin();
                if (hits.Count > 0)
                    return "League of Legends or the Riot Client is set to run as administrator, which the patcher cannot work with. " +
                           "Remove the setting by running these commands in a Command Prompt, then restart the Riot Client:\n\n" +
                           string.Join("\n", hits);
            }
            catch (Exception ex)
            {
                return $"Could not verify the system is safe for patching ({ex.Message}).";
            }
            return null;
        }

        private static List<string> FindRunAsAdmin()
        {
            var cmds = new List<string>();
            Scan(RegistryHive.CurrentUser, "HKCU", cmds);
            Scan(RegistryHive.LocalMachine, "HKLM", cmds);
            return cmds;
        }

        private static void Scan(RegistryHive hive, string label, List<string> cmds)
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
            using var key = baseKey.OpenSubKey(LayersPath);
            if (key == null) return;

            foreach (string name in key.GetValueNames())
            {
                if (!(key.GetValue(name) is string v) || v.IndexOf("RUNASADMIN", StringComparison.OrdinalIgnoreCase) < 0) continue;
                foreach (string exe in LeagueExes)
                {
                    if (name.Equals(exe, StringComparison.OrdinalIgnoreCase) ||
                        name.EndsWith("\\" + exe, StringComparison.OrdinalIgnoreCase))
                    {
                        cmds.Add($"reg delete \"{label}\\{LayersPath}\" /v \"{name}\" /f");
                        break;
                    }
                }
            }
        }
    }
}