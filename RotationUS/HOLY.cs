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
    internal RaidHealingController Raid {get;}=new();
    private HolyHealingSample? preparedHealing;
    private double preparedAt;
    private bool? lastPulse;
    private double lastCaptureAt=double.NegativeInfinity;
    private Dictionary<int,Color>? pressureFrames;
    internal HolyCooldownContext CooldownContext=>pressure.Current with{Healing=HealingSample,Sacrifice=Sacrifice.Current};
    internal HolySacrificeProtection Sacrifice {get;}=new();
    internal HolyHealingSample HealingSample=>healingWindow.Current;
    internal HolyHealingSample ObserveHealing(Dictionary<int,Color> frames,HealingThresholds t,double now,bool live=true,bool executing=true,bool foreground=true,IReadOnlyDictionary<int,Color>? raidBars=null,bool? pulse=null)
    {
        preparedAt=now;
        if(live&&executing&&!foreground) Sacrifice.Reset("游戏不在前台：联动已清除");
        else Sacrifice.Observe(frames,now,live,executing);
        Raid.Observe(frames,raidBars,t,now,live&&(!executing||foreground));
        bool party=frames.TryGetValue(41,out var mode)&&FuryBuffs.Boolean(mode)==false;
        if(!live||!party||!double.IsFinite(now)) {
            pressure.Reset();pressureFrames=null;lastPulse=null;lastCaptureAt=double.NegativeInfinity;
        } else if(now!=lastCaptureAt) {
            if(now<lastCaptureAt||now-lastCaptureAt>1.5) {pressure.Reset();lastPulse=null;}
            bool fresh=pulse is null||lastPulse is null||lastPulse!=pulse;
            lastPulse=pulse;lastCaptureAt=now;
            pressureFrames=HolyTriage.HealingFrames(frames);
            pressure.Observe(pressureFrames,t,now,fresh);
        }
        return preparedHealing=healingWindow.Observe(frames,t,now,live);
    }
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
    internal void CancelExecution(bool clearSacrifice=true)
    {
        ResetSelection();bloodReducedMask=0;pressure.ClearRequests();Raid.CancelExecution();
        if(clearSacrifice) Sacrifice.Reset();
    }
    internal void Reset() {ResetSelection();bloodReducedMask=0;pressure.Reset();healingWindow.Reset();preparedHealing=null;pressureFrames=null;lastPulse=null;lastCaptureAt=double.NegativeInfinity;Sacrifice.Reset();Raid.Reset();}
    public void Process(Dictionary<int,Color> frames,Dictionary<int,Color> bars,Dictionary<int,bool> states)=>Process(frames,bars,states,null);
    internal void Process(Dictionary<int,Color> frames,Dictionary<int,Color> bars,Dictionary<int,bool> states,double? observationTime)
    {
        bool? F(int id)=>frames.TryGetValue(id,out var c)?FuryBuffs.Boolean(c):null;
        double now=observationTime??(preparedHealing is not null?preparedAt:(double)System.Diagnostics.Stopwatch.GetTimestamp()/System.Diagnostics.Stopwatch.Frequency);
        double sacrificeAt=observationTime??(preparedHealing is not null?preparedAt:now);
        if(observationTime is not null||preparedHealing is null) Sacrifice.Observe(frames,sacrificeAt);
        if(F(41)==true) {
            ResetSelection();bloodReducedMask=0;pressure.Reset();healingWindow.Reset();preparedHealing=null;
            LastDecision=Raid.Process(frames,bars,Buffs,Thresholds,Sacrifice.Current,sacrificeAt);
            if(LastDecision.Key!=0) states[LastDecision.Key]=true;
            if(LastDecision.Key==5) Sacrifice.Requested(sacrificeAt);
            return;
        }
        Raid.Reset();
        if(observationTime is not null||preparedHealing is null) ObserveHealing(frames,Thresholds,now);
        var observed=preparedHealing!;
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
        bool targetConfirmed=pendingSlot==0||(F(45+pendingSlot)==true&&confirmations>=1);
        var decision=Decide(frames,Buffs,Thresholds,CooldownContext with{Healing=observed},PartyRoster,RunicPower,
            reevaluateSelection?pendingSlot:0,targetConfirmed:targetConfirmed);
        // All members recovered while selecting: a confirmed safe damage action
        // replaces the obsolete heal selection without waiting for its target.
        if(pendingSlot>0&&(decision.Key==18||
            ((decision.Urgent||reevaluateSelection)&&decision.Key is >=101 and <=105&&decision.Key!=100+pendingSlot))) ResetSelection();
        // Target acknowledgement must not delay emergency self macros/items.
        if(pendingSlot>0 && decision.Key is 1 or 2 or 3 or 5 or 14 or 22 or 23)
        {
            LastDecision=decision;states[decision.Key]=true;
            if(decision.Key==5) Sacrifice.Requested(sacrificeAt);
            pressure.Requested(decision.Key,pressureFrames??frames,Thresholds,now);
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
                ResetSelection();LastDecision=new(0,"选人未确认，请检查 F1～F5 绑定");return;
            }
        }
        LastDecision=decision;
        if(LastDecision.Key>=101&&LastDecision.Key<=105) {
            pendingSlot=LastDecision.Key-100;confirmations=0;selectionStarted=System.Diagnostics.Stopwatch.GetTimestamp();
        }
        if(LastDecision.Key!=0) {
            states[LastDecision.Key]=true;
            if(LastDecision.Key==5) Sacrifice.Requested(sacrificeAt);
            pressure.Requested(LastDecision.Key,pressureFrames??frames,Thresholds,now);
        }
    }
    internal static HolyDecision Decide(Dictionary<int,Color> frames,HolyBuffs buff,HealingThresholds? thresholds=null,HolyCooldownContext? cooldowns=null,PartyRosterSnapshot? party=null,PartyRunicPower? power=null,int pendingSelection=0,RaidRosterSnapshot? raidRoster=null,RaidHealingSample? raidHealing=null,bool targetConfirmed=true)
    {
        var t=thresholds??new();
        var context=cooldowns??new();
        if(frames.TryGetValue(41,out var mode)&&FuryBuffs.Boolean(mode)==true)
            return RaidHolyPolicy.Decide(frames,buff,t,context,raidRoster,raidHealing,pendingSelection,targetConfirmed);
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
        var all=HolyTriage.Analyze(frames,raid,t);
        var treatment=HolyTriage.HealingFrames(frames);
        var group=HolyTriage.Analyze(treatment,raid,t);
        if(P(6)<=.40&&F(16)==true) return new(22,"治疗石：血量≤40%");
        if(P(6)<=.40&&F(17)==true) return new(23,"治疗药水：血量≤40%");
        // Independent of HP and target. Global combat/alive/protocol guards above
        // still apply; this self defensive precedes damage and ordinary healing.
        if(F(19)==true&&F(41) is not null&&context.Sacrifice?.Request==true&&Skill(10))
            return new(5,"牺牲祝福后自动圣佑术（-）：无视血量，战斗且存活未骑乘",true);
        // Shield damage has first spell priority only when every observed living
        // member is healthy. Unknown members cannot be treated as full health.
        if(F(3)==true&&F(4)==false&&F(12)==true&&F(95)==true&&F(97)==true&&F(98)==true&&
            F(9)==true&&F(20)==true&&F(19)==true&&F(41) is not null&&
            P(6)>=t.HealBelow&&all.Reliable&&all.Unknown==0&&all.Injured==0&&!all.Major&&
            ((F(92)==true&&F(13)==true)||buff.PurposeExpiringConfirmed))
            return new(18,buff.PurposeExpiringConfirmed?
                "神圣意志临期且全队安全：忽略圣能与震击充能，近战盾击消耗免费触发（Shift+.）":
                "全队存活成员均≥治疗线、5圣能且震击至少2充能：近战盾击防溢出（Shift+.）");
        // Divine Protection is off the GCD; target acknowledgement and an
        // active healing cast must not postpone this self defensive.
        if(P(6)<.60&&Skill(10)&&!(F(19)==true&&F(41) is not null&&context.Sacrifice?.Holding==true)) return new(5,"自身低于60%：圣佑术（-）");
        if(F(9)!=true||F(20)!=true) return new(0,"等待玩家施法结束或GCD");
        bool emergency=raid?F(19)==true&&group.Reliable&&group.Urgent>=HolyTriage.MinimumGroupMembers:partyToll.Emergency;
        var masterySample=HolyMasterySample.Read(treatment,t) with{
            SevereConfirmed=context.Mastery?.SevereConfirmed==true,Worsening=context.Mastery?.Worsening==true};
        bool mastery=Skill(8)&&context.Allows(14,emergency)&&HolyMasteryPolicy.Eligible(masterySample,frames);
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
        bool HardFlash(double? hp)=>hp>=.75&&hp<.85;
        bool CanHardCast(double? hp)=>hp is >0&&hp<t.HealBelow&&F(10)==true&&Skill(HardFlash(hp)?5:6);
        bool capped=F(92)==true;
        bool canSpend=F(91)==true||capped||buff.DivinePurpose==true;
        HolyDecision? OrdinaryMastery()=>!emergency&&mastery&&F(19)==true&&F(41)==false&&
            F(91)==false&&F(92)==false&&buff.DivinePurpose!=true?
            new(14,HolyMasteryPolicy.Reason(masterySample,frames,t,false)):null;
        bool aoe=F(19)==true&&group.Aoe;
        bool big=F(19)==true&&group.Major;
        bool critical=need&&P(8)<t.Critical;
        bool HandFor(double? hp)=>hp is >0&&hp<t.HealBelow&&hand==true&&Skill(6)&&
            (hp<t.Major||buff.HandExpiring==true||virtueWindow);
        bool MovingHand(double? hp)=>hp is >0&&hp<t.HealBelow&&F(10)==false&&hand==true&&Skill(6);
        bool instant=(canSpend&&Skill(3))||(infusion==true&&Skill(5));
        bool CanFollow(double? hp,bool shockRoute=true)=>hp is >0&&hp<t.HealBelow&&
            (instant||(shockRoute&&Skill(2))||HandFor(hp)||MovingHand(hp)||CanHardCast(hp));
        bool urgentRecipient=selfCritical||critical||(!raid&&best>0&&bestHp<t.Critical);
        bool reselectCurrent=!raid&&pendingSelection>0&&best>0&&best!=pendingSelection&&F(45+best)==true;
        // Virtue may land on a different unit, but its follow-up must be usable
        // on the recipient normal triage will actually heal/select next.
        bool selfShockRoute=F(41) is not null&&F(19) is not null&&
            ((healing&&F(46)==true&&P(8)<t.HealBelow)||(!raid&&best==1));
        bool virtueFollowup=selfPriority?CanFollow(P(6),selfShockRoute):
            !raid&&best>0&&F(45+best)!=true?CanFollow(bestHp):
            healing&&CanFollow(P(8));
        HolyDecision? VirtueSetup(bool allowSelection)
        {
            if(!(aoe||emergency)||virtueWindow||!virtueFollowup||!Skill(9)||!context.Allows(15)) return null;
            if((F(46)==true||healing)&&!reselectCurrent&&(targetConfirmed||!(urgentRecipient||emergency))) {
                if(!targetConfirmed&&OrdinaryMastery() is {} readyMastery) return readyMastery;
                return new(15,urgentRecipient||emergency?"群体危急：优先美德铺垫，随后立即抢救":"群体掉血：优先美德铺垫，随后直接治疗");
            }
            if(allowSelection&&!raid&&best>0&&CanFollow(bestHp)&&F(45+best)!=true)
                return OrdinaryMastery()??new(100+best,"为美德选择治疗目标："+(best==1?"自己":"party"+(best-1)));
            return null;
        }
        // A usable Virtue opens group rescue too. Without a confirmed recipient
        // or executable follow-up, continue rescue immediately instead of
        // switching targets or waiting merely to prepare Virtue.
        if((urgentRecipient||emergency)&&VirtueSetup(false) is { } urgentVirtue) return urgentVirtue;
        // Mastery is a self cooldown; unavailable Virtue and Toll must not let
        // direct-heal or target-acknowledgement loops starve emergency Mastery.
        bool executableToll=healing&&targetConfirmed&&Skill(4)&&context.Allows(raid?10:4,true)&&!reselectCurrent;
        if(emergency&&mastery&&!executableToll)
            return new(14,"多人危急且鸣钟当前无可执行路径：紧急光环掌握（允许圣能溢出）",true);
        HolyDecision? Rescue(bool self)
        {
            int Glory=self?2:9,Flash=self?1:11,Light=self?3:12;
            // Group Virtue and emergency cooldowns retain their earlier priority.
            // Once direct self rescue is selected, spend usable Holy Power first.
            if(self&&canSpend&&Skill(3)) return new(Glory,"自身危急：优先荣耀圣令／永恒之火",true);
            if((capped||buff.DivinePurpose==true)&&canSpend&&Skill(3)) return new(Glory,"危急抢救：优先满圣能／免费荣耀圣令",true);
            if(F(13)==true&&Skill(2)&&(!self||(healing&&F(46)==true))) return new(8,"危急抢救：两充能神圣震击",true);
            if(infusion==true&&Skill(5)) return new(Flash,"危急抢救：立即灌注圣光闪现",true);
            if(hand==true&&Skill(6)) return new(Light,"危急抢救：立即神性之手圣光术",true);
            if(Skill(2)&&(!self||(healing&&F(46)==true))) return new(8,"危急抢救：立即神圣震击",true);
            if(canSpend&&Skill(3)) return new(Glory,"危急抢救：荣耀圣令／永恒之火",true);
            return null;
        }
        int tollKey=raid?10:4;
        if(emergency&&executableToll)
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

        // Expiring free healing precedes ordinary setup, but retains emergency
        // cooldowns, recipient triage and the normal two-sample target acknowledgement.
        if(buff.PurposeExpiringConfirmed&&Skill(3)) {
            if(selfPriority) return new(2,"神圣意志临期：优先自身免费荣耀圣令／永恒之火");
            if(!raid&&best>0&&F(45+best)!=true)
                return new(100+best,"神圣意志临期：选择治疗对象 "+(best==1?"自己":"party"+(best-1)));
            if(need&&F(41) is not null&&F(19) is not null)
                return new(9,"神圣意志临期：优先免费荣耀圣令／永恒之火，先于美德铺垫");
        }

        if(!urgentRecipient&&VirtueSetup(true) is { } virtue) return virtue;
        if(!targetConfirmed&&OrdinaryMastery() is {} independentMastery) return independentMastery;
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
                return OrdinaryMastery()??new(100+best,(party is null?"选择范围内血量最低：":"按血量及小队职责选择：")+(best==1?"自己":"party"+(best-1)),bestHp<t.Critical);
            if(!healing&&OrdinaryMastery() is {} noTargetMastery) return noTargetMastery;
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
        bool wingsWorsening=context.Sustained&&(raid?aoe:F(19)==true&&group.PartyWingsLow);
        bool wingsPressure=raid?big||emergency||wingsWorsening:F(19)==true&&(group.PartyWingsSevere||wingsWorsening);
        if(healing&&wingsWorsening&&wings) return new(13,raid?"持续恶化的团队群伤：优先翅膀":"小队至少3人低于70%且持续恶化：优先翅膀");
        // Unknown resources do not authorize a non-emergency generator burst.
        if(toll&&F(91)==false&&(raid?!capped:F(92)==false)) return new(tollKey,raid?"团队有效群伤：鸣钟（;）":
            partyToll.Immediate?"小队至少3人低于大掉血线：鸣钟（0）":"小队至少3人缺血、总缺血≥40个百分点且连续两次确认：鸣钟（0）");
        if(healing&&wingsPressure&&wings) return new(13,raid?"持续／严重团队治疗需求：复仇之怒":"小队至少3人低于60%：复仇之怒");
        if(mastery&&!emergency&&spender) return new(glory,"光环掌握附带鸣钟前先消耗圣能");
        if(OrdinaryMastery() is {} normalMastery) return normalMastery;
        if(Shock(true) is { } fullShock) return fullShock;
        if(needsHeal&&infusion==true&&Skill(5)) return new(flash,"灌注Buff存在：圣光闪现");
        // Presence confirms at least one instant Holy Light remains. Re-read after
        // each cast; neither wing key sends nor an assumed two-cast counter grant it.
        if(needsHeal&&HandFor(healHp)) return new(light,healHp<t.Major?"神性之手Buff存在且大掉血：瞬发圣光术":
            buff.HandExpiring==true?"神性之手临期且需要治疗：瞬发圣光术":"小队美德9秒推定窗口：神性之手瞬发圣光术");
        if(Shock() is { } shock) return shock;
        if(needsHeal&&F(91)==true&&Skill(3)) return new(glory,"消耗3圣能荣耀圣令");
        if(needsHeal&&MovingHand(healHp)) return new(light,"移动中无其他可执行瞬发：神性之手圣光术兜底");
        if(needsHeal&&CanHardCast(healHp)) return HardFlash(healHp)?
            new(flash,"血量75%～<85%：站定读条圣光闪现"):
            new(light,"闪现读条血量区间外：站定圣光术兜底");
        if(!needsHeal&&!capped&&F(12)==true&&F(99)==true&&Skill(1)) return new(7,"距离内敌对目标审判");
        if(capped&&!needsHeal) return new(0,F(12)==true&&F(95) is null?
            "5圣能：盾击近战距离未知（点95），等待有效治疗消耗目标":
            "5圣能：等待有效消耗目标，避免继续伤害产能");
        if(needsHeal) {
            if(F(10) is null) return new(0,"玩家静止状态未知（点10），普通治疗暂不读条");
            if(F(10)!=true) return new(0,"无可用瞬发治疗；普通读条治疗需要静止（点10）");
            int castSlot=HardFlash(healHp)?5:6;
            string castName=HardFlash(healHp)?"圣光闪现":"圣光术";
            if(F(20+castSlot)!=true) return new(0,$"等待{castName}就绪／已学状态（点{20+castSlot}）");
            if(F(30+castSlot)!=true) return new(0,$"等待{castName}可用状态／法力（点{30+castSlot}）");
        }
        return new(0,needsHeal?(infusion is null?"灌注状态未知，等待其他可用治疗技能":"等待可用治疗技能"):"轻微掉血，无需主动治疗");
    }
}
