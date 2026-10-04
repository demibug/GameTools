#nullable enable
using System.Drawing;

namespace RotationUS.Diagnostics;

internal sealed record PointCheck(string Id, string Name, SamplePoint Point, string Expected, Color Actual, bool Passed);

internal static class PositionVerification
{
    private static string Name(int kind) => kind switch { 0 => "黑", 1 => "蓝", 2 => "绿", 3 => "青", 4 => "红", 5 => "紫", 6 => "黄", _ => "未知" };
    public static PointCheck[] Check(DiagnosticLayout layout, Func<SamplePoint, Color> read)
    {
        var checks = new List<PointCheck>();
        void Add(string id, string name, SamplePoint point, params int[] allowed)
        {
            var actual = read(point);
            checks.Add(new(id, name, point, string.Join("/", allowed.Select(Name)), actual, allowed.Contains(Colors.Kind(actual))));
        }
        for (int i = 0; i < 50; i++) Add((i + 1).ToString(), "第一行定位色块", layout.Frames[i], new[] { 2, 1, 4 }[i % 3]);
        for (int i = 0; i < 50; i++) Add("B" + (i + 1), "第二行定位色块", layout.Bars[i], i % 2 == 0 ? 5 : 3);
        Add("M1", "定位测试标记", layout.Markers[0], 4);
        var identityColor = read(layout.Markers[1]);
        checks.Add(new("M2", "职业 / 专精标记", layout.Markers[1], "职业专精编码", identityColor,
            ClassProfiles.Identify(identityColor).ClassId != 0 || identityColor.ToArgb() == Color.Black.ToArgb()));
        Add("M3", "更新心跳标记", layout.Markers[2], 3, 5);
        return checks.ToArray();
    }
    public static bool SameGeometry(DiagnosticLayout a, DiagnosticLayout b) =>
        a.ClientWidth == b.ClientWidth && a.ClientHeight == b.ClientHeight &&
        a.Frames.SequenceEqual(b.Frames) && a.Bars.SequenceEqual(b.Bars) && a.Markers.SequenceEqual(b.Markers);
    public static bool CompatibleSpecialGeometry(DiagnosticLayout a, DiagnosticLayout b) =>
        a.MarkerLayout == MarkerLayouts.Wide && b.MarkerLayout == MarkerLayouts.Wide &&
        a.ClientWidth == b.ClientWidth && a.ClientHeight == b.ClientHeight &&
        a.Frames.SequenceEqual(b.Frames) && a.Bars.SequenceEqual(b.Bars) &&
        (a.Markers.SequenceEqual(b.Markers) || KnownMarkerGeometry(a) && KnownMarkerGeometry(b));
    private static bool KnownMarkerGeometry(DiagnosticLayout layout)
    {
        double cell = (layout.Frames[^1].X - layout.Frames[0].X) / 49.0;
        double start = layout.Frames[0].X - cell / 2;
        // Rounded cells can shift the inferred edge by one pixel.
        return layout.Markers.Select((p, i) => Math.Abs(p.X - (start + (MarkerLayouts.Offset(layout.MarkerLayout) + i) * cell)) <= 2 && p.Y == layout.Frames[0].Y).All(v => v);
    }
    public static Captured Crop(Captured scan, DiagnosticLayout layout)
    {
        // Point 51 has no calibration pattern; localization never uses it.
        var points = layout.Frames.Concat(layout.Bars).Concat(layout.Markers).ToArray();
        var area = Rectangle.FromLTRB(Math.Max(0, points.Min(p => p.X) - 6), Math.Max(0, points.Min(p => p.Y) - 3),
            Math.Min(scan.Bounds.Right, points.Max(p => p.X) + 7), Math.Min(scan.Bounds.Bottom, points.Max(p => p.Y) + 5));
        var relative = new Rectangle(area.X - scan.Bounds.X, area.Y - scan.Bounds.Y, area.Width, area.Height);
        return new(scan.Image.Clone(relative, scan.Image.PixelFormat), area);
    }
}

internal sealed class StableLocator
{
    private DiagnosticLayout? previous;
    private int count;
    public void Reset() { previous = null; count = 0; }
    public bool Observe(DiagnosticLayout candidate)
    {
        count = previous is not null && PositionVerification.SameGeometry(previous, candidate) ? count + 1 : 1;
        previous = candidate;
        return count >= 2;
    }
}
