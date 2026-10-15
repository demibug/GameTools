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
    private int pendingSlot, confirmations, bloodReducedMask;
    private bool reevaluateSelection;
    private long selectionStarted;
    private readonly HolyPressure pressure=new();
    private readonly HolyHealingWindow healingWindow=new();
    private HolyHealingSample? preparedHealing;
    internal HolyHealingSample HealingSample=>healingWindow.Current;
    internal HolyHealingSample ObserveHealing(Dictionary<int,Color> frames,HealingThresholds t,double now,bool live=true)
        =>preparedHealing=healingWindow.Observe(frames,t,now,live);
    internal PartyRosterSnapshot? PartyRoster {get;private set;}
    internal PartyRunicPower? RunicPower {get;private set;}
    internal void SetRunicPower(PartyRunicPower? value)
    {
        RunicPower=value;
    }
    internal void SetPartyRoster(PartyRosterSnapshot? value)
    {
        if(PartyRoster is null ? value is not null : value is null || !PartyRoster.Matches(value)) Reset();
        PartyRoster=value;
        if(value is null || RunicPower?.Roster.Matches(value)!=true) RunicPower=null;
    }
    private void ResetSelection() {pendingSlot=confirmations=0;reevaluateSelection=false;}
    internal void Reset() {ResetSelection();bloodReducedMask=0;pressure.Reset();healingWindow.Reset();preparedHealing=null;}
    public void Process(Dictionary<int,Color> frames,Dictionary<int,Color> bars,Dictionary<int,bool> states)=>Process(frames,bars,states,null);
    internal void Process(Dictionary<int,Color> frames,Dictionary<int,Color> bars,Dictionary<int,bool> states,double? observationTime)
    {
        bool? F(int id)=>frames.TryGetValue(id,out var c)?FuryBuffs.Boolean(c):null;
        double now=observationTime??(double)System.Diagnostics.Stopwatch.GetTimestamp()/System.Diagnostics.Stopwatch.Frequency;
        var observed=observationTime is not null?healingWindow.Observe(frames,Thresholds,now):
            preparedHealing??healingWindow.Observe(frames,Thresholds,now);
        preparedHealing=null;
        int nextBloodMask=0;
        if(F(41)==false && F(19)==true && F(1)==true)
            for(int slot=1;slot<=5;slot++)
                if(frames.TryGetValue(50+slot,out var color) && TeamHealthReading.Read(color).Percent is {} hp &&
                    hp>=Thresholds.Critical && PartyHealingPriority.BloodReduced(hp,slot,PartyRoster,RunicPower)) nextBloodMask|=1<<slot;
        // Re-evaluate changed priorities without discarding acknowledgement of
        // the same recipient. Retain this request while the GCD blocks Decide.
        if(pendingSlot>0 && nextBloodMask!=bloodReducedMask) reevaluateSelection=true;
        bloodReducedMask=nextBloodMask;
        var decision=Decide(frames,Buffs,Thresholds,pressure.Observe(frames,Thresholds,now) with {Healing=observed},PartyRoster,RunicPower,
            reevaluateSelection?pendingSlot:0);
        // All members recovered while selecting: a confirmed safe damage action
        // replaces the obsolete heal selection without waiting for its target.
        if(pendingSlot>0&&(decision.Key==18||
            ((decision.Urgent||reevaluateSelection)&&decision.Key is >=101 and <=105&&decision.Key!=100+pendingSlot))) ResetSelection();
        // Target acknowledgement must not delay emergency self macros/items.
        if(pendingSlot>0 && decision.Key is 1 or 2 or 3 or 5 or 14 or 22 or 23)
        {
            LastDecision=decision;states[decision.Key]=true;
            pressure.Requested(decision.Key,frames,Thresholds,now);
            return;
        }
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
    internal static HolyDecision Decide(Dictionary<int,Color> frames,HolyBuffs buff,HealingThresholds? thresholds=null,HolyCooldownContext? cooldowns=null,PartyRosterSnapshot? party=null,PartyRunicPower? power=null,int pendingSelection=0)
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
        var partyToll=PartyTollReading.Read(frames,t);
        bool partyTollReady=partyToll.Immediate||partyToll.Emergency||
            (partyToll.Ordinary&&context.Healing?.TollConfirmed==true);
        bool virtueWindow=!raid&&context.Healing?.VirtueActive==true;
        if(F(41)!=false||F(19)!=true||party?.Members.Any(m=>m.Role is 1 or 2)!=true) party=null;
        var group=HolyTriage.Analyze(frames,raid,t);
        // Shield damage has first spell priority only when every observed living
        // member is healthy. Unknown members cannot be treated as full health.
        if(F(3)==true&&F(4)==false&&F(12)==true&&F(95)==true&&F(97)==true&&F(98)==true&&
            F(9)==true&&F(20)==true&&F(19)==true&&F(41) is not null&&
            P(6)>=t.HealBelow&&group.Reliable&&group.Unknown==0&&group.Injured==0&&!group.Major&&
            F(92)==true&&F(13)==true)
            return new(18,"全队存活成员均≥治疗线、5圣能且震击至少2充能：近战盾击防溢出（Shift+.）");
        if(P(6)<=.40&&F(16)==true) return new(22,"治疗石：血量≤40%");
        if(P(6)<=.40&&F(17)==true) return new(23,"治疗药水：血量≤40%");
        // Divine Protection is off the GCD; target acknowledgement and an
        // active healing cast must not postpone this self defensive.
        if(P(6)<.50&&Skill(10)) return new(5,"自身低于50%：圣佑术（-）");
        if(F(9)!=true||F(20)!=true) return new(0,"等待玩家施法结束或GCD");
        bool emergency=raid?F(19)==true&&group.Reliable&&group.Urgent>=2:partyToll.Emergency;
        bool mastery=t.AutoAuraMastery&&t.AuraMasteryHasToll&&(!raid||t.RaidAutoAuraMastery)&&Skill(8)&&context.Allows(14,emergency);
        // Aura Mastery is a self cooldown. After Toll becomes unavailable,
        // multiple imminent deaths must not be perpetually preempted by one
        // target's direct heal, Holy Power spending or target acknowledgement.
        if(emergency&&mastery&&!Skill(4))
            return new(14,"多人危急且鸣钟不可用：紧急追加光环掌握（允许圣能溢出）",true);
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
                if(member.Percent is >0&&member.Percent<t.HealBelow&&
                    (best==0||PartyHealingPriority.Compare(member.Percent.Value,slot,bestHp,best,t,party,power)<0))
                    {bestHp=member.Percent.Value;best=slot;}
            }
        }
        // Self and reachable allies compete by health, including emergencies.
        // Self macros retain a usable route with no friendly target.
        double otherHp=need?P(8)!.Value:t.HealBelow;
        if(best>1) otherHp=Math.Min(otherHp,bestHp);
        bool selfPriority=selfNeed&&P(6)<=otherHp;
        if(party is not null&&selfNeed) {
            int targetSlot=Enumerable.Range(1,5).FirstOrDefault(slot=>F(45+slot)==true);
            selfPriority=(best<=1||PartyHealingPriority.Compare(P(6)!.Value,1,bestHp,best,t,party,power)<=0)&&
                (!need||PartyHealingPriority.Compare(P(6)!.Value,1,P(8)!.Value,targetSlot,t,party,power)<=0);
        }
        bool hardLight=infusion==false&&F(22)==false&&F(10)==true&&P(7)>=t.HolyLightMana&&Skill(6);
        bool capped=F(92)==true;
        bool canSpend=F(91)==true||capped||buff.DivinePurpose==true;
        HolyDecision? Rescue(bool self)
        {
            int Glory=self?2:9,Flash=self?1:11,Light=self?3:12;
            if((capped||buff.DivinePurpose==true)&&canSpend&&Skill(3)) return new(Glory,"危急抢救：优先满圣能／免费荣耀圣令",true);
            if(F(13)==true&&Skill(2)&&(!self||(healing&&F(46)==true))) return new(8,"危急抢救：两充能神圣震击",true);
            if(infusion==true&&Skill(5)) return new(Flash,"危急抢救：立即灌注圣光闪现",true);
            if(hand==true&&Skill(6)) return new(Light,"危急抢救：立即神性之手圣光术",true);
            if(Skill(2)&&(!self||(healing&&F(46)==true))) return new(8,"危急抢救：立即神圣震击",true);
            if(canSpend&&Skill(3)) return new(Glory,"危急抢救：荣耀圣令／永恒之火",true);
            return null;
        }
        int tollKey=raid?10:4;
        bool reselectCurrent=!raid&&pendingSelection>0&&best>0&&best!=pendingSelection&&F(45+best)==true;
        if(emergency&&healing&&Skill(4)&&context.Allows(tollKey,true)&&!reselectCurrent)
            return new(tollKey,"多人危急：立即鸣钟，允许圣能溢出",true);
        if(selfCritical&&selfPriority&&Rescue(true) is { } selfRescue && (selfRescue.Key!=8||!reselectCurrent)) return selfRescue;
        // A previous select key may still be in flight. If changed priorities
        // now prefer the current target, explicitly reselect it and start a new
        // acknowledgement instead of waiting for the obsolete recipient.
        if(reselectCurrent)
            return new(100+best,"治疗优先级变化，重新确认当前目标："+(best==1?"自己":"party"+(best-1)),bestHp<t.Critical);
        // Confirm a critical party recipient before any non-emergency setup.
        if(!raid&&best>0&&bestHp<t.Critical&&F(45+best)!=true)
            return new(100+best,"危急抢救选人："+(best==1?"自己":"party"+(best-1)),true);
        if(need&&P(8)<t.Critical&&Rescue(false) is { } targetRescue) return targetRescue;

        bool aoe=F(19)==true&&group.Aoe;
        bool big=F(19)==true&&group.Major;
        bool critical=need&&P(8)<t.Critical;
        bool HandFor(double? hp)=>hp is >0&&hp<t.HealBelow&&hand==true&&Skill(6)&&
            (hp<t.Major||buff.HandExpiring==true||virtueWindow);
        bool instant=(canSpend&&Skill(3))||Skill(2)||(infusion==true&&Skill(5));
        bool CanFollow(double? hp)=>hp is >0&&hp<t.HealBelow&&
            (instant||HandFor(hp)||(F(10)==true&&(Skill(5)||(hp<t.Major&&hardLight))));
        // Virtue is setup for healing, not an independently observed heal.
        // Keep an already valid recipient, including the player. The priority
        // belongs before ordinary self macros/triage, not to a special unit ID.
        bool urgentRecipient=selfCritical||critical||(!raid&&best>0&&bestHp<t.Critical);
        bool virtueOnSelf=F(46)==true;
        // The buff recipient and the subsequent direct-heal recipient need not
        // be the same unit. Confirmed self identity also proves zero distance.
        bool virtueFollowup=CanFollow(P(6))||(healing&&CanFollow(P(8)))||(!raid&&best>0&&CanFollow(bestHp));
        if(aoe&&!urgentRecipient&&!virtueWindow&&virtueFollowup&&Skill(9)&&context.Allows(15)) {
            if(virtueOnSelf||healing)
                return new(15,"群体掉血：优先美德铺垫，随后直接治疗");
            // Select only when the current recipient cannot support the heal.
            // Party triage treats self and allies equally; raids retain manual targeting.
            if(!raid&&best>0&&CanFollow(bestHp)&&F(45+best)!=true)
                return new(100+best,"为美德选择治疗目标："+(best==1?"自己":"party"+(best-1)));
        }
        // Choose the recipient once; self macros only change the key, not the
        // skill priority. Self Shock still needs a real target/selection route.
        bool selfHeal=selfPriority, needsHeal=selfHeal||need;
        double? healHp=selfHeal?P(6):P(8);
        int glory=selfHeal?2:9,flash=selfHeal?1:11,light=selfHeal?3:12;
        HolyDecision? Shock(bool fullOnly=false) {
            if(!needsHeal||!Skill(2)||(fullOnly&&F(13)!=true)||F(41) is null||F(19) is null) return null;
            if(!selfHeal||(healing&&F(46)==true&&P(8)<t.HealBelow))
                return new(8,fullOnly?"震击至少2充能：优先治疗，避免充能封顶":"神圣震击治疗");
            if(!raid&&best==1&&F(46)!=true) return new(101,"为神圣震击选择自己");
            return null;
        }
        if(!selfHeal) {
            if(F(41) is null||F(19) is null) return new(0,"等待队伍类型");
            // Auto-selection is a separate action. No healing shares its sample.
            if(!raid&&best>0&&F(45+best)!=true)
                return new(100+best,(party is null?"选择范围内血量最低：":"按血量及小队职责选择：")+(best==1?"自己":"party"+(best-1)),bestHp<t.Critical);
            if(F(3)!=true) return new(0,"等待存活目标");
            if(F(4) is null) return new(0,"等待目标友方状态（点4）");
            if(F(4)==true&&F(5)!=true) return new(0,"等待目标进入治疗距离／距离状态（点5）");
            if(healing&&(P(8) is null)) return new(0,"等待目标血量");
        }
        bool spender=needsHeal&&canSpend&&Skill(3);
        bool toll=healing&&F(19)==true&&(raid?group.Toll||emergency:partyTollReady)&&Skill(4)&&context.Allows(tollKey,emergency);
        // Only a confirmed proc waives the resource gate. Readiness and usability
        // still apply to the actual spender (including the Eternal Flame override).
        if(needsHeal&&buff.DivinePurpose==true&&Skill(3)) return new(glory,"神圣意志Buff存在：免费荣耀圣令技能族");
        if(needsHeal&&capped&&Skill(3)) return new(glory,"5圣能先消耗荣耀圣令");
        if(toll&&spender) return new(glory,"鸣钟前先消耗至少3圣能，再检查伤情");
        bool wings=t.AutoWings&&(!raid||t.RaidAutoWings)&&Skill(7)&&context.Allows(13,emergency);
        if(healing&&aoe&&context.Sustained&&wings) return new(13,"持续恶化的群伤：优先翅膀覆盖后续压力");
        // Unknown resources do not authorize a non-emergency generator burst.
        if(toll&&F(91)==false&&(raid?!capped:F(92)==false)) return new(tollKey,raid?"团队有效群伤：鸣钟（;）":
            partyToll.Immediate?"小队至少2人低于大掉血线：鸣钟（0）":"小队至少2人缺血、总缺血≥40个百分点且连续两次确认：鸣钟（0）");
        bool pressure=big||emergency||(aoe&&context.Sustained)||(critical&&!instant&&!HandFor(P(8)));
        if(healing&&pressure&&wings) return new(13,"持续／严重治疗需求：复仇之怒（不依赖灌注或鸣钟冷却）");
        if(healing&&pressure&&mastery&&!emergency&&spender) return new(glory,"光环掌握附带鸣钟前先消耗圣能");
        if(healing&&pressure&&mastery&&(emergency||(F(91)==false&&!capped))) return new(14,"治疗压力仍需加码：带鸣钟的光环掌握");
        if(Shock(true) is { } fullShock) return fullShock;
        if(needsHeal&&infusion==true&&Skill(5)) return new(flash,"灌注Buff存在：圣光闪现");
        // Presence confirms at least one instant Holy Light remains. Re-read after
        // each cast; neither wing key sends nor an assumed two-cast counter grant it.
        if(needsHeal&&HandFor(healHp)) return new(light,healHp<t.Major?"神性之手Buff存在且大掉血：瞬发圣光术":
            buff.HandExpiring==true?"神性之手临期且需要治疗：瞬发圣光术":"小队美德9秒推定窗口：神性之手瞬发圣光术");
        if(Shock() is { } shock) return shock;
        if(needsHeal&&F(91)==true&&Skill(3)) return new(glory,"消耗3圣能荣耀圣令");
        if(needsHeal&&healHp<t.Major&&hardLight) return new(light,"大掉血、无灌注、震击冷却、静止且法力足够：读条圣光术");
        if(needsHeal&&F(10)==true&&Skill(5)) return new(flash,"治疗兜底圣光闪现");
        if(!needsHeal&&!capped&&F(12)==true&&F(99)==true&&Skill(1)) return new(7,"距离内敌对目标审判");
        if(capped&&!needsHeal) return new(0,F(12)==true&&F(95) is null?
            "5圣能：盾击近战距离未知（点95），等待有效治疗消耗目标":
            "5圣能：等待有效消耗目标，避免继续伤害产能");
        if(needsHeal) {
            if(F(10) is null) return new(0,"玩家静止状态未知（点10），普通闪现暂不读条");
            if(F(10)!=true) return new(0,"无可用瞬发治疗；普通闪现需要静止（点10）");
            if(F(25)!=true) return new(0,"等待圣光闪现就绪／已学状态（点25）");
            if(F(35)!=true) return new(0,"等待圣光闪现可用状态／法力（点35）");
        }
        return new(0,needsHeal?(infusion is null?"灌注状态未知，等待其他可用治疗技能":"等待可用治疗技能"):"轻微掉血，无需主动治疗");
    }
}
