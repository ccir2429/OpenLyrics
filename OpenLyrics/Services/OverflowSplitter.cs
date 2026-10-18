using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OpenLyrics.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenLyrics.Services
{
    public static class OverflowSplitter
    {
        public static List<string> Apply(List<string> slides, double width, double height, AppSettings settings)
        {
            var safeWidth = width - settings.SafeMarginLeft - settings.SafeMarginRight;
            var safeHeight = height - settings.SafeMarginTop - settings.SafeMarginBottom;
            if (safeWidth <= 0 || safeHeight <= 0) return new List<string>();

            var measurer = new TextBlock
            {
                FontFamily = new Microsoft.UI.Xaml.Media.FontFamily(settings.FontFamily),
                FontSize = settings.FontSize,
                LineHeight = settings.FontSize * settings.LineSpacing,
                LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                Width = safeWidth
            };

            double MeasureHeight(string text)
            {
                measurer.Text = text;
                measurer.Measure(new Windows.Foundation.Size(safeWidth, double.PositiveInfinity));
                return measurer.DesiredSize.Height;
            }

            var lineHeight = settings.FontSize * settings.LineSpacing;
            int GetRowCount(string text) => lineHeight > 0 ? (int)Math.Round(MeasureHeight(text) / lineHeight) : 0;

            bool Overflows(string text)
            {
                if (MeasureHeight(text) > safeHeight) return true;
                if (settings.MaxDisplayRows > 0 && GetRowCount(text) > settings.MaxDisplayRows) return true;
                return false;
            }

            var result = new List<string>();
            foreach (var slide in slides)
            {
                var lines = slide.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
                var current = new List<string>();

                foreach (var line in lines)
                {
                    // A single line that cannot fit on its own, even alone, is dropped entirely
                    // (never shown cut off).
                    if (Overflows(line))
                        continue;

                    var candidate = new List<string>(current) { line };
                    if (Overflows(string.Join("\n", candidate)) && current.Count > 0)
                    {
                        result.Add(string.Join("\n", current));
                        current = new List<string> { line };
                    }
                    else
                    {
                        current = candidate;
                    }
                }

                if (current.Count > 0) result.Add(string.Join("\n", current));
            }

            return result;
        }
    }
}
