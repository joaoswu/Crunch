using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Crunch.Models;
using Microsoft.Win32;

namespace Crunch.Services
{
    public class OrphanHunterService
    {
        private static readonly HashSet<string> SystemReservedNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "Microsoft", "Windows", "Packages", "Temp", "Programs", "ConnectedDevicesPlatform",
            "D3DSCache", "Comms", "CrashDumps", "Package Cache", "USOPrivate", "USOShared",
            "System", "Common Files", "OEM", "Default", "Default User", "All Users"
        };

        public static async Task<List<OrphanAppItem>> ScanOrphanedAppFoldersAsync(
            IProgress<string>? progress = null, 
            CancellationToken ct = default)
        {
            var results = new ConcurrentBag<OrphanAppItem>();

            await Task.Run(() =>
            {
                progress?.Report("Querying Windows installed application inventory...");
                var installedNames = GetInstalledSoftwareNames();
                var runningProcesses = GetRunningProcessNames();

                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

                var searchRoots = new List<(string path, string type)>
                {
                    (localAppData, "LocalAppData"),
                    (appData, "AppData"),
                    (programData, "ProgramData")
                };

                foreach (var (root, locType) in searchRoots)
                {
                    if (ct.IsCancellationRequested) break;
                    if (!Directory.Exists(root)) continue;

                    try
                    {
                        var dirInfo = new DirectoryInfo(root);
                        foreach (var sub in dirInfo.EnumerateDirectories("*", new EnumerationOptions
                        {
                            IgnoreInaccessible = true,
                            RecurseSubdirectories = false
                        }))
                        {
                            if (ct.IsCancellationRequested) break;
                            string name = sub.Name;

                            if (SystemReservedNames.Contains(name) || name.StartsWith("."))
                                continue;

                            // Check if this folder corresponds to any installed software or running process
                            if (IsInstalledOrRunning(name, installedNames, runningProcesses))
                                continue;

                            // If folder has not been modified in > 30 days, inspect
                            if ((DateTime.Now - sub.LastWriteTime).TotalDays > 30)
                            {
                                progress?.Report($"Found orphan candidate: {name} in {locType}");
                                var (size, count) = CalculateFolderSize(sub, ct);

                                if (size > 5 * 1024 * 1024) // 5MB minimum to avoid trivial noise
                                {
                                    results.Add(new OrphanAppItem
                                    {
                                        Name = name,
                                        FullPath = sub.FullName,
                                        LocationType = locType,
                                        SizeBytes = size,
                                        FileCount = count,
                                        LastModified = sub.LastWriteTime,
                                        Details = $"Abandoned folder ({count} stale files, no uninstaller found)",
                                        Status = "Ready to purge"
                                    });
                                }
                            }
                        }
                    }
                    catch { }
                }
            }, ct);

            return results.OrderByDescending(x => x.SizeBytes).ToList();
        }

        private static bool IsInstalledOrRunning(string folderName, HashSet<string> installed, HashSet<string> processes)
        {
            string clean = folderName.ToLowerInvariant();
            if (installed.Any(x => x.Contains(clean) || clean.Contains(x))) return true;
            if (processes.Any(x => x.Contains(clean) || clean.Contains(x))) return true;
            return false;
        }

        private static HashSet<string> GetRunningProcessNames()
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (var p in Process.GetProcesses())
                {
                    try
                    {
                        set.Add(p.ProcessName);
                    }
                    catch { }
                }
            }
            catch { }
            return set;
        }

        private static HashSet<string> GetInstalledSoftwareNames()
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            string[] registryKeys = new[]
            {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
            };

            // HKLM
            foreach (var keyPath in registryKeys)
            {
                try
                {
                    using var key = Registry.LocalMachine.OpenSubKey(keyPath);
                    if (key != null)
                    {
                        foreach (var subkeyName in key.GetSubKeyNames())
                        {
                            using var subkey = key.OpenSubKey(subkeyName);
                            var displayName = subkey?.GetValue("DisplayName") as string;
                            if (!string.IsNullOrWhiteSpace(displayName))
                            {
                                names.Add(displayName.ToLowerInvariant());
                            }
                        }
                    }
                }
                catch { }
            }

            // HKCU
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
                if (key != null)
                {
                    foreach (var subkeyName in key.GetSubKeyNames())
                    {
                        using var subkey = key.OpenSubKey(subkeyName);
                        var displayName = subkey?.GetValue("DisplayName") as string;
                        if (!string.IsNullOrWhiteSpace(displayName))
                        {
                            names.Add(displayName.ToLowerInvariant());
                        }
                    }
                }
            }
            catch { }

            return names;
        }

        private static (long totalBytes, int fileCount) CalculateFolderSize(DirectoryInfo dir, CancellationToken ct)
        {
            long bytes = 0;
            int count = 0;

            try
            {
                foreach (var file in dir.EnumerateFiles("*", new EnumerationOptions
                {
                    IgnoreInaccessible = true,
                    RecurseSubdirectories = true
                }))
                {
                    if (ct.IsCancellationRequested) break;
                    try
                    {
                        bytes += file.Length;
                        count++;
                    }
                    catch { }
                }
            }
            catch { }

            return (bytes, count);
        }

        public static async Task<bool> DeleteOrphanFolderAsync(string folderPath, bool sendToRecycleBin = false)
        {
            return await DevBloatService.DeleteDevBloatFolderAsync(folderPath, sendToRecycleBin);
        }
    }
}
