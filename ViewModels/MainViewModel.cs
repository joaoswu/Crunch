using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Crunch.Models;
using Crunch.Services;

namespace Crunch.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _currentTab = "Dashboard"; // Dashboard, SleepingGiants, Installers, DevBloat, Duplicates, BlackHole

        [ObservableProperty]
        private bool _isScanning;

        [ObservableProperty]
        private string _scanStatusText = "System idle. Ready to crunch.";

        [ObservableProperty]
        private string _currentScanPath = string.Empty;

        [ObservableProperty]
        private long _scannedFileCount;

        [ObservableProperty]
        private long _foundItemCount;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FormattedTotalFound))]
        private long _totalBytesFound;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FormattedReclaimed))]
        private long _totalReclaimedBytes;

        [ObservableProperty]
        private bool _safeRecycleBinMode = true; // Safe by default: send to Recycle Bin

        [ObservableProperty]
        private bool _forceProcessUnlock = true; // Auto-unlock files held by processes

        [ObservableProperty]
        private DriveSpaceInfo? _selectedDrive;

        [ObservableProperty]
        private string _blackHoleDropPath = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FormattedBlackHoleSize))]
        private long _blackHoleSizeBytes;

        [ObservableProperty]
        private int _blackHoleFileCount;

        [ObservableProperty]
        private string _blackHoleStatus = "Drop any stubborn folder here to inspect & shred";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ActiveScannerFiles))]
        [NotifyPropertyChangedFor(nameof(FormattedActiveCategorySize))]
        [NotifyPropertyChangedFor(nameof(ActiveScannerFilesCount))]
        private string _currentScannerCategory = "SleepingGiants";

        [ObservableProperty]
        private string _currentTempFilter = "All";

        [ObservableProperty]
        private long _selectedTempSizeBytes;

        [ObservableProperty]
        private int _selectedTempCount;

        [ObservableProperty]
        private string _currentDevTab = "Projects"; // "Projects", "Orphans"

        [ObservableProperty]
        private bool _isScanningOrphans;

        public ObservableCollection<DriveSpaceInfo> Drives { get; } = new();
        public ObservableCollection<TempCleanCategory> TempCategories { get; } = new();
        public ObservableCollection<ScannedFileItem> SleepingGiants { get; } = new();
        public ObservableCollection<ScannedFileItem> Installers { get; } = new();
        public ObservableCollection<ScannedFileItem> MediaFiles { get; } = new();
        public ObservableCollection<ScannedFileItem> Archives { get; } = new();
        public ObservableCollection<DevBloatItem> DevBloatItems { get; } = new();
        public ObservableCollection<OrphanAppItem> OrphanAppItems { get; } = new();
        public ObservableCollection<DuplicateGroup> DuplicateGroups { get; } = new();

        private CancellationTokenSource? _scanCts;

        public string FormattedSelectedTempSize => ScannedFileItem.FormatSize(SelectedTempSizeBytes);
        public string SelectedTempButtonText => SelectedTempSizeBytes > 0 
            ? $"Purge Selected ({FormattedSelectedTempSize})" 
            : "Purge Selected Bloat";

        public ObservableCollection<ScannedFileItem> ActiveScannerFiles => CurrentScannerCategory switch
        {
            "Installers" => Installers,
            "Media" => MediaFiles,
            "Archives" => Archives,
            _ => SleepingGiants
        };

        public int ActiveScannerFilesCount => ActiveScannerFiles.Count;
        public string FormattedActiveCategorySize => ScannedFileItem.FormatSize(ActiveScannerFiles.Sum(f => f.SizeBytes));

        public long TotalDevBloatBytes => DevBloatItems.Sum(x => x.SizeBytes);
        public string FormattedTotalDevBloatSize => ScannedFileItem.FormatSize(TotalDevBloatBytes);

        public long TotalOrphanBytes => OrphanAppItems.Sum(x => x.SizeBytes);
        public string FormattedTotalOrphanSize => ScannedFileItem.FormatSize(TotalOrphanBytes);

        public long TotalDuplicateWastedBytes => DuplicateGroups.Sum(g => g.SizeBytes * Math.Max(0, g.Duplicates.Count - 1));
        public string FormattedTotalDuplicateWasted => ScannedFileItem.FormatSize(TotalDuplicateWastedBytes);

        public string FormattedTotalFound => ScannedFileItem.FormatSize(TotalBytesFound);
        public string FormattedReclaimed => ScannedFileItem.FormatSize(TotalReclaimedBytes);
        public string FormattedBlackHoleSize => ScannedFileItem.FormatSize(BlackHoleSizeBytes);

        public MainViewModel()
        {
            RefreshDrives();
            InitTempCategories();

            // Auto-analyze temp categories on startup so actual storage numbers populate immediately
            _ = Task.Run(async () =>
            {
                // Optimization 7: Instant State Load (0ms startup latency)
                await LoadStateCacheAsync();
                await Task.Delay(400);
                await ScanTempCategoriesAsync();
            });
        }

        private async Task LoadStateCacheAsync()
        {
            try
            {
                var snapshot = await StateCacheService.LoadSnapshotAsync();
                if (snapshot == null) return;

                Application.Current?.Dispatcher.Invoke(() =>
                {
                    if (snapshot.CachedLargeFiles.Count > 0 && SleepingGiants.Count == 0)
                    {
                        foreach (var f in snapshot.CachedLargeFiles)
                        {
                            switch (f.Category)
                            {
                                case FileCategory.InstallerOrImage:
                                    Installers.Add(f);
                                    break;
                                case FileCategory.MassiveMedia:
                                    MediaFiles.Add(f);
                                    break;
                                case FileCategory.ColdArchive:
                                    Archives.Add(f);
                                    break;
                                default:
                                    SleepingGiants.Add(f);
                                    break;
                            }
                        }
                        TotalBytesFound = snapshot.CachedLargeFiles.Sum(f => f.SizeBytes);
                        OnPropertyChanged(nameof(ActiveScannerFiles));
                        OnPropertyChanged(nameof(FormattedActiveCategorySize));
                        OnPropertyChanged(nameof(ActiveScannerFilesCount));
                        OnPropertyChanged(nameof(FormattedTotalFound));
                    }

                    if (snapshot.CachedDevBloat.Count > 0 && DevBloatItems.Count == 0)
                    {
                        foreach (var b in snapshot.CachedDevBloat)
                        {
                            DevBloatItems.Add(b);
                        }
                        OnPropertyChanged(nameof(TotalDevBloatBytes));
                        OnPropertyChanged(nameof(FormattedTotalDevBloatSize));
                    }

                    if (snapshot.CachedOrphans.Count > 0 && OrphanAppItems.Count == 0)
                    {
                        foreach (var o in snapshot.CachedOrphans)
                        {
                            OrphanAppItems.Add(o);
                        }
                        OnPropertyChanged(nameof(TotalOrphanBytes));
                        OnPropertyChanged(nameof(FormattedTotalOrphanSize));
                    }
                });
            }
            catch { }
        }

        private void TriggerStateSave()
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    var snapshot = new DiskStateSnapshot
                    {
                        SavedAt = DateTime.UtcNow,
                        SystemDriveLetter = SelectedDrive?.Name ?? "C:\\",
                        TotalDriveBytes = SelectedDrive?.TotalBytes ?? 0,
                        FreeDriveBytes = SelectedDrive?.FreeBytes ?? 0,
                        UsedDriveBytes = SelectedDrive?.UsedBytes ?? 0,
                        CachedLargeFiles = SleepingGiants.Concat(Installers).Concat(MediaFiles).Concat(Archives).ToList(),
                        CachedDevBloat = DevBloatItems.ToList(),
                        CachedOrphans = OrphanAppItems.ToList(),
                        TotalReclaimableBytes = TotalBytesFound + TotalDevBloatBytes + TotalOrphanBytes
                    };
                    await StateCacheService.SaveSnapshotAsync(snapshot);
                }
                catch { }
            });
        }

        public void UpdateSelectedTempTotals()
        {
            var selected = TempCategories.Where(c => c.IsSelected).ToList();
            SelectedTempSizeBytes = selected.Sum(c => c.SizeBytes);
            SelectedTempCount = selected.Count;
            OnPropertyChanged(nameof(SelectedTempSizeBytes));
            OnPropertyChanged(nameof(SelectedTempCount));
            OnPropertyChanged(nameof(FormattedSelectedTempSize));
            OnPropertyChanged(nameof(SelectedTempButtonText));
        }

        public void RefreshDrives()
        {
            Drives.Clear();
            var drives = FileActionService.GetDrives();
            foreach (var d in drives)
            {
                Drives.Add(d);
            }
            SelectedDrive = Drives.FirstOrDefault();
        }

        private void InitTempCategories()
        {
            TempCategories.Clear();
            foreach (var cat in TempCleanerService.GetDefaultCategories())
            {
                cat.IsSelected = false; // Zero selected by default!
                cat.SelectionChangedCallback = () => UpdateSelectedTempTotals();
                TempCategories.Add(cat);
            }
            UpdateSelectedTempTotals();
        }

        [RelayCommand]
        private void SelectTab(string tabName)
        {
            CurrentTab = tabName;
        }

        [RelayCommand]
        private void ToggleSelectAllTemp()
        {
            bool anyUnselected = TempCategories.Any(c => !c.IsSelected);
            foreach (var cat in TempCategories)
            {
                cat.IsSelected = anyUnselected;
            }
            UpdateSelectedTempTotals();
        }

        [RelayCommand]
        private void SetScannerCategory(string category)
        {
            CurrentScannerCategory = category;
            OnPropertyChanged(nameof(ActiveScannerFiles));
            OnPropertyChanged(nameof(FormattedActiveCategorySize));
            OnPropertyChanged(nameof(ActiveScannerFilesCount));
        }

        [RelayCommand]
        private void ToggleSelectAllActiveFiles()
        {
            var list = ActiveScannerFiles;
            bool anyUnselected = list.Any(f => !f.IsSelected);
            foreach (var item in list)
            {
                item.IsSelected = anyUnselected;
            }
        }

        [RelayCommand]
        private void ToggleSelectAllFiles(string category)
        {
            var list = category switch
            {
                "Installers" => Installers,
                "Media" => MediaFiles,
                "Archives" => Archives,
                _ => SleepingGiants
            };

            bool anyUnselected = list.Any(f => !f.IsSelected);
            foreach (var item in list)
            {
                item.IsSelected = anyUnselected;
            }
        }

        [RelayCommand]
        private void FilterTempCategories(string filter)
        {
            CurrentTempFilter = filter;
            foreach (var cat in TempCategories)
            {
                cat.IsVisibleInFilter = (filter == "All" || cat.CategoryGroup.Equals(filter, StringComparison.OrdinalIgnoreCase));
            }
        }

        [RelayCommand]
        private void SetDevTab(string tab)
        {
            CurrentDevTab = tab;
            if (tab == "Orphans" && OrphanAppItems.Count == 0 && !IsScanningOrphans)
            {
                _ = ScanOrphansAsync();
            }
        }

        [RelayCommand]
        private void ToggleSelectAllOrphans()
        {
            bool anyUnselected = OrphanAppItems.Any(x => !x.IsSelected);
            foreach (var item in OrphanAppItems)
            {
                item.IsSelected = anyUnselected;
            }
        }

        [RelayCommand]
        private async Task ScanOrphansAsync()
        {
            if (IsScanningOrphans) return;
            IsScanningOrphans = true;
            ScanStatusText = "Scanning for abandoned application leftovers in AppData & ProgramData...";

            try
            {
                var progress = new Progress<string>(msg => ScanStatusText = msg);
                var orphans = await OrphanHunterService.ScanOrphanedAppFoldersAsync(progress);

                OrphanAppItems.Clear();
                foreach (var o in orphans)
                {
                    OrphanAppItems.Add(o);
                }
                OnPropertyChanged(nameof(TotalOrphanBytes));
                OnPropertyChanged(nameof(FormattedTotalOrphanSize));
                ScanStatusText = $"Found {OrphanAppItems.Count} orphaned app folders ({FormattedTotalOrphanSize}).";
                TriggerStateSave();
            }
            catch (Exception ex)
            {
                ScanStatusText = $"Orphan scan failed: {ex.Message}";
            }
            finally
            {
                IsScanningOrphans = false;
            }
        }

        [RelayCommand]
        private async Task PurgeSelectedOrphansAsync()
        {
            var toPurge = OrphanAppItems.Where(x => x.IsSelected).ToList();
            if (toPurge.Count == 0) return;

            ScanStatusText = $"Purging {toPurge.Count} orphaned folders...";
            long cleaned = 0;

            foreach (var item in toPurge)
            {
                bool ok = await OrphanHunterService.DeleteOrphanFolderAsync(item.FullPath, SafeRecycleBinMode);
                if (ok)
                {
                    cleaned += item.SizeBytes;
                    OrphanAppItems.Remove(item);
                }
            }

            TotalReclaimedBytes += cleaned;
            OnPropertyChanged(nameof(TotalOrphanBytes));
            OnPropertyChanged(nameof(FormattedTotalOrphanSize));
            ScanStatusText = $"Purged {ScannedFileItem.FormatSize(cleaned)} in orphaned software remnants.";
            RefreshDrives();
        }

        [RelayCommand]
        private void ToggleSelectAllDevBloat()
        {
            bool anyUnselected = DevBloatItems.Any(d => !d.IsSelected);
            foreach (var item in DevBloatItems)
            {
                item.IsSelected = anyUnselected;
            }
        }

        [RelayCommand]
        private void ToggleSelectAllDuplicates()
        {
            var allDups = DuplicateGroups.SelectMany(g => g.Duplicates).ToList();
            bool anyUnselected = allDups.Any(d => !d.IsSelected);
            foreach (var item in allDups)
            {
                item.IsSelected = anyUnselected;
            }
        }

        [RelayCommand]
        private async Task ScanTempCategoriesAsync()
        {
            if (IsScanning) return;
            IsScanning = true;
            ScanStatusText = "Analyzing temporary system junk & caches...";

            try
            {
                _scanCts = new CancellationTokenSource();
                foreach (var cat in TempCategories)
                {
                    if (_scanCts.IsCancellationRequested) break;
                    await TempCleanerService.AnalyzeCategoryAsync(cat, _scanCts.Token);
                }
                UpdateSelectedTempTotals();
                ScanStatusText = "Temp cache analysis complete.";
            }
            catch (Exception ex)
            {
                ScanStatusText = $"Scan failed: {ex.Message}";
            }
            finally
            {
                IsScanning = false;
            }
        }

        [RelayCommand]
        private async Task CleanTempCategoriesAsync()
        {
            if (IsScanning) return;
            IsScanning = true;
            ScanStatusText = "Purging selected temporary files...";

            long totalCleaned = 0;
            int totalFiles = 0;

            try
            {
                _scanCts = new CancellationTokenSource();
                foreach (var cat in TempCategories.Where(c => c.IsSelected && c.SizeBytes > 0))
                {
                    if (_scanCts.IsCancellationRequested) break;
                    var (cleanedBytes, files, _) = await TempCleanerService.CleanCategoryAsync(
                        cat, ForceProcessUnlock, _scanCts.Token);

                    totalCleaned += cleanedBytes;
                    totalFiles += files;
                }

                TotalReclaimedBytes += totalCleaned;
                ScanStatusText = $"Purged {ScannedFileItem.FormatSize(totalCleaned)} across {totalFiles} items.";
                RefreshDrives();
            }
            catch (Exception ex)
            {
                ScanStatusText = $"Purge interrupted: {ex.Message}";
            }
            finally
            {
                IsScanning = false;
            }
        }

        [RelayCommand]
        private async Task StartLargeFileScanAsync()
        {
            if (IsScanning) return;
            if (SelectedDrive == null) return;

            IsScanning = true;
            SleepingGiants.Clear();
            Installers.Clear();
            MediaFiles.Clear();
            Archives.Clear();
            ScannedFileCount = 0;
            FoundItemCount = 0;
            TotalBytesFound = 0;

            _scanCts = new CancellationTokenSource();
            string scanRoot = SelectedDrive.Name;

            var progress = new Progress<DiskScannerService.ScanProgressEventArgs>(e =>
            {
                ScannedFileCount = e.ScannedFileCount;
                FoundItemCount = e.FoundItemCount;
                CurrentScanPath = e.CurrentDirectory;
                TotalBytesFound = e.TotalBytesFound;
                ScanStatusText = $"Scanning {e.ScannedFileCount:N0} files... ({ScannedFileItem.FormatSize(e.TotalBytesFound)} found)";
            });

            try
            {
                var files = await DiskScannerService.ScanPathAsync(
                    scanRoot, 100 * 1024 * 1024, progress, _scanCts.Token);

                foreach (var f in files)
                {
                    switch (f.Category)
                    {
                        case FileCategory.SleepingGiant:
                            SleepingGiants.Add(f);
                            break;
                        case FileCategory.InstallerOrImage:
                            Installers.Add(f);
                            break;
                        case FileCategory.MassiveMedia:
                            MediaFiles.Add(f);
                            break;
                        case FileCategory.ColdArchive:
                            Archives.Add(f);
                            break;
                        default:
                            SleepingGiants.Add(f);
                            break;
                    }
                }

                ScanStatusText = $"Scan complete. Discovered {files.Count} large items ({ScannedFileItem.FormatSize(TotalBytesFound)}).";
                OnPropertyChanged(nameof(ActiveScannerFiles));
                OnPropertyChanged(nameof(FormattedActiveCategorySize));
                OnPropertyChanged(nameof(ActiveScannerFilesCount));
                TriggerStateSave();
            }
            catch (Exception ex)
            {
                ScanStatusText = $"Scan stopped: {ex.Message}";
            }
            finally
            {
                IsScanning = false;
            }
        }

        [RelayCommand]
        private async Task ScanDevBloatAsync()
        {
            if (IsScanning) return;
            if (SelectedDrive == null) return;

            IsScanning = true;
            DevBloatItems.Clear();
            ScanStatusText = "Scanning for node_modules, target, .venv, and build caches...";

            _scanCts = new CancellationTokenSource();
            // Start scanning from Users directory on primary drive for maximum relevance
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string root = Directory.Exists(userProfile) ? userProfile : SelectedDrive.Name;

            var progress = new Progress<string>(msg =>
            {
                CurrentScanPath = msg;
            });

            try
            {
                var bloat = await DevBloatService.ScanDevBloatAsync(root, progress, _scanCts.Token);
                foreach (var b in bloat)
                {
                    DevBloatItems.Add(b);
                }

                long totalBloatBytes = bloat.Sum(x => x.SizeBytes);
                OnPropertyChanged(nameof(TotalDevBloatBytes));
                OnPropertyChanged(nameof(FormattedTotalDevBloatSize));
                ScanStatusText = $"Found {bloat.Count} dev cache directories ({ScannedFileItem.FormatSize(totalBloatBytes)}).";
                TriggerStateSave();
            }
            catch (Exception ex)
            {
                ScanStatusText = $"Dev scan stopped: {ex.Message}";
            }
            finally
            {
                IsScanning = false;
            }
        }

        [RelayCommand]
        private async Task PurgeSelectedDevBloatAsync()
        {
            var selected = DevBloatItems.Where(x => x.IsSelected).ToList();
            if (selected.Count == 0) return;

            IsScanning = true;
            ScanStatusText = $"Vaporizing {selected.Count} build cache folders...";

            long freed = 0;
            foreach (var item in selected)
            {
                bool deleted = await DevBloatService.DeleteDevBloatFolderAsync(item.FullPath, SafeRecycleBinMode);
                if (deleted)
                {
                    freed += item.SizeBytes;
                    DevBloatItems.Remove(item);
                }
            }

            TotalReclaimedBytes += freed;
            OnPropertyChanged(nameof(TotalDevBloatBytes));
            OnPropertyChanged(nameof(FormattedTotalDevBloatSize));
            ScanStatusText = $"Reclaimed {ScannedFileItem.FormatSize(freed)} from developer bloat.";
            RefreshDrives();
            IsScanning = false;
        }

        [RelayCommand]
        private async Task ScanDuplicatesAsync()
        {
            if (IsScanning) return;
            if (SelectedDrive == null) return;

            IsScanning = true;
            DuplicateGroups.Clear();
            _scanCts = new CancellationTokenSource();

            // Combine all large files scanned so far or scan quickly if not yet cached
            var allFiles = SleepingGiants.Concat(Installers).Concat(MediaFiles).Concat(Archives).ToList();
            if (allFiles.Count == 0)
            {
                ScanStatusText = "Scanning drive for potential duplicate candidate files...";
                var progressQuick = new Progress<DiskScannerService.ScanProgressEventArgs>(e =>
                {
                    ScanStatusText = $"Scanning files for duplicates... ({e.ScannedFileCount:N0} files scanned)";
                });

                try
                {
                    allFiles = await DiskScannerService.ScanPathAsync(
                        SelectedDrive.Name, 10 * 1024 * 1024, progressQuick, _scanCts.Token);
                }
                catch { }
            }

            if (allFiles.Count == 0)
            {
                ScanStatusText = "No large files found on drive to compare.";
                IsScanning = false;
                return;
            }

            ScanStatusText = "Analyzing hash signatures of duplicate candidates...";
            var progress = new Progress<string>(msg => ScanStatusText = msg);

            try
            {
                var groups = await DuplicateScannerService.FindDuplicatesAsync(allFiles, progress, _scanCts.Token);
                foreach (var g in groups)
                {
                    DuplicateGroups.Add(g);
                }

                OnPropertyChanged(nameof(TotalDuplicateWastedBytes));
                OnPropertyChanged(nameof(FormattedTotalDuplicateWasted));

                long wasted = groups.Sum(g => g.SizeBytes * Math.Max(0, g.Duplicates.Count - 1));
                ScanStatusText = $"Identified {groups.Count} duplicate clusters ({ScannedFileItem.FormatSize(wasted)} wasted).";
            }
            catch (Exception ex)
            {
                ScanStatusText = $"Duplicate scan stopped: {ex.Message}";
            }
            finally
            {
                IsScanning = false;
            }
        }

        [RelayCommand]
        private async Task PurgeSelectedDuplicatesAsync()
        {
            var toDelete = DuplicateGroups
                .SelectMany(g => g.Duplicates)
                .Where(d => d.IsSelected)
                .ToList();

            if (toDelete.Count == 0) return;

            IsScanning = true;
            ScanStatusText = $"Purging {toDelete.Count} duplicate copies...";

            long freed = 0;
            foreach (var file in toDelete)
            {
                bool ok = await FileActionService.DeleteItemAsync(file.FullPath, SafeRecycleBinMode, ForceProcessUnlock);
                if (ok)
                {
                    freed += file.SizeBytes;
                }
            }

            TotalReclaimedBytes += freed;
            ScanStatusText = $"Freed {ScannedFileItem.FormatSize(freed)} by removing redundant copies.";
            DuplicateGroups.Clear();
            RefreshDrives();
            IsScanning = false;
        }

        [RelayCommand]
        private void CancelScan()
        {
            _scanCts?.Cancel();
            ScanStatusText = "Scan cancelled by user.";
        }

        [RelayCommand]
        private void RevealFile(string path)
        {
            FileActionService.RevealInExplorer(path);
        }

        [RelayCommand]
        private async Task DeleteSingleFileAsync(ScannedFileItem item)
        {
            bool ok = await FileActionService.DeleteItemAsync(item.FullPath, SafeRecycleBinMode, ForceProcessUnlock);
            if (ok)
            {
                TotalReclaimedBytes += item.SizeBytes;
                SleepingGiants.Remove(item);
                Installers.Remove(item);
                MediaFiles.Remove(item);
                Archives.Remove(item);
                RefreshDrives();
            }
        }

        [RelayCommand]
        private async Task ForceUnlockAndKillAsync(ScannedFileItem item)
        {
            if (ProcessLockService.UnlockAndKillLockingProcesses(item.FullPath))
            {
                item.LockingProcess = "Unlocked!";
                await DeleteSingleFileAsync(item);
            }
            else
            {
                item.LockingProcess = "No active lock found or lack permissions.";
            }
        }

        public async Task HandleBlackHoleDropAsync(string path)
        {
            if (!Directory.Exists(path) && !File.Exists(path)) return;

            BlackHoleDropPath = path;
            BlackHoleStatus = "Analyzing dropped target...";

            await Task.Run(() =>
            {
                long totalBytes = 0;
                int count = 0;

                if (File.Exists(path))
                {
                    var fi = new FileInfo(path);
                    totalBytes = fi.Length;
                    count = 1;
                }
                else if (Directory.Exists(path))
                {
                    var di = new DirectoryInfo(path);
                    foreach (var f in di.EnumerateFiles("*", new EnumerationOptions
                    {
                        IgnoreInaccessible = true,
                        RecurseSubdirectories = true
                    }))
                    {
                        try
                        {
                            totalBytes += f.Length;
                            count++;
                        }
                        catch { }
                    }
                }

                BlackHoleSizeBytes = totalBytes;
                BlackHoleFileCount = count;
                BlackHoleStatus = $"Target ready: {count:N0} files ({ScannedFileItem.FormatSize(totalBytes)})";
            });
        }

        [RelayCommand]
        private async Task ShredBlackHoleTargetAsync()
        {
            if (string.IsNullOrEmpty(BlackHoleDropPath)) return;

            BlackHoleStatus = "Deleting target...";
            bool ok = await FileActionService.DeleteItemAsync(BlackHoleDropPath, SafeRecycleBinMode, ForceProcessUnlock);

            if (ok)
            {
                TotalReclaimedBytes += BlackHoleSizeBytes;
                BlackHoleStatus = $"Deleted. Reclaimed {ScannedFileItem.FormatSize(BlackHoleSizeBytes)}.";
                BlackHoleDropPath = string.Empty;
                BlackHoleSizeBytes = 0;
                BlackHoleFileCount = 0;
                RefreshDrives();
            }
            else
            {
                BlackHoleStatus = "Shred failed (file in use or permission denied).";
            }
        }
    }
}
