#nullable enable
using System.Drawing;

namespace RotationUS.Diagnostics;

internal delegate void RotationLogic(Dictionary<int, Color> frames, Dictionary<int, Color> bars, Dictionary<int, bool> states);
internal sealed record ClassProfile(int SpecId, int ClassId, string Key, string ClassName, string SpecName,
    Field[] Fields, Func<int[]> SkipKeys, RotationLogic Process, bool HasSpecial = false)
{
    internal string DisplayName => $"{ClassName} · {SpecName}";
}
internal readonly record struct ClassIdentity(int SpecId, int ClassId)
{
    internal string DisplayName => ClassProfiles.Find(SpecId) is { } p && p.ClassId == ClassId
        ? p.DisplayName : $"{ClassProfiles.ClassName(ClassId)} · 专精 {SpecId}（暂无执行代码）";
}
internal static class ClassProfiles
{
    internal static string ClassName(int id) => id switch
    { 1 => "战士", 2 => "圣骑士", 3 => "猎人", 4 => "潜行者", 5 => "牧师", 6 => "死亡骑士", 7 => "萨满祭司",
      8 => "法师", 9 => "术士", 10 => "武僧", 11 => "德鲁伊", 12 => "恶魔猎手", 13 => "唤魔师", _ => "职业未识别" };
    internal static ClassIdentity Identify(Color color)
    {
        if (color.B is >= 1 and <= 13) return new(color.R + color.G * 256, color.B);
        int spec = Colors.Kind(color) switch { 1 => 73, 3 => 71, 6 => 103, 5 => 104, _ => 0 };
        return new(spec, spec is 71 or 73 ? 1 : spec is 103 or 104 ? 11 : 0);
    }
    internal static Color Encode(int spec, int classId) => Color.FromArgb(spec % 256, spec / 256, classId);
    internal static ClassProfile? Find(int spec) => All.FirstOrDefault(p => p.SpecId == spec);
    internal static ClassProfile Protection => Find(73)!;
    internal static Field[] RawFields { get; } = Enumerable.Range(1, 50).Select(i => new Field(i.ToString(), "未定义", ValueKind.Raw)).ToArray();
    private static Field[] Fields(string definitions, string bars = "", bool soul = false)
    {
        var result = RawFields.ToList();
        foreach (string entry in definitions.Split('|', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = entry.Split('=', 2); string id = parts[0];
            var kind = id[0] == 'P' ? ValueKind.Percent : id[0] == 'C' ? ValueKind.Cooldown : ValueKind.Boolean;
            if (!char.IsDigit(id[0])) id = id[1..];
            result[int.Parse(id) - 1] = new(id, parts[1], kind);
        }
        foreach (string entry in bars.Split('|', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = entry.Split('=', 2); result.Add(new("B" + parts[0], parts[1], ValueKind.Boolean, true));
        }
        if (soul) result.Add(new("51", "灵魂残片：外部特殊点", ValueKind.NonBlack));
        return result.ToArray();
    }
    private static string Resources(int count, string name) => string.Join('|', Enumerable.Range(1, count).Select(i => $"{i}={name}：至少 {i}"));
    internal static ClassProfile[] All { get; } = new ClassProfile[]
    {
        new(73, 1, "fz", "战士", "防护", ProtectionFields.All, () => FZ.Inst.skipKeys, FZ.Inst.Process, true),
        new(72, 1, "kbz", "战士", "狂暴", FuryBuffs.Fields(), () => KBZ.Inst.skipKeys, KBZ.Inst.Process, true),
        new(71, 1, "wqz", "战士", "武器", Fields("1=战斗且可攻击目标|2=敌人≥3|3=5码综合条件|4=10码综合条件|5=15码综合条件|P6=玩家血量|P7=怒气比例|P8=目标血量|9=战场军官追踪|10=目标吸收编码|11=治疗石可用|12=治疗药水可用|C13=天神下凡冷却|C14=巨人打击冷却|C15=横扫攻击冷却|C16=剑刃风暴冷却|C17=致死打击冷却|C18=碎裂投掷冷却|20=建议：撕裂|21=建议：天神下凡|22=撕裂可用|23=致死打击可用|24=斩杀可用|25=猛击可用|26=胜利在望可用|27=建议：英勇投掷", Resources(2, "压制充能")), () => WQZ.Inst.skipKeys, WQZ.Inst.Process),
        new(103, 11, "cat", "德鲁伊", "野性", Fields("1=战斗且可攻击目标|2=敌人≥3|3=5码综合条件|4=13码综合条件|5=友方40码条件|P6=玩家血量|P7=能量比例|8=未施法|9=可驱散减益|10=战场军官追踪|11=餐饮供应商追踪|13=目标施法|14=乌索尔状态|15=静止|16=猛虎之怒状态|17=掠食者状态|18=治疗石可用|19=治疗药水可用|C20=公共冷却|C21=狂暴冷却|C22=万灵之召冷却|C23=猛虎之怒冷却|C24=野性狂乱冷却|C25=技能 Chomp冷却|C26=生存本能冷却|C27=打断冷却|C28=驱散冷却|C29=台风冷却|C30=裂伤冷却|32=驱散可用|33=割裂可用|34=毁灭可用|35=凶猛撕咬可用|36=野性狂乱可用|37=撕碎可用|38=斜掠可用|39=技能 Chomp可用|40=愈合可用|41=建议：猛虎之怒|42=建议：割裂|43=建议：凶猛撕咬|44=建议：撕碎|45=建议：斜掠|46=建议：万灵之召|47=建议：技能 Chomp|48=建议：横扫|49=建议：野性狂乱", Resources(5, "连击点")), () => CAT.Inst.skipKeys, CAT.Inst.Process),
        new(104, 11, "bear", "德鲁伊", "守护", Fields("1=战斗且可攻击目标|2=敌人≥3|3=5码综合条件|4=13码综合条件|5=友方40码条件|P6=玩家血量|P7=怒气比例|8=未施法|9=可驱散减益|10=战场军官追踪|12=目标施法|13=铁鬃状态|14=狂暴回复状态|15=乌索尔状态|16=静止|18=治疗石可用|19=治疗药水可用|C20=公共冷却|C21=痛击冷却|C22=裂伤冷却|C23=月神之怒冷却|C24=狂暴回复冷却|C25=生存本能冷却|C26=打断冷却|C27=驱散冷却|C28=台风冷却|C29=安抚冷却|30=狂暴回复可用|31=驱散可用|32=铁鬃可用|33=毁灭可用|34=技能 Raze可用|35=安抚可用|40=建议：月火术"), () => BEAR.Inst.skipKeys, BEAR.Inst.Process),
        new(252, 6, "udk", "死亡骑士", "邪恶", Fields("1=战斗且可攻击目标|2=敌人≥3|3=5码综合条件|4=15码综合条件|5=20码综合条件|P6=玩家血量|P7=符文能量比例|8=战场军官追踪|9=目标引导|10=目标施法|11=治疗石可用|12=治疗药水可用|C13=冰封之韧冷却|14=灵界打击可用|15=凋零缠绕可用|21=建议：爆发|22=建议：亡者大军|23=建议：黑暗突变|24=建议：腐化|25=建议：灵魂收割|26=建议：脓疮打击|27=建议：凋零缠绕|28=建议：天灾打击|29=建议：传染|30=建议：亡者复生", Resources(6, "符文")), () => UDK.Inst.skipKeys, UDK.Inst.Process),
        new(66, 2, "fq", "圣骑士", "防护", Fields("1=战斗且可攻击目标|2=敌人≥3|3=5码综合条件|4=10码综合条件|5=友方40码条件|P6=玩家血量|P7=法力比例|8=未施法|9=可驱散减益|10=战场军官追踪|11=目标引导|12=目标施法|13=闪耀之光状态|14=静止|18=治疗石可用|19=治疗药水可用|C20=公共冷却|C21=圣洁鸣钟冷却|C22=奉献冷却|C23=审判冷却|C24=愤怒之锤冷却|C25=复仇者之盾冷却|C26=炽热防御者冷却|C27=打断冷却|C28=驱散冷却|C29=祝福之锤冷却|30=圣洁鸣钟可用|31=审判可用|32=愤怒之锤可用|33=祝福之锤可用|34=荣耀圣令可用|35=驱散可用|36=圣光之锤可用|40=建议：奉献", Resources(5, "神圣能量")), () => FQ.Inst.skipKeys, FQ.Inst.Process),
        new(260, 4, "ktz", "潜行者", "狂徒", Fields("1=战斗且可攻击目标|2=敌人≥3|3=5码综合条件|4=20码综合条件|P5=玩家血量|P6=能量比例|7=未施法|8=战场军官追踪|9=目标引导|10=需要延长骰子|15=治疗石可用|16=治疗药水可用|C20=公共冷却|C21=冲动冷却|C22=嫁祸诀窍冷却|C23=脚踢冷却|C24=猩红之瓶冷却|C25=时运延长冷却|30=建议：手枪射击|31=建议：影袭|32=建议：斩击|33=建议：正中眉心|34=建议：剑刃乱舞|35=建议：杀戮盛筵|36=建议：刀锋冲刺|37=建议：命运骨骰", Resources(7, "连击点")), () => KTZ.Inst.skipKeys, KTZ.Inst.Process),
        new(268, 10, "jx", "武僧", "酒仙", Fields("1=战斗且可攻击目标|2=敌人≥3|3=5码综合条件|4=10码综合条件|5=15码综合条件|6=20码综合条件|P7=玩家血量|P8=能量比例|P9=目标血量|10=未施法|11=战场军官追踪|P12=醉拳编码|13=幻灭连击状态|15=治疗石可用|16=治疗药水可用|C20=公共冷却|C21=爆炸酒桶冷却|C22=幻灭踢冷却|C23=醉酿投冷却|C24=火焰之息冷却|C25=移花接木冷却|C26=天神酒冷却|30=移花接木可用|31=轮回之触可用|32=醉酿投可用|33=天神酒可用|34=火焰之息可用|35=猛虎掌可用|36=神鹤引项踢可用|40=建议：幻灭踢|41=建议：醉酿投|42=建议：轮回之触|43=建议：猛虎掌|44=建议：神鹤引项踢|45=建议：火焰之息|46=建议：爆炸酒桶", "1=活血酒：至少1充能|2=活血酒：2充能|3=醉酿投：至少1充能|4=醉酿投：2充能"), () => JX.Inst.skipKeys, JX.Inst.Process),
        new(269, 10, "tf", "武僧", "踏风", Fields("1=战斗且可攻击目标|2=敌人≥3|3=5码综合条件|4=10码综合条件|5=15码综合条件|6=20码综合条件|P7=玩家血量|P8=能量比例|P9=目标血量|10=未施法|11=战场军官追踪|12=引导切割之风|13=活血术状态|15=治疗石可用|16=治疗药水可用|C20=公共冷却|C21=轮回之触冷却|C22=风领主之击冷却|C23=怒雷破冷却|C24=旭日东升踢冷却|C25=切割之风冷却|C26=幻灭踢冷却|C27=移花接木冷却|30=轮回之触可用|31=风领主之击可用|32=怒雷破可用|33=旭日东升踢可用|34=切割之风可用|35=幻灭踢可用|36=猛虎掌可用|37=移花接木可用|38=活血术可用|40=上次幻灭踢状态"), () => TF.Inst.skipKeys, TF.Inst.Process),
        new(581, 12, "dht", "恶魔猎手", "复仇", Fields("1=战斗且可攻击目标|2=敌人≥3|3=5码综合条件|4=10码综合条件|5=15码综合条件|P6=玩家血量|P7=恶魔之怒比例|8=未施法|9=可驱散状态|10=战场军官追踪|11=目标施法|12=静止|13=恶魔尖刺状态|14=餐饮供应商追踪|18=治疗石可用|19=治疗药水可用|C20=公共冷却|C21=打断冷却|C22=恶魔尖刺冷却|C23=地狱火撞击冷却|C24=破裂冷却|C25=灵魂炸弹冷却|C26=烈火烙印冷却|C27=烈焰咒符冷却|C28=献祭光环冷却|C29=怨恨咒符冷却|C30=邪能毁灭冷却|C31=邪能之刃冷却|C32=投掷利刃冷却|C33=驱散冷却|34=灵魂炸弹可用|35=灵魂裂劈可用|36=邪能毁灭可用|40=建议：烈焰咒符", "1=破裂：至少1充能|2=破裂：2充能|3=地狱火撞击：至少1充能|4=地狱火撞击：2充能", true), () => DHT.Inst.skipKeys, DHT.Inst.Process, true),
    };
}
