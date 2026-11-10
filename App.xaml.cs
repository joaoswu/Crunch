using System;
using System.IO;
using System.Windows;
using System.Windows.Media;

namespace Crunch
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            Crunch.Services.WindowIconHelper.InitializeAppId();

            try
            {
                string fontsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Fonts");
                if (!Directory.Exists(fontsDir))
                {
                    fontsDir = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "Assets", "Fonts"));
                }

                if (Directory.Exists(fontsDir))
                {
                    string normalized = fontsDir.Replace('\\', '/');
                    var fontUri = new Uri($"file:///{normalized}/#Inter");
                    Resources["AppFont"] = new FontFamily(fontUri, "Inter, Segoe UI Variable Text, Segoe UI");
                }
                else
                {
                    Resources["AppFont"] = new FontFamily("Inter, Segoe UI Variable Text, Segoe UI, sans-serif");
                }
            }
            catch
            {
                // Fall back to system default font if loading custom font fails
            }
        }
    }
}
