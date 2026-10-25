#nullable enable
using System.Drawing;

namespace RotationUS.Diagnostics;

internal sealed class RetBuffs
{
    internal BuffObservation Art, Legacy, FreeHammer, Ruling, Wings, Sentence, Expurgation;
    // Same order/IDs as HijiAddon.retAuras. Fixed-point IDs 101..107 must remain stable.
    internal sealed record AuraIdentity(string Key,string Name,int AuraId,int TalentId,string Unit,string Note);
    internal static readonly AuraIdentity[] Auras = {
        new("ret_art","战争艺术",406086,406064,"player","两层；不要取天赋406064"),
        new("ret_legacy","苍穹之遗",387178,387170,"player","增益有无"),
        new("ret_free","圣光裁决",433732,425518,"player","免费大锤机会；叠层433674不作临期点；触发状态看B7"),
        new("ret_ruling","公允裁定",432629,432626,"player","急速增益；≤1.5GCD"),
        new("ret_wings","复仇之怒",454351,458359,"player","灰烬触发；≤2GCD；462048是光辉荣耀被动替换技能"),
        new("ret_sentence","处决宣判",343527,343527,"target","仅旧构筑；自己的目标减益"),
        new("ret_expurgation","异端逐除",383346,383344,"target","自己的目标减益；神圣烈焰406545是另一个天赋"),
    };
    internal static readonly (string Key,string Label)[] Points =
        Auras.Select(a=>(a.Key,a.Name+" "+a.AuraId)).ToArray();
    internal static string Identity(string key,bool radiant=true)
    {
        var a=Auras.Single(a=>a.Key==key);
        int auraId=key=="ret_wings"&&!radiant?31884:a.AuraId;
        int talentId=key=="ret_wings"&&!radiant?31884:a.TalentId;
        string note=key=="ret_wings"&&!radiant?"非光辉荣耀构筑；按旧分支观察":a.Note;
        return $"{a.Name}：光环ID {auraId}；天赋/技能ID {talentId}；"+
            (a.Unit=="target"?"目标上自己施加的减益":"自身增益")+"；"+note;
    }
    internal static bool UsesGcdColor(string key)=>key is "ret_free" or "ret_wings" or "ret_ruling";
    internal static bool UsesColor(string key)=>UsesGcdColor(key)||key is "ret_art" or "ret_sentence";
    internal static string Comparison(string key)=>UsesGcdColor(key)?"ret-gcd":UsesColor(key)?"expiring":"presence";
    internal static string Label(string key,bool radiant=true)=>
        key=="ret_wings"&&!radiant?"复仇之怒 31884":Points.First(p=>p.Key==key).Label;
    internal static string Threshold(string key)=>key=="ret_art"?"两层":key=="ret_ruling"?"≤1.5GCD":
        UsesGcdColor(key)?"≤2GCD":"≤3秒";
    internal static string Rule(string key)=>key=="ret_free"?"免费机会；指定色=≤2GCD":
        UsesColor(key)?"黑=无；指定色="+Threshold(key):"黑=无；非黑=有";
    // Old profiles retain their coordinates/presence, but a <=3-second reference
    // is not silently reinterpreted as a haste-dependent GCD reference.
    internal static bool? RadiantExpiry(BuffObservation value)=>
        value.Present==false?false:value.Source=="固定点（需重配GCD临期色）"?null:value.Expiring;
    internal static BuffObservation Observe(DiagnosticLayout layout,string key,Func<SamplePoint,Color> read)
    {
        if(!layout.SpecialPoints.TryGetValue(key,out var p)||!p.Enabled) return new(null,null,"特殊点未配置/停用");
        Color c;
        try { c=read(p.Point); } catch(ArgumentException) { return new(null,null,"采样无效"); }
        bool present=c.R!=0||c.G!=0||c.B!=0;
        bool? match=!present?false:UsesColor(key)&&(p.Comparison is "expiring" or "ret-gcd")&&p.ReferenceColor is {} reference
            ?SampleColor.From(c)==reference:null;
        return new(present,match,UsesGcdColor(key)&&p.Comparison!="ret-gcd"?"固定点（需重配GCD临期色）":"固定点");
    }
    internal static bool IsRadiant(DiagnosticLayout layout,Func<SamplePoint,Color> read)=>
        FuryBuffs.Boolean(read(layout.Bars[10]))==true;
    internal static IEnumerable<(string Key,string Label)> ActivePoints(bool radiant)=>
        Points.Where(p=>!radiant||p.Key!="ret_sentence");
    internal static RetBuffs Read(DiagnosticLayout layout,Func<SamplePoint,Color> read)=>new() {
        Art=Observe(layout,"ret_art",read),Legacy=Observe(layout,"ret_legacy",read),FreeHammer=Observe(layout,"ret_free",read),
        Ruling=Observe(layout,"ret_ruling",read),Wings=Observe(layout,"ret_wings",read),
        Sentence=IsRadiant(layout,read)?new(null,null,"当前天赋不使用"):Observe(layout,"ret_sentence",read),
        Expurgation=Observe(layout,"ret_expurgation",read) };
    internal static readonly Color Signature=Color.FromArgb(70,5,2);
    internal static int? TargetEpoch(Dictionary<int,Color> bars)=>bars.TryGetValue(14,out var c)&&c.G==70&&c.B==1&&c.R>0?c.R:null;
    internal static string Decode(DiagnosticLayout layout,Field field,Color color,bool radiant=false)
    {
        string? key=FuryBuffs.SpecialKey(layout,field);
        if(key is not null) {
            var o=Observe(layout,key,_=>color);
            if(radiant&&key=="ret_free")
                return o.Present!=true?"临期未知；触发状态见B7":RadiantExpiry(o) is {} expiry?
                    expiry?"≤2GCD；触发状态见B7":"未临期；触发状态见B7":"临期未知 · 需重配GCD临期色；触发状态见B7";
            if(o.Present==true&&o.Source=="固定点（需重配GCD临期色）") return "存在 · 需重配GCD临期色";
            return o.Present is null?"未知 · "+o.Source:o.Present==false?"缺失 · 固定点":!UsesColor(key)?"存在 · 固定点":
                o.Expiring is null?"存在 · 参考色未配置":o.Expiring==true?Threshold(key)+" · 固定点":"存在 · 未匹配条件色";
        }
        if(field.Id=="19") return color.ToArgb()==Signature.ToArgb()?"惩戒协议 v5":"未知协议，请 /reload";
        if(field.Id=="B14") return color.G==70&&color.B==1&&color.R>0?$"当前目标版本 {color.R}":"目标版本未知";
        if(field.Id=="6"&&!(color.R==color.G&&color.G==color.B)) return "未知（血量采集失败）";
        return field.Kind==ValueKind.FuryBoolean?FuryBuffs.Boolean(color) is {} value?value?"是":"否":"未知（采集失败/受限）":field.Decode(color);
    }
    internal static Field[] Fields()
    {
        string[] names={"玩家战斗中","玩家存活未骑乘","当前敌对存活目标","未读条/引导（物品与责难可例外）","制裁之锤10码内战斗敌人≥2","玩家血量",
            "神圣能量≥3","神圣能量≥5","圣能来源有效","军官标记（翅膀＋处决）","食物标记（责难）","目标施法/引导","目标可打断（仅诊断）",
            "治疗石可用","治疗药水可用","GCD就绪","审判为愤怒之锤形态","翅膀自身冷却中","惩戒协议版本"};
        var result=Enumerable.Range(1,50).Select(i=>new Field(i.ToString(),i<=19?names[i-1]:"预留",i==6?ValueKind.Percent:i==19?ValueKind.Raw:ValueKind.FuryBoolean)).ToList();
        string[] skills={"复仇之怒","处决宣判","圣光之锤","最终审判","神圣风暴","灰烬觉醒","圣洁鸣钟","公正之剑","审判技能族","责难"};
        for(int i=0;i<10;i++) {
            result[19+i*2]=new((20+i*2).ToString(),skills[i]+"自身就绪",ValueKind.FuryBoolean);
            result[20+i*2]=new((21+i*2).ToString(),skills[i]+"可用",ValueKind.FuryBoolean);
            if(i>0) result[38+i]=new((39+i).ToString(),skills[i]+(i<9?"制裁之锤10码内":"自身技能范围内"),ValueKind.FuryBoolean);
        }
        result[48]=new("49","翅膀自身剩余CD>5秒",ValueKind.FuryBoolean);
        result[49]=new("50","惩戒本轮发布有效",ValueKind.FuryBoolean);
        result.AddRange(Enumerable.Range(51,50).Select(DisplayProtocol.CommonField));
        for(int i=1;i<=5;i++) result.Add(new("B"+i,"神圣能量≥"+i,ValueKind.FuryBoolean,true));
        string[] flags={"灰烬已替换为锤","圣光之锤实际为免费形态","翅膀已学","处决已学","神圣烈焰已学","光辉荣耀已学（自动爆发）",
            "圣殿英雄已学","自动十字军打击已学","当前目标版本","苍穹之遗已学","处决自身冷却中","圣光裁决已学","战争艺术已学",
            "自身血量<80%","自身血量<60%","荣耀圣令自身就绪","荣耀圣令可用","圣光闪现自身就绪","圣光闪现可用",
            "圣佑术技能族自身就绪","圣佑术技能族可用","圣光闪现当前施法时间为零","复仇之怒目标在制裁之锤10码内"};
        for(int i=0;i<flags.Length;i++) result.Add(new("B"+(i+6),flags[i],i==8?ValueKind.Raw:ValueKind.FuryBoolean,true));
        for(int i=0;i<skills.Length;i++) result.Add(new("B"+(30+i),skills[i]+"未报告资源不足",ValueKind.FuryBoolean,true));
        foreach(var entry in new[]{(40,"荣耀圣令"),(41,"圣光闪现"),(42,"圣佑术")})
            result.Add(new("B"+entry.Item1,entry.Item2+"未报告资源不足",ValueKind.FuryBoolean,true));
        result.Add(new("B43","灰烬基础技能自身冷却中（忽略GCD与按钮替换）",ValueKind.FuryBoolean,true));
        result.AddRange(Points.Select((p,i)=>new Field((101+i).ToString(),p.Label,ValueKind.Raw)));
        return result.ToArray();
    }
}
