using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Crunch.ViewModels;

namespace Crunch.Views
{
    public partial class BlackHoleView : UserControl
    {
        public BlackHoleView()
        {
            InitializeComponent();
        }

        private void OnDragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
                DropZoneBorder.BorderBrush = (Brush)FindResource("AccentGreenBrush");
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void OnDragLeave(object sender, DragEventArgs e)
        {
            DropZoneBorder.BorderBrush = (Brush)FindResource("BorderCardBrush");
            e.Handled = true;
        }

        private async void OnDrop(object sender, DragEventArgs e)
        {
            DropZoneBorder.BorderBrush = (Brush)FindResource("BorderCardBrush");

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0)
                {
                    if (DataContext is MainViewModel vm)
                    {
                        await vm.HandleBlackHoleDropAsync(files[0]);
                    }
                }
            }
        }
    }
}
