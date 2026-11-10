using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Crunch.Models;

namespace Crunch.Services
{
    public class FileActionService
    {
        public static void RevealInExplorer(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"/select,\"{path}\"",
                        UseShellExecute = true
                    });
                }
                else if (Directory.Exists(path))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"\"{path}\"",
                        UseShellExecute = true
                    });
                }
            }
            catch { }
        }

        public static async Task<bool> DeleteItemAsync(string fullPath, bool toRecycleBin = true, bool forceUnlock = false)
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (File.Exists(fullPath))
                    {
                        if (toRecycleBin)
                        {
                            Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(
                                fullPath,
                                Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                                Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                        }
                        else
                        {
                            File.Delete(fullPath);
                        }
                        return true;
                    }
                    else if (Directory.Exists(fullPath))
                    {
                        if (toRecycleBin)
                        {
                            Microsoft.VisualBasic.FileIO.FileSystem.DeleteDirectory(
                                fullPath,
                                Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                                Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                        }
                        else
                        {
                            Directory.Delete(fullPath, true);
                        }
                        return true;
                    }
                }
                catch (IOException)
                {
                    if (forceUnlock && ProcessLockService.UnlockAndKillLockingProcesses(fullPath))
                    {
                        try
                        {
                            if (File.Exists(fullPath)) File.Delete(fullPath);
                            else if (Directory.Exists(fullPath)) Directory.Delete(fullPath, true);
                            return true;
                        }
                        catch { }
                    }
                }
                catch { }

                return false;
            });
        }

        public static List<DriveSpaceInfo> GetDrives()
        {
            var list = new List<DriveSpaceInfo>();
            try
            {
                foreach (var d in DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed))
                {
                    list.Add(new DriveSpaceInfo
                    {
                        Name = d.Name,
                        VolumeLabel = d.VolumeLabel,
                        DriveFormat = d.DriveFormat,
                        TotalBytes = d.TotalSize,
                        FreeBytes = d.TotalFreeSpace
                    });
                }
            }
            catch { }
            return list;
        }
    }
}
