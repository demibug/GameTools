#nullable enable
using System.Drawing;
using RotationUS.Diagnostics;

internal enum RetStage { Idle, Prepare, Wings, Sentence, Clear, RadiantPrepare, RadiantReady }
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
        bool? Skill(int index)=>F(20+index*2)&F(21+index*2)&B(30+index)&(index==0?B(28):F(39+index));
        string branch=F(5)==true?"多目标":F(5)==false?"单体":"目标数未知";
        RetDecision Result(int key,string reason)=>new(key,reason,branch,state);
        RetDecision Cancel(string reason) { state=new(RetStage.Clear);return Result(F(10)==true?28:0,reason); }
        RetDecision? ItemRescue()
        {
            if(f.TryGetValue(6,out var hp)&&hp.R==hp.G&&hp.G==hp.B&&hp.R<=102) {
                if(F(14)==true) return Result(22,"治疗石：血量≤40%");
                if(F(15)==true) return Result(23,"治疗药水：血量≤40%");
            }
            return null;
        }
        if(!f.TryGetValue(19,out var sig)||sig.ToArgb()!=RetBuffs.Signature.ToArgb()||F(50)!=true)
            return Cancel("等待惩戒v5有效协议，请 /reload") with {Key=0};
        // Casting/unknown permits only the three explicitly allowed exceptions.
        // Keep their normal combat, health, request, usability and range gates.
        if(F(4)!=true) {
            if((F(1)&F(2))==true) {
                if(ItemRescue() is {} rescue) return rescue;
                if((F(11)&F(3)&F(12)&F(38)&F(39)&F(48)&B(39))==true)
                    return Result(21,"责难：食物标记请求（允许读条期间使用）");
            }
            return Result(0,F(4)==false?
                "正在读条/引导，仅允许治疗石、药水和责难":"施法状态未知，仅允许治疗石、药水和责难");
        }
        bool radiant=B(11)==true;
        bool radiantState=state.Stage is RetStage.RadiantPrepare or RetStage.RadiantReady;
        if(radiant&&!radiantState||!radiant&&radiantState) state=new();
        if(!radiant&&F(10)==false) state=new();
        if(!radiant&&F(10) is null&&state.Stage!=RetStage.Idle) state=new(RetStage.Clear);
        if(F(1)!=true||F(2)!=true) {
            if(radiant) state=new();
            else if(F(10)==true) return Cancel("脱战/死亡/骑乘，取消军官");
            return Result(F(11)==true?29:0,"等待存活、战斗且未骑乘");
        }
        // Cancel before self-rescue too; a temporarily invalid target must not keep a request alive.
        int? epoch=RetBuffs.TargetEpoch(b);
        if(state.Stage!=RetStage.Idle&&(F(3)!=true||epoch is null||state.Epoch!=epoch))
            state=radiant?new():new(RetStage.Clear);
        if(ItemRescue() is {} itemRescue) return itemRescue;
        // Self-target macros: independent of enemy target and offensive buff points.
        // Protection is off the GCD; heals must wait for the GCD and never interrupt a cast.
        if((B(20)&B(25)&B(26)&B(42)&F(4))==true) return Result(3,"自身血量<60%：圣佑术（9）");
        if((B(19)&F(4)&F(16))==true) {
            bool? flash=B(23)&B(24)&B(27)&B(41);
            if(flash==true) return Result(1,"自身血量<80%：瞬发圣光闪现（7）");
            if(flash is null) return Result(0,"等待圣光闪现可用/瞬发来源，不读条");
            bool? glory=B(21)&B(22)&B(40);
            if(glory==true) return Result(2,"瞬发闪现不可用且血量<80%：荣耀圣令（8）");
            if(glory is null) return Result(0,"等待自身荣耀圣令可用状态");
        }
        if(state.Stage==RetStage.Clear) return Result(F(10)==true?28:0,"清理已完成/已取消的军官请求");
        if(F(11)==true) {
            var gates=new (int Field,string Invalid,string Unknown)[] {
                (3,"无有效敌对目标","目标有效性"),(12,"目标没有读条或引导","目标施法状态"),
                // Match Protection/Arms: cast presence gates the request; interruptibility is diagnostic only.
                (38,"责难未就绪或未学","责难自身冷却"),
                (39,"责难不可用","责难可用状态"),(48,"目标超出责难自身射程","责难自身射程") };
            foreach(var gate in gates) if(F(gate.Field)==false) return Result(29,"取消食物："+gate.Invalid);
            if(B(39)==false) return Result(29,"取消食物：责难资源不足");
            foreach(var gate in gates) if(F(gate.Field) is null) return Result(0,"等待"+gate.Unknown);
            if(B(39) is null) return Result(0,"等待责难资源状态");
            return Result(21,"责难：食物标记请求");
        }
        if(F(3)!=true) return !radiant&&F(10)==true?Cancel("无有效敌对目标，取消军官"):Result(0,"等待有效敌对目标");
        if(B(11) is null||B(12)!=true||B(13)!=true)
            return state.Stage!=RetStage.Idle?Cancel("天赋来源变化，取消军官请求"):
                Result(0,"等待圣殿＋自动十字军天赋，请核验天赋");
        if(radiant) return DecideRadiant(f,b,buff,state,now);
        if(F(10) is null) return Result(0,"等待军官标记状态");
        if(F(10)==true&&state.Stage==RetStage.Idle) {
            if(epoch is null||B(8) is null||B(9) is null) return Result(0,"等待爆发已学状态/目标版本");
            if((B(8)==true&&F(20)==false)||(B(9)==true&&F(22)==false)) return Cancel("爆发组尚在自身冷却，取消请求");
            if((B(8)==true&&F(20)!=true)||(B(9)==true&&F(22)!=true)) return Result(0,"等待爆发组自身冷却来源");
            state=new(RetStage.Prepare,now,Epoch:epoch.Value);
        }
        if(state.Stage!=RetStage.Idle) {
            if(now-state.Started>8) return Cancel("爆发请求超过8秒未完成，取消请求");
            if(state.Stage!=RetStage.Prepare&&state.Sent&&now-state.SentAt>2) return Cancel("发键后2秒未确认技能进入自身冷却，取消请求");
            if(state.Stage==RetStage.Prepare) {
                // Optional setup gets at most two seconds of the request budget.
                // A black/missing debuff pixel must not hold Wings until the whole request expires.
                // Sending Blade is not confirmation: wait for its own cooldown, or skip on timeout.
                if(now-state.Started<2) {
                    if(B(10) is null) return Result(0,"等待神圣烈焰天赋来源（准备最多2秒）");
                    if(B(10)==true&&buff.Expurgation.Present!=true) {
                        if(buff.Expurgation.Present is null) return Result(0,"等待当前目标异端逐除取色（准备最多2秒）");
                        if(state.Sent) {
                            if(F(34)!=false) return Result(0,"等待准备公正之剑进入自身冷却（最多2秒）");
                        } else if(Skill(7)!=false) {
                            if((F(4)&F(16)&Skill(7))!=true) return Result(0,"等待准备公正之剑可施放（最多2秒）");
                            state=state with {Sent=true,SentAt=now};
                            return Result(14,"爆发前尝试公正之剑建立异端逐除");
                        }
                    }
                }
                state=state with {Stage=RetStage.Wings,Sent=false,SentAt=0};
            }
            if(state.Stage==RetStage.Wings) {
                if(B(8)==false||(state.Sent&&F(18)==true)) state=state with {Stage=RetStage.Sentence,Sent=false};
                else {
                    if(B(28)!=true) return Result(0,"等待复仇之怒／征伐目标进入制裁之锤10码范围");
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
        // Wake and Hammer share the native Wake button (action 12, period).
        // A Hammer candidate requires that this button is actually overridden.
        bool? hammer=Skill(2)&B(6);
        bool? normal=hammer&B(6)&!B(7)&!free;
        if(normal==true) return Result(12,"灰烬后的常规圣光之锤");
        if(normal is null) return Result(0,"等待圣光之锤形态/免费触发");
        bool? freeUse=hammer&free&(buff.FreeHammer.Expiring|
            (buff.Wings.Present&buff.Wings.Expiring)|(buff.Sentence.Present&buff.Sentence.Expiring)|
            (!buff.Ruling.Present & (F(10)==false?true:F(49))));
        if(freeUse==true) return Result(12,"免费圣光之锤：临期/爆发末尾/无公允裁定增益");
        if(freeUse is null) return Result(0,"等待免费锤/爆发临期/公允裁定取色");
        bool? legacy=B(15)==false?false:buff.Legacy.Present;
        int spender=F(5)==true?legacy==true?3:4:3;
        bool? spend=F(5)==true&&legacy is null?null:Skill(spender);
        string spendName=spender==3?"最终审判"+(F(5)==true?"：苍穹之遗":""):"神圣风暴";
        if(F(9)!=true) return Result(0,"等待神圣能量来源");
        bool? cap=F(8)&spend;
        if(cap==true) return Result(spender==3?11:9,"满圣能："+spendName);
        if(cap is null) return Result(0,"等待满圣能/终结技状态");
        var rules=new (bool? Condition,int Key,string Reason)[] {
            (Skill(5)&!B(6),12,"灰烬觉醒：自动"), (Skill(6),13,"圣洁鸣钟：自动"),
            (Skill(7)&(B(18)==false?false:buff.Art.Expiring),14,"公正之剑：战争艺术两层"),
            (spend,spender==3?11:9,spendName), (Skill(7),14,"公正之剑"),
            (Skill(8),10,F(17)==true?"愤怒之锤（审判替换）":"审判技能族") };
        foreach(var rule in rules) {
            if(rule.Condition==true) return Result(rule.Key,rule.Reason);
            if(rule.Condition is null) return Result(0,"等待状态："+rule.Reason);
        }
        return Result(0,"暂无合法输出技能");
    }

    // Radiant Glory, Templar and Crusading Strikes; no tier-set assumptions.
    // Tracking flags and inactive Wings/Sentence cooldowns never drive this branch.
    private static RetDecision DecideRadiant(Dictionary<int,Color> f,Dictionary<int,Color> b,
        RetBuffs buff,RetRequest state,double now)
    {
        bool? F(int id)=>f.TryGetValue(id,out var c)?FuryBuffs.Boolean(c):null;
        bool? B(int id)=>b.TryGetValue(id,out var c)?FuryBuffs.Boolean(c):null;
        bool? Skill(int i)=>F(20+i*2)&F(21+i*2)&B(30+i)&F(39+i);
        string branch="光辉荣耀 · "+(F(5)==true?"多目标":F(5)==false?"单体":"目标数未知");
        RetDecision Result(int key,string reason)=>new(key,reason,branch,state);
        if(B(9)!=false) return Result(0,"光辉荣耀分支要求未学处决宣判，请核验天赋");
        if(B(43)==true) state=new();
        if((F(4)&F(16))!=true) return Result(0,"等待施法结束/公共冷却");
        if(F(5) is null) return Result(0,"等待附近敌人数");
        if(F(9)!=true) return Result(0,"等待神圣能量来源");
        // The actual replacement form is authoritative; the external point only supplies expiry.
        bool? free=B(7);
        bool? hammer=Skill(2)&B(6);
        bool? urgent=hammer&free&(buff.FreeHammer.Present==true?RetBuffs.RadiantExpiry(buff.FreeHammer):null);
        if(urgent==true) return Result(12,"免费圣光之锤：剩余≤2GCD");
        if(urgent is null) return Result(0,"等待免费大锤形态/临期色（≤2GCD）");

        RetDecision? Finish()
        {
            bool? normal=hammer&!free;
            if(normal==true) return Result(12,"灰烬后的常规圣光之锤");
            if(normal is null) return Result(0,"等待圣光之锤形态/免费触发");
            bool? release=hammer&free&((buff.Wings.Present&RetBuffs.RadiantExpiry(buff.Wings))|
                !buff.Ruling.Present|RetBuffs.RadiantExpiry(buff.Ruling));
            if(release==true) return Result(12,"免费圣光之锤：复仇之怒临期/公允裁定临期或消失");
            if(release is null) return Result(0,"等待复仇之怒/公允裁定临期色，请重配GCD阈值");
            bool? legacy=B(15)==false?false:buff.Legacy.Present;
            if(F(5)==true&&legacy is null) return Result(0,"等待苍穹之遗");
            int spender=F(5)==true&&legacy!=true?4:3;
            bool? spend=Skill(spender);
            if(spend==true) return Result(spender==3?11:9,spender==3?"最终审判":"神圣风暴");
            if(spend is null) return Result(0,"等待终结技可用性/资源/距离");
            return null;
        }

        // Base cooldown is observed independently of Wake's replacement and usability.
        bool? capped=F(8)&B(43);
        if(capped==true&&Finish() is { } cappedAction) return cappedAction;
        if(capped is null) return Result(0,"等待满圣能/灰烬自身冷却");
        bool? wake=Skill(5)&!B(6);
        if(wake is null) return Result(0,"等待灰烬觉醒形态/可施放状态");
        if(wake==true) {
            // Try Expurgation once per Wake preparation; actual cooldown or timeout
            // ends preparation. Sending a key never fabricates the target debuff.
            if(state.Stage!=RetStage.RadiantReady&&B(10)!=false&&buff.Expurgation.Present!=true) {
                int? epoch=RetBuffs.TargetEpoch(b);
                if(epoch is null) return Result(0,"等待当前目标版本");
                if(state.Stage!=RetStage.RadiantPrepare)
                    state=new(RetStage.RadiantPrepare,now,Epoch:epoch.Value);
                if(now-state.Started<2) {
                    if(B(10) is null||buff.Expurgation.Present is null)
                        return Result(0,"等待灰烬前异端逐除状态（最多2秒）");
                    if(state.Sent) {
                        if(F(34)!=false) return Result(0,"等待准备公正之剑进入冷却（最多2秒）");
                    } else if(Skill(7)!=false) {
                        if(Skill(7)!=true) return Result(0,"等待准备公正之剑（最多2秒）");
                        state=state with {Sent=true,SentAt=now};
                        return Result(14,"灰烬前尝试公正之剑建立异端逐除");
                    }
                }
                state=state with {Stage=RetStage.RadiantReady};
            }
            return Result(12,"灰烬觉醒：自动触发复仇之怒");
        }
        bool? toll=Skill(6);
        if(toll==true) return Result(13,"圣洁鸣钟：自动");
        if(toll is null) return Result(0,"等待圣洁鸣钟状态");
        bool? art=Skill(7)&(B(18)==false?false:buff.Art.Expiring);
        if(art==true) return Result(14,"公正之剑：战争艺术两层");
        if(art is null) return Result(0,"等待战争艺术两层/公正之剑状态");
        if(Finish() is { } finisher) return finisher;
        foreach(var entry in new[]{(Index:7,Key:14,Name:"公正之剑"),(Index:8,Key:10,Name:F(17)==true?"愤怒之锤（审判替换）":"审判")}) {
            bool? usable=Skill(entry.Index);
            if(usable==true) return Result(entry.Key,entry.Name);
            if(usable is null) return Result(0,"等待"+entry.Name+"状态");
        }
        return Result(0,"暂无合法输出技能");
    }
}
