using OpenLyrics.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace OpenLyrics.Services
{
    public static class XmlSongImporter
    {
        public static List<Song> Import(string xmlFilePath)
        {
            var songs = new List<Song>();
            if (!File.Exists(xmlFilePath)) return songs;

            var doc = XDocument.Load(xmlFilePath);
            var entries = doc.Descendants("Cantari_Textele");

            foreach (var entry in entries)
            {
                var stearsa = entry.Element("Stearsa")?.Value?.Trim();
                if (stearsa != "0") continue;

                var title = entry.Element("Titlu")?.Value?.Trim() ?? string.Empty;
                var text = entry.Element("Textul")?.Value?.Trim() ?? string.Empty;

                var details = new StringBuilder();
                AppendDetail(details, "Autor", entry.Element("Autor")?.Value);
                AppendDetail(details, "Sursa", entry.Element("Sursa")?.Value);
                AppendDetail(details, "Gama", entry.Element("Gama")?.Value);
                AppendDetail(details, "Tema", entry.Element("Tema")?.Value);
                AppendDetail(details, "Obs", entry.Element("Obs")?.Value);

                songs.Add(new Song
                {
                    Title = title,
                    Text = text,
                    Details = details.ToString()
                });
            }

            return songs;
        }

        private static void AppendDetail(StringBuilder builder, string label, string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            if (builder.Length > 0) builder.Append('\n');
            builder.Append(label).Append(": ").Append(value.Trim());
        }
    }
}
