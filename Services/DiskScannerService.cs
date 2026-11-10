using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Crunch.Models;

namespace Crunch.Services
{
    /// <summary>
    /// Scans directories for large and dormant files across multiple worker threads.
    /// </summary>
    public class DiskScannerService
    {
        private static readonly HashSet<string> InstallerExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".iso", ".exe", ".msi", ".vhd", ".vhdx", ".img", ".cab", ".msix", ".appx"
        };

        private static readonly HashSet<string> MediaExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".mp4", ".mkv", ".mov", ".avi", ".webm", ".flv", ".ts", ".wmv", ".m4v"
        };

        private static readonly HashSet<string> ArchiveExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".zip", ".rar", ".7z", ".tar", ".gz", ".bz2", ".xz", ".tgz"
        };

        private static readonly HashSet<string> ExcludedFolderNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "Windows", "$Recycle.Bin", "System Volume Information", "Recovery", 
            "AppData", "ProgramData", "Microsoft", "WindowsApps"
        };

        public class ScanProgressEventArgs
        {
            public long ScannedFileCount { get; set; }
            public long FoundItemCount { get; set; }
            public string CurrentDirectory { get; set; } = string.Empty;
            public long TotalBytesFound { get; set; }
        }

        public static async Task<List<ScannedFileItem>> ScanPathAsync(
            string rootPath,
            long minSizeBytes = 100 * 1024 * 1024, // 100MB default
            IProgress<ScanProgressEventArgs>? progress = null,
            CancellationToken ct = default)
        {
            var results = new ConcurrentBag<ScannedFileItem>();
            long scannedFiles = 0;
            long totalBytesFound = 0;
            string latestDir = rootPath;

            var dirQueue = new ConcurrentQueue<string>();
            dirQueue.Enqueue(rootPath);

            var options = new EnumerationOptions
            {
                IgnoreInaccessible = true,
                RecurseSubdirectories = false,
                ReturnSpecialDirectories = false,
                AttributesToSkip = FileAttributes.ReparsePoint
            };

            int workerCount = Math.Clamp(Environment.ProcessorCount, 4, 12);
            int activeWorkers = 0;
            long lastReportTick = Environment.TickCount64;

            void TryReportProgress(string currentDir)
            {
                long now = Environment.TickCount64;
                if (now - Interlocked.Read(ref lastReportTick) > 100)
                {
                    Interlocked.Exchange(ref lastReportTick, now);
                    progress?.Report(new ScanProgressEventArgs
                    {
                        ScannedFileCount = Interlocked.Read(ref scannedFiles),
                        FoundItemCount = results.Count,
                        CurrentDirectory = currentDir,
                        TotalBytesFound = Interlocked.Read(ref totalBytesFound)
                    });
                }
            }

            var workerTasks = new Task[workerCount];

            for (int w = 0; w < workerCount; w++)
            {
                workerTasks[w] = Task.Run(async () =>
                {
                    while (!ct.IsCancellationRequested)
                    {
                        if (dirQueue.TryDequeue(out var currentDir))
                        {
                            Interlocked.Increment(ref activeWorkers);
                            latestDir = currentDir;
                            TryReportProgress(currentDir);

                            try
                            {
                                // Enumerate files directly to filter by size before allocating items
                                var fileEnum = new FileSystemEnumerable<ScannedFileItem?>(
                                    currentDir,
                                    (ref FileSystemEntry entry) =>
                                    {
                                        if (!entry.IsDirectory)
                                        {
                                            Interlocked.Increment(ref scannedFiles);
                                            long length = entry.Length;
                                            if (length >= minSizeBytes)
                                            {
                                                Interlocked.Add(ref totalBytesFound, length);
                                                return CategorizeFile(ref entry, length);
                                            }
                                        }
                                        return null;
                                    },
                                    options);

                                foreach (var item in fileEnum)
                                {
                                    if (ct.IsCancellationRequested) break;
                                    if (item != null)
                                    {
                                        results.Add(item);
                                    }
                                }

                                // 2. Subdirectory Discovery
                                var subDirEnum = new FileSystemEnumerable<string?>(
                                    currentDir,
                                    (ref FileSystemEntry entry) =>
                                    {
                                        if (entry.IsDirectory && !entry.Attributes.HasFlag(FileAttributes.ReparsePoint))
                                        {
                                            string name = entry.FileName.ToString();
                                            if (!ExcludedFolderNames.Contains(name))
                                            {
                                                return entry.ToFullPath();
                                            }
                                        }
                                        return null;
                                    },
                                    options);

                                foreach (var subDir in subDirEnum)
                                {
                                    if (ct.IsCancellationRequested) break;
                                    if (!string.IsNullOrEmpty(subDir))
                                    {
                                        dirQueue.Enqueue(subDir);
                                    }
                                }
                            }
                            catch
                            {
                                // Access denied / path not found - safe skip in surgical mode
                            }
                            finally
                            {
                                Interlocked.Decrement(ref activeWorkers);
                            }
                        }
                        else
                        {
                            // Queue is empty. If all workers are idle, scan is complete.
                            if (Volatile.Read(ref activeWorkers) == 0 && dirQueue.IsEmpty)
                            {
                                break;
                            }

                            // Brief back-off to let busy workers push new discovered subdirectories
                            try
                            {
                                await Task.Delay(3, ct).ConfigureAwait(false);
                            }
                            catch (OperationCanceledException)
                            {
                                break;
                            }

                            if (Volatile.Read(ref activeWorkers) == 0 && dirQueue.IsEmpty)
                            {
                                break;
                            }
                        }
                    }
                }, ct);
            }

            await Task.WhenAll(workerTasks);

            // Final report
            progress?.Report(new ScanProgressEventArgs
            {
                ScannedFileCount = Interlocked.Read(ref scannedFiles),
                FoundItemCount = results.Count,
                CurrentDirectory = "Scan complete",
                TotalBytesFound = Interlocked.Read(ref totalBytesFound)
            });

            return results.OrderByDescending(x => x.SizeBytes).ToList();
        }

        private static ScannedFileItem CategorizeFile(ref FileSystemEntry entry, long size)
        {
            string fullPath = entry.ToFullPath();
            string fileName = entry.FileName.ToString();
            string ext = Path.GetExtension(fileName);
            var now = DateTime.Now;
            var lastMod = entry.LastWriteTimeUtc.LocalDateTime;
            var lastAcc = entry.LastAccessTimeUtc.LocalDateTime;

            var mostRecentUse = lastMod > lastAcc ? lastMod : lastAcc;
            bool isDormant = (now - mostRecentUse).TotalDays >= 180; // 6 months

            FileCategory cat;

            if (InstallerExtensions.Contains(ext))
            {
                cat = FileCategory.InstallerOrImage;
            }
            else if (MediaExtensions.Contains(ext) && size >= 500 * 1024 * 1024)
            {
                cat = FileCategory.MassiveMedia;
            }
            else if (ArchiveExtensions.Contains(ext))
            {
                cat = FileCategory.ColdArchive;
            }
            else if (isDormant && size >= 250 * 1024 * 1024)
            {
                cat = FileCategory.SleepingGiant;
            }
            else
            {
                cat = FileCategory.GeneralLarge;
            }

            return new ScannedFileItem
            {
                FullPath = fullPath,
                FileName = fileName,
                Extension = ext,
                DirectoryName = entry.Directory.ToString(),
                SizeBytes = size,
                LastModified = lastMod,
                LastAccessed = lastAcc,
                Category = cat
            };
        }
    }
}
