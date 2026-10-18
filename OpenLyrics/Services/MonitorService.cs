using Microsoft.UI.Windowing;
using System.Collections.Generic;
using Windows.Graphics;

namespace OpenLyrics.Services
{
    public sealed class MonitorInfo
    {
        public string Name { get; init; } = string.Empty;
        public RectInt32 Bounds { get; init; }

        public override string ToString() => Name;
    }

    public static class MonitorService
    {
        public static IReadOnlyList<MonitorInfo> GetMonitors()
        {
            var result = new List<MonitorInfo>();
            var areas = DisplayArea.FindAll();
            for (var i = 0; i < areas.Count; i++)
            {
                var b = areas[i].OuterBounds;
                var primary = areas[i].IsPrimary ? " (primary)" : string.Empty;
                result.Add(new MonitorInfo
                {
                    Name = $"Monitor {i + 1}{primary} - {b.Width}x{b.Height} at {b.X},{b.Y}",
                    Bounds = b
                });
            }
            return result;
        }
    }
}
