using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OpenLyrics.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace OpenLyrics
{
    public sealed partial class ImportSongsWindow : Window
    {
        private readonly ObservableCollection<SelectableSong> _songs;
        private readonly Action<List<Song>> _onConfirm;

        public ImportSongsWindow(List<Song> songs, Action<List<Song>> onConfirm)
        {
            InitializeComponent();
            _onConfirm = onConfirm;
            _songs = new ObservableCollection<SelectableSong>(songs.Select(s => new SelectableSong(s)));
            SongsList.ItemsSource = _songs;
            HeaderText.Text = $"Select songs to import ({_songs.Count} found)";
        }

        private void SelectAllButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (var song in _songs) song.IsSelected = true;
        }

        private void SelectNoneButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (var song in _songs) song.IsSelected = false;
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            var selected = _songs.Where(s => s.IsSelected).Select(s => s.Song).ToList();
            _onConfirm(selected);
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
