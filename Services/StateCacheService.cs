using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Crunch.Models;

namespace Crunch.Services
{
    public class DiskStateSnapshot
    {
        public DateTime SavedAt { get; set; } = DateTime.UtcNow;
        public string SystemDriveLetter { get; set; } = "C:\\";
        public long TotalDriveBytes { get; set; }
        public long FreeDriveBytes { get; set; }
        public long UsedDriveBytes { get; set; }
        public List<ScannedFileItem> CachedLargeFiles { get; set; } = new();
        public List<DevBloatItem> CachedDevBloat { get; set; } = new();
        public List<OrphanAppItem> CachedOrphans { get; set; } = new();
        public long TotalReclaimableBytes { get; set; }
    }

    /// <summary>
    /// Optimization 7: Instant State & Incremental Disk Cache.
    /// Serializes and deserializes the latest disk scan findings to %LocalAppData%\Crunch\cache.json
    /// to guarantee instant 0ms startup without waiting for a re-scan.
    /// </summary>
    public static class StateCacheService
    {
        private static readonly string CacheDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
            "Crunch");

        private static readonly string CacheFilePath = Path.Combine(CacheDirectory, "cache.json");

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        public static async Task SaveSnapshotAsync(DiskStateSnapshot snapshot)
        {
            try
            {
                if (!Directory.Exists(CacheDirectory))
                {
                    Directory.CreateDirectory(CacheDirectory);
                }

                string json = JsonSerializer.Serialize(snapshot, JsonOpts);
                await File.WriteAllTextAsync(CacheFilePath, json);
            }
            catch
            {
                // Non-critical cache failure
            }
        }

        public static async Task<DiskStateSnapshot?> LoadSnapshotAsync()
        {
            try
            {
                if (!File.Exists(CacheFilePath))
                {
                    return null;
                }

                string json = await File.ReadAllTextAsync(CacheFilePath);
                var snapshot = JsonSerializer.Deserialize<DiskStateSnapshot>(json, JsonOpts);
                return snapshot;
            }
            catch
            {
                return null;
            }
        }
    }
}
