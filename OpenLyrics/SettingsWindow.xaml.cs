using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Shapes;
using OpenLyrics.Models;
using OpenLyrics.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Foundation;
using Windows.Graphics;

namespace OpenLyrics
{
    public sealed partial class SettingsWindow : Window
    {
        private readonly AppSettings _settings;
        private readonly Func<ProjectionWindow?> _getProjection;
        private readonly Action _onSettingsChanged;

        private IReadOnlyList<MonitorInfo> _monitors = new List<MonitorInfo>();
        private bool _draggingRegion;
        private bool _resizingRegion;
        private Point _dragStart;
        private Point _dragStartRegion;
        private Point _dragStartSize;
        private bool _updatingRegionBoxes;

        private ProjectionWindow? Projection => _getProjection();

        public SettingsWindow(AppSettings settings, Func<ProjectionWindow?> getProjection, Action onSettingsChanged)
        {
            InitializeComponent();
            _settings = settings;
            _getProjection = getProjection;
            _onSettingsChanged = onSettingsChanged;

            DelimitersBox.Text = string.Join(" ; ", _settings.SlideDelimiters);
            BlankLineCheck.IsChecked = _settings.BlankLineSeparates;
            FullMonitorCheck.IsChecked = _settings.UseFullMonitor;
            RegionXBox.Value = _settings.RegionX;
            RegionYBox.Value = _settings.RegionY;
            RegionWidthBox.Value = _settings.RegionWidth;
            RegionHeightBox.Value = _settings.RegionHeight;
            UpdateRegionEnabled();
            AutoSplitCheck.IsChecked = _settings.AutoSplitOverflow;
            MaxDisplayRowsBox.Value = _settings.MaxDisplayRows;
            InitStyleControls();

            LoadMonitors();
            EnsureDefaultRegion();
        }

        private void LoadMonitors()
        {
            var selected = MonitorCombo.SelectedIndex;
            _monitors = MonitorService.GetMonitors();
            MonitorCombo.ItemsSource = _monitors;
            MonitorCombo.SelectedIndex = _monitors.Count == 0
                ? -1
                : (selected >= 0 && selected < _monitors.Count ? selected
                    : (_settings.MonitorIndex < _monitors.Count ? _settings.MonitorIndex : 0));
            UpdateMonitorPreview();
        }

        private void EnsureDefaultRegion()
        {
            if (_settings.RegionWidth > 0 && _settings.RegionHeight > 0) return;
            if (MonitorCombo.SelectedItem is not MonitorInfo monitor) return;

            var size = (int)Math.Min(monitor.Bounds.Width, monitor.Bounds.Height) / 2;
            SetRegionBoxes(0, 0, Math.Max(1, size), Math.Max(1, size));
            SaveSettings();
        }

        private (int X, int Y, int W, int H) ClampRegion(MonitorInfo monitor, int x, int y, int w, int h)
        {
            w = Math.Clamp(w, 1, monitor.Bounds.Width);
            h = Math.Clamp(h, 1, monitor.Bounds.Height);
            x = Math.Clamp(x, 0, monitor.Bounds.Width - w);
            y = Math.Clamp(y, 0, monitor.Bounds.Height - h);
            return (x, y, w, h);
        }

        private void SaveSettings()
        {
            if (MonitorCombo.SelectedIndex >= 0) _settings.MonitorIndex = MonitorCombo.SelectedIndex;
            _settings.UseFullMonitor = FullMonitorCheck.IsChecked == true;
            _settings.RegionX = (int)RegionXBox.Value;
            _settings.RegionY = (int)RegionYBox.Value;
            _settings.RegionWidth = (int)RegionWidthBox.Value;
            _settings.RegionHeight = (int)RegionHeightBox.Value;
            SettingsService.Save(_settings);
        }

        private void ApplyDelimiters()
        {
            _settings.SlideDelimiters = DelimitersBox.Text
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
            _settings.BlankLineSeparates = BlankLineCheck.IsChecked == true;
            SettingsService.Save(_settings);
            _onSettingsChanged();
        }

        private void DelimitersBox_LostFocus(object sender, RoutedEventArgs e) => ApplyDelimiters();

        private void BlankLineCheck_Click(object sender, RoutedEventArgs e) => ApplyDelimiters();

        private void AutoSplitCheck_Click(object sender, RoutedEventArgs e)
        {
            _settings.AutoSplitOverflow = AutoSplitCheck.IsChecked == true;
            SettingsService.Save(_settings);
            _onSettingsChanged();
        }

        private void MaxDisplayRowsBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            if (double.IsNaN(MaxDisplayRowsBox.Value)) return;
            _settings.MaxDisplayRows = Math.Max(0, (int)MaxDisplayRowsBox.Value);
            SettingsService.Save(_settings);
            _onSettingsChanged();
        }

        private void InitStyleControls()
        {
            FontCombo.SelectedItem = _settings.FontFamily;
            if (FontCombo.SelectedIndex < 0) FontCombo.SelectedIndex = 0;
            FontSizeBox.Value = _settings.FontSize;
            ColorCombo.SelectedItem = ColorNameFromHex(_settings.TextColor);
            if (ColorCombo.SelectedIndex < 0) ColorCombo.SelectedIndex = 0;
            AlignCombo.SelectedItem = _settings.TextAlign;
            if (AlignCombo.SelectedIndex < 0) AlignCombo.SelectedIndex = 0;

            BackgroundModeCombo.SelectedItem = _settings.BackgroundMode;
            if (BackgroundModeCombo.SelectedIndex < 0) BackgroundModeCombo.SelectedIndex = 0;
            BackgroundColorCombo.SelectedItem = BackgroundColorNameFromHex(_settings.BackgroundColor);
            if (BackgroundColorCombo.SelectedIndex < 0) BackgroundColorCombo.SelectedIndex = 0;
        }

        private static readonly (string Name, string Hex)[] BackgroundColorOptions =
        {
            ("Black", "#FF000000"),
            ("White", "#FFFFFFFF"),
            ("Green", "#FF00FF00"),
            ("Blue", "#FF0000FF"),
        };

        private static string BackgroundColorNameFromHex(string hex)
            => BackgroundColorOptions.FirstOrDefault(c => c.Hex.Equals(hex, StringComparison.OrdinalIgnoreCase)).Name ?? "Black";

        private static string BackgroundHexFromColorName(string name)
            => BackgroundColorOptions.FirstOrDefault(c => c.Name == name).Hex ?? "#FF000000";

        private void BackgroundControl_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (BackgroundModeCombo.SelectedItem == null || BackgroundColorCombo.SelectedItem == null) return;

            _settings.BackgroundMode = (string)BackgroundModeCombo.SelectedItem;
            _settings.BackgroundColor = BackgroundHexFromColorName((string)BackgroundColorCombo.SelectedItem);
            SettingsService.Save(_settings);

            Projection?.ApplyBackground(_settings);
        }

        private async void BrowseImageButton_Click(object sender, RoutedEventArgs e)
        {
            var picker = new Windows.Storage.Pickers.FileOpenPicker();
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            picker.FileTypeFilter.Add(".png");
            picker.FileTypeFilter.Add(".jpg");
            picker.FileTypeFilter.Add(".jpeg");
            picker.FileTypeFilter.Add(".bmp");

            var file = await picker.PickSingleFileAsync();
            if (file == null) return;

            _settings.BackgroundImagePath = file.Path;
            _settings.BackgroundMode = "Image";
            BackgroundModeCombo.SelectedItem = "Image";
            SettingsService.Save(_settings);
            Projection?.ApplyBackground(_settings);
        }

        private async void BrowseVideoButton_Click(object sender, RoutedEventArgs e)
        {
            var picker = new Windows.Storage.Pickers.FileOpenPicker();
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            picker.FileTypeFilter.Add(".mp4");
            picker.FileTypeFilter.Add(".wmv");
            picker.FileTypeFilter.Add(".mov");
            picker.FileTypeFilter.Add(".avi");
            picker.FileTypeFilter.Add(".mkv");

            var file = await picker.PickSingleFileAsync();
            if (file == null) return;

            _settings.BackgroundVideoPath = file.Path;
            _settings.BackgroundMode = "Video";
            BackgroundModeCombo.SelectedItem = "Video";
            SettingsService.Save(_settings);
            Projection?.ApplyBackground(_settings);
        }

        private static readonly (string Name, string Hex)[] ColorOptions =
        {
            ("White", "#FFFFFFFF"),
            ("Black", "#FF000000"),
            ("Yellow", "#FFFFFF00"),
            ("Red", "#FFFF0000"),
            ("Cyan", "#FF00FFFF"),
        };

        private static string ColorNameFromHex(string hex)
            => ColorOptions.FirstOrDefault(c => c.Hex.Equals(hex, StringComparison.OrdinalIgnoreCase)).Name ?? "White";

        private static string HexFromColorName(string name)
            => ColorOptions.FirstOrDefault(c => c.Name == name).Hex ?? "#FFFFFFFF";

        private void StyleControl_Changed(object sender, SelectionChangedEventArgs e) => ApplyStyleChange();

        private void StyleControl_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args) => ApplyStyleChange();

        private void ApplyStyleChange()
        {
            if (FontCombo.SelectedItem == null || ColorCombo.SelectedItem == null || AlignCombo.SelectedItem == null) return;

            _settings.FontFamily = (string)FontCombo.SelectedItem;
            _settings.FontSize = FontSizeBox.Value;
            _settings.TextColor = HexFromColorName((string)ColorCombo.SelectedItem);
            _settings.TextAlign = (string)AlignCombo.SelectedItem;
            SettingsService.Save(_settings);

            Projection?.ApplyStyle(_settings);
            _onSettingsChanged();
        }

        private void ApplyMonitor()
        {
            if (MonitorCombo.SelectedIndex >= 0) _settings.MonitorIndex = MonitorCombo.SelectedIndex;
            if (Projection != null && MonitorCombo.SelectedItem is MonitorInfo monitor)
            {
                Projection.PlaceOnMonitor(monitor);
                Projection.ApplyTextRegion(GetRegion(monitor));
            }
            UpdateMonitorPreview();
            SettingsService.Save(_settings);
            _onSettingsChanged();
        }

        private RectInt32? GetRegion(MonitorInfo monitor)
        {
            if (_settings.UseFullMonitor) return null;
            return new RectInt32(
                (int)RegionXBox.Value,
                (int)RegionYBox.Value,
                Math.Max(1, (int)RegionWidthBox.Value),
                Math.Max(1, (int)RegionHeightBox.Value));
        }

        private void UpdateRegionEnabled()
        {
            var enabled = FullMonitorCheck.IsChecked != true;
            RegionXBox.IsEnabled = enabled;
            RegionYBox.IsEnabled = enabled;
            RegionWidthBox.IsEnabled = enabled;
            RegionHeightBox.IsEnabled = enabled;
        }

        private void FullMonitorCheck_Click(object sender, RoutedEventArgs e)
        {
            _settings.UseFullMonitor = FullMonitorCheck.IsChecked == true;
            UpdateRegionEnabled();
            ApplyMonitor();
            SettingsService.Save(_settings);
        }

        private void RegionBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            if (_updatingRegionBoxes) return;
            if (MonitorCombo.SelectedItem is not MonitorInfo monitor) return;

            var (x, y, w, h) = ClampRegion(monitor,
                (int)RegionXBox.Value, (int)RegionYBox.Value,
                (int)RegionWidthBox.Value, (int)RegionHeightBox.Value);

            SetRegionBoxes(x, y, w, h);
            ApplyMonitor();
            SettingsService.Save(_settings);
        }

        private void UpdateMonitorPreview()
        {
            if (MonitorCombo.SelectedItem is not MonitorInfo monitor) return;
            var bounds = monitor.Bounds;
            if (bounds.Width <= 0 || bounds.Height <= 0) return;

            var scaleX = MonitorPreviewCanvas.Width > 0 ? MonitorPreviewCanvas.ActualWidth / bounds.Width : 0;
            var scaleY = MonitorPreviewCanvas.ActualHeight / bounds.Height;
            if (scaleX <= 0) scaleX = scaleY;
            if (scaleY <= 0) scaleY = scaleX;
            if (scaleX <= 0 || scaleY <= 0) return;

            var useFull = _settings.UseFullMonitor;
            var x = useFull ? 0 : _settings.RegionX;
            var y = useFull ? 0 : _settings.RegionY;
            var w = useFull ? bounds.Width : Math.Max(1, _settings.RegionWidth);
            var h = useFull ? bounds.Height : Math.Max(1, _settings.RegionHeight);

            Canvas.SetLeft(MonitorPreviewRect, x * scaleX);
            Canvas.SetTop(MonitorPreviewRect, y * scaleY);
            MonitorPreviewRect.Width = w * scaleX;
            MonitorPreviewRect.Height = h * scaleY;

            Canvas.SetLeft(ResizeHandle, x * scaleX + w * scaleX - ResizeHandle.Width);
            Canvas.SetTop(ResizeHandle, y * scaleY + h * scaleY - ResizeHandle.Height);
            ResizeHandle.Visibility = _settings.UseFullMonitor ? Visibility.Collapsed : Visibility.Visible;
        }

        private void MonitorPreviewRect_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (MonitorCombo.SelectedItem is not MonitorInfo || _settings.UseFullMonitor) return;
            _draggingRegion = true;
            _dragStart = e.GetCurrentPoint(MonitorPreviewCanvas).Position;
            _dragStartRegion = new Point(_settings.RegionX, _settings.RegionY);
            MonitorPreviewRect.CapturePointer(e.Pointer);
        }

        private void MonitorPreviewRect_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (!_draggingRegion || MonitorCombo.SelectedItem is not MonitorInfo monitor) return;
            var pos = e.GetCurrentPoint(MonitorPreviewCanvas).Position;
            var scaleX = MonitorPreviewCanvas.ActualWidth / monitor.Bounds.Width;
            var scaleY = MonitorPreviewCanvas.ActualHeight / monitor.Bounds.Height;
            if (scaleX <= 0 || scaleY <= 0) return;

            var dx = (pos.X - _dragStart.X) / scaleX;
            var dy = (pos.Y - _dragStart.Y) / scaleY;

            var regionWidth = Math.Min(_settings.RegionWidth, monitor.Bounds.Width);
            var regionHeight = Math.Min(_settings.RegionHeight, monitor.Bounds.Height);

            var x = (int)Math.Clamp(_dragStartRegion.X + dx, 0, monitor.Bounds.Width - regionWidth);
            var y = (int)Math.Clamp(_dragStartRegion.Y + dy, 0, monitor.Bounds.Height - regionHeight);

            SetRegionBoxes(x, y, regionWidth, regionHeight);
        }

        private void MonitorPreviewRect_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (!_draggingRegion) return;
            _draggingRegion = false;
            MonitorPreviewRect.ReleasePointerCapture(e.Pointer);
            ApplyMonitor();
            SettingsService.Save(_settings);
        }

        private void ResizeHandle_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (MonitorCombo.SelectedItem is not MonitorInfo || _settings.UseFullMonitor) return;
            _resizingRegion = true;
            _dragStart = e.GetCurrentPoint(MonitorPreviewCanvas).Position;
            _dragStartSize = new Point(_settings.RegionWidth, _settings.RegionHeight);
            ResizeHandle.CapturePointer(e.Pointer);
        }

        private void ResizeHandle_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (!_resizingRegion || MonitorCombo.SelectedItem is not MonitorInfo monitor) return;
            var pos = e.GetCurrentPoint(MonitorPreviewCanvas).Position;
            var scaleX = MonitorPreviewCanvas.ActualWidth / monitor.Bounds.Width;
            var scaleY = MonitorPreviewCanvas.ActualHeight / monitor.Bounds.Height;
            if (scaleX <= 0 || scaleY <= 0) return;

            var dx = (pos.X - _dragStart.X) / scaleX;
            var dy = (pos.Y - _dragStart.Y) / scaleY;

            var w = (int)Math.Clamp(_dragStartSize.X + dx, 20, monitor.Bounds.Width - _settings.RegionX);
            var h = (int)Math.Clamp(_dragStartSize.Y + dy, 20, monitor.Bounds.Height - _settings.RegionY);

            SetRegionBoxes(_settings.RegionX, _settings.RegionY, w, h);
        }

        private void ResizeHandle_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (!_resizingRegion) return;
            _resizingRegion = false;
            ResizeHandle.ReleasePointerCapture(e.Pointer);
            ApplyMonitor();
            SettingsService.Save(_settings);
        }

        private void SetRegionBoxes(int x, int y, int w, int h)
        {
            _updatingRegionBoxes = true;
            RegionXBox.Value = x;
            RegionYBox.Value = y;
            RegionWidthBox.Value = w;
            RegionHeightBox.Value = h;
            _updatingRegionBoxes = false;

            _settings.RegionX = x;
            _settings.RegionY = y;
            _settings.RegionWidth = w;
            _settings.RegionHeight = h;
            UpdateMonitorPreview();
        }

        private void MonitorCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyMonitor();
        }

        private void RefreshMonitorsButton_Click(object sender, RoutedEventArgs e)
        {
            LoadMonitors();
            ApplyMonitor();
        }
    }
}
