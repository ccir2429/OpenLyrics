using System;
using System.Collections.Generic;
using System.Text;

namespace OpenLyrics.Services
{
    public static class SlideSplitter
    {
        public static List<string> Split(string text, OpenLyrics.Models.AppSettings settings)
        {
            var delimiters = new HashSet<string>();
            foreach (var d in settings.SlideDelimiters)
                if (!string.IsNullOrWhiteSpace(d)) delimiters.Add(d.Trim());
            var slides = new List<string>();
            var current = new StringBuilder();

            void Flush()
            {
                var slide = current.ToString().Trim();
                if (slide.Length > 0) slides.Add(slide);
                current.Clear();
            }

            foreach (var line in (text ?? string.Empty).Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None))
            {
                var trimmed = line.Trim();
                if ((trimmed.Length == 0 && settings.BlankLineSeparates) || delimiters.Contains(trimmed))
                {
                    Flush();
                }
                else
                {
                    current.AppendLine(trimmed);
                }
            }
            Flush();
            return slides;
        }
    }
}
