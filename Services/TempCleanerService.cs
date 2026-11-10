using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Crunch.Models;

namespace Crunch.Services
{
    public class TempCleanerService
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        struct SHQUERYRBINFO
        {
            public int cbSize;
            public long i64Size;
            public long i64NumItems;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        static extern int SHQueryRecycleBin(string? pszRootPath, ref SHQUERYRBINFO pSHQueryRBInfo);

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        static extern int SHEmptyRecycleBin(IntPtr hwnd, string? pszRootPath, uint dwFlags);

        const uint SHERB_NOCONFIRMATION = 0x00000001;
        const uint SHERB_NOPROGRESSUI   = 0x00000002;
        const uint SHERB_NOSOUND        = 0x00000004;

        public static List<TempCleanCategory> GetDefaultCategories()
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            return new List<TempCleanCategory>
            {
                // 1. SYSTEM TARGETS
                new TempCleanCategory
                {
                    Id = "user_temp",
                    Title = "User Temp Files",
                    Subtitle = "%LocalAppData%\\Temp - Safe to clear, created by running applications",
                    PathOrLocation = Path.GetTempPath(),
                    CategoryGroup = "System",
                    IconKey = "FlameIcon"
                },
                new TempCleanCategory
                {
                    Id = "system_temp",
                    Title = "System Temp Files",
                    Subtitle = "C:\\Windows\\Temp - Stale system temp files and logs",
                    PathOrLocation = Path.Combine(winDir, "Temp"),
                    CategoryGroup = "System",
                    IconKey = "ServerIcon"
                },
                new TempCleanCategory
                {
                    Id = "win_update",
                    Title = "Windows Update Cache",
                    Subtitle = "SoftwareDistribution\\Download - Old downloaded update installers",
                    PathOrLocation = Path.Combine(winDir, "SoftwareDistribution", "Download"),
                    CategoryGroup = "System",
                    IconKey = "DownloadIcon"
                },
                new TempCleanCategory
                {
                    Id = "windows_old",
                    Title = "Windows.old Upgrade Archive",
                    Subtitle = "C:\\Windows.old - Previous Windows installations from major upgrades",
                    PathOrLocation = @"C:\Windows.old",
                    CategoryGroup = "System",
                    IconKey = "ServerIcon"
                },
                new TempCleanCategory
                {
                    Id = "driver_installers",
                    Title = "Driver Installer Repositories",
                    Subtitle = "NVIDIA / AMD extracted installer archives (C:\\NVIDIA, C:\\AMD, Downloader)",
                    PathOrLocation = @"C:\ProgramData\NVIDIA Corporation\Downloader",
                    AdditionalPaths = new List<string> { @"C:\NVIDIA", @"C:\AMD" },
                    CategoryGroup = "System",
                    IconKey = "CpuIcon"
                },
                new TempCleanCategory
                {
                    Id = "crash_dumps",
                    Title = "Crash Dumps & Error Logs",
                    Subtitle = "%LocalAppData%\\CrashDumps - Memory dumps from application crashes",
                    PathOrLocation = Path.Combine(localAppData, "CrashDumps"),
                    CategoryGroup = "System",
                    IconKey = "BugIcon"
                },
                new TempCleanCategory
                {
                    Id = "recycle_bin",
                    Title = "Windows Recycle Bin",
                    Subtitle = "Deleted files still occupying drive storage across all partitions",
                    PathOrLocation = "RECYCLE_BIN",
                    CategoryGroup = "System",
                    IconKey = "TrashIcon"
                },
                new TempCleanCategory
                {
                    Id = "thumbnail_cache",
                    Title = "Thumbnail Cache",
                    Subtitle = "Explorer image and video thumbnail database files",
                    PathOrLocation = Path.Combine(localAppData, @"Microsoft\Windows\Explorer"),
                    CategoryGroup = "System",
                    IconKey = "ImageIcon"
                },

                // 2. GAMER BLOAT & SHADERS (Approved Idea 1)
                new TempCleanCategory
                {
                    Id = "gpu_shader_cache",
                    Title = "DirectX & GPU Shader Cache",
                    Subtitle = "DirectX D3DSCache, NVIDIA DXCache/GLCache, AMD DxCache pipeline data",
                    PathOrLocation = Path.Combine(localAppData, "D3DSCache"),
                    AdditionalPaths = new List<string>
                    {
                        Path.Combine(localAppData, @"NVIDIA\DXCache"),
                        Path.Combine(localAppData, @"NVIDIA\GLCache"),
                        Path.Combine(localAppData, @"AMD\DxCache")
                    },
                    CategoryGroup = "Gaming",
                    IconKey = "ZapIcon"
                },
                new TempCleanCategory
                {
                    Id = "game_engine_cache",
                    Title = "Game Engine Shader Pipelines",
                    Subtitle = "Unreal Engine and Unity cached compiled shaders and derived data",
                    PathOrLocation = Path.Combine(localAppData, "UnrealEngineCommon"),
                    AdditionalPaths = new List<string>
                    {
                        Path.Combine(localAppData, @"Unity\ShaderCache")
                    },
                    CategoryGroup = "Gaming",
                    IconKey = "GamepadIcon"
                },
                new TempCleanCategory
                {
                    Id = "steam_redist",
                    Title = "Steam Downloading & Redist Cache",
                    Subtitle = "Steam pending download fragments and duplicate DirectX/VC++ installers",
                    PathOrLocation = @"C:\Program Files (x86)\Steam\steamapps\downloading",
                    AdditionalPaths = new List<string>
                    {
                        @"C:\Program Files (x86)\Steam\steamapps\temp"
                    },
                    CategoryGroup = "Gaming",
                    IconKey = "GamepadIcon"
                },

                // 3. PACKAGE MANAGERS & DEVELOPER (Approved Idea 6)
                new TempCleanCategory
                {
                    Id = "pkg_manager_cache",
                    Title = "Package Manager Download Caches",
                    Subtitle = "npm, pip, cargo, nuget, and winget global package archives",
                    PathOrLocation = Path.Combine(localAppData, "npm-cache"),
                    AdditionalPaths = new List<string>
                    {
                        Path.Combine(localAppData, @"pip\cache"),
                        Path.Combine(userProfile, @".cargo\cache"),
                        Path.Combine(userProfile, @".nuget\packages"),
                        Path.Combine(localAppData, @"Packages\Microsoft.DesktopAppInstaller_8wekyb3d8bbwe\LocalState")
                    },
                    CategoryGroup = "Developer",
                    IconKey = "TerminalIcon"
                },

                // 4. BROWSER & MEDIA TARGETS (Deep Browser Profile Hygiene - Idea 5)
                new TempCleanCategory
                {
                    Id = "discord_cache",
                    Title = "Discord Media & App Cache",
                    Subtitle = "AppData\\Roaming\\discord - Cached memes, attachments, GPUCache & V8 data",
                    PathOrLocation = Path.Combine(appData, @"discord\Cache\Cache_Data"),
                    AdditionalPaths = new List<string>
                    {
                        Path.Combine(appData, @"discord\Code Cache\js"),
                        Path.Combine(appData, @"discord\GPUCache"),
                        Path.Combine(appData, @"discord\DawnCache")
                    },
                    CategoryGroup = "Media",
                    IconKey = "MessageIcon"
                },
                new TempCleanCategory
                {
                    Id = "chrome_cache",
                    Title = "Chrome HTTP & Media Cache",
                    Subtitle = "%LocalAppData%\\Google\\Chrome - Temporary web cache & network storage",
                    PathOrLocation = Path.Combine(localAppData, @"Google\Chrome\User Data\Default\Cache\Cache_Data"),
                    AdditionalPaths = new List<string>
                    {
                        Path.Combine(localAppData, @"Google\Chrome\User Data\Default\Cache")
                    },
                    CategoryGroup = "Media",
                    IconKey = "GlobeIcon"
                },
                new TempCleanCategory
                {
                    Id = "chrome_code_cache",
                    Title = "Chrome V8 Code & Shader Cache",
                    Subtitle = "Compiled JavaScript bytecode, WebAssembly, and GPU hardware shader caches",
                    PathOrLocation = Path.Combine(localAppData, @"Google\Chrome\User Data\Default\Code Cache\js"),
                    AdditionalPaths = new List<string>
                    {
                        Path.Combine(localAppData, @"Google\Chrome\User Data\Default\Code Cache\wasm"),
                        Path.Combine(localAppData, @"Google\Chrome\User Data\Default\GPUCache"),
                        Path.Combine(localAppData, @"Google\Chrome\User Data\ShaderCache"),
                        Path.Combine(localAppData, @"Google\Chrome\User Data\GrShaderCache")
                    },
                    CategoryGroup = "Media",
                    IconKey = "ZapIcon"
                },
                new TempCleanCategory
                {
                    Id = "edge_cache",
                    Title = "Microsoft Edge Media Cache",
                    Subtitle = "%LocalAppData%\\Microsoft\\Edge - Temporary web and streaming cache",
                    PathOrLocation = Path.Combine(localAppData, @"Microsoft\Edge\User Data\Default\Cache\Cache_Data"),
                    AdditionalPaths = new List<string>
                    {
                        Path.Combine(localAppData, @"Microsoft\Edge\User Data\Default\Cache")
                    },
                    CategoryGroup = "Media",
                    IconKey = "GlobeIcon"
                },
                new TempCleanCategory
                {
                    Id = "edge_code_cache",
                    Title = "Edge V8 Code & Shader Cache",
                    Subtitle = "Compiled JS bytecode and GPU render pipeline caches (logins untouched)",
                    PathOrLocation = Path.Combine(localAppData, @"Microsoft\Edge\User Data\Default\Code Cache\js"),
                    AdditionalPaths = new List<string>
                    {
                        Path.Combine(localAppData, @"Microsoft\Edge\User Data\Default\Code Cache\wasm"),
                        Path.Combine(localAppData, @"Microsoft\Edge\User Data\Default\GPUCache"),
                        Path.Combine(localAppData, @"Microsoft\Edge\User Data\ShaderCache")
                    },
                    CategoryGroup = "Media",
                    IconKey = "ZapIcon"
                },
                new TempCleanCategory
                {
                    Id = "browser_service_workers",
                    Title = "Web App & ServiceWorker Cache",
                    Subtitle = "Chrome & Edge offline CacheStorage from PWAs (logins & cookies untouched)",
                    PathOrLocation = Path.Combine(localAppData, @"Google\Chrome\User Data\Default\Service Worker\CacheStorage"),
                    AdditionalPaths = new List<string>
                    {
                        Path.Combine(localAppData, @"Microsoft\Edge\User Data\Default\Service Worker\CacheStorage"),
                        Path.Combine(localAppData, @"Google\Chrome\User Data\Default\Service Worker\ScriptCache"),
                        Path.Combine(localAppData, @"Microsoft\Edge\User Data\Default\Service Worker\ScriptCache")
                    },
                    CategoryGroup = "Media",
                    IconKey = "GlobeIcon"
                },
                new TempCleanCategory
                {
                    Id = "brave_cache",
                    Title = "Brave Browser Cache",
                    Subtitle = "%LocalAppData%\\BraveSoftware - Temporary browsing and streaming cache",
                    PathOrLocation = Path.Combine(localAppData, @"BraveSoftware\Brave-Browser\User Data\Default\Cache\Cache_Data"),
                    AdditionalPaths = new List<string>
                    {
                        Path.Combine(localAppData, @"BraveSoftware\Brave-Browser\User Data\Default\Code Cache\js"),
                        Path.Combine(localAppData, @"BraveSoftware\Brave-Browser\User Data\Default\GPUCache")
                    },
                    CategoryGroup = "Media",
                    IconKey = "GlobeIcon"
                }
            };
        }

        private static List<string> CollectAllPaths(TempCleanCategory cat)
        {
            var paths = new List<string>();
            if (!string.IsNullOrEmpty(cat.PathOrLocation) && cat.PathOrLocation != "RECYCLE_BIN")
            {
                paths.Add(cat.PathOrLocation);
            }
            if (cat.AdditionalPaths != null)
            {
                foreach (var p in cat.AdditionalPaths)
                {
                    if (!string.IsNullOrEmpty(p) && !paths.Contains(p))
                    {
                        paths.Add(p);
                    }
                }
            }
            return paths;
        }

        public static async Task AnalyzeCategoryAsync(TempCleanCategory cat, CancellationToken ct = default)
        {
            cat.IsScanning = true;
            cat.Status = "Analyzing...";

            await Task.Run(() =>
            {
                try
                {
                    if (cat.Id == "recycle_bin")
                    {
                        var info = new SHQUERYRBINFO { cbSize = Marshal.SizeOf(typeof(SHQUERYRBINFO)) };
                        int hr = SHQueryRecycleBin(null, ref info);
                        if (hr == 0)
                        {
                            cat.SizeBytes = info.i64Size;
                            cat.FileCount = (int)Math.Min(info.i64NumItems, int.MaxValue);
                            cat.Status = $"{cat.FileCount} items in Recycle Bin";
                        }
                        else
                        {
                            cat.SizeBytes = 0;
                            cat.FileCount = 0;
                            cat.Status = "Empty";
                        }
                        return;
                    }

                    var allPaths = CollectAllPaths(cat);
                    long totalBytes = 0;
                    int count = 0;
                    int existingPaths = 0;

                    foreach (var path in allPaths)
                    {
                        if (ct.IsCancellationRequested) break;
                        if (!Directory.Exists(path)) continue;

                        existingPaths++;
                        try
                        {
                            var dirInfo = new DirectoryInfo(path);
                            foreach (var file in dirInfo.EnumerateFiles("*", new EnumerationOptions
                            {
                                IgnoreInaccessible = true,
                                RecurseSubdirectories = true
                            }))
                            {
                                if (ct.IsCancellationRequested) break;
                                try
                                {
                                    totalBytes += file.Length;
                                    count++;
                                }
                                catch { }
                            }
                        }
                        catch { }
                    }

                    cat.SizeBytes = totalBytes;
                    cat.FileCount = count;
                    if (existingPaths == 0)
                    {
                        cat.Status = "Clean (Location not present)";
                    }
                    else
                    {
                        cat.Status = count > 0 ? $"{count} items found" : "Clean";
                    }
                }
                catch (Exception ex)
                {
                    cat.Status = $"Unavailable: {ex.Message}";
                }
                finally
                {
                    cat.IsScanning = false;
                }
            }, ct);
        }

        public static async Task<(long bytesCleaned, int filesCleaned, int skipped)> CleanCategoryAsync(
            TempCleanCategory cat, bool forceUnlock = false, CancellationToken ct = default)
        {
            cat.IsCleaning = true;
            cat.Status = "Cleaning...";

            long freedBytes = 0;
            int cleanedCount = 0;
            int skippedCount = 0;

            await Task.Run(() =>
            {
                try
                {
                    if (cat.Id == "recycle_bin")
                    {
                        var info = new SHQUERYRBINFO { cbSize = Marshal.SizeOf(typeof(SHQUERYRBINFO)) };
                        if (SHQueryRecycleBin(null, ref info) == 0)
                        {
                            freedBytes = info.i64Size;
                            cleanedCount = (int)info.i64NumItems;
                        }

                        SHEmptyRecycleBin(IntPtr.Zero, null, SHERB_NOCONFIRMATION | SHERB_NOPROGRESSUI | SHERB_NOSOUND);
                        cat.SizeBytes = 0;
                        cat.FileCount = 0;
                        cat.Status = "Emptied";
                        return;
                    }

                    var allPaths = CollectAllPaths(cat);

                    foreach (var path in allPaths)
                    {
                        if (ct.IsCancellationRequested) break;
                        if (!Directory.Exists(path)) continue;

                        try
                        {
                            var dirInfo = new DirectoryInfo(path);

                            // Delete files
                            foreach (var file in dirInfo.EnumerateFiles("*", new EnumerationOptions
                            {
                                IgnoreInaccessible = true,
                                RecurseSubdirectories = true
                            }))
                            {
                                if (ct.IsCancellationRequested) break;

                                try
                                {
                                    long len = file.Length;
                                    file.Attributes = FileAttributes.Normal;
                                    file.Delete();
                                    freedBytes += len;
                                    cleanedCount++;
                                }
                                catch (IOException)
                                {
                                    if (forceUnlock)
                                    {
                                        if (ProcessLockService.UnlockAndKillLockingProcesses(file.FullName))
                                        {
                                            try
                                            {
                                                long len = file.Length;
                                                file.Delete();
                                                freedBytes += len;
                                                cleanedCount++;
                                                continue;
                                            }
                                            catch { }
                                        }
                                    }
                                    skippedCount++;
                                }
                                catch
                                {
                                    skippedCount++;
                                }
                            }

                            // Delete empty subdirectories where safe
                            foreach (var subDir in dirInfo.EnumerateDirectories("*", new EnumerationOptions
                            {
                                IgnoreInaccessible = true,
                                RecurseSubdirectories = true
                            }))
                            {
                                if (ct.IsCancellationRequested) break;
                                try
                                {
                                    if (subDir.GetFileSystemInfos().Length == 0)
                                    {
                                        subDir.Delete(false);
                                    }
                                }
                                catch { }
                            }
                        }
                        catch { }
                    }

                    cat.SizeBytes = Math.Max(0, cat.SizeBytes - freedBytes);
                    cat.FileCount = Math.Max(0, cat.FileCount - cleanedCount);
                    cat.Status = skippedCount > 0 
                        ? $"Cleaned ({skippedCount} in-use files safely skipped)" 
                        : "Cleaned successfully";
                }
                catch (Exception ex)
                {
                    cat.Status = $"Error: {ex.Message}";
                }
                finally
                {
                    cat.IsCleaning = false;
                }
            }, ct);

            return (freedBytes, cleanedCount, skippedCount);
        }
    }
}
