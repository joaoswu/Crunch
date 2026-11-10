using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Crunch.Services
{
    public static class WindowIconHelper
    {
        private const int WM_SETICON = 0x0080;
        private const int ICON_SMALL = 0;
        private const int ICON_BIG = 1;

        private const int GCLP_HICON = -14;
        private const int GCLP_HICONSM = -34;

        private const int SM_CXICON = 11;
        private const int SM_CYICON = 12;
        private const int SM_CXSMICON = 49;
        private const int SM_CYSMICON = 50;

        private const uint IMAGE_ICON = 1;
        private const uint LR_LOADFROMFILE = 0x00000010;
        private const uint LR_DEFAULTSIZE = 0x00000040;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        [DllImport("shell32.dll", SetLastError = true)]
        private static extern void SetCurrentProcessExplicitAppUserModelID([MarshalAs(UnmanagedType.LPWStr)] string AppID);

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern uint ExtractIconEx(string szFileName, int nIconIndex, IntPtr[] phiconLarge, IntPtr[] phiconSmall, uint nIcons);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr LoadImage(IntPtr hinst, string lpszName, uint uType, int cxDesired, int cyDesired, uint fuLoad);

        [DllImport("user32.dll", EntryPoint = "SetClassLongPtr")]
        private static extern IntPtr SetClassLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetClassLong")]
        private static extern int SetClassLong32(IntPtr hWnd, int nIndex, int dwNewLong);

        private static IntPtr SetClassLong(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
        {
            if (IntPtr.Size == 8)
                return SetClassLongPtr64(hWnd, nIndex, dwNewLong);
            else
                return new IntPtr(SetClassLong32(hWnd, nIndex, dwNewLong.ToInt32()));
        }

        public static void InitializeAppId()
        {
            try
            {
                SetCurrentProcessExplicitAppUserModelID("Crunch.StorageReclamation.App");
            }
            catch { }
        }

        public static void ApplyIcon(Window window)
        {
            if (window == null) return;

            void OnSourceInitialized(object? sender, EventArgs e)
            {
                window.SourceInitialized -= OnSourceInitialized;
                SetWindowIcons(window);
            }

            if (window.IsLoaded)
            {
                SetWindowIcons(window);
            }
            else
            {
                window.SourceInitialized += OnSourceInitialized;
            }
        }

        private static void SetWindowIcons(Window window)
        {
            try
            {
                var hwnd = new WindowInteropHelper(window).Handle;
                if (hwnd == IntPtr.Zero) return;

                int cxBig = GetSystemMetrics(SM_CXICON);
                int cyBig = GetSystemMetrics(SM_CYICON);
                int cxSmall = GetSystemMetrics(SM_CXSMICON);
                int cySmall = GetSystemMetrics(SM_CYSMICON);

                if (cxBig <= 0) cxBig = 32;
                if (cyBig <= 0) cyBig = 32;
                if (cxSmall <= 0) cxSmall = 16;
                if (cySmall <= 0) cySmall = 16;

                IntPtr hIconBig = IntPtr.Zero;
                IntPtr hIconSmall = IntPtr.Zero;

                // 1. Try loading directly from Assets/app.ico on disk
                string[] candidates = new[]
                {
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "app.ico"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "Assets", "app.ico"),
                    Path.GetFullPath("Assets/app.ico")
                };

                foreach (var path in candidates)
                {
                    if (File.Exists(path))
                    {
                        hIconBig = LoadImage(IntPtr.Zero, path, IMAGE_ICON, cxBig, cyBig, LR_LOADFROMFILE);
                        hIconSmall = LoadImage(IntPtr.Zero, path, IMAGE_ICON, cxSmall, cySmall, LR_LOADFROMFILE);
                        if (hIconBig != IntPtr.Zero || hIconSmall != IntPtr.Zero)
                            break;
                    }
                }

                // 2. Fallback to executable PE embedded icon
                if (hIconBig == IntPtr.Zero || hIconSmall == IntPtr.Zero)
                {
                    var exePath = Process.GetCurrentProcess().MainModule?.FileName;
                    if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                    {
                        var large = new IntPtr[1];
                        var small = new IntPtr[1];
                        if (ExtractIconEx(exePath, 0, large, small, 1) > 0)
                        {
                            if (hIconBig == IntPtr.Zero) hIconBig = large[0];
                            if (hIconSmall == IntPtr.Zero) hIconSmall = small[0];
                        }
                    }
                }

                // 3. Dispatch WM_SETICON to Window and Taskbar
                if (hIconBig != IntPtr.Zero)
                {
                    SendMessage(hwnd, WM_SETICON, (IntPtr)ICON_BIG, hIconBig);
                    SetClassLong(hwnd, GCLP_HICON, hIconBig);
                }
                if (hIconSmall != IntPtr.Zero)
                {
                    SendMessage(hwnd, WM_SETICON, (IntPtr)ICON_SMALL, hIconSmall);
                    SetClassLong(hwnd, GCLP_HICONSM, hIconSmall);
                }
            }
            catch { }
        }
    }
}
