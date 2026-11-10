using CommunityToolkit.Mvvm.ComponentModel;

namespace Crunch.Models
{
    public partial class DriveSpaceInfo : ObservableObject
    {
        public string Name { get; set; } = string.Empty;
        public string VolumeLabel { get; set; } = string.Empty;
        public string DriveFormat { get; set; } = string.Empty;
        public long TotalBytes { get; set; }
        public long FreeBytes { get; set; }
        public long UsedBytes => TotalBytes - FreeBytes;

        public double UsedPercentage
        {
            get => TotalBytes > 0 ? ((double)UsedBytes / TotalBytes) * 100 : 0;
            set { }
        }

        public double FreePercentage
        {
            get => TotalBytes > 0 ? ((double)FreeBytes / TotalBytes) * 100 : 0;
            set { }
        }

        public string FormattedTotal => ScannedFileItem.FormatSize(TotalBytes);
        public string FormattedFree => ScannedFileItem.FormatSize(FreeBytes);
        public string FormattedUsed => ScannedFileItem.FormatSize(UsedBytes);

        public string DisplayName => string.IsNullOrEmpty(VolumeLabel) ? Name : $"{VolumeLabel} ({Name})";
    }

    public partial class TempCleanCategory : ObservableObject
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Subtitle { get; set; } = string.Empty;
        public string PathOrLocation { get; set; } = string.Empty;
        public List<string> AdditionalPaths { get; set; } = new();
        public string CategoryGroup { get; set; } = "System"; // "System", "Gaming", "Developer", "Media"
        public string IconKey { get; set; } = "TrashIcon";

        [ObservableProperty]
        private bool _isSelected = false;

        partial void OnIsSelectedChanged(bool value)
        {
            SelectionChangedCallback?.Invoke();
        }

        public Action? SelectionChangedCallback { get; set; }

        [ObservableProperty]
        private bool _isScanning;

        [ObservableProperty]
        private bool _isCleaning;

        [ObservableProperty]
        private bool _isVisibleInFilter = true;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FormattedSize))]
        private long _sizeBytes;

        partial void OnSizeBytesChanged(long value)
        {
            SelectionChangedCallback?.Invoke();
        }

        [ObservableProperty]
        private int _fileCount;

        [ObservableProperty]
        private string _status = "Ready to scan";

        public string FormattedSize => ScannedFileItem.FormatSize(SizeBytes);
    }

    public partial class OrphanAppItem : ObservableObject
    {
        public string Name { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public string LocationType { get; set; } = string.Empty; // "LocalAppData", "AppData", "ProgramData"
        public string Details { get; set; } = "No registered uninstaller found in Windows";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FormattedSize))]
        private long _sizeBytes;

        [ObservableProperty]
        private int _fileCount;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FormattedLastModified))]
        private DateTime _lastModified;

        [ObservableProperty]
        private bool _isSelected = true;

        [ObservableProperty]
        private string _status = "Orphaned App Remnant";

        public string FormattedSize => ScannedFileItem.FormatSize(SizeBytes);
        public string FormattedLastModified => LastModified.ToString("yyyy-MM-dd");
    }

    public partial class DevBloatItem : ObservableObject
    {
        public string FullPath { get; set; } = string.Empty;
        public string FolderName { get; set; } = string.Empty;
        public string ProjectName { get; set; } = string.Empty;
        public string BloatType { get; set; } = string.Empty; // "Node.js (node_modules)", "Rust (target)", "Python (.venv)", ".NET (bin/obj)"

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FormattedSize))]
        private long _sizeBytes;

        [ObservableProperty]
        private int _fileCount;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FormattedLastModified))]
        private DateTime _lastModified;

        [ObservableProperty]
        private bool _isSelected = true;

        public string FormattedSize => ScannedFileItem.FormatSize(SizeBytes);
        public string FormattedLastModified => LastModified.ToString("yyyy-MM-dd");
    }

    public partial class DuplicateGroup : ObservableObject
    {
        public string Hash { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
        public string FormattedSize => ScannedFileItem.FormatSize(SizeBytes);
        public List<ScannedFileItem> Duplicates { get; set; } = new();
        public string TotalWastedSize => ScannedFileItem.FormatSize(SizeBytes * Math.Max(0, Duplicates.Count - 1));
    }
}
