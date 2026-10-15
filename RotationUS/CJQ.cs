#nullable enable
using System.Drawing;
using RotationUS.Diagnostics;

internal enum RetStage { Idle, Prepare, Wings, Sentence, Clear }
internal sealed record RetRequest(RetStage Stage=RetStage.Idle,double Started=0,double SentAt=0,bool Sent=false,int Epoch=0);
internal sealed record RetDecision(int Key,string Reason,string Branch,RetRequest Request);

internal sealed class CJQ
{
    public static CJQ Inst { get; }=new();
    // All configured action keys also yield to physical input. Injected keys are released synchronously.
    public int[] skipKeys=FZ.Inst.skipKeys.Concat(Enumerable.Range(0x30,10)).Concat(new[]{0xBD,0xBB,0xDB,0xDD,0xBA,0xDE,0xDC,0xBE}).Distinct().ToArray();
    internal RetBuffs Buffs {get;set;}=new();
    internal RetRequest Request {get;private set;}=new();
    internal RetDecision LastDecision {get;private set;}=new(0,"等待惩戒采样","通用",new());
    internal void Cancel() { Request=new(RetStage.Clear); }
    internal void Reset() { Cancel();Buffs=new(); }
    public void Process(Dictionary<int,Color> frames,Dictionary<int,Color> bars,Dictionary<int,bool> states)
    {
        LastDecision=Decide(frames,bars,Buffs,Request,Environment.TickCount64/1000.0);
        Request=LastDecision.Request;
        if(LastDecision.Key!=0) states[LastDecision.Key]=true;
    }
    // Pure transition: diagnostics may call this without advancing the live request.
    internal static RetDecision Decide(Dictionary<int,Color> f,Dictionary<int,Color> b,RetBuffs buff,RetRequest? request=null,double now=0)
    {
        var state=request??new();
        bool? F(int id)=>f.TryGetValue(id,out var c)?FuryBuffs.Boolean(c):null;
        bool? B(int id)=>b.TryGetValue(id,out var c)?FuryBuffs.Boolean(c):null;
        bool? Skill(int index)=>F(20+index*2)&F(21+index*2)&(index==0?true:F(39+index));
        string branch=F(5)==true?"多目标":F(5)==false?"单体":"目标数未知";
        RetDecision Result(int key,string reason)=>new(key,reason,branch,state);
        RetDecision Cancel(string reason) { state=new(RetStage.Clear);return Result(F(10)==true?28:0,reason); }
        if(!f.TryGetValue(19,out var sig)||sig.ToArgb()!=RetBuffs.Signature.ToArgb()||F(50)!=true)
            return Cancel("等待惩戒v2有效协议，请 /reload") with {Key=0};
        if(F(10)==false) state=new();
        if(F(10) is null&&state.Stage!=RetStage.Idle) state=new(RetStage.Clear);
        if(F(1)!=true||F(2)!=true) {
            if(F(10)==true) return Cancel("脱战/死亡/骑乘，取消军官");
            return Result(F(11)==true?29:0,"等待存活、战斗且未骑乘");
        }
        // Cancel before self-rescue too; a temporarily invalid target must not keep a request alive.
        int? epoch=RetBuffs.TargetEpoch(b);
        if(state.Stage!=RetStage.Idle&&(F(3)!=true||epoch is null||state.Epoch!=epoch)) state=new(RetStage.Clear);
        if(f.TryGetValue(6,out var hp)&&hp.R==hp.G&&hp.G==hp.B&&hp.R<=102) {
            if(F(14)==true) return Result(22,"治疗石：血量≤40%");
            if(F(15)==true) return Result(23,"治疗药水：血量≤40%");
        }
        // Self-target macros: independent of enemy target and offensive buff points.
        // Protection is off the GCD; heals must wait for the GCD and never interrupt a cast.
        if((B(20)&B(25)&B(26)&F(4))==true) return Result(3,"自身血量<60%：圣佑术（9）");
        if((B(19)&F(4)&F(16))==true) {
            bool? flash=B(23)&B(24)&B(27);
            if(flash==true) return Result(2,"自身血量<80%：瞬发圣光闪现（8）");
            if(flash is null) return Result(0,"等待圣光闪现可用/瞬发来源，不读条");
            bool? glory=B(21)&B(22);
            if(glory==true) return Result(1,"瞬发闪现不可用且血量<80%：荣耀圣令（7）");
            if(glory is null) return Result(0,"等待自身荣耀圣令可用状态");
        }
        if(state.Stage==RetStage.Clear) return Result(F(10)==true?28:0,"清理已完成/已取消的军官请求");
        if(F(11)==true) {
            bool? interrupt=F(3)&F(12)&F(13)&Skill(9);
            if(interrupt==true) return Result(21,"责难：食物标记请求");
            if(interrupt==false) return Result(29,"取消食物：无可打断目标/技能冷却/超范围");
            return Result(0,"等待责难范围/可打断状态");
        }
        if(F(3)!=true) return F(10)==true?Cancel("无有效敌对目标，取消军官"):Result(0,"等待有效敌对目标");
        if(B(11)!=false||B(12)!=true||B(13)!=true)
            return state.Stage!=RetStage.Idle?Cancel("天赋来源变化，取消军官请求"):
                Result(0,"等待圣殿＋自动十字军天赋（不支持光辉荣耀），请核验天赋");
        if(F(10) is null) return Result(0,"等待军官标记状态");
        if(F(10)==true&&state.Stage==RetStage.Idle) {
            if(epoch is null||B(8) is null||B(9) is null) return Result(0,"等待爆发已学状态/目标版本");
            if((B(8)==true&&F(20)==false)||(B(9)==true&&F(22)==false)) return Cancel("爆发组尚在自身冷却，取消请求");
            if((B(8)==true&&F(20)!=true)||(B(9)==true&&F(22)!=true)) return Result(0,"等待爆发组自身冷却来源");
            state=new(RetStage.Prepare,now,Epoch:epoch.Value);
        }
        if(state.Stage!=RetStage.Idle) {
            if(now-state.Started>8) return Cancel("爆发请求超过8秒未完成，取消请求");
            if(state.Sent&&now-state.SentAt>2) return Cancel("发键后2秒未确认技能进入自身冷却，取消请求");
            if(state.Stage==RetStage.Prepare) {
                if(B(10) is null) return Result(0,"等待Holy Flames天赋来源");
                if(B(10)==true&&buff.Expurgation.Present!=true) {
                    if(buff.Expurgation.Present is null) return Result(0,"等待当前目标灼烧取色");
                    return Result((F(4)&F(16)&Skill(7))==true?14:0,"爆发前公正之剑建立灼烧");
                }
                state=state with {Stage=RetStage.Wings};
            }
            if(state.Stage==RetStage.Wings) {
                if(B(8)==false||(state.Sent&&F(18)==true)) state=state with {Stage=RetStage.Sentence,Sent=false};
                else {
                    if(F(4)!=true||Skill(0)!=true) return Result(0,"等待复仇之怒可施放/自身冷却确认");
                    if(!state.Sent) state=state with {Sent=true,SentAt=now};
                    return Result(7,"军官：复仇之怒（含征伐）");
                }
            }
            if(state.Stage==RetStage.Sentence) {
                if(B(9)==false||(state.Sent&&B(16)==true)) return Cancel("翅膀＋处决已确认，清除军官");
                if((F(4)&F(16)&Skill(1))!=true) return Result(0,"等待处决可施放/自身冷却确认");
                if(!state.Sent) state=state with {Sent=true,SentAt=now};
                return Result(8,"军官：翅膀后处决宣判");
            }
        }
        if((F(4)&F(16))!=true) return Result(0,"等待施法结束/公共冷却");
        if(F(5) is null) return Result(0,"等待附近敌人数");
        // Native override shape plus fixed free-proc source; never infer a proc from a key press.
        bool? free=B(17)==false?false:buff.FreeHammer.Present;
        bool? hammer=Skill(2);
        bool? normal=hammer&B(6)&!B(7)&!free;
        if(normal==true) return Result(10,"灰烬后的常规圣光之锤");
        if(normal is null) return Result(0,"等待圣光之锤形态/免费触发");
        bool? freeUse=hammer&free&(buff.FreeHammer.Expiring|
            (buff.Wings.Present&buff.Wings.Expiring)|(buff.Sentence.Present&buff.Sentence.Expiring)|
            (!buff.Ruling.Present & (F(10)==false?true:F(49))));
        if(freeUse==true) return Result(10,"免费圣光之锤：临期/爆发末尾/无统治增益");
        if(freeUse is null) return Result(0,"等待免费锤/爆发临期/统治取色");
        bool? legacy=B(15)==false?false:buff.Legacy.Present;
        int spender=F(5)==true?legacy==true?3:4:3;
        bool? spend=F(5)==true&&legacy is null?null:Skill(spender);
        string spendName=spender==3?"最终审判"+(F(5)==true?"：天穹之遗":""):"神圣风暴";
        if(F(9)!=true) return Result(0,"等待神圣能量来源");
        bool? cap=F(8)&spend;
        if(cap==true) return Result(spender==3?11:9,"满圣能："+spendName);
        if(cap is null) return Result(0,"等待满圣能/终结技状态");
        var rules=new (bool? Condition,int Key,string Reason)[] {
            (Skill(5)&!B(6),12,"灰烬觉醒：自动"), (Skill(6),13,"圣洁鸣钟：自动"),
            (Skill(7)&(B(18)==false?false:buff.Art.Expiring),14,"公正之剑：战争艺术两层"),
            (spend,spender==3?11:9,spendName), (Skill(7),14,"公正之剑"),
            (Skill(8),16,F(17)==true?"愤怒之锤（审判替换）":"审判技能族") };
        foreach(var rule in rules) {
            if(rule.Condition==true) return Result(rule.Key,rule.Reason);
            if(rule.Condition is null) return Result(0,"等待状态："+rule.Reason);
        }
        return Result(0,"暂无合法输出技能");
    }
}
