#nullable enable
using System.Drawing;
namespace RotationUS.Diagnostics;

internal sealed class HealingItemTrace
{
    private readonly int?[] sequences=new int?[2];
    private readonly double[] requested={double.NaN,double.NaN},nextLog={0,0};
    private int lastUi;
    private static string Name(int i)=>i==0?"治疗石5512／Shift+4":"治疗药水241305／Shift+5";
    private static string Status(int code)=>code switch {
        0=>"数量大于0，enable已确认，冷却结束",1=>"背包数量为0",2=>"物品／背包格子冷却中",
        3=>"enable为false／0，当前不可使用",4=>"接口返回受限值，不能判断",
        5=>"接口报错／缺少或无效返回值",6=>"本地药水冷却保护尚未结束",7=>"数量与背包格子不一致，未找到指定药水",
        _=>"诊断码无效"};
    private static string Ui(int code)=>code switch {
        1=>"物品／技能冷却中",2=>"找不到物品",4=>"战斗状态限制",5=>"当前无法使用",
        6=>"其他客户端错误（具体文本未传输）",7=>"客户端错误内容受限",_=>"无"};
    internal static bool Packet(Color c)=>c.R is >=200 and <=207 && c.G%16<=7 && c.B%16<=7 && (c.G/16%4)<=2 && (c.B/16%4)<=2;
    internal static string Describe(Dictionary<int,Color> f,Color? packet)
    {
        bool valid=packet is {} p&&Packet(p);
        string Item(int i) {
            int id=16+i;
            string flag=f.TryGetValue(id,out var color)?FuryBuffs.Boolean(color) switch {true=>"可用",false=>"不可用",_=>"未知"}:"缺失";
            int value=valid?(i==0?packet!.Value.G:packet!.Value.B):0;
            return Name(i)+$"：点{id}={flag}；"+(valid?Status(value%16):"未收到物品原因协议（旧插件／采样缺失），不能推断失败原因");
        }
        return Item(0)+"；"+Item(1);
    }
    internal IEnumerable<string> Observe(Dictionary<int,Color> f,Color? packet,bool live,bool executing,string reason,double now)
    {
        var lines=new List<string>();
        bool valid=packet is {} p&&Packet(p);
        if(!live||!executing) {
            for(int i=0;i<2;i++) if(!double.IsNaN(requested[i])) {
                lines.Add($"[救命物品] {Name(i)}：反馈观察中断（{reason}），未确认成功，也不能认定使用失败");requested[i]=double.NaN;
            }
        }
        if(!live) {Array.Clear(sequences);lastUi=0;return lines;}
        int ui=valid?packet!.Value.R-200:0;
        for(int i=0;i<2;i++) {
            int value=valid?(i==0?packet!.Value.G:packet!.Value.B):0;
            int sequence=value/64,kind=value/16%4;
            if(valid&&sequences[i] is {} old&&sequence!=old&&kind!=0) {
                lines.Add($"[救命物品] {Name(i)}："+(kind==1?"客户端成功事件已确认":"客户端失败事件已确认；具体原因="+(ui!=0?Ui(ui)+"（附近提示，归属未证实）":"客户端未提供／未收到")));
                requested[i]=double.NaN;
            }
            sequences[i]=valid?sequence:null;
        }
        bool low=f.TryGetValue(6,out var hp)&&hp.R==hp.G&&hp.G==hp.B&&hp.R>0&&hp.R<=102;
        if(ui!=0&&ui!=lastUi&&(low||requested.Any(t=>!double.IsNaN(t))))
            lines.Add("[救命物品] 客户端附近提示："+Ui(ui)+"；提示事件不带物品身份，不能单凭此提示判定哪个物品失败");
        lastUi=ui;
        int selected=reason.StartsWith("治疗石：")?0:reason.StartsWith("治疗药水：")?1:-1;
        for(int i=0;i<2;i++) {
            if(!double.IsNaN(requested[i])&&now-requested[i]>=2) {
                lines.Add($"[救命物品] {Name(i)}：请求后2秒未收到成功确认；"+Describe(f,packet)+"；不能仅凭此记录认定按键已送达或实际使用失败");
                requested[i]=double.NaN;
            }
            if(live&&executing&&selected==i&&double.IsNaN(requested[i])&&now>=nextLog[i]) {
                requested[i]=now;nextLog[i]=now+3;
                lines.Add($"[救命物品] {Name(i)}：执行分支已请求使用，等待客户端成功／失败反馈；"+Describe(f,packet));
            }
        }
        return lines;
    }
    internal static int RunTests()
    {
        int n=0;void Check(bool x,string why){if(!x)throw new Exception("FAIL item trace: "+why);n++;}
        var f=new Dictionary<int,Color>{{6,Color.FromArgb(80,80,80)},{16,Color.Black},{17,Color.White}};
        var trace=new HealingItemTrace();var p=Color.FromArgb(200,1,0);
        Check(Describe(f,p).Contains("背包数量为0"),"missing item reason");
        Check(Describe(f,Color.FromArgb(200,2,6)).Contains("本地药水冷却"),"local cooldown reason");
        Check(Describe(f,null).Contains("旧插件"),"old plugin remains unknown");
        Check(!Packet(Color.White),"unrelated pixel cannot report events");
        trace.Observe(f,p,true,true,"普通治疗",0).ToArray();
        Check(trace.Observe(f,p,true,true,"治疗药水：血量≤40%",.1).Single().Contains("等待客户端"),"request is not success");
        Check(!trace.Observe(f,p,true,true,"普通治疗",1).Any(),"no premature failure");
        Check(trace.Observe(f,p,true,true,"普通治疗",2.2).Single().Contains("未收到成功确认"),"timeout is explicitly uncertain");
        trace.Observe(f,p,true,true,"治疗药水：血量≤40%",4).ToArray();
        Check(trace.Observe(f,Color.FromArgb(200,1,80),true,true,"普通治疗",4.2).Single().Contains("成功事件已确认"),"matching success confirms request");
        Check(!trace.Observe(f,Color.FromArgb(200,1,80),true,true,"普通治疗",5).Any(),"unchanged feedback deduplicated");
        Check(trace.Observe(f,Color.FromArgb(201,1,160),true,true,"普通治疗",6).Any(s=>s.Contains("失败事件已确认")&&s.Contains("归属未证实")),"failure retains uncertainty of global error association");
        Check(!trace.Observe(f,p,false,true,"采样失效",7).Any(),"no event inferred from invalid sample");
        trace.Observe(f,p,true,true,"治疗药水：血量≤40%",8).ToArray();
        Check(trace.Observe(f,p,true,false,"用户关闭",8.1).Single().Contains("观察中断"),"manual stop is not a failed use");
        return n;
    }
}
