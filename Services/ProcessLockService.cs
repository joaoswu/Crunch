using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Crunch.Services
{
    public class ProcessLockService
    {
        [StructLayout(LayoutKind.Sequential)]
        struct RM_UNIQUE_PROCESS
        {
            public int dwProcessId;
            public System.Runtime.InteropServices.ComTypes.FILETIME ProcessStartTime;
        }

        const int CCH_RM_MAX_APP_NAME = 255;
        const int CCH_RM_MAX_SVC_NAME = 63;

        enum RM_APP_TYPE
        {
            RmUnknownApp = 0,
            RmMainWindow = 1,
            RmOtherWindow = 2,
            RmService = 3,
            RmExplorer = 4,
            RmConsole = 5,
            RmCritical = 1000
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct RM_PROCESS_INFO
        {
            public RM_UNIQUE_PROCESS Process;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCH_RM_MAX_APP_NAME + 1)]
            public string strAppName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCH_RM_MAX_SVC_NAME + 1)]
            public string strServiceShortName;
            public RM_APP_TYPE ApplicationType;
            public uint AppStatus;
            public uint TSSessionId;
            [MarshalAs(UnmanagedType.Bool)]
            public bool bRestartable;
        }

        [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
        static extern int RmStartSession(out uint pSessionHandle, int dwSessionFlags, string strSessionKey);

        [DllImport("rstrtmgr.dll")]
        static extern int RmEndSession(uint pSessionHandle);

        [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
        static extern int RmRegisterResources(uint pSessionHandle, uint nFiles, string[] rgsFilenames,
            uint nApplications, [In] RM_UNIQUE_PROCESS[]? rgApplications, uint nServices, string[]? rgsServiceNames);

        [DllImport("rstrtmgr.dll")]
        static extern int RmGetList(uint pSessionHandle, out uint pnProcInfoNeeded,
            ref uint pnProcInfo, [In, Out] RM_PROCESS_INFO[]? rgAffectedApps, ref uint lpdwRebootReasons);

        private const int ERROR_MORE_DATA = 234;

        /// <summary>
        /// Finds all processes that currently have a lock or open handle on the specified file.
        /// </summary>
        public static List<Process> GetLockingProcesses(string filePath)
        {
            var processes = new List<Process>();
            string key = Guid.NewGuid().ToString();
            int res = RmStartSession(out uint handle, 0, key);
            if (res != 0) return processes;

            try
            {
                string[] resources = new[] { filePath };
                res = RmRegisterResources(handle, (uint)resources.Length, resources, 0, null, 0, null);
                if (res != 0) return processes;

                uint pnProcInfoNeeded = 0;
                uint pnProcInfo = 0;
                uint lpdwRebootReasons = 0;

                res = RmGetList(handle, out pnProcInfoNeeded, ref pnProcInfo, null, ref lpdwRebootReasons);
                if (res == ERROR_MORE_DATA && pnProcInfoNeeded > 0)
                {
                    var processInfo = new RM_PROCESS_INFO[pnProcInfoNeeded];
                    pnProcInfo = pnProcInfoNeeded;

                    res = RmGetList(handle, out pnProcInfoNeeded, ref pnProcInfo, processInfo, ref lpdwRebootReasons);
                    if (res == 0)
                    {
                        for (int i = 0; i < pnProcInfo; i++)
                        {
                            try
                            {
                                var p = Process.GetProcessById(processInfo[i].Process.dwProcessId);
                                processes.Add(p);
                            }
                            catch
                            {
                                // Process may have exited in the interim
                            }
                        }
                    }
                }
            }
            finally
            {
                RmEndSession(handle);
            }

            return processes;
        }

        /// <summary>
        /// Forcefully releases or terminates the processes holding a lock on the file.
        /// </summary>
        public static bool UnlockAndKillLockingProcesses(string filePath)
        {
            var lockingProcs = GetLockingProcesses(filePath);
            bool anyKilled = false;

            foreach (var proc in lockingProcs)
            {
                try
                {
                    // Avoid killing critical Windows processes
                    string procName = proc.ProcessName.ToLowerInvariant();
                    if (procName == "explorer" || procName == "csrss" || procName == "lsass" || procName == "smss")
                        continue;

                    proc.Kill();
                    proc.WaitForExit(1000);
                    anyKilled = true;
                }
                catch
                {
                    // Ignored if lacking permissions
                }
            }

            return anyKilled;
        }
    }
}
