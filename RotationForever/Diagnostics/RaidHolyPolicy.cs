#nullable enable
using System.Drawing;

namespace RotationUS.Diagnostics;

// Deliberately separate from the party policy: four-person raid gates and raid
// targeting must never change party roles, Blood DK priority, or its windows.
internal static class RaidHolyPolicy
{
    internal static int Best(Dictionary<int,Color> f,RaidRosterSnapshot? roster,HealingThresholds t)
    {
        if(roster is null||!roster.BindingsReady) return 0;
        int best=0;double lowest=t.HealBelow;
        for(int slot=1;slot<=30;slot++) {
            if(!roster.Reachable(slot)||!f.TryGetValue(50+slot,out var c)||TeamHealthReading.Read(c).Percent is not {} hp||hp<=0||hp>=t.HealBelow) continue;
            if(best==0||hp<lowest||(hp==lowest&&roster.Target(slot))) {best=slot;lowest=hp;}
        }
        return best;
    }
    internal static HolyDecision Decide(Dictionary<int,Color> f,HolyBuffs buff,HealingThresholds t,HolyCooldownContext context,
        RaidRosterSnapshot? roster,RaidHealingSample? sample=null,int pendingSelection=0,bool targetConfirmed=true)
    {
        bool? F(int id)=>f.TryGetValue(id,out var c)?FuryBuffs.Boolean(c):null;
        double? P(int id)=>f.TryGetValue(id,out var c)&&c.R==c.G&&c.G==c.B?c.R/255.0:null;
        double? Member(int slot)=>f.TryGetValue(50+slot,out var c)?TeamHealthReading.Read(c).Percent:null;
        bool Skill(int n)=>F(20+n)==true&&F(30+n)==true;
        HolyDecision Select(int slot,string why,bool urgent=false)=>new(200+slot,why+" raid"+slot,urgent);
        if(F(96)!=true||F(100)!=true) return new(0,"等待新版神圣采集器，请更新插件并 /reload");
        if(F(1)!=true||F(2)!=true) return new(0,"等待玩家存活、战斗且未骑乘");
        if(P(6)<=.40&&F(16)==true) return new(22,"治疗石：血量≤40%");
        if(P(6)<=.40&&F(17)==true) return new(23,"治疗药水：血量≤40%");
        if(F(19)==true&&context.Sacrifice?.Request==true&&Skill(10)) return new(5,"牺牲祝福后自动圣佑术（-）",true);
        var all=HolyTriage.Analyze(f,true,t);
        if(roster is not null&&F(3)==true&&F(4)==false&&F(12)==true&&F(95)==true&&F(97)==true&&F(98)==true&&
            F(9)==true&&F(20)==true&&F(19)==true&&P(6)>=t.HealBelow&&all.Reliable&&all.Unknown==0&&all.Injured==0&&!all.Major&&
            ((F(92)==true&&F(13)==true)||buff.PurposeExpiringConfirmed))
            return new(18,"全团健康：近战盾击消耗满圣能／临期免费触发（Shift+.）");
        if(P(6)<.60&&Skill(10)&&context.Sacrifice?.Holding!=true) return new(5,"自身低于60%：圣佑术（-）");
        if(F(9)!=true||F(20)!=true) return new(0,"等待玩家施法结束或GCD");

        bool? infusion=F(93)??buff.Infusion,hand=F(94)??buff.HandOfDivinity;
        var group=F(19)==true?RaidInjury.Read(f,roster,t):new();
        bool window=sample?.VirtueActive==true;
        bool emergency=group.Emergency;
        var masterySample=HolyMasterySample.Read(HolyTriage.HealingFrames(f,roster),t) with{
            SevereConfirmed=context.Mastery?.SevereConfirmed==true,Worsening=context.Mastery?.Worsening==true};
        bool mastery=t.RaidAutoAuraMastery&&Skill(8)&&context.Allows(14,emergency)&&HolyMasteryPolicy.Eligible(masterySample,f);
        bool selfNeed=P(6) is >0&&P(6)<t.HealBelow,selfCritical=selfNeed&&P(6)<t.Critical;
        int best=F(19)==true?Best(f,roster,t):0;
        double bestHp=best>0?Member(best)!.Value:t.HealBelow;
        int target=roster?.TargetSlot??0,selfSlot=roster?.SelfSlot??0;
        bool targetSelf=F(46)==true||(target>0&&target==selfSlot);
        bool healing=roster is {BindingsReady:true}&&target>0&&roster.Reachable(target)&&F(3)==true&&F(4)==true&&(targetSelf||F(5)==true);
        bool need=healing&&P(8) is >0&&P(8)<t.HealBelow;
        double otherHp=need?P(8)!.Value:t.HealBelow;
        if(best>0&&best!=selfSlot) otherHp=Math.Min(otherHp,bestHp);
        bool selfPriority=selfNeed&&P(6)<=otherHp;
        bool critical=need&&P(8)<t.Critical;
        bool capped=F(92)==true,canSpend=F(91)==true||capped||buff.DivinePurpose==true;
        bool masterySpender=canSpend&&Skill(3)&&(selfPriority||
            (need&&targetConfirmed&&(best==0||roster!.Target(best))));
        HolyDecision? OrdinaryMastery()=>!emergency&&mastery&&F(19)==true&&!masterySpender?
            new(14,HolyMasteryPolicy.Reason(masterySample,f,t,true)):null;
        bool HardFlash(double? hp)=>hp>=.75&&hp<.85;
        int HardCast(double? hp) {
            if(hp is not >0||hp>=t.HealBelow||F(10)!=true) return 0;
            int preferred=HardFlash(hp)?5:6,other=preferred==5?6:5;
            return Skill(preferred)?preferred:Skill(other)?other:0;
        }
        bool CanHardCast(double? hp)=>HardCast(hp)!=0;
        bool HandFor(double? hp)=>hp is >0&&hp<t.HealBelow&&hand==true&&Skill(6)&&(hp<t.Major||buff.HandExpiring==true||window);
        bool MovingHand(double? hp)=>hp is >0&&hp<t.HealBelow&&F(10)==false&&hand==true&&Skill(6);
        bool instant=(canSpend&&Skill(3))||(infusion==true&&Skill(5));
        bool CanFollow(double? hp,bool shockRoute=true)=>hp is >0&&hp<t.HealBelow&&
            (instant||(shockRoute&&Skill(2))||HandFor(hp)||MovingHand(hp)||CanHardCast(hp));
        bool urgentRecipient=selfCritical||critical||(best>0&&bestHp<t.Critical);
        bool reselect=pendingSelection>0&&best>0&&best!=pendingSelection&&roster!.Target(best);
        // Match normal triage, including self macros outside the first30.
        // A heal for another health band cannot promise this recipient's heal.
        // Tied critical self/target health may retain the current ally for the
        // direct-rescue Shock before ordinary self targeting runs.
        bool currentRescueShock=critical&&!reselect&&(best==0||bestHp>=t.Critical||roster!.Target(best));
        bool selfShockRoute=(healing&&targetSelf)||(best==selfSlot&&selfSlot is >=1 and <=30)||currentRescueShock;
        bool followup=selfPriority?CanFollow(P(6),selfShockRoute):
            best>0&&!roster!.Target(best)?CanFollow(bestHp):
            healing&&CanFollow(P(8));
        HolyDecision? Virtue(bool allowSelection)
        {
            if(!group.Aoe||window||!followup||!Skill(9)||!context.Allows(15)) return null;
            if(healing&&!reselect&&(targetConfirmed||!urgentRecipient)) {
                if(!targetConfirmed&&OrdinaryMastery() is {} readyMastery) return readyMastery;
                return new(15,"团队至少4人受伤：先美德铺垫，随后直接治疗");
            }
            if(allowSelection&&best>0&&CanFollow(bestHp)&&!roster!.Target(best)) return OrdinaryMastery()??Select(best,"为团队美德选择");
            return null;
        }
        if(urgentRecipient&&Virtue(false) is {} urgentVirtue) return urgentVirtue;
        bool executableToll=healing&&targetConfirmed&&Skill(4)&&context.Allows(10,true)&&!reselect;
        if(emergency&&mastery&&!executableToll) return new(14,"团队至少4人危急，鸣钟当前无可执行路径：紧急光环掌握",true);
        HolyDecision? Rescue(bool self)
        {
            int glory=self?2:9,flash=self?1:11,light=self?3:12;
            if(self&&canSpend&&Skill(3)) return new(glory,"自身危急：优先荣耀圣令／永恒之火",true);
            if((capped||buff.DivinePurpose==true)&&canSpend&&Skill(3)) return new(glory,"危急抢救：满圣能／免费荣耀",true);
            if(F(13)==true&&Skill(2)&&(!self||(healing&&targetSelf))) return new(8,"危急抢救：两充能神圣震击",true);
            if(infusion==true&&Skill(5)) return new(flash,"危急抢救：灌注圣光闪现",true);
            if(hand==true&&Skill(6)) return new(light,"危急抢救：神性之手圣光术",true);
            if(Skill(2)&&(!self||(healing&&targetSelf))) return new(8,"危急抢救：神圣震击",true);
            if(canSpend&&Skill(3)) return new(glory,"危急抢救：荣耀圣令／永恒之火",true);
            return null;
        }
        if(emergency&&executableToll) return new(10,"团队至少4人危急：立即鸣钟，允许圣能溢出",true);
        if(selfCritical&&selfPriority&&Rescue(true) is {} selfRescue&&(selfRescue.Key!=8||!reselect)) return selfRescue;
        if(reselect) return Select(best,"团队治疗优先级变化，重新确认当前目标",bestHp<t.Critical);
        if(best>0&&bestHp<t.Critical&&!roster!.Target(best)) return Select(best,"团队危急抢救选人",true);
        if(critical&&Rescue(false) is {} rescue) return rescue;
        if(buff.PurposeExpiringConfirmed&&Skill(3)) {
            if(selfPriority) return new(2,"神圣意志临期：自身免费荣耀");
            if(best>0&&!roster!.Target(best)) return Select(best,"神圣意志临期：选择团队治疗对象");
            if(need) return new(9,"神圣意志临期：免费荣耀先于普通美德铺垫");
        }
        if(!urgentRecipient&&Virtue(true) is {} virtue) return virtue;
        if(!targetConfirmed&&OrdinaryMastery() is {} independentMastery) return independentMastery;
        bool selfHeal=selfPriority,needsHeal=selfHeal||need;
        double? healHp=selfHeal?P(6):P(8);
        int gloryKey=selfHeal?2:9,flashKey=selfHeal?1:11,lightKey=selfHeal?3:12;
        HolyDecision? Shock(bool fullOnly=false)
        {
            if(!needsHeal||!Skill(2)||(fullOnly&&F(13)!=true)) return null;
            if(!selfHeal||(healing&&targetSelf)) return new(8,fullOnly?"两充能震击优先治疗":"神圣震击治疗");
            if(best==selfSlot&&selfSlot is >=1 and <=30&&!roster!.Target(selfSlot)) return Select(selfSlot,"为自身神圣震击选择");
            return null;
        }
        if(!selfHeal) {
            if(roster is null) return new(0,"等待团队选人协议，请更新插件并脱战 /reload");
            // Missing selection bindings must not block a qualified self cooldown.
            // Keep the ordinary resource and injury gates in OrdinaryMastery.
            if(!roster.BindingsReady) return OrdinaryMastery()??new(0,"等待团队快捷键就绪，请脱战初始化插件");
            if(best>0&&!roster.Target(best)) return OrdinaryMastery()??Select(best,"选择范围内最低血量团队成员",bestHp<t.Critical);
            if(!healing&&OrdinaryMastery() is {} noTargetMastery) return noTargetMastery;
            if(F(3)!=true) return new(0,"等待存活目标／可治疗团队成员");
            if(F(4)==true&&!healing) return new(0,"等待团队目标身份及治疗距离确认");
            if(healing&&P(8) is null) return new(0,"等待目标血量");
        }
        bool spender=needsHeal&&canSpend&&Skill(3);
        bool toll=healing&&(group.Immediate||emergency||(group.Ordinary&&sample?.TollConfirmed==true))&&Skill(4)&&context.Allows(10,emergency);
        if(needsHeal&&buff.DivinePurpose==true&&Skill(3)) return new(gloryKey,"神圣意志：免费荣耀圣令");
        if(needsHeal&&capped&&Skill(3)) return new(gloryKey,"5圣能优先荣耀消耗");
        if(toll&&spender) return new(gloryKey,"团队鸣钟前先消耗至少3圣能，再检查伤情");
        bool wings=t.AutoWings&&t.RaidAutoWings&&Skill(7)&&context.Allows(13,emergency);
        bool worsening=group.Reliable&&group.WingsLow>=4&&context.Sustained;
        if(healing&&worsening&&wings) return new(13,"团队至少4人低于70%且持续恶化：优先翅膀");
        if(toll&&F(91)==false&&F(92)==false) return new(10,group.Immediate?"团队至少4人大掉血：鸣钟（;）":"团队至少4人缺血、总缺血≥40个百分点且连续两次确认：鸣钟（;）");
        if(healing&&group.Reliable&&(group.WingsSevere>=4||worsening)&&wings) return new(13,"团队至少4人严重掉血：复仇之怒");
        if(mastery&&!emergency&&masterySpender) return new(gloryKey,"光环掌握附带鸣钟前先消耗可用荣耀圣令，再检查伤情");
        if(OrdinaryMastery() is {} normalMastery) return normalMastery;
        if(Shock(true) is {} fullShock) return fullShock;
        if(needsHeal&&infusion==true&&Skill(5)) return new(flashKey,"灌注Buff：圣光闪现");
        if(needsHeal&&HandFor(healHp)) return new(lightKey,healHp<t.Major?"神性之手：大掉血瞬发圣光术":buff.HandExpiring==true?"神性之手临期：瞬发圣光术":"团队美德9秒推定窗口：神性之手瞬发圣光术");
        if(Shock() is {} shock) return shock;
        if(needsHeal&&F(91)==true&&Skill(3)) return new(gloryKey,"消耗3圣能荣耀圣令");
        if(needsHeal&&MovingHand(healHp)) return new(lightKey,"移动中无其他可执行瞬发：神性之手圣光术兜底");
        if(needsHeal&&HardCast(healHp) is var cast&&cast!=0) return cast==5?
            new(flashKey,HardFlash(healHp)?"血量75%～<85%：优先站定读条圣光闪现":"圣光术不可用：站定圣光闪现兜底"):
            new(lightKey,HardFlash(healHp)?"圣光闪现不可用：站定圣光术兜底":"闪现优先区间外：优先站定圣光术");
        if(!needsHeal&&!capped&&F(12)==true&&F(99)==true&&Skill(1)) return new(7,"距离内敌对目标审判");
        return new(0,needsHeal?"等待可用治疗技能／站定读条":"轻微掉血，无需主动治疗");
    }
}
