using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using OpenLyrics.Models;
using OpenLyrics.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Windows.Graphics;

namespace OpenLyrics
{
    public sealed partial class MainWindow : Window
    {
        private readonly SongRepository _repository = new();
        private readonly ObservableCollection<Song> _libraryView = new();
        private readonly ObservableCollection<Song> _session = new();
        private readonly ObservableCollection<string> _slides = new();
        private Song? _currentSong;
        private bool _loadingEditor;
        private ProjectionWindow? _projection;
        private SettingsWindow? _settingsWindow;
        private IReadOnlyList<MonitorInfo> _monitors = new List<MonitorInfo>();
        private readonly AppSettings _settings = SettingsService.Load();
        private IOutput? Output => _projection;

        public MainWindow()
        {
            InitializeComponent();

            Closed += (_, _) =>
            {
                SettingsService.Save(_settings);
                _projection?.Close();
                _settingsWindow?.Close();
            };

            var xmlPath = Path.Combine(AppContext.BaseDirectory, "Cantari_Textele.xml");
            _repository.SeedIfEmpty(XmlSongImporter.Import(xmlPath));
            SessionList.ItemsSource = _session;
            LibraryList.ItemsSource = _libraryView;
            SlidesList.ItemsSource = _slides;
            ApplySearch(string.Empty);
            LoadMonitors();
        }



        private void ApplySearch(string query)
        {
            var q = query?.Trim() ?? string.Empty;
            var results = _repository.Search(q);

            _libraryView.Clear();
            foreach (var song in results) _libraryView.Add(song);
        }

        private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
                ApplySearch(sender.Text);
        }

        private void AddSongToSession(Song? song)
        {
            if (song == null) return;
            if (!_session.Contains(song)) _session.Add(song);
            SessionList.SelectedItem = song;
        }

        private void AddToSessionButton_Click(object sender, RoutedEventArgs e)
            => AddSongToSession(LibraryList.SelectedItem as Song);

        private void LibraryList_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
            => AddSongToSession(LibraryList.SelectedItem as Song);

        private void RemoveFromSessionButton_Click(object sender, RoutedEventArgs e)
        {
            if (SessionList.SelectedItem is not Song song) return;
            _session.Remove(song);
            if (_currentSong == song) LoadSong(null);
        }

        private void SessionList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SessionList.SelectedItem is Song song) LoadSong(song);
        }

        private void LibraryList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LibraryList.SelectedItem is Song song) LoadSong(song);
        }

        private void NewSongButton_Click(object sender, RoutedEventArgs e)
        {
            LoadSong(new Song { Title = string.Empty, Text = string.Empty });
        }

        private void SaveSongButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentSong == null) return;
            _currentSong.Title = string.IsNullOrWhiteSpace(EditorTitle.Text) ? "Untitled" : EditorTitle.Text.Trim();
            _currentSong.Text = LyricsEditor.Text;
            // Description is kept on the in-memory song for this session only and is
            // intentionally excluded from repository Insert/Update.
            if (_currentSong.Id == 0)
            {
                _repository.Insert(_currentSong);
            }
            else
            {
                _repository.Update(_currentSong);
            }
            EditorTitle.Text = _currentSong.Title;
            ApplySearch(SearchBox.Text);
            StatusText.Text = "Saved";
        }

        private void DeleteSongButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentSong == null || _currentSong.Id == 0) return;
            _repository.Delete(_currentSong.Id);
            _session.Remove(_currentSong);
            LoadSong(null);
            ApplySearch(SearchBox.Text);
            StatusText.Text = "Deleted";
        }

        private void LoadSong(Song? song)
        {
            _currentSong = song;
            _loadingEditor = true;
            EditorTitle.Text = song?.Title ?? string.Empty;
            EditorDescription.Text = song?.Description ?? string.Empty;
            LyricsEditor.Text = song?.Text ?? string.Empty;
            _loadingEditor = false;
            RefreshSlides();
        }

        private void EditorTitle_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_loadingEditor || _currentSong == null) return;
            _currentSong.Title = EditorTitle.Text;
        }

        private void EditorDescription_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_loadingEditor || _currentSong == null) return;
            // Session-only: kept on the in-memory Song but never persisted to the repository.
            _currentSong.Description = EditorDescription.Text;
        }

        private void LyricsEditor_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_loadingEditor) return;
            if (_currentSong != null) _currentSong.Text = LyricsEditor.Text;
            RefreshSlides();
            StatusText.Text = $"{LyricsEditor.Text.Length} characters";
        }

        private void RefreshSlides()
        {
            var previous = SlidesList.SelectedIndex;
            _slides.Clear();
            var slides = SlideSplitter.Split(LyricsEditor.Text, _settings);

            if ((_settings.AutoSplitOverflow || _settings.MaxDisplayRows > 0) && GetSelectedMonitor() is MonitorInfo monitor)
            {
                var region = GetRegion(monitor);
                var width = region?.Width ?? monitor.Bounds.Width;
                var height = region?.Height ?? monitor.Bounds.Height;
                slides = OverflowSplitter.Apply(slides, width, height, _settings);
            }

            foreach (var slide in slides) _slides.Add(slide);

            if (_slides.Count == 0)
            {
                PreviewText.Text = string.Empty;
            }
            else
            {
                SlidesList.SelectedIndex = previous >= 0 && previous < _slides.Count ? previous : 0;
            }
        }

        private void SlidesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            PreviewText.Text = SlidesList.SelectedItem as string ?? string.Empty;
            Output?.SetText(PreviewText.Text);
        }

        private async void ImportXmlButton_Click(object sender, RoutedEventArgs e)
        {
            var picker = new Windows.Storage.Pickers.FileOpenPicker();
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            picker.FileTypeFilter.Add(".xml");

            var file = await picker.PickSingleFileAsync();
            if (file == null) return;

            var songs = XmlSongImporter.Import(file.Path);
            if (songs.Count == 0)
            {
                StatusText.Text = "No songs found in XML";
                return;
            }

            var importWindow = new ImportSongsWindow(songs, selected =>
            {
                foreach (var song in selected)
                {
                    _repository.Insert(song);
                }
                ApplySearch(SearchBox.Text);
                StatusText.Text = $"Imported {selected.Count} song(s) from XML";
            });
            importWindow.Activate();
        }

        private void ClearProjectionAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            Output?.Clear();
            args.Handled = true;
        }

        private void CloseProjectorAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            _projection?.Close();
            args.Handled = true;
        }

        private void ProjectButton_Click(object sender, RoutedEventArgs e)
        {
            if (_projection == null)
            {
                _projection = new ProjectionWindow();
                _projection.Closed += (_, _) => _projection = null;
            }
            _projection.ApplyStyle(_settings);
            _projection.ApplyBackground(_settings);
            ApplyMonitor();
            _projection.SetText(PreviewText.Text);
            _projection.Activate();
            StatusText.Text = "Projecting";
        }

        private void LoadMonitors()
        {
            _monitors = MonitorService.GetMonitors();
        }

        private MonitorInfo? GetSelectedMonitor()
        {
            if (_monitors.Count == 0) return null;
            var index = _settings.MonitorIndex;
            return _monitors[index >= 0 && index < _monitors.Count ? index : 0];
        }

        private RectInt32? GetRegion(MonitorInfo monitor)
        {
            if (_settings.UseFullMonitor) return null;
            return new RectInt32(
                _settings.RegionX,
                _settings.RegionY,
                Math.Max(1, _settings.RegionWidth),
                Math.Max(1, _settings.RegionHeight));
        }

        private void ApplyMonitor()
        {
            LoadMonitors();
            if (_projection != null && GetSelectedMonitor() is MonitorInfo monitor)
            {
                _projection.PlaceOnMonitor(monitor);
                _projection.ApplyTextRegion(GetRegion(monitor));
            }
            if (_settings.AutoSplitOverflow) RefreshSlides();
        }

        private void PrevButton_Click(object sender, RoutedEventArgs e)
        {
            if (SlidesList.SelectedIndex > 0) SlidesList.SelectedIndex--;
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            if (SlidesList.SelectedIndex < _slides.Count - 1) SlidesList.SelectedIndex++;
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            Output?.Clear();
            StatusText.Text = "Projection cleared";
        }

        private void CloseProjectionButton_Click(object sender, RoutedEventArgs e)
        {
            _projection?.Close();
            _projection = null;
            StatusText.Text = "Projection closed";
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            if (_settingsWindow == null)
            {
                _settingsWindow = new SettingsWindow(_settings, () => _projection, OnSettingsChanged);
                _settingsWindow.Closed += (_, _) => _settingsWindow = null;
            }
            _settingsWindow.Activate();
        }

        private void OnSettingsChanged()
        {
            ApplyMonitor();
            RefreshSlides();
        }
    }
}
