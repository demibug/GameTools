#nullable enable
using System.Drawing;

namespace RotationUS.Diagnostics;

internal sealed class RetBuffs
{
    internal BuffObservation Art, Legacy, FreeHammer, Ruling, Wings, Sentence, Expurgation;
    internal static readonly (string Key,string Label)[] Points = {
        ("ret_art","战争艺术两层"),("ret_legacy","天穹之遗"),("ret_free","免费圣光之锤（≤3秒色）"),
        ("ret_ruling","无可争议的统治"),("ret_wings","复仇之怒（≤3秒色）"),
        ("ret_sentence","当前目标处决（≤3秒色）"),("ret_expurgation","当前目标灼烧") };
    internal static bool UsesColor(string key)=>key is "ret_art" or "ret_free" or "ret_wings" or "ret_sentence";
    internal static string Label(string key)=>Points.First(p=>p.Key==key).Label;
    internal static string Rule(string key)=>key=="ret_art"?"黑=无；指定色=两层":UsesColor(key)?"黑=无；指定色=≤3秒":"黑=无；非黑=有";
    internal static BuffObservation Observe(DiagnosticLayout layout,string key,Func<SamplePoint,Color> read)
    {
        if(!layout.SpecialPoints.TryGetValue(key,out var p)||!p.Enabled) return new(null,null,"特殊点未配置/停用");
        Color c;
        try { c=read(p.Point); } catch(ArgumentException) { return new(null,null,"采样无效"); }
        bool present=c.R!=0||c.G!=0||c.B!=0;
        bool? match=!present?false:UsesColor(key)&&p.Comparison=="expiring"&&p.ReferenceColor is {} reference
            ?SampleColor.From(c)==reference:null;
        return new(present,match,"固定点");
    }
    internal static RetBuffs Read(DiagnosticLayout layout,Func<SamplePoint,Color> read)=>new() {
        Art=Observe(layout,"ret_art",read),Legacy=Observe(layout,"ret_legacy",read),FreeHammer=Observe(layout,"ret_free",read),
        Ruling=Observe(layout,"ret_ruling",read),Wings=Observe(layout,"ret_wings",read),
        Sentence=Observe(layout,"ret_sentence",read),Expurgation=Observe(layout,"ret_expurgation",read) };
    internal static readonly Color Signature=Color.FromArgb(70,2,2);
    internal static int? TargetEpoch(Dictionary<int,Color> bars)=>bars.TryGetValue(14,out var c)&&c.G==70&&c.B==1&&c.R>0?c.R:null;
    internal static string Decode(DiagnosticLayout layout,Field field,Color color)
    {
        string? key=FuryBuffs.SpecialKey(layout,field);
        if(key is not null) {
            var o=Observe(layout,key,_=>color);
            return o.Present is null?"未知 · "+o.Source:o.Present==false?"缺失 · 固定点":!UsesColor(key)?"存在 · 固定点":
                o.Expiring is null?"存在 · 参考色未配置":o.Expiring==true?(key=="ret_art"?"两层":"≤3秒")+" · 固定点":"存在 · 未匹配条件色";
        }
        if(field.Id=="19") return color.ToArgb()==Signature.ToArgb()?"惩戒协议 v2":"未知协议，请 /reload";
        if(field.Id=="B14") return color.G==70&&color.B==1&&color.R>0?$"当前目标版本 {color.R}":"目标版本未知";
        if(field.Id=="6"&&!(color.R==color.G&&color.G==color.B)) return "未知（血量采集失败）";
        return field.Kind==ValueKind.FuryBoolean?FuryBuffs.Boolean(color) is {} value?value?"是":"否":"未知（采集失败/受限）":field.Decode(color);
    }
    internal static Field[] Fields()
    {
        string[] names={"玩家战斗中","玩家存活未骑乘","当前敌对存活目标","玩家未施法/引导","附近敌人≥2（范围估算）","玩家血量",
            "神圣能量≥3","神圣能量≥5","圣能来源有效","军官标记（翅膀＋处决）","食物标记（责难）","目标施法/引导","目标可打断",
            "治疗石可用","治疗药水可用","GCD就绪","审判为愤怒之锤形态","翅膀自身冷却中","惩戒协议版本"};
        var result=Enumerable.Range(1,50).Select(i=>new Field(i.ToString(),i<=19?names[i-1]:"预留",i==6?ValueKind.Percent:i==19?ValueKind.Raw:ValueKind.FuryBoolean)).ToList();
        string[] skills={"复仇之怒","处决宣判","圣光之锤","最终审判","神圣风暴","灰烬觉醒","圣洁鸣钟","公正之剑","审判技能族","责难"};
        for(int i=0;i<10;i++) {
            result[19+i*2]=new((20+i*2).ToString(),skills[i]+"自身就绪",ValueKind.FuryBoolean);
            result[20+i*2]=new((21+i*2).ToString(),skills[i]+"可用",ValueKind.FuryBoolean);
            if(i>0) result[38+i]=new((39+i).ToString(),skills[i]+"范围内",ValueKind.FuryBoolean);
        }
        result[48]=new("49","翅膀自身剩余CD>5秒",ValueKind.FuryBoolean);
        result[49]=new("50","惩戒本轮发布有效",ValueKind.FuryBoolean);
        result.AddRange(Enumerable.Range(51,50).Select(DisplayProtocol.CommonField));
        for(int i=1;i<=5;i++) result.Add(new("B"+i,"神圣能量≥"+i,ValueKind.FuryBoolean,true));
        string[] flags={"灰烬已替换为锤","圣光之锤实际为免费形态","翅膀已学","处决已学","Holy Flames已学","光辉荣耀已学（当前不支持）",
            "圣殿英雄已学","自动十字军打击已学","当前目标版本","天穹之遗已学","处决自身冷却中","圣光救赎已学","战争艺术已学",
            "自身血量<80%","自身血量<60%","荣耀圣令自身就绪","荣耀圣令可用","圣光闪现自身就绪","圣光闪现可用",
            "圣佑术技能族自身就绪","圣佑术技能族可用","圣光闪现当前施法时间为零"};
        for(int i=0;i<flags.Length;i++) result.Add(new("B"+(i+6),flags[i],i==8?ValueKind.Raw:ValueKind.FuryBoolean,true));
        result.AddRange(Points.Select((p,i)=>new Field((101+i).ToString(),p.Label,ValueKind.Raw)));
        return result.ToArray();
    }
}
