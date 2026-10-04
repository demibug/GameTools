#nullable enable
using System.Drawing;
using RotationUS.Diagnostics;

internal sealed record FuryDecision(int Key, string Reason, string Branch = "通用", string Mode = "")
{
    internal bool Waiting => Key == 0 && Reason.StartsWith("等待");
}

class KBZ
{
    public static KBZ Inst { get; } = new();
    public int[] skipKeys = FZ.Inst.skipKeys;
    internal FuryBuffs Buffs { get; set; } = new();
    internal FuryDecision LastDecision { get; private set; } = new(0, "等待有效采样");
    public void Process(Dictionary<int, Color> frames, Dictionary<int, Color> bars, Dictionary<int, bool> states)
    {
        LastDecision = Decide(frames, bars, Buffs);
        if (LastDecision.Key != 0) states[LastDecision.Key] = true;
    }
    internal static FuryDecision Decide(Dictionary<int, Color> frames, Dictionary<int, Color> bars, FuryBuffs buff)
    {
        bool? F(int id) => frames.TryGetValue(id, out var c) ? FuryBuffs.Boolean(c) : null;
        bool? B(int id) => bars.TryGetValue(id, out var c) ? FuryBuffs.Boolean(c) : null;
        bool? Hp(double limit) => frames.TryGetValue(6, out var c) && c.R == c.G && c.G == c.B ? c.R / 255.0 <= limit : null;
        bool? Skill(int usable, int ready) => F(usable) & F(ready);
        if (F(42) != true) return new(0, "等待狂暴协议有效数据");
        // Utility and requests do not depend on unknown, unrelated DPS buffs.
        if ((F(3) & Hp(.7) & Skill(14,13) & F(28)) == true) return new(1,"胜利在望：血量≤70%");
        if ((F(1) & F(5) & Hp(.6) & F(16) & F(28)) == true) return new(18,"袋里乾坤：血量≤60%");
        if ((F(4) & Hp(.4) & F(11)) == true) return new(5,"治疗石：血量≤40%");
        if ((F(4) & Hp(.4) & F(12)) == true) return new(6,"治疗药水：血量≤40%");
        if ((F(17) & (!F(1) | !F(18) | !F(15) | !F(3))) == true) return new(29,"取消食物标记");
        if ((F(17) & F(1) & F(18) & F(15) & F(3)) == true) return new(4,"拳击：食物标记请求");
        if ((F(8) & (!F(1) | F(35) & F(36))) == true) return new(28,"取消军官：脱战或两爆发自身冷却");
        if ((F(8) & F(3) & Skill(30,19)) == true) return new(17,"鲁莽：军官标记请求");
        if ((F(8) & F(3) & Skill(31,20)) == true) return new(3,"天神下凡：军官标记请求");
        if ((F(5) & F(10) & F(21) & F(28)) == true) return new(15,"碎裂投掷：目标吸收");
        if (F(3) == false) return (F(5) & F(22) & F(28)) == true ? new(16,"英勇投掷：内置建议兜底") : new(0,"无近战输出条件");
        if ((F(3) & F(28)) != true) return new(0,"等待距离/施法条件或GCD");
        bool? burst = F(29) | buff.Avatar.Present;
        if (burst is null || F(2) is null) return new(0,"等待爆发状态或敌人数");
        bool aoe = F(2) == true, active = burst == true;
        string branch = (aoe ? "多目标" : "单体") + (active ? "爆发" : "非爆发");
        bool? ramp = Skill(23,37), blood = Skill(24,38), execute = Skill(26,40);
        bool? raging = F(25) & (F(43) == true ? B(1) : F(43) == false ? F(39) : null);
        bool? thunderPresence = F(44) == true ? B(3) : buff.Thunder;
        bool? two = F(44) == true ? B(4) : buff.ThunderTwo;
        string mode = buff.ThunderMode == "exact-stacks" && two is null && thunderPresence.HasValue
            ? "presence-only（两层来源失效，已降级）" : buff.ThunderMode;
        bool? hack = F(47) == true ? F(45) : buff.Hack;
        bool? whirlwind = F(48) == true ? F(46) : buff.Whirlwind;
        bool? thunderSkill = Skill(27,41);
        bool? blast = thunderSkill & (F(32) | thunderPresence);
        bool? clap = thunderSkill & !F(32) & !thunderPresence;
        bool? enrageNeeded = !buff.Enrage.Present | buff.Enrage.Expiring;
        bool? avatarEnding = buff.Avatar.Present & (buff.Avatar.Expiring ?? false);
        var rules = new List<(bool? Condition, int Key, string Why)>();
        void Add(bool? condition, int key, string why) => rules.Add((condition,key,why));
        if (!aoe && active)
        {
            Add(ramp & F(7),7,"暴怒：怒气>100");
            Add(blast & ((mode == "exact-stacks" ? two : false) | avatarEnding),11,"雷霆轰击：两层或天神临近结束");
            Add(raging & hack,9,"怒击技能族：Hack and Slash"); Add(blood,8,"嗜血技能族");
            Add(ramp,7,"暴怒"); Add(blast,11,"雷霆轰击"); Add(raging,9,"怒击技能族"); Add(execute,10,"斩杀"); Add(clap,11,"雷霆一击兜底");
        }
        else if (!aoe)
        {
            Add(ramp & (F(7) | enrageNeeded),7,"暴怒：怒气>100或补激怒");
            Add(raging & hack,9,"怒击：Hack and Slash"); Add(blast,11,"雷霆轰击"); Add(ramp,7,"暴怒");
            Add(blood,8,"嗜血"); Add(raging,9,"怒击"); Add(execute,10,"斩杀"); Add(clap,11,"雷霆一击兜底");
        }
        else if (active)
        {
            Add(ramp & !buff.Enrage.Present,7,"暴怒：激怒缺失"); Add(blast,11,"雷霆轰击");
            Add(clap & F(9),11,"雷霆一击：敌人>6"); Add(ramp & F(7),7,"暴怒：怒气>100");
            Add(blood,8,"嗜血技能族"); Add(raging & hack,9,"怒击技能族：Hack and Slash");
            Add(ramp,7,"暴怒"); Add(clap,11,"雷霆一击"); Add(raging,9,"怒击技能族"); Add(execute,10,"斩杀");
        }
        else
        {
            Add(blast,11,"雷霆轰击"); Add(clap & !whirlwind,11,"雷霆一击：顺劈增益缺失");
            Add(ramp & (F(7) | enrageNeeded),7,"暴怒：怒气>100或补激怒"); Add(blood,8,"嗜血");
            Add(raging & hack,9,"怒击：Hack and Slash"); Add(execute,10,"斩杀");
            Add(ramp,7,"暴怒"); Add(raging,9,"怒击"); Add(clap,11,"雷霆一击兜底");
        }
        foreach (var rule in rules)
        {
            if (rule.Condition == true) return new(rule.Key,rule.Why,branch,mode);
            if (rule.Condition is null) return new(0,"等待状态：" + rule.Why,branch,mode);
        }
        return (F(5) & F(22) & F(28)) == true ? new(16,"英勇投掷：内置建议兜底",branch,mode) : new(0,"暂无可施放技能",branch,mode);
    }
}
