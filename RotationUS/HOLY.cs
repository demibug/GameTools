#nullable enable
using System.Drawing;
using RotationUS.Diagnostics;

internal sealed record HolyDecision(int Key,string Reason);
internal sealed class HOLY
{
    internal static HOLY Inst {get;}=new();
    // Let manual combat/targeting keys pause execution. Never pause on movement alone.
    internal int[] skipKeys=FZ.Inst.skipKeys.Concat(new[]{0x10,0x11,0x30,0x34,0x37,0x38,0x39,0xBB,0xBD,0xDB,0xDD,0xDC,0xBA,0xDE,0xBE,0x70,0x71,0x72,0x73,0x74}).Distinct().ToArray();
    internal HolyBuffs Buffs=new();
    internal HealingThresholds Thresholds=new();
    internal HolyDecision LastDecision {get;private set;}=new(0,"等待神圣采样");
    private int pendingSlot, confirmations;
    private long selectionStarted;
    internal void Reset() {pendingSlot=confirmations=0;}
    public void Process(Dictionary<int,Color> frames,Dictionary<int,Color> bars,Dictionary<int,bool> states)
    {
        bool? F(int id)=>frames.TryGetValue(id,out var c)?FuryBuffs.Boolean(c):null;
        var decision=Decide(frames,Buffs,Thresholds);
        // All members recovered while selecting: a confirmed safe damage action
        // replaces the obsolete heal selection without waiting for its target.
        if(pendingSlot>0&&decision.Key==18) Reset();
        // Target acknowledgement must not delay emergency self macros/items.
        if(pendingSlot>0 && decision.Key is 1 or 2 or 3 or 5 or 22 or 23)
        { LastDecision=decision;states[decision.Key]=true;return; }
        if(pendingSlot>0) {
            if(F(41)!=false||F(1)!=true||F(2)!=true) Reset();
            else if(F(45+pendingSlot)==true) {
                if(++confirmations<2) {LastDecision=new(0,"等待选人后的第二次目标确认");return;}
                Reset();
            } else {
                confirmations=0;
                if(System.Diagnostics.Stopwatch.GetElapsedTime(selectionStarted).TotalSeconds<2) {
                    LastDecision=new(0,"等待目标切换确认");return;
                }
                Reset();LastDecision=new(0,"选人未确认，请检查 Shift+F1～F5 绑定");return;
            }
        }
        LastDecision=decision;
        if(LastDecision.Key>=101&&LastDecision.Key<=105) {
            pendingSlot=LastDecision.Key-100;confirmations=0;selectionStarted=System.Diagnostics.Stopwatch.GetTimestamp();
        }
        if(LastDecision.Key!=0) states[LastDecision.Key]=true;
    }
    internal static HolyDecision Decide(Dictionary<int,Color> frames,HolyBuffs buff,HealingThresholds? thresholds=null)
    {
        var t=thresholds??new();
        bool? F(int id)=>frames.TryGetValue(id,out var c)?FuryBuffs.Boolean(c):null;
        double? P(int id)=>frames.TryGetValue(id,out var c)&&c.R==c.G&&c.G==c.B?c.R/255.0:null;
        bool Skill(int n)=>F(20+n)==true&&F(30+n)==true;
        bool? infusion=F(93)??buff.Infusion,hand=F(94)??buff.HandOfDivinity;
        if(F(96)!=true||F(100)!=true) return new(0,"等待新版神圣采集器，请更新插件并 /reload");
        if(F(1)!=true||F(2)!=true) return new(0,"等待玩家存活、战斗且未骑乘");
        bool raid=F(41)==true;
        var group=HolyTriage.Analyze(frames,raid,t);
        // Shield damage has first spell priority only when every observed living
        // member is healthy. Unknown members cannot be treated as full health.
        if(F(3)==true&&F(4)==false&&F(12)==true&&F(95)==true&&F(97)==true&&F(98)==true&&
            F(9)==true&&F(20)==true&&F(19)==true&&F(41) is not null&&
            P(6)>=t.HealBelow&&group.Reliable&&group.Unknown==0&&group.Injured==0&&!group.Major&&
            F(92)==true&&F(13)==true)
            return new(18,"全队存活成员均≥治疗线、5圣能且震击至少2充能：近战盾击（Shift+.）");
        if(F(3)==true) {
            if(P(6)<=.40&&F(16)==true) return new(22,"治疗石：血量≤40%");
            if(P(6)<=.40&&F(17)==true) return new(23,"治疗药水：血量≤40%");
            // Divine Protection is off the GCD; target acknowledgement and an
            // active healing cast must not postpone this self defensive.
            if(P(6)<.50&&Skill(10)) return new(5,"自身低于50%：圣佑术（-）");
        }
        if(F(9)!=true||F(20)!=true) return new(0,"等待玩家施法结束或GCD");
        bool selfNeed=F(3)==true&&P(6)<t.HealBelow;
        int best=0;double bestHp=t.HealBelow;
        if(F(41)==false&&F(19) is not null) {
            for(int slot=1;slot<=5;slot++) {
                if(slot>1&&(F(19)!=true||F(40+slot)!=true)) continue;
                if(!frames.TryGetValue(50+slot,out var c)) continue;
                var member=TeamHealthReading.Read(c);
                if(member.Percent is >0&&member.Percent<bestHp) {bestHp=member.Percent.Value;best=slot;}
            }
        }
        // A self macro must not repeatedly preempt an available Shock. In a
        // party we can first select the injured ally; raids retain the target.
        bool shockRoute=F(41) is not null&&F(19) is not null&&Skill(2)&&
            ((best>0&&F(45+best)!=true)||
            (F(3)==true&&F(4)==true&&F(5)==true&&P(8)<t.HealBelow));
        bool fullShock=shockRoute&&F(13)==true;
        bool hardLight=infusion==false&&F(22)==false&&F(10)==true&&P(7)>=t.HolyLightMana&&Skill(6);
        if(selfNeed&&(F(91)==true||buff.DivinePurpose==true)&&Skill(3)) return new(2,
            buff.DivinePurpose==true?"自身神圣意志免费荣耀圣令（8）：低于治疗线":"自身荣耀圣令（8）：低于治疗线且圣能≥3");
        if(selfNeed&&!fullShock&&infusion==true&&Skill(5)) return new(1,"自身灌注圣光闪现：低于治疗线");
        if(selfNeed&&!fullShock&&hand==true&&Skill(6)) return new(3,"自身神性之手瞬发圣光术（9）：低于治疗线");
        if(selfNeed&&P(6)<t.Major&&hardLight) return new(3,"自身大掉血、无灌注、震击冷却、静止且法力足够：读条圣光术（9）");
        if(selfNeed&&!shockRoute&&F(10)==true&&Skill(5)) return new(1,"自身低于治疗线且静止：圣光闪现（7）");
        if(F(41) is null||F(19) is null) return new(0,"等待队伍类型");
        // Auto-selection is a separate action. No healing shares its sample.
        if(!raid) {
            if(best>0&&F(45+best)!=true) return new(100+best,"选择范围内血量最低："+(best==1?"自己":"party"+(best-1)));
        }
        if(F(3)!=true) return new(0,"等待存活目标");
        if(F(4) is null) return new(0,"等待目标友方状态（点4）");
        if(F(4)==true&&F(5)!=true) return new(0,"等待目标进入治疗距离／距离状态（点5）");
        bool healing=F(4)==true&&F(5)==true;
        if(healing&&(P(8) is null)) return new(0,"等待目标血量");
        bool need=healing&&P(8)<t.HealBelow;
        bool aoe=F(19)==true&&group.Aoe;
        bool big=F(19)==true&&group.Major;
        // Readiness prevents repeat casts; no beacon aura/window is required.
        if(healing&&aoe&&Skill(9)) return new(15,"群体掉血且技能就绪：美德道标");
        // Only a confirmed proc waives the resource gate. Readiness and usability
        // still apply to the actual spender (including the Eternal Flame override).
        if(need&&buff.DivinePurpose==true&&Skill(3)) return new(9,"神圣意志Buff存在：免费荣耀圣令技能族");
        if(need&&F(92)==true&&Skill(3)) return new(9,"5圣能先消耗荣耀圣令");
        if(healing&&big&&Skill(4)) return raid
            ? new(10,"团队群体大掉血：对当前目标圣洁鸣钟（;）")
            : new(4,"小队群体大掉血：对自己圣洁鸣钟（0，@player宏）");
        if(need&&F(13)==true&&Skill(2)) return new(8,"震击至少2充能：优先治疗，避免充能封顶");
        if(need&&infusion==true&&Skill(5)) return new(11,"灌注Buff存在：圣光闪现");
        // Presence confirms at least one instant Holy Light remains. Re-read after
        // each cast; neither wing key sends nor an assumed two-cast counter grant it.
        if(need&&hand==true&&Skill(6)) return new(12,"神性之手Buff存在：瞬发圣光术（.）");
        if(healing&&big) {
            // Unknown proc/cooldown observations skip only dependent cooldowns;
            // confirmed ordinary heals remain available below.
            if(F(24)==false&&infusion==false&&Skill(7)) return new(13,"群体大掉血、鸣钟冷却且无灌注：复仇之怒");
            if(F(24)==false&&infusion==false&&F(27)==false&&Skill(8)) return new(14,"群体大掉血、鸣钟和翅膀冷却且无灌注：光环掌握");
        }
        if(need) {
            if(Skill(2)) return new(8,"神圣震击治疗");
            if(F(91)==true&&Skill(3)) return new(9,"消耗3圣能荣耀圣令");
        }
        if(F(12)==true&&F(99)==true&&Skill(1)) return new(7,"距离内敌对目标审判");
        if(need&&P(8)<t.Major&&hardLight) return new(12,"目标大掉血、无灌注、震击冷却、静止且法力足够：读条圣光术（.）");
        if(need&&F(10)==true&&Skill(5)) return new(11,"治疗兜底圣光闪现");
        if(need) {
            if(F(10) is null) return new(0,"玩家静止状态未知（点10），普通闪现暂不读条");
            if(F(10)!=true) return new(0,"无可用瞬发治疗；普通闪现需要静止（点10）");
            if(F(25)!=true) return new(0,"等待圣光闪现就绪／已学状态（点25）");
            if(F(35)!=true) return new(0,"等待圣光闪现可用状态／法力（点35）");
        }
        return new(0,need?(infusion is null?"灌注状态未知，等待其他可用治疗技能":"等待可用治疗技能"):"轻微掉血，无需主动治疗");
    }
}
