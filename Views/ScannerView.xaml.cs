using System.Windows;
using System.Windows.Controls;
using Crunch.ViewModels;

namespace Crunch.Views
{
    public partial class ScannerView : UserControl
    {
        public ScannerView()
        {
            InitializeComponent();
        }

        private void OnSleepingGiantsSelected(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.SetScannerCategoryCommand.Execute("SleepingGiants");
            }
        }

        private void OnInstallersSelected(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.SetScannerCategoryCommand.Execute("Installers");
            }
        }

        private void OnMediaSelected(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.SetScannerCategoryCommand.Execute("Media");
            }
        }

        private void OnArchivesSelected(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.SetScannerCategoryCommand.Execute("Archives");
            }
        }
    }
}
