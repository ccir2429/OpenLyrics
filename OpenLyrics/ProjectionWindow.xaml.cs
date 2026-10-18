using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using OpenLyrics.Models;
using OpenLyrics.Services;
using System.Runtime.InteropServices;
using Windows.Graphics;
using Windows.UI;
using WinRT.Interop;
using WinUIEx;
using MonitorInfo = OpenLyrics.Services.MonitorInfo;

namespace OpenLyrics
{
    public sealed partial class ProjectionWindow : Window, IOutput
    {
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_LAYERED = 0x00080000;

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
        private static extern nint GetWindowLongPtr64(nint hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
        private static extern int GetWindowLong32(nint hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
        private static extern nint SetWindowLongPtr64(nint hWnd, int nIndex, nint dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
        private static extern int SetWindowLong32(nint hWnd, int nIndex, int dwNewLong);

        private static nint GetWindowLongPtr(nint hWnd, int nIndex)
            => nint.Size == 8 ? GetWindowLongPtr64(hWnd, nIndex) : GetWindowLong32(hWnd, nIndex);

        private static nint SetWindowLongPtr(nint hWnd, int nIndex, nint dwNewLong)
            => nint.Size == 8 ? SetWindowLongPtr64(hWnd, nIndex, dwNewLong) : SetWindowLong32(hWnd, nIndex, (int)dwNewLong);

        public ProjectionWindow()
        {
            InitializeComponent();

            ExtendsContentIntoTitleBar = true;
            if (AppWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.SetBorderAndTitleBar(false, false);
                presenter.IsAlwaysOnTop = true;
                presenter.IsResizable = false;
            }
            AppWindow.IsShownInSwitchers = false;
            AppWindow.MoveAndResize(new RectInt32(100, 100, 960, 540));

            var hwnd = WindowNative.GetWindowHandle(this);
            EnableTransparency(hwnd);
        }

        public void PlaceOnMonitor(MonitorInfo monitor)
        {
            AppWindow.MoveAndResize(monitor.Bounds);
        }

        public void ApplyTextRegion(RectInt32? region)
        {
            if (region is RectInt32 r)
            {
                ProjectedText.HorizontalAlignment = HorizontalAlignment.Left;
                ProjectedText.VerticalAlignment = VerticalAlignment.Top;
                ProjectedText.Margin = new Thickness(r.X, r.Y, 0, 0);
                ProjectedText.Width = r.Width;
                ProjectedText.Height = r.Height;
            }
            else
            {
                ProjectedText.HorizontalAlignment = HorizontalAlignment.Stretch;
                ProjectedText.VerticalAlignment = VerticalAlignment.Stretch;
                ProjectedText.Margin = new Thickness(0);
                ProjectedText.Width = double.NaN;
                ProjectedText.Height = double.NaN;
            }
        }

        public void ApplyBackground(AppSettings settings)
        {
            var hwnd = WindowNative.GetWindowHandle(this);

            if (settings.BackgroundMode != "Video")
            {
                StopVideo();
            }

            switch (settings.BackgroundMode)
            {
                case "Image" when !string.IsNullOrEmpty(settings.BackgroundImagePath):
                    DisableTransparency(hwnd);
                    this.SystemBackdrop = null;
                    RootGrid.Background = new ImageBrush
                    {
                        ImageSource = new BitmapImage(new System.Uri(settings.BackgroundImagePath)),
                        Stretch = Stretch.UniformToFill
                    };
                    break;
                case "Video" when !string.IsNullOrEmpty(settings.BackgroundVideoPath):
                    DisableTransparency(hwnd);
                    this.SystemBackdrop = null;
                    RootGrid.Background = new SolidColorBrush(Microsoft.UI.Colors.Black);
                    PlayVideo(settings.BackgroundVideoPath);
                    break;
                case "Color":
                    DisableTransparency(hwnd);
                    this.SystemBackdrop = null;
                    RootGrid.Background = TryParseColor(settings.BackgroundColor, out var bgColor)
                        ? new SolidColorBrush(bgColor)
                        : new SolidColorBrush(Microsoft.UI.Colors.Black);
                    break;
                default:
                    RootGrid.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
                    this.SystemBackdrop = new TransparentTintBackdrop();
                    EnableTransparency(hwnd);
                    break;
            }
        }

        private void PlayVideo(string path)
        {
            BackgroundVideo.Visibility = Visibility.Visible;
            var player = BackgroundVideo.MediaPlayer ?? new Windows.Media.Playback.MediaPlayer();
            player.IsLoopingEnabled = true;
            player.Source = Windows.Media.Core.MediaSource.CreateFromUri(new System.Uri(path));
            BackgroundVideo.SetMediaPlayer(player);
            player.Play();
        }

        private void StopVideo()
        {
            if (BackgroundVideo.MediaPlayer != null)
            {
                BackgroundVideo.MediaPlayer.Pause();
            }
            BackgroundVideo.Visibility = Visibility.Collapsed;
        }

        private static void EnableTransparency(nint hwnd)
        {
            var style = GetWindowLongPtr(hwnd, GWL_EXSTYLE);
            SetWindowLongPtr(hwnd, GWL_EXSTYLE, style | WS_EX_LAYERED);
        }

        private static void DisableTransparency(nint hwnd)
        {
            var style = GetWindowLongPtr(hwnd, GWL_EXSTYLE);
            SetWindowLongPtr(hwnd, GWL_EXSTYLE, style & ~WS_EX_LAYERED);
        }

        public void ApplyStyle(AppSettings settings)
        {
            ProjectedText.FontFamily = new FontFamily(settings.FontFamily);
            ProjectedText.FontSize = settings.FontSize;
            ProjectedText.LineHeight = settings.FontSize * settings.LineSpacing;
            ProjectedText.TextAlignment = settings.TextAlign switch
            {
                "Left" => Microsoft.UI.Xaml.TextAlignment.Left,
                "Right" => Microsoft.UI.Xaml.TextAlignment.Right,
                _ => Microsoft.UI.Xaml.TextAlignment.Center
            };
            if (TryParseColor(settings.TextColor, out var color))
                ProjectedText.Foreground = new SolidColorBrush(color);

            RootGrid.Padding = new Thickness(
                settings.SafeMarginLeft, settings.SafeMarginTop,
                settings.SafeMarginRight, settings.SafeMarginBottom);
        }

        private static bool TryParseColor(string hex, out Color color)
        {
            color = Microsoft.UI.Colors.White;
            hex = hex.TrimStart('#');
            if (hex.Length != 8) return false;
            try
            {
                var a = System.Convert.ToByte(hex.Substring(0, 2), 16);
                var r = System.Convert.ToByte(hex.Substring(2, 2), 16);
                var g = System.Convert.ToByte(hex.Substring(4, 2), 16);
                var b = System.Convert.ToByte(hex.Substring(6, 2), 16);
                color = Color.FromArgb(a, r, g, b);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public void Clear() => SetText(string.Empty);

        public void SetText(string text)
        {
            ProjectedText.Text = text;
        }
    }
}
