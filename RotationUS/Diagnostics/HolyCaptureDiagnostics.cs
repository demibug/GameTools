#nullable enable
using System.Drawing;

namespace RotationUS.Diagnostics;

// Diagnostic-only lower row 95..100, mirrored by Classes/Holy.lua. Never feeds
// spell readiness, resource decisions, or input. Unknown protocols fail closed.
internal static class HolyCaptureDiagnostics
{
    private static readonly Color signature=Color.FromArgb(72,68,1);
    private static string Stage(int stage)=>stage switch {
        0=>"未标明接口",1=>"C_Spell.GetOverrideSpell",2=>"技能已学查询（IsSpellKnown / IsPlayerSpell）",
        3=>"C_Spell.IsSpellDisabled",4=>"C_Spell.GetSpellCooldownDuration",5=>"C_Spell.GetSpellCooldown",
        6=>"冷却信息 isEnabled",7=>"冷却信息 isActive",8=>"冷却计时对象原生颜色求值",
        9=>"C_Spell.IsSpellUsable",10=>"可用性原生布尔颜色显示",_=>"未知接口"};
    private static string State(Dictionary<int,Color> values,int point)=>
        !values.TryGetValue(point,out var color)?"点位缺失":FuryBuffs.Boolean(color) is {} value?
            value?"是":"否":"未知";

    internal static string Describe(Dictionary<int,Color> values,IReadOnlyDictionary<int,Color>? bars)
    {
        int version=bars is not null&&bars.TryGetValue(95,out var head)&&head.R==72&&head.G==68&&head.B is 1 or 2&&
            bars.TryGetValue(100,out var tail)&&tail.ToArgb()==head.ToArgb()?head.B:0;
        string Point(int id,int cell,bool valid)
        {
            string state=State(values,id);
            if(!valid) return $"点{id}={state}（未收到具体原因：旧插件或诊断协议无效）";
            if(!bars!.TryGetValue(cell,out var code)||(version==1&&code.B!=0)||code.R>10||code.G>10)
                return $"点{id}={state}（诊断码无效）";
            string why=code.G switch {
                0=>state=="未知"?version==2&&id==26&&code.B>=16?
                    "冷却查询已完成；已学未确认，点26不授权施法":"原生显示像素无效／未标注错误":id==26?
                    state=="是"?"已学且就绪":"冷却中／未启用":"可用性接口已确认",
                1=>"接口调用报错",2=>"返回nil／缺少字段",3=>"字段受限（secret），不能在Lua读取",
                4=>"返回类型或数值无效",5=>"技能未学",6=>"客户端判定技能已禁用（等级同步等环境限制需结合等级判断）",
                8=>"明确不可用：资源不足（insufficientPower=true）",
                9=>"明确不可用：未确认资源不足，属于技能状态而非采集失败",
                10=>"接口缺失",_=>"未识别原因"};
            return $"点{id}={state}；{Stage(code.R)}：{why}（诊断码{code.R}/{code.G}）";
        }
        string Observation(string name,int code)
        {
            string value=code switch {
                0=>"未查询",1=>"是",2=>"否",17=>"未知：接口报错",18=>"未知：返回nil／缺少字段",
                19=>"未知：字段受限（secret）",20=>"未知：无效返回",26=>"未知：接口缺失",_=>"诊断码无效"};
            return name+"="+value;
        }
        bool valid=version!=0;
        string metadata="";
        if(valid&&bars!.TryGetValue(98,out var spell)&&bars.TryGetValue(99,out var levels)) {
            int id=spell.R+(spell.G<<8)+(spell.B<<16);
            string Level(int value)=>value==255?"未知":value.ToString();
            metadata=$"；圣光术基础ID=82326，实际查询ID={(id==0?"未解析":id.ToString())}；等级：实际={Level(levels.R)}，有效={Level(levels.G)}，技能学习={Level(levels.B)}";
        }
        if(version==2&&bars!.TryGetValue(96,out var knowledge)&&bars.TryGetValue(97,out var disabled)) {
            metadata+="；"+Observation("已学查询",knowledge.B)+"；"+Observation("禁用查询（辅助）",disabled.B);
            if(disabled.B>=16) metadata+="；禁用未确认，继续依据已学／冷却／可用性判断";
        }
        return "圣光术采集诊断："+Point(26,96,valid)+"；"+Point(36,97,valid)+metadata+
            $"；闪现兜底：点25={State(values,25)}，点35={State(values,35)}";
    }

    internal static int RunTests()
    {
        int count=0;
        void Check(bool ok,string why) {if(!ok) throw new Exception("FAIL Holy capture diagnostics: "+why);count++;}
        var f=new Dictionary<int,Color>{{26,Color.Orange},{36,Color.Orange},{25,Color.White},{35,Color.White}};
        var b=new Dictionary<int,Color>{{95,signature},{96,Color.FromArgb(4,1,0)},
            {97,Color.FromArgb(9,2,0)},{98,Color.FromArgb(150,65,1)},{99,Color.FromArgb(90,60,70)},{100,signature}};
        string text=Describe(f,b);
        Check(text.Contains("点26=未知；C_Spell.GetSpellCooldownDuration：接口调用报错")&&
            text.Contains("点36=未知；C_Spell.IsSpellUsable：返回nil"),"points retain independent interface failure reasons");
        Check(text.Contains("实际查询ID=82326")&&text.Contains("实际=90，有效=60，技能学习=70"),"spell and level context decoded");
        foreach(int cause in new[]{1,2,3,4,5,6,8,9,10}) {
            b[96]=Color.FromArgb(6,cause,0);
            string line=Describe(f,b);
            Check(line!=text&&line.Contains($"诊断码6/{cause}"),"failure categories are distinct and readable");
        }
        b[96]=Color.Black;b[97]=Color.FromArgb(9,8,0);f[26]=Color.White;f[36]=Color.Black;
        Check(Describe(f,b).Contains("资源不足"),"unusable due to mana is not a capture failure");
        b[97]=Color.FromArgb(3,6,0);
        Check(Describe(f,b).Contains("技能已禁用"),"disabled is separate from an API failure");
        b[96]=b[97]=Color.Black;f[36]=Color.White;
        Check(Describe(f,b).Contains("点26=是")&&Describe(f,b).Contains("点36=是"),"recovery clears previous failure");
        b[100]=Color.Red;
        Check(Describe(f,b).Contains("诊断协议无效")&&!Describe(f,b).Contains("等级："),"bad / old protocol cannot invent a reason");
        Check(Describe(f,null).Contains("旧插件"),"old collectors are explicitly identified");
        b[100]=signature;b[96]=Color.FromArgb(50,50,50);
        Check(Describe(f,b).Contains("诊断码无效"),"unknown codes are not decoded as gameplay data");
        b[99]=Color.White;
        Check(Describe(f,b).Contains("实际=未知，有效=未知，技能学习=未知"),"missing context is not zero level");
        b[95]=b[100]=Color.FromArgb(72,68,2);
        b[96]=Color.FromArgb(0,0,1);b[97]=Color.FromArgb(0,0,17);
        Check(Describe(f,b).Contains("已学查询=是")&&Describe(f,b).Contains("禁用查询（辅助）=未知：接口报错")&&
            Describe(f,b).Contains("点26=是")&&Describe(f,b).Contains("点36=是"),"failed auxiliary query leaves successful primary evidence intact");
        b[96]=Color.FromArgb(4,1,19);b[97]=Color.FromArgb(9,2,18);f[26]=f[36]=Color.Orange;
        text=Describe(f,b);
        Check(text.Contains("GetSpellCooldownDuration：接口调用报错")&&text.Contains("IsSpellUsable：返回nil")&&
            text.Contains("已学查询=未知：字段受限")&&text.Contains("禁用查询（辅助）=未知：返回nil"),"all four independent failures survive in one sample");
        b[96]=Color.FromArgb(0,0,19);f[36]=Color.White;
        Check(Describe(f,b).Contains("已学未确认，点26不授权施法"),"unknown necessary knowledge still cannot grant a cast");
        b[97]=Color.FromArgb(0,0,2);
        Check(Describe(f,b).Contains("禁用查询（辅助）=否")&&!Describe(f,b).Contains("禁用未确认"),"auxiliary recovery clears the previous failure");
        return count;
    }
}
