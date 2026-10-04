#nullable enable
using System.Drawing;

namespace RotationUS.Diagnostics;

internal readonly record struct BuffObservation(bool? Present, bool? Expiring, string Source);
internal sealed class FuryBuffs
{
    internal BuffObservation Enrage, Avatar;
    internal bool? Thunder, ThunderTwo, Hack, Whirlwind;
    internal string ThunderMode = "presence-only";
    internal static readonly (string Key, string Label)[] Points = {
        ("enrage", "激怒"), ("thunder", "雷霆轰击"), ("hack", "劈斩"),
        ("whirlwind", "旋风斩"), ("avatar", "天神下凡") };
    internal static bool UsesTimeColor(string key) => key is "enrage" or "avatar";
    internal static string Label(string key) => Points.First(p=>p.Key==key).Label;
    internal static bool? Boolean(Color color) => color.R == 255 && color.G == 255 && color.B == 255 ? true :
        color.R == 0 && color.G == 0 && color.B == 0 ? false : null;
    internal static BuffObservation Observe(DiagnosticLayout layout, string key, Func<SamplePoint, Color> read)
    {
        if (!layout.SpecialPoints.TryGetValue(key, out var point) || !point.Enabled)
            return new(null, null, "特殊点未配置/停用");
        Color color;
        try { color = read(point.Point); } catch (ArgumentException) { return new(null, null, "采样无效"); }
        bool present = color.R != 0 || color.G != 0 || color.B != 0;
        bool? expiring = !present ? false : UsesTimeColor(key) && point.Comparison == "expiring" && point.ReferenceColor is { } reference
            ? SampleColor.From(color) == reference : null;
        return new(present, expiring, "固定点");
    }
    internal static FuryBuffs Read(DiagnosticLayout layout, Func<SamplePoint, Color> read)
    {
        var thunder = Observe(layout, "thunder", read);
        return new() { Enrage = Observe(layout, "enrage", read), Avatar = Observe(layout, "avatar", read),
            Thunder = thunder.Present, ThunderTwo = thunder.Present == false ? false : null,
            Hack = Observe(layout, "hack", read).Present, Whirlwind = Observe(layout, "whirlwind", read).Present,
            ThunderMode = layout.ThunderMode };
    }
    internal static SamplePoint? Point(DiagnosticLayout layout, Field field) => field.Id.StartsWith("S:")
        ? layout.SpecialPoints.TryGetValue(field.Id[2..], out var p) && p.Enabled ? p.Point : null
        : field.Bar ? layout.Bars[int.Parse(field.Id[1..]) - 1]
        : field.Id == "51" ? layout.SpecialEnabled ? layout.Special : null : layout.Frames[int.Parse(field.Id) - 1];
    internal static string Decode(DiagnosticLayout layout, Field field, Color color, Func<SamplePoint,Color>? read = null)
    {
        if(layout.SpecId==72 && field.Bar)
        {
            int flag=int.Parse(field.Id[1..])<=2?43:44;
            bool? valid=read is null?null:Boolean(read(layout.Frames[flag-1]));
            if(valid!=true) return valid==false?"未知（接口条未启用）":"未知（接口条采集失败/受限）";
            return Boolean(color) is { } barValue ? (barValue?"是":"否")+" · 接口进度条" : "未知（条像素无效）";
        }
        if (!field.Id.StartsWith("S:")) return layout.SpecId == 72 && field.Id == "6" && (color.R != color.G || color.G != color.B) ? "未知（血量采集失败）" : field.Kind == ValueKind.FuryBoolean
            ? Boolean(color) is { } v ? v ? "是" : "否" : "未知（采集失败/受限）" : field.Decode(color, layout.SpecialReferenceColor);
        string key = field.Id[2..];
        if (!layout.SpecialPoints.TryGetValue(key, out var setting)) return "未知 · 未配置";
        bool present = color.R != 0 || color.G != 0 || color.B != 0;
        string state = !present ? "缺失" : UsesTimeColor(key) && setting.Comparison == "expiring" && setting.ReferenceColor == SampleColor.From(color)
            ? "存在 · 临近结束" : "存在";
        return $"{state} · 固定点" + (key.StartsWith("thunder") ? $" · {layout.ThunderMode}" : "");
    }
    internal static Field[] Fields()
    {
        string[] names = { "战斗且敌对目标", "敌人≥3（姓名板估算）", "5码综合条件", "10码综合条件", "15码综合条件", "玩家血量", "怒气>100（原始值阈值）",
            "军官标记", "敌人>6（姓名板估算）", "目标有吸收", "治疗石可用", "治疗药水可用", "胜利在望就绪", "胜利在望可用", "拳击就绪", "袋里乾坤就绪",
            "食物标记", "目标正在施法/引导", "鲁莽自身冷却就绪", "天神自身冷却就绪", "碎裂投掷就绪", "建议英勇投掷", "暴怒可用", "嗜血技能族可用", "怒击技能族可用",
            "斩杀可用", "雷霆技能族可用", "GCD就绪", "鲁莽存在（纯计时）", "鲁莽可用", "天神可用", "雷霆强化形态", "Bloodbath形态", "Crushing Blow形态",
            "鲁莽自身冷却中", "天神自身冷却中", "暴怒就绪", "嗜血技能族就绪", "怒击技能族就绪（无充能时）", "斩杀就绪", "雷霆技能族就绪", "狂暴协议有效", "怒击充能来源有效", "精确层数条来源有效", "Hack and Slash（接口）", "旋风斩增益（接口）", "Hack接口来源有效", "旋风斩接口来源有效" };
        var result = Enumerable.Range(1, 50).Select(i => new Field(i.ToString(), i <= names.Length ? names[i-1] : "未定义",
            i == 6 ? ValueKind.Percent : i <= names.Length ? ValueKind.FuryBoolean : ValueKind.Raw)).ToList();
        result.Add(new("B1", "怒击至少一充能", ValueKind.FuryBoolean, true));
        result.Add(new("B2", "怒击两充能", ValueKind.FuryBoolean, true));
        result.Add(new("B3", "雷霆轰击至少一层（接口）", ValueKind.FuryBoolean, true));
        result.Add(new("B4", "雷霆轰击两层（接口）", ValueKind.FuryBoolean, true));
        result.AddRange(Points.Select(p => new Field("S:" + p.Key, p.Label, ValueKind.Raw)));
        return result.ToArray();
    }
}
