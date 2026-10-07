#nullable enable
using System.Drawing;

namespace RotationUS.Diagnostics;

// Same fixed-point model as FuryBuffs, with weapon-warrior matching conditions.
internal sealed class ArmsBuffs
{
    internal BuffObservation RendTen, RendFive, SuddenDeath, Demise, Precision, Collateral;
    internal bool? Smash;
    internal static readonly (string Key, string Label)[] Points = {
        ("rend10", "撕裂不足10秒"), ("rend5", "撕裂不足5秒"), ("smash", "巨人打击"),
        ("sudden", "猝死两层"), ("demise", "殒命在即三层"),
        ("precision", "刽子手的精准两层"), ("collateral", "间接伤害三层") };
    internal static bool UsesColor(string key) => key != "smash";
    internal static string Label(string key) => Points.First(p => p.Key == key).Label;
    internal static string Rule(string key) => key switch {
        "rend10" => "黑=无；指定色=不足10秒",
        "rend5" => "黑=无；指定色=不足5秒",
        "smash" => "黑=无；非黑=有",
        "sudden" or "precision" => "黑=无；指定色=两层",
        _ => "黑=无；指定色=三层" };
    internal static BuffObservation Observe(DiagnosticLayout layout, string key, Func<SamplePoint, Color> read)
    {
        if (!layout.SpecialPoints.TryGetValue(key, out var point) || !point.Enabled)
            return new(null, null, "特殊点未配置/停用");
        Color color;
        try { color = read(point.Point); }
        catch (ArgumentException) { return new(null, null, "采样无效"); }
        bool present = color.R != 0 || color.G != 0 || color.B != 0;
        bool? matched = !present ? false : UsesColor(key) && point.Comparison == "expiring" && point.ReferenceColor is { } reference
            ? SampleColor.From(color) == reference : null;
        return new(present, matched, "固定点");
    }
    internal static ArmsBuffs Read(DiagnosticLayout layout, Func<SamplePoint, Color> read) => new() {
        RendTen = Observe(layout, "rend10", read), RendFive = Observe(layout, "rend5", read),
        Smash = Observe(layout, "smash", read).Present,
        SuddenDeath = Observe(layout, "sudden", read), Demise = Observe(layout, "demise", read),
        Precision = Observe(layout, "precision", read), Collateral = Observe(layout, "collateral", read) };
    internal static string Decode(DiagnosticLayout layout, Field field, Color color, Func<SamplePoint, Color>? read = null)
    {
        if (field.Bar)
        {
            bool? valid = read is null ? null : FuryBuffs.Boolean(read(layout.Frames[37]));
            return valid == true ? FuryBuffs.Boolean(color) is { } v ? v ? "是 · 充能进度条" : "否 · 充能进度条" : "未知（条像素无效）"
                : "未知（压制充能来源无效）";
        }
        if (!field.Id.StartsWith("S:"))
            return (field.Id is "6" or "8") && (color.R != color.G || color.G != color.B) ? "未知（血量采集失败）"
                : field.Kind == ValueKind.FuryBoolean ? FuryBuffs.Boolean(color) is { } value ? value ? "是" : "否" : "未知（采集失败/受限）"
                : field.Decode(color);
        string key = field.Id[2..];
        var observation = Observe(layout, key, _ => color);
        if (observation.Present is null) return "未知 · " + observation.Source;
        if (observation.Present == false) return "缺失 · 固定点";
        if (key == "smash") return "存在 · 固定点";
        if (observation.Expiring is null) return "存在 · 参考色未配置";
        return (key switch {
            "rend10" => observation.Expiring == true ? "存在 · 不足10秒" : "存在 · 未进入不足10秒状态",
            "rend5" => observation.Expiring == true ? "存在 · 不足5秒" : "存在 · 未进入不足5秒状态",
            "sudden" => observation.Expiring == true ? "两层" : "一层",
            "precision" => observation.Expiring == true ? "两层" : "不足两层",
            _ => observation.Expiring == true ? "三层" : "不足三层" }) + " · 固定点";
    }
    internal static Field[] Fields()
    {
        string[] names = { "战斗且敌对目标", "敌人≥2（姓名板估算）", "5码综合条件", "10码综合条件", "15码综合条件",
            "玩家血量", "怒气>40（原始值阈值）", "目标血量", "未定义", "目标有吸收", "治疗石可用", "治疗药水可用",
            "天神下凡就绪", "巨人打击就绪", "横扫攻击就绪", "剑刃风暴就绪", "致死打击就绪", "碎裂投掷就绪",
            "剑在人在就绪", "公共冷却就绪", "胜利在望就绪", "天神下凡可用", "巨人打击可用", "横扫攻击可用",
            "剑刃风暴可用", "致死打击可用", "顺劈斩可用", "斩杀可用", "压制可用", "猛击技能族可用",
            "胜利在望可用", "剑在人在可用", "顺劈斩就绪", "斩杀就绪", "压制就绪", "猛击技能族就绪",
            "猛击已升级英勇打击", "压制充能来源有效", "武器协议有效", "建议英勇投掷", "碎裂投掷可用", "目标血量低于35%", "玩家血量低于50%",
            "食物标记", "目标正在施法/引导", "拳击就绪", "袋里乾坤就绪", "军官标记", "天神下凡自身冷却中", "武器通用字段已加载（需重载插件）" };
        var result = Enumerable.Range(1, 50).Select(i => new Field(i.ToString(), i <= names.Length ? names[i-1] : "未定义",
            i is 6 or 8 ? ValueKind.Percent : i <= names.Length && i != 9 ? ValueKind.FuryBoolean : ValueKind.Raw)).ToList();
        result.Add(new("B1", "压制至少一充能", ValueKind.FuryBoolean, true));
        result.Add(new("B2", "压制两充能", ValueKind.FuryBoolean, true));
        result.AddRange(Points.Select(p => new Field("S:" + p.Key, p.Label, ValueKind.Raw)));
        return result.ToArray();
    }
}
