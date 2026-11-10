using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Crunch.Models;

namespace Crunch.Services
{
    public class DevBloatService
    {
        private static readonly Dictionary<string, string> TargetFolderSignatures = new(StringComparer.OrdinalIgnoreCase)
        {
            { "node_modules", "Node.js / npm packages" },
            { "target", "Rust Cargo build cache" },
            { ".venv", "Python virtual environment" },
            { "venv", "Python virtual environment" },
            { "bin", ".NET compilation binary output" },
            { "obj", ".NET intermediate build cache" },
            { ".gradle", "Gradle build cache" }
        };

        public static async Task<List<DevBloatItem>> ScanDevBloatAsync(
            string rootPath,
            IProgress<string>? progress = null,
            CancellationToken ct = default)
        {
            var results = new ConcurrentBag<DevBloatItem>();

            await Task.Run(() =>
            {
                var dirQueue = new Queue<string>();
                dirQueue.Enqueue(rootPath);

                var options = new EnumerationOptions
                {
                    IgnoreInaccessible = true,
                    RecurseSubdirectories = false
                };

                while (dirQueue.Count > 0)
                {
                    if (ct.IsCancellationRequested) break;
                    string currentDir = dirQueue.Dequeue();

                    DirectoryInfo dirInfo;
                    try
                    {
                        dirInfo = new DirectoryInfo(currentDir);
                        if (!dirInfo.Exists) continue;

                        string dirName = dirInfo.Name;

                        // Check if this directory itself is a bloat target
                        if (TargetFolderSignatures.TryGetValue(dirName, out var bloatType))
                        {
                            // Avoid flagging system or root bin folders
                            if (dirName.Equals("bin", StringComparison.OrdinalIgnoreCase) && 
                                !File.Exists(Path.Combine(dirInfo.Parent?.FullName ?? "", $"{dirInfo.Parent?.Name}.csproj")) &&
                                !File.Exists(Path.Combine(dirInfo.Parent?.FullName ?? "", $"{dirInfo.Parent?.Name}.sln")))
                            {
                                // Skip non-.NET bin folders
                            }
                            else
                            {
                                progress?.Report($"Analyzing bloat folder: {dirInfo.FullName}");
                                var (size, count) = CalculateFolderSize(dirInfo, ct);

                                if (size > 10 * 1024 * 1024) // 10MB minimum threshold
                                {
                                    results.Add(new DevBloatItem
                                    {
                                        FullPath = dirInfo.FullName,
                                        FolderName = dirName,
                                        ProjectName = dirInfo.Parent?.Name ?? "Unknown Project",
                                        BloatType = bloatType,
                                        SizeBytes = size,
                                        FileCount = count,
                                        LastModified = dirInfo.LastWriteTime
                                    });
                                }

                                // Do not recurse into node_modules or target subdirectories
                                continue;
                            }
                        }

                        // Don't recurse into Windows, AppData, etc.
                        if (dirName.Equals("Windows", StringComparison.OrdinalIgnoreCase) ||
                            dirName.Equals("AppData", StringComparison.OrdinalIgnoreCase) ||
                            dirName.Equals("$Recycle.Bin", StringComparison.OrdinalIgnoreCase) ||
                            dirName.StartsWith("."))
                        {
                            // Still allow checking project folders
                            if (!dirName.Equals(".venv", StringComparison.OrdinalIgnoreCase))
                                continue;
                        }
                    }
                    catch
                    {
                        continue;
                    }

                    // Enqueue children
                    try
                    {
                        foreach (var sub in dirInfo.EnumerateDirectories("*", options))
                        {
                            if (ct.IsCancellationRequested) break;
                            if (!sub.Attributes.HasFlag(FileAttributes.ReparsePoint))
                            {
                                dirQueue.Enqueue(sub.FullName);
                            }
                        }
                    }
                    catch { }
                }
            }, ct);

            return results.OrderByDescending(x => x.SizeBytes).ToList();
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

        public static async Task<bool> DeleteDevBloatFolderAsync(string folderPath, bool sendToRecycleBin = false)
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (!Directory.Exists(folderPath)) return true;

                    if (sendToRecycleBin)
                    {
                        Microsoft.VisualBasic.FileIO.FileSystem.DeleteDirectory(
                            folderPath,
                            Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                            Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                    }
                    else
                    {
                        Directory.Delete(folderPath, true);
                    }
                    return true;
                }
                catch (IOException)
                {
                    // Locked by process, attempt unlock
                    if (ProcessLockService.UnlockAndKillLockingProcesses(folderPath))
                    {
                        try
                        {
                            Directory.Delete(folderPath, true);
                            return true;
                        }
                        catch { }
                    }
                    return false;
                }
                catch
                {
                    return false;
                }
            });
        }
    }
}
