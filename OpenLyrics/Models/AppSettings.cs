using System.Collections.Generic;

namespace OpenLyrics.Models
{
    public class AppSettings
    {
        public List<string> SlideDelimiters { get; set; } = new() { "---" };
        public bool BlankLineSeparates { get; set; } = true;
        public bool AutoSplitOverflow { get; set; } = false;
        public int MaxDisplayRows { get; set; } = 0;

        public int MonitorIndex { get; set; }
        public bool UseFullMonitor { get; set; } = true;
        public int RegionX { get; set; }
        public int RegionY { get; set; }
        public int RegionWidth { get; set; }
        public int RegionHeight { get; set; }

        public string FontFamily { get; set; } = "Segoe UI";
        public double FontSize { get; set; } = 64;
        public string TextColor { get; set; } = "#FFFFFFFF";
        public string TextAlign { get; set; } = "Center";
        public double LineSpacing { get; set; } = 1.2;

        public double SafeMarginLeft { get; set; } = 0;
        public double SafeMarginTop { get; set; } = 0;
        public double SafeMarginRight { get; set; } = 0;
        public double SafeMarginBottom { get; set; } = 0;

        public string BackgroundMode { get; set; } = "Transparent";
        public string BackgroundColor { get; set; } = "#FF000000";
        public string BackgroundImagePath { get; set; } = string.Empty;
        public string BackgroundVideoPath { get; set; } = string.Empty;
    }
}
