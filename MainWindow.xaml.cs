using System;
using System.Windows;
using System.Windows.Input;
using Crunch.ViewModels;

namespace Crunch
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Crunch.Services.WindowIconHelper.ApplyIcon(this);
            DataContext = new MainViewModel();
        }

        private void OnTitleBarMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                if (e.ClickCount == 2)
                {
                    OnMaximizeClick(sender, e);
                }
                else
                {
                    DragMove();
                }
            }
        }

        private void OnMinimizeClick(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void OnMaximizeClick(object sender, RoutedEventArgs e)
        {
            WindowState = (WindowState == WindowState.Maximized) ? WindowState.Normal : WindowState.Maximized;
        }

        private void OnCloseClick(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
