#nullable enable
using System.Drawing;
using System.Text.Json;

namespace RotationUS.Diagnostics;

internal sealed record SamplePoint(int X, int Y)
{
    public Point Point => new(X, Y);
}

internal sealed record SampleColor(byte R, byte G, byte B)
{
    public static SampleColor From(Color color) => new(color.R, color.G, color.B);
    public Color ToColor() => Color.FromArgb(R, G, B);
    public override string ToString() => $"{R}, {G}, {B}";
}

internal sealed class SpecialPointSetting
{
    public SamplePoint Point { get; set; } = new(530, 8);
    public bool Enabled { get; set; }
    public string Comparison { get; set; } = "presence";
    public SampleColor? ReferenceColor { get; set; }
    public SpecialPointSetting Copy() => new() { Point = Point with { }, Enabled = Enabled, Comparison = Comparison, ReferenceColor = ReferenceColor };
}

internal static class MarkerLayouts
{
    internal const string Legacy = "legacy";
    internal const string PreviousWide = "wide-v1";
    internal const string Wide = "wide-v2";
    internal static double Offset(string kind) => kind switch { Legacy => 55.5, PreviousWide => 68.5, Wide => 73.5, _ => throw new InvalidDataException("未知标记布局。") };
    internal static SamplePoint[] Points(int left, double cell, int y, string kind) =>
        Enumerable.Range(0, 3).Select(n => new SamplePoint((int)Math.Floor(left + (Offset(kind) + n) * cell), y)).ToArray();
}

internal sealed class DiagnosticLayout
{
    public int Version { get; set; } = 1;
    public int SpecId { get; set; }
    public int ClientWidth { get; set; }
    public int ClientHeight { get; set; }
    public SamplePoint[] Frames { get; set; } = Array.Empty<SamplePoint>();
    public SamplePoint[] Bars { get; set; } = Array.Empty<SamplePoint>();
    public SamplePoint[] Markers { get; set; } = Array.Empty<SamplePoint>();
    public SamplePoint Special { get; set; } = new(610, 8);
    public bool SpecialEnabled { get; set; }
    public SampleColor? SpecialReferenceColor { get; set; }
    public string MarkerLayout { get; set; } = MarkerLayouts.Legacy;
    public Dictionary<string, SpecialPointSetting> SpecialPoints { get; set; } = new();
    public string ThunderMode { get; set; } = "presence-only";
    public DiagnosticLayout Copy() => new()
    {
        Version = Version, SpecId = SpecId, ClientWidth = ClientWidth, ClientHeight = ClientHeight,
        Frames = Frames.ToArray(), Bars = Bars.ToArray(), Markers = Markers.ToArray(), Special = Special, SpecialEnabled = SpecialEnabled,
        SpecialReferenceColor = SpecialReferenceColor, MarkerLayout = MarkerLayout, ThunderMode = ThunderMode,
        SpecialPoints = SpecialPoints.ToDictionary(p => p.Key, p => p.Value.Copy())
    };
    public void Validate()
    {
        if (Version != 1 || ClientWidth <= 0 || ClientHeight <= 0 || Frames is null || Bars is null || Markers is null || Special is null ||
            Frames.Length != 50 || Bars.Length != 50 || Markers.Length != 3)
            throw new InvalidDataException("布局格式不正确，请重新校准。");
        _ = MarkerLayouts.Offset(MarkerLayout);
        if (SpecialPoints is null || ThunderMode is not ("presence-only" or "exact-stacks")) throw new InvalidDataException("特殊点配置格式不正确。");
        foreach (var setting in SpecialPoints.Values)
        {
            if (setting is null || setting.Point is null || setting.Comparison is not ("presence" or "expiring")) throw new InvalidDataException("特殊点判断类型不正确。");
            if (setting.Comparison=="expiring" && setting.ReferenceColor == new SampleColor(0, 0, 0)) throw new InvalidDataException("临近结束参考色必须非黑。");
            if (setting.Enabled && setting.Comparison == "expiring" && setting.ReferenceColor is null) throw new InvalidDataException("请先采集临近结束参考色。");
        }
        foreach (var p in Frames.Concat(Bars).Concat(Markers).Concat(SpecialEnabled ? new[] { Special } : Array.Empty<SamplePoint>()).Concat(SpecialPoints.Values.Where(s => s.Enabled).Select(s => s.Point)))
            if (p is null || p.X < 0 || p.Y < 0 || p.X >= ClientWidth || p.Y >= ClientHeight)
                throw new InvalidDataException("布局点位超出游戏客户区。");
        if (Frames.Select(p => p.X).Distinct().Count() != 50 || Bars.Select(p => p.X).Distinct().Count() != 50)
            throw new InvalidDataException("布局包含重复点位。");
    }
    public Rectangle CaptureBounds
    {
        get
        {
            Validate();
            var pts = Frames.Concat(Bars).Concat(Markers).Concat(SpecialEnabled ? new[] { Special } : Array.Empty<SamplePoint>()).Concat(SpecialPoints.Values.Where(s => s.Enabled).Select(s => s.Point)).ToArray();
            int left = Math.Max(0, pts.Min(p => p.X) - 6), top = Math.Max(0, pts.Min(p => p.Y) - 3);
            return Rectangle.FromLTRB(left, top, Math.Min(ClientWidth, pts.Max(p => p.X) + 7), Math.Min(ClientHeight, pts.Max(p => p.Y) + 5));
        }
    }
    public void Save(string path) { Validate(); File.WriteAllText(path, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true })); }
    public static DiagnosticLayout Load(string path)
    {
        var value = JsonSerializer.Deserialize<DiagnosticLayout>(File.ReadAllText(path)) ?? throw new InvalidDataException("布局为空。");
        value.Validate(); return value;
    }
}

internal static class Colors
{
    // Calibration tolerates small display differences; live values retain the legacy R == 255 rule.
    public static int Kind(Color c)
    {
        int r = c.R > 200 ? 1 : c.R < 60 ? 0 : -1;
        int g = c.G > 200 ? 1 : c.G < 60 ? 0 : -1;
        int b = c.B > 200 ? 1 : c.B < 60 ? 0 : -1;
        return r < 0 || g < 0 || b < 0 ? -1 : (r << 2) | (g << 1) | b;
    }
    public static string Rgb(Color c) => $"{c.R}, {c.G}, {c.B}";
    public static int Spec(Color c) => ClassProfiles.Identify(c).SpecId;
}

internal static class Calibration
{
    private sealed record Run(int Start, int End, int Kind);
    private static List<Run> Runs(Bitmap image, int y)
    {
        var result = new List<Run>(); int start = 0, previous = Colors.Kind(image.GetPixel(0, y));
        for (int x = 1; x <= image.Width; x++)
        {
            int next = x < image.Width ? Colors.Kind(image.GetPixel(x, y)) : -99;
            if (next == previous) continue;
            result.Add(new(start, x, previous)); start = x; previous = next;
        }
        return result;
    }
    private static bool Match(List<Run> runs, int offset, int[] pattern)
    {
        if (offset + 50 > runs.Count) return false;
        var widths = runs.Skip(offset).Take(50).Select(r => r.End - r.Start).ToArray();
        double mean = widths.Average();
        if (mean < 2 || mean > 80 || widths.Any(w => Math.Abs(w - mean) > Math.Max(1.1, mean * .2))) return false;
        return Enumerable.Range(0, 50).All(i => runs[offset + i].Kind == pattern[i % pattern.Length]);
    }
    public static DiagnosticLayout Find(Bitmap image, Size client)
    {
        for (int y = 0; y < Math.Min(image.Height, 96); y++)
        {
            var runs = Runs(image, y);
            for (int i = 0; i + 50 <= runs.Count; i++)
            {
                if (!Match(runs, i, new[] { 2, 1, 4 })) continue;
                var row = runs.Skip(i).Take(50).ToArray();
                int left = row[0].Start, right = row[^1].End;
                double cell = (right - left) / 50.0;
                for (int by = y + 1; by <= Math.Min(image.Height - 1, y + 16); by++)
                {
                    var lower = Runs(image, by);
                    int j = lower.FindIndex(r => r.Start == left && r.Kind == 5);
                    if (j < 0 || !Match(lower, j, new[] { 5, 3 }) || lower[j + 49].End != right) continue;
                    var candidates = new[] { MarkerLayouts.Legacy, MarkerLayouts.PreviousWide, MarkerLayouts.Wide }.Select(kind => (kind, points: MarkerLayouts.Points(left, cell, y, kind)))
                        .Where(c => c.points.All(p => p.X < image.Width) && Colors.Kind(image.GetPixel(c.points[0].X, y)) == 4 &&
                            Colors.Kind(image.GetPixel(c.points[2].X, y)) is (3 or 5) &&
                            ClassProfiles.Identify(image.GetPixel(c.points[1].X, y)).ClassId != 0).ToArray();
                    if (candidates.Length > 1) throw new InvalidDataException("新旧标记布局同时匹配，请移开遮挡并重新定位；执行已关闭。");
                    if (candidates.Length == 0) continue;
                    if (candidates[0].kind != MarkerLayouts.Wide)
                        throw new InvalidDataException("所有职业需使用统一右侧布局 wide-v2。请更新插件、/reload 后重新定位；旧布局不再用于执行。");
                    var markers = candidates[0].points;
                    var layout = new DiagnosticLayout
                    {
                        ClientWidth = client.Width, ClientHeight = client.Height,
                        Frames = row.Select(r => new SamplePoint((r.Start + r.End - 1) / 2, y)).ToArray(),
                        Bars = lower.Skip(j).Take(50).Select(r => new SamplePoint((r.Start + r.End - 1) / 2, by)).ToArray(),
                        Markers = markers, MarkerLayout = candidates[0].kind
                    };
                    layout.Validate(); return layout;
                }
            }
        }
        throw new InvalidDataException("未找到完整的 50 个彩色点及第二行。请 /reload 后输入 /hiji locate on，确保顶部色条未被遮挡；UI 缩放过小使两行合并时需调大缩放。");
    }
}

internal sealed class Heartbeat
{
    private int previous = -1;
    private double changedAt;
    private bool changed;
    public void Reset() { previous = -1; changedAt = 0; changed = false; }
    public bool Observe(Color color, double now)
    {
        int kind = Colors.Kind(color);
        if (kind is not (3 or 5)) { Reset(); return false; }
        if (previous == -1) { previous = kind; changedAt = now; return false; }
        if (previous != kind) { previous = kind; changedAt = now; changed = true; }
        return changed && now - changedAt < 1.5;
    }
}

internal enum ValueKind { Boolean, Percent, Cooldown, Raw, Special, NonBlack, FuryBoolean }
internal sealed record Field(string Id, string Name, ValueKind Kind, bool Bar = false)
{
    public string Decode(Color color, SampleColor? specialReference = null) => Kind switch
    {
        ValueKind.Percent => $"{color.R / 255.0:P1}",
        ValueKind.Cooldown => $"编码 {color.R / 255.0:P1}；旧规则{(color.R == 255 ? "就绪" : "未就绪")}",
        ValueKind.Boolean => color.R == 255 ? "是" : "否",
        ValueKind.Special => $"需要施放无视苦痛：{(FZ.IsIgnorePainNeeded(color, specialReference?.ToColor()) ? "是" : "否")}",
        ValueKind.NonBlack => color.R != 0 || color.G != 0 || color.B != 0 ? "是（非黑）" : "否（纯黑）",
        _ => "原始颜色（未定义）"
    };
}
internal static class ProtectionFields
{
    public static Field[] All { get; } = Build();
    private static Field[] Build()
    {
        string[] names = { "战斗且可攻击目标", "附近敌人 ≥ 3", "5 码综合条件", "10 码综合条件", "15 码综合条件", "玩家血量", "怒气比例", "战场军官追踪", "队伍或团队", "目标吸收编码", "治疗石可用", "治疗药水可用", "胜利在望冷却", "盾墙冷却", "盾牌冲锋冷却", "挫志怒吼冷却", "雷霆一击冷却", "天神下凡冷却", "碎裂投掷冷却", "拳击冷却", "建议：盾牌猛击", "建议：雷霆一击", "建议：复仇", "建议：斩杀", "建议：英勇投掷", "胜利在望可用", "袋里乾坤冷却", "餐饮供应商追踪", "目标施法或引导" };
        var fields = Enumerable.Range(1, 50).Select(n => new Field(n.ToString(), n == 30 ? "怒气≥80（原始值阈值）" : n == 31 ? "盾牌冲锋请求已完成（自身冷却/未学习）" : n == 32 ? "天神请求已完成（自身冷却/未学习）" : n <= names.Length ? names[n - 1] : "未定义", n is >= 30 and <= 32 ? ValueKind.FuryBoolean : n is 6 or 7 ? ValueKind.Percent : n is >= 13 and <= 20 or 27 ? ValueKind.Cooldown : n <= 29 ? ValueKind.Boolean : ValueKind.Raw)).ToList();
        fields.Add(new("B1", "盾牌格挡：至少 1 充能", ValueKind.Boolean, true));
        fields.Add(new("B2", "盾牌格挡：2 充能", ValueKind.Boolean, true));
        fields.Add(new("51", "无视苦痛：特殊判断点", ValueKind.Special));
        return fields.ToArray();
    }
}
