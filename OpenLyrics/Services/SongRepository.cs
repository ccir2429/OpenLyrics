using Microsoft.Data.Sqlite;
using OpenLyrics.Models;
using System.Collections.Generic;
using System.IO;
using Windows.Storage;

namespace OpenLyrics.Services
{
    public class SongRepository
    {
        private readonly string _connectionString;

        public SongRepository()
        {
            string dbPath;
            try
            {
                dbPath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "openlyrics.db");
            }
            catch
            {
                var dir = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "OpenLyrics");
                Directory.CreateDirectory(dir);
                dbPath = Path.Combine(dir, "openlyrics.db");
            }

            _connectionString = new SqliteConnectionStringBuilder { DataSource = dbPath }.ToString();
            Initialize();
        }

        private SqliteConnection OpenConnection()
        {
            var connection = new SqliteConnection(_connectionString);
            connection.Open();
            return connection;
        }

        private void Initialize()
        {
            using var connection = OpenConnection();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS Songs (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Title TEXT NOT NULL,
                    Text TEXT NOT NULL,
                    Details TEXT NOT NULL DEFAULT ''
                );

                CREATE VIRTUAL TABLE IF NOT EXISTS SongsFts USING fts5(
                    Title,
                    Text,
                    content='',
                    tokenize='unicode61 remove_diacritics 2'
                );

                CREATE TRIGGER IF NOT EXISTS Songs_ai AFTER INSERT ON Songs BEGIN
                    INSERT INTO SongsFts(rowid, Title, Text) VALUES (new.Id, new.Title, new.Text);
                END;

                CREATE TRIGGER IF NOT EXISTS Songs_ad AFTER DELETE ON Songs BEGIN
                    INSERT INTO SongsFts(SongsFts, rowid, Title, Text) VALUES ('delete', old.Id, old.Title, old.Text);
                END;

                CREATE TRIGGER IF NOT EXISTS Songs_au AFTER UPDATE ON Songs BEGIN
                    INSERT INTO SongsFts(SongsFts, rowid, Title, Text) VALUES ('delete', old.Id, old.Title, old.Text);
                    INSERT INTO SongsFts(rowid, Title, Text) VALUES (new.Id, new.Title, new.Text);
                END;
            ";
            cmd.ExecuteNonQuery();

            using var migrateCmd = connection.CreateCommand();
            migrateCmd.CommandText = "ALTER TABLE Songs ADD COLUMN Details TEXT NOT NULL DEFAULT ''";
            try { migrateCmd.ExecuteNonQuery(); } catch (SqliteException) { /* column already exists */ }
        }

        public List<Song> GetAll()
        {
            var results = new List<Song>();
            using var connection = OpenConnection();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT Id, Title, Text, Details FROM Songs ORDER BY Title COLLATE NOCASE";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                results.Add(new Song
                {
                    Id = reader.GetInt64(0),
                    Title = reader.GetString(1),
                    Text = reader.GetString(2),
                    Details = reader.GetString(3)
                });
            }
            return results;
        }

        public List<Song> Search(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return GetAll();

            var results = new List<Song>();
            using var connection = OpenConnection();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT s.Id, s.Title, s.Text, s.Details
                FROM SongsFts f
                JOIN Songs s ON s.Id = f.rowid
                WHERE SongsFts MATCH $query
                ORDER BY bm25(SongsFts)";
            cmd.Parameters.AddWithValue("$query", BuildMatchQuery(query));
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                results.Add(new Song
                {
                    Id = reader.GetInt64(0),
                    Title = reader.GetString(1),
                    Text = reader.GetString(2),
                    Details = reader.GetString(3)
                });
            }
            return results;
        }

        private static string BuildMatchQuery(string query)
        {
            var terms = query.Split(' ', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries);
            var escaped = new List<string>();
            foreach (var term in terms)
            {
                var sanitized = term.Replace("\"", string.Empty);
                if (sanitized.Length == 0) continue;
                escaped.Add($"\"{sanitized}\"*");
            }
            return escaped.Count == 0 ? "\"\"" : string.Join(" ", escaped);
        }

        public Song Insert(Song song)
        {
            using var connection = OpenConnection();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "INSERT INTO Songs (Title, Text, Details) VALUES ($title, $text, $details); SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("$title", song.Title);
            cmd.Parameters.AddWithValue("$text", song.Text);
            cmd.Parameters.AddWithValue("$details", song.Details);
            song.Id = (long)cmd.ExecuteScalar()!;
            return song;
        }

        public void Update(Song song)
        {
            using var connection = OpenConnection();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "UPDATE Songs SET Title = $title, Text = $text, Details = $details WHERE Id = $id";
            cmd.Parameters.AddWithValue("$title", song.Title);
            cmd.Parameters.AddWithValue("$text", song.Text);
            cmd.Parameters.AddWithValue("$details", song.Details);
            cmd.Parameters.AddWithValue("$id", song.Id);
            cmd.ExecuteNonQuery();
        }

        public void Delete(long id)
        {
            using var connection = OpenConnection();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM Songs WHERE Id = $id";
            cmd.Parameters.AddWithValue("$id", id);
            cmd.ExecuteNonQuery();
        }

        public void SeedIfEmpty(IEnumerable<Song> seed)
        {
            using var connection = OpenConnection();
            using var countCmd = connection.CreateCommand();
            countCmd.CommandText = "SELECT COUNT(*) FROM Songs";
            var count = (long)countCmd.ExecuteScalar()!;
            if (count > 0) return;

            foreach (var song in seed)
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "INSERT INTO Songs (Title, Text, Details) VALUES ($title, $text, $details)";
                cmd.Parameters.AddWithValue("$title", song.Title);
                cmd.Parameters.AddWithValue("$text", song.Text);
                cmd.Parameters.AddWithValue("$details", song.Details);
                cmd.ExecuteNonQuery();
            }
        }
    }
}
