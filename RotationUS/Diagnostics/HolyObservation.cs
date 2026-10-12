#nullable enable
using System.Drawing;

namespace RotationUS.Diagnostics;

internal sealed class HolyBuffs
{
    internal bool? Infusion, HandOfDivinity, DivinePurpose;
    internal static readonly (string Key,string Label)[] Points = {
        ("holy_i1","圣光灌注Buff"),
        ("holy_hand","神性之手：瞬发圣光术"),
        ("holy_purpose","神圣意志：免费圣能治疗") };
    // Retain existing display IDs and stored coordinates when a point is removed.
    internal static int PointId(string key)=>key switch {
        "holy_i1"=>101,"holy_hand"=>104,"holy_purpose"=>105,
        _=>throw new ArgumentException("未知神圣特殊点",nameof(key)) };
    internal static string Label(string key)=>Points.First(p=>p.Key==key).Label;
    internal static bool UsesColor(string key)=>true;
    internal static string Rule(string key)=>"黑=否；指定色=是；其他色=未知";
    internal static bool? Observe(DiagnosticLayout layout,string key,Func<SamplePoint,Color> read)
    {
        if(!layout.SpecialPoints.TryGetValue(key,out var p)||!p.Enabled||p.ReferenceColor is null) return null;
        var c=read(p.Point);
        if(c.R==0&&c.G==0&&c.B==0) return false;
        return SampleColor.From(c)==p.ReferenceColor ? true : null;
    }
    internal static HolyBuffs Read(DiagnosticLayout layout,Func<SamplePoint,Color> read)=>new() {
        Infusion=Observe(layout,"holy_i1",read),HandOfDivinity=Observe(layout,"holy_hand",read),
        DivinePurpose=Observe(layout,"holy_purpose",read) };
    internal static string InstantSourceHint(DiagnosticLayout layout,Dictionary<int,Color> frames,HolyBuffs external)
    {
        var hints=new List<string>();
        foreach(var (id,key,label,fallback) in new[]{(93,"holy_i1","灌注",external.Infusion),(94,"holy_hand","神性之手",external.HandOfDivinity)}) {
            bool? native=frames.TryGetValue(id,out var c)?FuryBuffs.Boolean(c):null;
            if(native is not null||fallback is not null) continue;
            bool configured=layout.SpecialPoints.TryGetValue(key,out var setting)&&setting.Enabled&&setting.ReferenceColor is not null;
            hints.Add(label+(configured?"颜色不匹配，请重取稳定点":"外部点未配置"));
        }
        return string.Join("；",hints);
    }
    internal static Field[] Fields()
    {
        string[] names={"玩家战斗中","玩家存活且未骑乘","当前目标存活","当前目标友方","当前目标治疗距离内","玩家血量","法力比例","当前目标血量","玩家未施法/引导","玩家静止","食物标记（仅采集）","当前目标可攻击","预留","预留","预留","治疗石可用","治疗药水可用","预留","有队伍","GCD就绪"};
        string[] spells={"审判","神圣震击","荣耀圣令技能族","圣洁鸣钟","圣光闪现","圣光术","复仇之怒","光环掌握","美德道标","圣佑术"};
        var result=Enumerable.Range(1,100).Select(DisplayProtocol.CommonField).ToArray();
        for(int i=1;i<=20;i++) result[i-1]=new(i.ToString(),names[i-1],i is 6 or 7 or 8?ValueKind.Percent:ValueKind.FuryBoolean);
        for(int i=0;i<spells.Length;i++) {
            result[20+i]=new((21+i).ToString(),spells[i]+"就绪",ValueKind.FuryBoolean);
            result[30+i]=new((31+i).ToString(),spells[i]+"可用",ValueKind.FuryBoolean);
        }
        foreach(var (id,item) in HolyItemRange.Probes) result[id-1]=new(id.ToString(),$"5码物品{item}查询",ValueKind.Raw);
        result[12]=new("13","神圣震击至少2充能",ValueKind.FuryBoolean);
        result[40]=new("41","团队模式",ValueKind.FuryBoolean);
        for(int i=1;i<=4;i++) result[40+i]=new((41+i).ToString(),"party"+i+"存活在线且治疗距离内",ValueKind.FuryBoolean);
        for(int i=1;i<=5;i++) result[44+i]=new((45+i).ToString(),"当前目标是"+(i==1?"自己":"party"+(i-1)),ValueKind.FuryBoolean);
        string[] tail={"圣能至少3","圣能5","圣光灌注Buff（接口）","神性之手Buff（接口）","当前目标5码距离（物品查询）","神圣采集器已加载","盾击已学且就绪","盾击可用"};
        for(int i=0;i<tail.Length;i++) result[90+i]=new((91+i).ToString(),tail[i],ValueKind.FuryBoolean);
        result[98]=new("99","当前目标审判距离内",ValueKind.FuryBoolean);
        result[99]=new("100","简化灌注采集器已加载",ValueKind.FuryBoolean);
        return result.Concat(Points.Select(p=>new Field(PointId(p.Key).ToString(),p.Label,ValueKind.Raw))).ToArray();
    }
    internal static string Decode(DiagnosticLayout layout,Field field,Color c)
    {
        if(field.Id is "14" or "15" or "18") return HolyItemRange.Decode(c);
        if(FuryBuffs.SpecialKey(layout,field) is { } key) return Observe(layout,key,_=>c) is { } value?value?"是 · 固定点":"否 · 固定点":"未知 · 未配置或颜色不匹配";
        if(field.Id is "6" or "7" or "8" && (c.R!=c.G||c.G!=c.B)) return "未知（百分比采集失败）";
        return field.Kind==ValueKind.FuryBoolean?FuryBuffs.Boolean(c) is { } v?v?"是":"否":"未知（采集失败/受限）":field.Decode(c);
    }
}

// Dedicated diagnostic palette, mirrored by Classes/Holy.lua. Never used as
// an action boolean; point 95 alone carries the merged three-state verdict.
internal static class HolyItemRange
{
    internal static readonly (int Id,int Item)[] Probes={(14,8149),(15,136605),(18,63427)};
    internal static string Decode(Color c)=>(c.R,c.G,c.B) switch {
        (0,255,0)=>"范围内",
        (255,0,0)=>"范围外",
        (255,127 or 128,0)=>"nil或无效返回",
        (127 or 128,0,255)=>"秘密值",
        (255,0,255)=>"查询报错",
        (0,0,255)=>"未查询（目标无效或可攻击性未知）",
        (0,255,255)=>"物品距离接口缺失",
        (0,0,0)=>"旧采集器或未显示",
        _=>"未知颜色"
    };
}

internal sealed record HealingThresholds(double Critical=.20,double Major=.60,double HealBelow=.85,
    double GroupLow=.70,double GroupCritical=.50,double RaidLowFraction=.30,double RaidCriticalFraction=.20,
    double MinimumCoverage=.75,double HolyLightMana=.30,
    double TollLow=.80,double TollCritical=.60,double CooldownReviewSeconds=2,
    bool AutoWings=true,bool AutoAuraMastery=true,bool AuraMasteryHasToll=false,
    bool RaidAutoWings=true,bool RaidAutoAuraMastery=true)
{
    internal void Validate()
    {
        double[] values={Critical,Major,HealBelow,GroupLow,GroupCritical,RaidLowFraction,RaidCriticalFraction,MinimumCoverage,HolyLightMana,TollLow,TollCritical};
        if(values.Any(v=>!double.IsFinite(v)||v<=0||v>1)||Critical>=Major||Major>=HealBelow||GroupCritical>=GroupLow||TollCritical>=TollLow)
            throw new InvalidDataException("治疗阈值必须在0到1之间，且危急<大掉血<治疗线、群体危急线<群体低血线。");
        if(!double.IsFinite(CooldownReviewSeconds)||CooldownReviewSeconds<.5||CooldownReviewSeconds>10)
            throw new InvalidDataException("冷却观察时间必须在0.5到10秒之间。");
    }
}
internal sealed record GroupInjury(int Alive,int Low,int Critical,int Injured,bool Major,bool Aoe,bool Reliable,int Unknown=0,
    bool Toll=false,int Urgent=0);
internal static class HolyTriage
{
    internal static string Single(double? hp,HealingThresholds t)=>hp is null?"血量未知":hp<t.Critical?"单体危急":hp<t.Major?"单体大掉血":hp<t.HealBelow?"单体普通掉血":"轻微掉血，无需治疗";
    internal static GroupInjury Analyze(Dictionary<int,Color> frames,bool raid,HealingThresholds t)
    {
        int alive=0,low=0,critical=0,injured=0,unknown=0,tollLow=0,tollCritical=0,urgent=0;
        for(int slot=1;slot<=(raid?40:5);slot++) {
            var hp=frames.TryGetValue(50+slot,out var c)?TeamHealthReading.Read(c):new(TeamHealthStatus.Unknown,null);
            if(hp.Status==TeamHealthStatus.Unknown) {unknown++;continue;}
            if(hp.Percent is not >0) continue;
            alive++;if(hp.Percent<t.GroupLow) low++;if(hp.Percent<t.GroupCritical) critical++;if(hp.Percent<t.HealBelow) injured++;
            if(hp.Percent<t.TollLow) tollLow++;if(hp.Percent<t.TollCritical) tollCritical++;if(hp.Percent<t.Critical) urgent++;
        }
        bool reliable=alive>0&&(double)alive/(alive+unknown)>=t.MinimumCoverage;
        bool major=reliable&&(low>=(raid?Math.Max(3,(int)Math.Ceiling(alive*t.RaidLowFraction)):3)||critical>=(raid?Math.Max(2,(int)Math.Ceiling(alive*t.RaidCriticalFraction)):2));
        // The relaxed Toll thresholds apply to parties; raid population scaling is retained.
        bool toll=reliable&&(raid?major:tollLow>=3||tollCritical>=2);
        return new(alive,low,critical,injured,major,reliable&&injured>=(raid?Math.Max(3,(int)Math.Ceiling(alive*.20)):2),reliable,unknown,toll,urgent);
    }
}
