#nullable enable
using System.Drawing;
using RotationUS.Diagnostics;

internal sealed record HolyDecision(int Key,string Reason,bool Urgent=false);
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
    private readonly HolyPressure pressure=new();
    private void ResetSelection() {pendingSlot=confirmations=0;}
    internal void Reset() {ResetSelection();pressure.Reset();}
    public void Process(Dictionary<int,Color> frames,Dictionary<int,Color> bars,Dictionary<int,bool> states)=>Process(frames,bars,states,null);
    internal void Process(Dictionary<int,Color> frames,Dictionary<int,Color> bars,Dictionary<int,bool> states,double? observationTime)
    {
        bool? F(int id)=>frames.TryGetValue(id,out var c)?FuryBuffs.Boolean(c):null;
        double now=observationTime??(double)System.Diagnostics.Stopwatch.GetTimestamp()/System.Diagnostics.Stopwatch.Frequency;
        var decision=Decide(frames,Buffs,Thresholds,pressure.Observe(frames,Thresholds,now));
        // All members recovered while selecting: a confirmed safe damage action
        // replaces the obsolete heal selection without waiting for its target.
        if(pendingSlot>0&&(decision.Key==18||
            (decision.Urgent&&decision.Key is >=101 and <=105&&decision.Key!=100+pendingSlot))) ResetSelection();
        // Target acknowledgement must not delay emergency self macros/items.
        if(pendingSlot>0 && decision.Key is 1 or 2 or 3 or 5 or 22 or 23)
        { LastDecision=decision;states[decision.Key]=true;return; }
        if(pendingSlot>0) {
            if(F(41)!=false||F(1)!=true||F(2)!=true) ResetSelection();
            else if(F(45+pendingSlot)==true) {
                if(++confirmations<2) {LastDecision=new(0,"等待选人后的第二次目标确认");return;}
                ResetSelection();
            } else {
                confirmations=0;
                if(System.Diagnostics.Stopwatch.GetElapsedTime(selectionStarted).TotalSeconds<2) {
                    LastDecision=new(0,"等待目标切换确认");return;
                }
                ResetSelection();LastDecision=new(0,"选人未确认，请检查 Shift+F1～F5 绑定");return;
            }
        }
        LastDecision=decision;
        if(LastDecision.Key>=101&&LastDecision.Key<=105) {
            pendingSlot=LastDecision.Key-100;confirmations=0;selectionStarted=System.Diagnostics.Stopwatch.GetTimestamp();
        }
        if(LastDecision.Key!=0) {
            states[LastDecision.Key]=true;
            pressure.Requested(LastDecision.Key,frames,Thresholds,now);
        }
    }
    internal static HolyDecision Decide(Dictionary<int,Color> frames,HolyBuffs buff,HealingThresholds? thresholds=null,HolyCooldownContext? cooldowns=null)
    {
        var t=thresholds??new();
        var context=cooldowns??new();
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
            F(92)==true)
            return new(18,"全队存活成员均≥治疗线且5圣能：近战盾击防溢出（Shift+.）");
        if(P(6)<=.40&&F(16)==true) return new(22,"治疗石：血量≤40%");
        if(P(6)<=.40&&F(17)==true) return new(23,"治疗药水：血量≤40%");
        // Divine Protection is off the GCD; target acknowledgement and an
        // active healing cast must not postpone this self defensive.
        if(P(6)<.50&&Skill(10)) return new(5,"自身低于50%：圣佑术（-）");
        if(F(9)!=true||F(20)!=true) return new(0,"等待玩家施法结束或GCD");
        bool selfNeed=P(6) is >0&&P(6)<t.HealBelow;
        bool selfCritical=selfNeed&&P(6)<t.Critical;
        bool healing=F(3)==true&&F(4)==true&&F(5)==true;
        bool need=healing&&P(8) is >0&&P(8)<t.HealBelow;
        int best=0;double bestHp=t.HealBelow;
        if(F(41)==false&&F(19) is not null) {
            for(int slot=1;slot<=5;slot++) {
                if(slot>1&&(F(19)!=true||F(40+slot)!=true)) continue;
                if(!frames.TryGetValue(50+slot,out var c)) continue;
                var member=TeamHealthReading.Read(c);
                if(member.Percent is >0&&member.Percent<bestHp) {bestHp=member.Percent.Value;best=slot;}
            }
        }
        // Ordinary self healing competes with reachable allies; emergency self
        // macros still work with no target and while selection is pending.
        double otherHp=need?P(8)!.Value:t.HealBelow;
        if(best>1) otherHp=Math.Min(otherHp,bestHp);
        bool selfPriority=selfNeed&&(selfCritical||P(6)<=otherHp);
        // A self macro must not repeatedly preempt an available Shock. In a
        // party we can first select the injured ally; raids retain the target.
        bool shockRoute=F(41) is not null&&F(19) is not null&&Skill(2)&&
            ((best>0&&F(45+best)!=true)||
            (F(3)==true&&F(4)==true&&F(5)==true&&P(8)<t.HealBelow));
        bool fullShock=shockRoute&&F(13)==true;
        bool hardLight=infusion==false&&F(22)==false&&F(10)==true&&P(7)>=t.HolyLightMana&&Skill(6);
        bool capped=F(92)==true;
        bool generate=!capped||selfCritical;
        if(selfPriority&&(F(91)==true||buff.DivinePurpose==true)&&Skill(3)) return new(2,
            buff.DivinePurpose==true?"自身神圣意志免费荣耀圣令（8）：低于治疗线":"自身荣耀圣令（8）：低于治疗线且圣能≥3");
        if(selfPriority&&generate&&(selfCritical||!fullShock)&&infusion==true&&Skill(5)) return new(1,"自身灌注圣光闪现：当前最需治疗");
        if(selfPriority&&generate&&(selfCritical||!fullShock)&&hand==true&&Skill(6)) return new(3,"自身神性之手瞬发圣光术（9）：当前最需治疗");
        if(selfPriority&&generate&&P(6)<t.Major&&hardLight) return new(3,"自身大掉血、无灌注、震击冷却、静止且法力足够：读条圣光术（9）");
        if(selfPriority&&generate&&!shockRoute&&F(10)==true&&Skill(5)) return new(1,"自身当前最需治疗且静止：圣光闪现（7）");
        if(F(41) is null||F(19) is null) return new(0,"等待队伍类型");
        // Auto-selection is a separate action. No healing shares its sample.
        if(!raid) {
            if(best>0&&F(45+best)!=true) return new(100+best,"选择范围内血量最低："+(best==1?"自己":"party"+(best-1)),bestHp<t.Critical);
        }
        if(F(3)!=true) return new(0,"等待存活目标");
        if(F(4) is null) return new(0,"等待目标友方状态（点4）");
        if(F(4)==true&&F(5)!=true) return new(0,"等待目标进入治疗距离／距离状态（点5）");
        if(healing&&(P(8) is null)) return new(0,"等待目标血量");
        bool aoe=F(19)==true&&group.Aoe;
        bool big=F(19)==true&&group.Major;
        bool critical=need&&P(8)<t.Critical;
        bool emergency=F(19)==true&&group.Reliable&&group.Urgent>=2;
        bool spender=need&&(F(91)==true||buff.DivinePurpose==true)&&Skill(3);
        bool instant=need&&(spender||((!capped||critical)&&(Skill(2)||(infusion==true&&Skill(5))||(hand==true&&Skill(6)))));
        int tollKey=raid?10:4;
        bool toll=healing&&F(19)==true&&(group.Toll||emergency)&&Skill(4)&&context.Allows(tollKey,emergency);
        // Multiple imminent deaths justify accepting overflow and skipping setup.
        if(toll&&emergency) return new(tollKey,"多人危急：立即鸣钟，允许圣能溢出");
        if(critical) {
            if(spender) return new(9,"单体危急：立即荣耀圣令／永恒之火");
            if(infusion==true&&Skill(5)) return new(11,"单体危急：立即灌注闪现");
            if(hand==true&&Skill(6)) return new(12,"单体危急：立即神性之手圣光术");
            if(Skill(2)) return new(8,"单体危急：立即神圣震击");
        }
        // Never open Virtue with no currently executable follow-up heal. A
        // current observation is a prerequisite, not a promise of future mana.
        bool followup=instant||(need&&!capped&&F(10)==true&&(Skill(5)||hardLight));
        if(healing&&aoe&&!critical&&followup&&Skill(9)&&context.Allows(15)) return new(15,"群体掉血且有后续治疗：美德道标");
        // Only a confirmed proc waives the resource gate. Readiness and usability
        // still apply to the actual spender (including the Eternal Flame override).
        if(need&&buff.DivinePurpose==true&&Skill(3)) return new(9,"神圣意志Buff存在：免费荣耀圣令技能族");
        if(need&&F(92)==true&&Skill(3)) return new(9,"5圣能先消耗荣耀圣令");
        if(toll&&spender) return new(9,"鸣钟前先消耗至少3圣能，再检查伤情");
        bool wings=t.AutoWings&&(!raid||t.RaidAutoWings)&&Skill(7)&&context.Allows(13,emergency);
        if(healing&&aoe&&context.Sustained&&wings) return new(13,"持续恶化的群伤：优先翅膀覆盖后续压力");
        // Unknown resources do not authorize a non-emergency generator burst.
        if(toll&&F(91)==false&&!capped) return new(tollKey,raid?"团队有效群伤：鸣钟（;）":"小队有效群伤：鸣钟（0）");
        bool pressure=big||emergency||(aoe&&context.Sustained)||(critical&&!instant);
        if(healing&&pressure&&wings) return new(13,"持续／严重治疗需求：复仇之怒（不依赖灌注或鸣钟冷却）");
        bool mastery=t.AutoAuraMastery&&t.AuraMasteryHasToll&&(!raid||t.RaidAutoAuraMastery)&&Skill(8)&&context.Allows(14,emergency);
        if(healing&&pressure&&mastery&&!emergency&&spender) return new(9,"光环掌握附带鸣钟前先消耗圣能");
        if(healing&&pressure&&mastery&&(emergency||(F(91)==false&&!capped))) return new(14,"治疗压力仍需加码：带鸣钟的光环掌握");
        generate=!capped||critical;
        if(need&&generate&&F(13)==true&&Skill(2)) return new(8,"震击至少2充能：优先治疗，避免充能封顶");
        if(need&&generate&&infusion==true&&Skill(5)) return new(11,"灌注Buff存在：圣光闪现");
        // Presence confirms at least one instant Holy Light remains. Re-read after
        // each cast; neither wing key sends nor an assumed two-cast counter grant it.
        if(need&&generate&&hand==true&&Skill(6)) return new(12,"神性之手Buff存在：瞬发圣光术（.）");
        if(need) {
            if(generate&&Skill(2)) return new(8,"神圣震击治疗");
            if(F(91)==true&&Skill(3)) return new(9,"消耗3圣能荣耀圣令");
        }
        if(!capped&&F(12)==true&&F(99)==true&&Skill(1)) return new(7,"距离内敌对目标审判");
        if(need&&generate&&P(8)<t.Major&&hardLight) return new(12,"目标大掉血、无灌注、震击冷却、静止且法力足够：读条圣光术（.）");
        if(need&&generate&&F(10)==true&&Skill(5)) return new(11,"治疗兜底圣光闪现");
        if(capped) return new(0,"5圣能：等待有效消耗目标，避免继续产能");
        if(need) {
            if(F(10) is null) return new(0,"玩家静止状态未知（点10），普通闪现暂不读条");
            if(F(10)!=true) return new(0,"无可用瞬发治疗；普通闪现需要静止（点10）");
            if(F(25)!=true) return new(0,"等待圣光闪现就绪／已学状态（点25）");
            if(F(35)!=true) return new(0,"等待圣光闪现可用状态／法力（点35）");
        }
        return new(0,need?(infusion is null?"灌注状态未知，等待其他可用治疗技能":"等待可用治疗技能"):"轻微掉血，无需主动治疗");
    }
}
