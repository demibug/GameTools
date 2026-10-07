#nullable enable
using System.Drawing;
using RotationUS.Diagnostics;

internal sealed record ArmsDecision(int Key, string Reason, string Branch = "通用");

class WQZ
{
    public static WQZ Inst { get; } = new();
    public int[] skipKeys = FZ.Inst.skipKeys;
    internal ArmsBuffs Buffs { get; set; } = new();
    internal ArmsDecision LastDecision { get; private set; } = new(0, "等待有效采样");
    public void Process(Dictionary<int, Color> frames, Dictionary<int, Color> bars, Dictionary<int, bool> states)
    {
        LastDecision = Decide(frames, bars, Buffs);
        if (LastDecision.Key != 0) states[LastDecision.Key] = true;
    }
    internal static ArmsDecision Decide(Dictionary<int, Color> frames, Dictionary<int, Color> bars, ArmsBuffs buff)
    {
        bool? F(int id) => frames.TryGetValue(id, out var c) ? FuryBuffs.Boolean(c) : null;
        bool? B(int id) => bars.TryGetValue(id, out var c) ? FuryBuffs.Boolean(c) : null;
        bool? Hp(double limit, bool inclusive = true) => frames.TryGetValue(6, out var c) && c.R == c.G && c.G == c.B
            ? inclusive ? c.R / 255.0 <= limit : c.R / 255.0 < limit : null;
        bool? Skill(int usable, int ready) => F(usable) & F(ready);
        if (F(39) != true) return new(0, "等待武器协议有效数据");
        if (F(50) != true) return new(0, "武器通用字段未加载，请在游戏执行 /reload");
        if ((F(4) & F(43) & Skill(32,19)) == true) return new(2,"剑在人在：血量<50%");
        if ((F(3) & Hp(.75,false) & Skill(31,21) & F(20)) == true) return new(1,"胜利在望/乘胜追击：血量<75%");
        if ((F(1) & F(5) & Hp(.8,false) & F(47) & F(20)) == true) return new(18,"袋里乾坤：血量<80%");
        if ((F(4) & Hp(.4) & F(11)) == true) return new(5,"治疗石：血量≤40%");
        if ((F(4) & Hp(.4) & F(12)) == true) return new(6,"治疗药水：血量≤40%");
        if ((F(44) & (!F(1) | !F(45) | !F(46) | !F(3))) == true) return new(29,"取消食物标记");
        if ((F(44) & F(1) & F(45) & F(46) & F(3)) == true) return new(4,"拳击：食物标记请求");
        // Arms has Avatar only; reuse Fury's request/cooldown-confirmation pattern.
        if ((F(48) & (!F(1) | F(49))) == true) return new(28,"取消军官：脱战或天神自身冷却");
        if ((F(48) & F(3) & Skill(22,13)) == true) return new(3,"天神下凡：军官标记请求");
        if ((F(5) & F(10) & F(18) & F(20)) == true) return new(15,"碎裂投掷：目标吸收");
        if (F(3) == false) return (F(5) & F(40) & F(20)) == true ? new(16,"英勇投掷：内置建议兜底") : new(0,"无近战输出条件");
        if ((F(3) & F(20)) != true) return new(0,"等待距离/施法条件或公共冷却");
        if (F(2) is null) return new(0,"等待敌人数");
        bool aoe = F(2) == true;
        if (!aoe && F(42) is null) return new(0,"等待目标血量阈值");
        bool executePhase = !aoe && F(42) == true;
        string branch = aoe ? "多目标" : executePhase ? "单体斩杀" : "单体";
        bool? smash=Skill(23,14), sweep=Skill(24,15), blade=Skill(25,16);
        bool? mortal=Skill(26,17), cleave=Skill(27,33), execute=Skill(28,34), slam=Skill(30,36);
        bool? overpower=F(29) & (F(38) == true ? B(1) : null);
        bool? rendTenNeeded=!buff.RendTen.Present | buff.RendTen.Expiring;
        bool? rendFiveNeeded=!buff.RendFive.Present | buff.RendFive.Expiring;
        var rules = new List<(bool? Condition, int Key, string Why)>();
        void Add(bool? condition, int key, string why) => rules.Add((condition,key,why));
        if (aoe)
        {
            Add(sweep,9,"横扫攻击");
            Add(cleave & !buff.RendTen.Present,12,"顺劈斩：补撕裂");
            Add(smash,8,"巨人打击");
            Add(cleave & buff.Collateral.Expiring,12,"顺劈斩：间接伤害三层");
            Add(blade,7,"剑刃风暴");
            Add(execute & buff.SuddenDeath.Expiring,14,"斩杀：猝死两层");
            Add(cleave,12,"顺劈斩");
            Add(overpower & B(2),10,"压制：两充能");
            Add(execute & buff.SuddenDeath.Present,14,"斩杀：猝死存在");
            Add(overpower,10,"压制"); Add(execute,14,"斩杀");
            Add(mortal,11,"致死打击"); Add(slam,13,"猛击技能族兜底");
        }
        else if (executePhase)
        {
            Add(smash,8,"巨人打击"); Add(slam & F(37),13,"英勇打击");
            Add(blade & buff.Smash,7,"剑刃风暴：巨人打击期间");
            Add(mortal & buff.Precision.Expiring,11,"致死打击：刽子手的精准两层");
            Add(execute & (buff.SuddenDeath.Present | F(7)),14,"斩杀：猝死或怒气>40");
            Add(overpower,10,"压制"); Add(execute,14,"斩杀兜底");
        }
        else
        {
            Add(cleave & rendTenNeeded & F(14),12,"顺劈斩：撕裂缺失/不足10秒且巨人打击就绪");
            Add(smash,8,"巨人打击");
            Add(execute & buff.SuddenDeath.Expiring,14,"斩杀：猝死两层");
            Add(execute & blade & buff.Smash & !buff.Demise.Expiring & buff.SuddenDeath.Present,
                14,"斩杀：剑刃风暴前补殒命在即");
            Add(blade & buff.Smash,7,"剑刃风暴：巨人打击期间");
            Add(slam & F(37),13,"英勇打击"); Add(mortal,11,"致死打击");
            Add(execute & buff.SuddenDeath.Present,14,"斩杀：猝死存在");
            Add(overpower,10,"压制");
            Add(cleave & rendFiveNeeded,12,"顺劈斩：撕裂缺失/不足5秒");
            Add(slam,13,"猛击技能族兜底");
        }
        foreach (var rule in rules)
        {
            if (rule.Condition == true) return new(rule.Key,rule.Why,branch);
            if (rule.Condition is null) return new(0,"等待状态："+rule.Why,branch);
        }
        return (F(5) & F(40) & F(20)) == true ? new(16,"英勇投掷：内置建议兜底",branch) : new(0,"暂无可施放技能",branch);
    }
}
