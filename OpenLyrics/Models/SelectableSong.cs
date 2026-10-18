using System.ComponentModel;

namespace OpenLyrics.Models
{
    public class SelectableSong : INotifyPropertyChanged
    {
        private bool _isSelected = true;

        public Song Song { get; }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value) return;
                _isSelected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }

        public string Title => Song.Title;

        public SelectableSong(Song song)
        {
            Song = song;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
