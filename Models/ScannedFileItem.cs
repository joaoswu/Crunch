using System;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Crunch.Models
{
    public enum FileCategory
    {
        SleepingGiant,     // Large files untouched for > 6 months
        InstallerOrImage,  // .exe, .msi, .iso, .vhd
        MassiveMedia,      // .mp4, .mkv, .mov, etc. > 500MB
        ColdArchive,       // .zip, .rar, .7z untouched
        DeveloperBloat,    // node_modules, target, .venv
        DuplicateGiant,    // Exact duplicate large file
        GeneralLarge       // Any file > 100MB
    }

    public partial class ScannedFileItem : ObservableObject
    {
        public string FullPath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string Extension { get; set; } = string.Empty;
        public string DirectoryName { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
        public DateTime LastModified { get; set; }
        public DateTime LastAccessed { get; set; }
        public FileCategory Category { get; set; }

        [ObservableProperty]
        private bool _isSelected = true;

        [ObservableProperty]
        private string? _lockingProcess;

        public string FormattedSize => FormatSize(SizeBytes);
        public string FormattedLastModified => LastModified.ToString("yyyy-MM-dd HH:mm");
        public string FormattedLastAccessed => LastAccessed.ToString("yyyy-MM-dd");

        public int AgeInDays => (int)(DateTime.Now - LastModified).TotalDays;

        public static string FormatSize(long bytes)
        {
            if (bytes >= 1024L * 1024L * 1024L)
                return $"{(double)bytes / (1024 * 1024 * 1024):0.00} GB";
            if (bytes >= 1024L * 1024L)
                return $"{(double)bytes / (1024 * 1024):0.0} MB";
            if (bytes >= 1024L)
                return $"{(double)bytes / 1024:0} KB";
            return $"{bytes} B";
        }
    }
}
