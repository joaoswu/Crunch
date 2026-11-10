using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Animation;

namespace Crunch
{
    public partial class SplashWindow : Window
    {
        public SplashWindow()
        {
            InitializeComponent();
            Crunch.Services.WindowIconHelper.ApplyIcon(this);
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            // 1. Staggered clockwise assembly of the 5 segments of the new logo
            AnimateSegmentIn(Seg1, Scale1, 0);
            AnimateSegmentIn(Seg2, Scale2, 130);
            AnimateSegmentIn(Seg3, Scale3, 260);
            AnimateSegmentIn(Seg4, Scale4, 390);
            AnimateSegmentIn(Seg5, Scale5, 520);

            // 2. Step through the subsystem initializations
            await AnimateProgressStepAsync(28, 500);
            await AnimateProgressStepAsync(60, 550);
            await AnimateProgressStepAsync(88, 500);
            await AnimateProgressStepAsync(100, 350);

            await Task.Delay(250);

            // 3. Smooth, cinematic cross-fade into MainWindow
            var mainWin = new MainWindow
            {
                Opacity = 0.0
            };
            mainWin.Show();

            // Fade in MainWindow
            var fadeInMain = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(450))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            mainWin.BeginAnimation(OpacityProperty, fadeInMain);

            // Fade out SplashWindow
            var fadeOutSplash = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(450))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            fadeOutSplash.Completed += (s, ev) => Close();
            BeginAnimation(OpacityProperty, fadeOutSplash);
        }

        private void AnimateSegmentIn(FrameworkElement element, System.Windows.Media.ScaleTransform scale, int delayMs)
        {
            var ease = new BackEase { Amplitude = 0.35, EasingMode = EasingMode.EaseOut };

            var opacityAnim = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(350))
            {
                BeginTime = TimeSpan.FromMilliseconds(delayMs)
            };

            var scaleXAnim = new DoubleAnimation(0.6, 1.0, TimeSpan.FromMilliseconds(420))
            {
                BeginTime = TimeSpan.FromMilliseconds(delayMs),
                EasingFunction = ease
            };

            var scaleYAnim = new DoubleAnimation(0.6, 1.0, TimeSpan.FromMilliseconds(420))
            {
                BeginTime = TimeSpan.FromMilliseconds(delayMs),
                EasingFunction = ease
            };

            element.BeginAnimation(OpacityProperty, opacityAnim);
            scale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, scaleXAnim);
            scale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, scaleYAnim);
        }

        private async Task AnimateProgressStepAsync(double targetProgress, int delayMs)
        {
            var anim = new DoubleAnimation
            {
                To = targetProgress,
                Duration = TimeSpan.FromMilliseconds(delayMs),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            SplashProgressBar.BeginAnimation(System.Windows.Controls.Primitives.RangeBase.ValueProperty, anim);

            await Task.Delay(delayMs);
        }
    }
}
