using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Hashing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Crunch.Models;

namespace Crunch.Services
{
    public class DuplicateScannerService
    {
        public static async Task<List<DuplicateGroup>> FindDuplicatesAsync(
            IEnumerable<ScannedFileItem> files,
            IProgress<string>? progress = null,
            CancellationToken ct = default)
        {
            var duplicateGroups = new List<DuplicateGroup>();

            await Task.Run(() =>
            {
                // Group by file size first to quickly isolate candidates
                var sizeGroups = files
                    .GroupBy(f => f.SizeBytes)
                    .Where(g => g.Count() > 1 && g.Key > 5 * 1024 * 1024) // Files > 5MB
                    .ToList();

                int totalChecks = sizeGroups.Count;
                int current = 0;

                foreach (var sGroup in sizeGroups)
                {
                    if (ct.IsCancellationRequested) break;
                    current++;
                    progress?.Report($"Comparing candidate group {current} of {totalChecks}...");

                    // Compare head and tail hashes before computing full file hashes
                    var partialHashGroups = new Dictionary<string, List<ScannedFileItem>>();

                    foreach (var file in sGroup)
                    {
                        if (ct.IsCancellationRequested) break;
                        string? partialHash = ComputeHeaderFooterHash(file.FullPath);
                        if (partialHash == null) continue;

                        if (!partialHashGroups.TryGetValue(partialHash, out var list))
                        {
                            list = new List<ScannedFileItem>();
                            partialHashGroups[partialHash] = list;
                        }
                        list.Add(file);
                    }

                    // For matching partial hashes, compute full stream hash
                    foreach (var pGroup in partialHashGroups.Values.Where(g => g.Count > 1))
                    {
                        var fullHashGroups = new Dictionary<string, List<ScannedFileItem>>();

                        foreach (var file in pGroup)
                        {
                            if (ct.IsCancellationRequested) break;
                            string? fullHash = ComputeFullXxHash(file.FullPath, ct);
                            if (fullHash == null) continue;

                            if (!fullHashGroups.TryGetValue(fullHash, out var list))
                            {
                                list = new List<ScannedFileItem>();
                                fullHashGroups[fullHash] = list;
                            }
                            list.Add(file);
                        }

                        foreach (var fGroup in fullHashGroups)
                        {
                            if (fGroup.Value.Count > 1)
                            {
                                // Keep the first as original (unselected), mark duplicates for cleanup
                                for (int i = 0; i < fGroup.Value.Count; i++)
                                {
                                    fGroup.Value[i].IsSelected = (i > 0);
                                    fGroup.Value[i].Category = FileCategory.DuplicateGiant;
                                }

                                duplicateGroups.Add(new DuplicateGroup
                                {
                                    Hash = fGroup.Key,
                                    SizeBytes = fGroup.Value[0].SizeBytes,
                                    Duplicates = fGroup.Value
                                });
                            }
                        }
                    }
                }
            }, ct);

            return duplicateGroups.OrderByDescending(g => g.SizeBytes * (g.Duplicates.Count - 1)).ToList();
        }

        /// <summary>
        /// Reads the first 64KB and last 64KB to calculate a fast preliminary hash.
        /// </summary>
        private static string? ComputeHeaderFooterHash(string path)
        {
            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 65536, FileOptions.SequentialScan);
                long fileLength = stream.Length;
                var hasher = new XxHash64();

                byte[] buffer = new byte[65536];
                int readHead = stream.Read(buffer, 0, buffer.Length);
                if (readHead <= 0) return null;
                hasher.Append(new ReadOnlySpan<byte>(buffer, 0, readHead));

                // If file is > 128KB, read the tail as well
                if (fileLength > 131072)
                {
                    stream.Seek(-65536, SeekOrigin.End);
                    int readTail = stream.Read(buffer, 0, buffer.Length);
                    if (readTail > 0)
                    {
                        hasher.Append(new ReadOnlySpan<byte>(buffer, 0, readTail));
                    }
                }

                return Convert.ToHexString(hasher.GetCurrentHash());
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Computes a full file hash using XxHash64.
        /// </summary>
        private static string? ComputeFullXxHash(string path, CancellationToken ct)
        {
            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 131072, FileOptions.SequentialScan);
                var hasher = new XxHash64();
                byte[] buffer = new byte[131072]; // 128KB chunks
                int read;

                while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    if (ct.IsCancellationRequested) return null;
                    hasher.Append(new ReadOnlySpan<byte>(buffer, 0, read));
                }

                return Convert.ToHexString(hasher.GetCurrentHash());
            }
            catch
            {
                return null;
            }
        }
    }
}
