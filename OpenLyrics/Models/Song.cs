namespace OpenLyrics.Models
{
    public class Song
    {
        public long Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;

        // Additional metadata (Autor, Sursa, Gama, Tema, Obs) persisted as a single column.
        public string Details { get; set; } = string.Empty;

        // Session-only note; never persisted to the repository.
        public string Description { get; set; } = string.Empty;
    }
}
