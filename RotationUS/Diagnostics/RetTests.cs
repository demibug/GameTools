#nullable enable
using System.Drawing;
using System.Windows.Forms;

namespace RotationUS.Diagnostics;

internal static class RetTests
{
    internal static int Run()
    {
        int count=0;
        void Check(bool condition,string why) { if(!condition) throw new Exception("FAIL: 惩戒 · "+why);count++; }
        var f=new Dictionary<int,Color>();var b=new Dictionary<int,Color>();var buff=new RetBuffs();
        void F(int i,bool v=true)=>f[i]=v?Color.White:Color.Black;
        void B(int i,bool v=true)=>b[i]=v?Color.White:Color.Black;
        BuffObservation O(bool present=false,bool match=false)=>new(present,match,"测试");
        void Skill(int i,bool v=true) { F(20+i*2,v);F(21+i*2,v);if(i>0) F(39+i,v); }
        void Reset(bool aoe=false)
        {
            for(int i=1;i<=100;i++) { F(i,false);B(i,false); }
            foreach(int i in new[]{1,2,3,4,9,16,50}) F(i);
            foreach(int i in new[]{8,9,10,12,13,15,17,18,28}) B(i);
            for(int i=30;i<=42;i++) B(i);
            f[19]=RetBuffs.Signature;f[6]=Color.White;b[14]=Color.FromArgb(1,70,1);F(5,aoe);
            buff=new(){Art=O(),Legacy=O(),FreeHammer=O(),Ruling=O(),Wings=O(),Sentence=O(),Expurgation=O(true)};
        }
        RetDecision Decide(RetRequest? r=null,double now=0)=>CJQ.Decide(f,b,buff,r,now);
        void Want(int key,string why) { var d=Decide();Check(d.Key==key,why+"；实际="+d.Reason); }
        void Dps() { for(int i=0;i<9;i++) if(i!=2) Skill(i); }
        Reset();Dps();Want(12,"无标记翅膀处决就绪也不抢灰烬");
        Skill(5,false);Want(13,"无标记鸣钟照常自动");
        Skill(6,false);buff.Art=O(true,true);Want(14,"战争艺术两层先于普通消耗");
        buff.Art=O(true);Want(11,"单体普通终结技");
        F(8);Skill(5);Skill(6);buff.Art=O(true,true);Want(11,"满圣能先消耗");
        Want(11,"免费消耗后重采样仍5圣能，不能虚减资源");
        Reset(true);Dps();F(8);Want(9,"两目标满圣能神圣风暴");
        buff.Legacy=O(true);Want(11,"群体苍穹之遗例外");
        F(8,false);Skill(5,false);Skill(6,false);Want(11,"普通消耗也尊重苍穹之遗");
        buff.Legacy=default;Want(0,"相关苍穹之遗未知不猜测");
        B(15,false);Want(9,"未学苍穹之遗无须配置对应点");
        Reset();Skill(2);B(6);Dps();Want(12,"常规锤与灰烬共用句号键");
        Check(Decide().Reason=="灰烬后的常规圣光之锤","同键仍识别常规锤候选");
        buff.FreeHammer=default;Want(0,"锤形态可用但免费触发未知不误放");
        buff.FreeHammer=O(true);B(7);buff.Ruling=O(true);Want(13,"免费锤可短暂保留同时允许鸣钟，灰烬已被替换");
        buff.FreeHammer=O(true,true);Want(12,"免费锤临期用共用句号键消耗");
        Check(Decide().Reason.StartsWith("免费圣光之锤"),"同键仍识别免费锤候选");
        buff.FreeHammer=O(true);buff.Wings=O(true,true);Want(12,"翅膀临期消耗免费锤");
        buff.Wings=O();buff.Sentence=O(true,true);Want(12,"处决临期消耗免费锤");
        buff.Sentence=O();buff.Ruling=O();Want(12,"未请求翅膀不无限扣住免费锤");
        Skill(5,false);B(6,false);Want(13,"免费锤可用但灰烬按钮未替换时不按共用键");
        b[6]=Color.Orange;Want(0,"共用按钮替换状态未知时等待");
        Reset();Dps();F(5);b[15]=Color.Orange;buff.Legacy=default;F(8);Want(0,"群体增益未知挡住满能量消耗");
        Reset();Dps();f[5]=Color.Orange;Want(0,"目标数未知不猜单体");
        Reset();Dps();F(9,false);Want(0,"资源来源失效");
        foreach(bool aoe in new[]{false,true}) {
            Reset(aoe);F(7,false);F(8,false);Skill(aoe?4:3);Skill(7);
            F(aoe?29:27,false);Want(14,"1圣能且终结技不可用时选择公正之剑产能");
            Skill(7,false);Want(0,"1圣能且无产能技能时不反复按不可用终结技");
            F(aoe?29:27);Want(aoe?9:11,"低圣能但真正免费可用的终结技仍允许释放");
        }
        Reset();Dps();F(16,false);Want(0,"GCD门槛");
        // Every offensive/interrupt action must honor the corrected usability observation.
        int[] spellKeys={7,8,12,11,9,12,13,14,10,21};
        for(int index=0;index<spellKeys.Length;index++) {
            Reset(index==4);Skill(index);
            if(index==0) { F(10);B(9,false);B(10,false); }
            if(index==1) { F(10);B(8,false);B(10,false); }
            if(index==2) B(6);
            if(index==9) { F(11);F(12); }
            Want(spellKeys[index],"各技能可用时允许："+index);
            F(21+index*2,false);Want(index==9?29:0,"各技能不可用时不误发："+index);
            f[21+index*2]=Color.Orange;Want(0,"各技能可用性未知时不误发："+index);
            F(21+index*2);Want(spellKeys[index],"各技能可用性恢复后允许："+index);
            B(30+index,false);Want(index==9?29:0,"可用为真但资源不足仍禁止技能："+index);
            b[30+index]=Color.Orange;Want(0,"资源状态未知不能只凭可用放行："+index);
            B(30+index);Want(spellKeys[index],"资源状态恢复允许技能："+index);
        }
        foreach(bool aoe in new[]{false,true}) {
            Reset(aoe);Skill(aoe?4:3);Skill(7);F(7,false);F(8,false);
            B(aoe?34:33,false);Want(14,"低圣能且可用为真但资源不足时继续产能");
            B(aoe?34:33);Want(aoe?9:11,"免费消耗未报告资源不足时允许终结技");
        }
        Reset();Dps();f[19]=Color.FromArgb(70,3,2);Want(0,"v3缺少独立资源校验必须拒绝");
        Reset();B(19);B(23);B(24);B(27);B(21);B(22);B(41,false);Want(2,"闪现资源不足回退圣令");
        B(40,false);Want(0,"圣令资源不足不发键");
        B(20);B(25);B(26);B(42,false);Want(0,"圣佑资源不足不发键");
        // These fields now carry the collector's Hammer of Justice 10-yard gate.
        foreach(var entry in new[]{(Index:3,Key:11),(Index:5,Key:12),(Index:6,Key:13),(Index:7,Key:14),(Index:8,Key:10)}) {
            Reset();Skill(entry.Index);Want(entry.Key,"10码内允许对应输出技能");
            F(39+entry.Index,false);Want(0,"10码外阻止对应输出技能");
            f[39+entry.Index]=Color.Orange;Want(0,"10码范围未知不放行");
        }
        foreach(bool free in new[]{false,true}) {
            Reset();Skill(2);B(6);B(7,free);buff.FreeHammer=O(free,free);
            Want(12,"普通与免费圣光之锤10码内允许共用键");
            F(41,false);Want(0,"普通与免费圣光之锤10码外不放行");
            f[41]=Color.Orange;Want(0,"普通与免费圣光之锤范围未知不放行");
        }
        foreach(bool capped in new[]{false,true}) {
            Reset(true);Skill(4);F(8,capped);Want(9,"普通与满圣能神圣风暴10码内允许");
            F(43,false);Want(0,"普通与满圣能神圣风暴10码外不放行");
            f[43]=Color.Orange;Want(0,"普通与满圣能神圣风暴范围未知不放行");
        }
        Reset();Skill(8);F(17);Want(10,"审判替换愤怒之锤沿用同键和10码门槛");
        F(47,false);Want(0,"审判替换形态10码外也不放行");
        Reset();Skill(3);F(8);F(42,false);Want(0,"满圣能最终审判也不能越过10码门槛");
        Reset();Skill(1);F(10);B(8,false);B(10,false);Want(8,"10码内允许军官处决");
        F(40,false);Want(0,"军官处决10码外等待");
        f[40]=Color.Orange;Want(0,"军官处决范围未知等待");
        Reset();Dps();F(10);buff.Expurgation=O();F(46,false);B(28,false);Want(0,"爆发前公正之剑和翅膀都不能越过10码");
        B(28);Want(7,"公正之剑范围未通过但翅膀范围通过时仅跳过准备，不按公正之剑");
        Reset();Skill(0);F(10);B(9,false);B(10,false);
        B(28,false);Want(0,"单独翅膀／征伐在10码外不能发键");
        b[28]=Color.Orange;Want(0,"翅膀／征伐10码范围未知不能发键");
        b.Remove(28);Want(0,"旧采样缺失翅膀范围不能放行");
        B(28);Want(7,"未学处决也能在10码内单独开翅膀");
        var wingsSent=Decide();B(28,false);
        Check(Decide(wingsSent.Request,.1).Key==0,"翅膀重试也检查10码范围");
        F(18);F(20,false);
        Check(Decide(wingsSent.Request,.2).Key==28,"翅膀确认成功后离开10码仍能收尾");
        f[19]=Color.FromArgb(70,2,2);Want(0,"旧v2协议不能绕过新增翅膀距离条件");
        Reset();Dps();B(19);foreach(int i in new[]{21,22,23,24,25,26,27}) B(i);
        F(3,false);buff=new();Want(1,"低于80%空目标优先7瞬发闪现，不依赖输出点");
        B(27,false);Want(2,"非瞬发闪现不读条，回退8荣耀圣令");
        B(27);B(24,false);Want(2,"闪现资源不可用回退圣令");
        B(24);B(23,false);Want(2,"闪现自身冷却中回退圣令");
        B(23);b[27]=Color.Orange;Want(0,"瞬发来源未知不误按7");
        B(27,false);B(22,false);Want(0,"两种自疗都不可用不按自疗键");
        B(27);B(24);B(20);Want(3,"低于60%优先9圣佑术");
        F(16,false);Want(3,"圣佑术不等治疗GCD");
        B(25,false);Want(0,"GCD中不释放两种自疗");
        F(16);Want(1,"圣佑术冷却中回到瞬发闪现");
        B(20,false);B(19,false);Want(0,"不低于80%不自疗");
        B(19);F(4,false);Want(0,"不打断当前读条或引导");
        F(4);F(1,false);Want(0,"脱战不自动自疗");
        F(1);F(2,false);Want(0,"死亡骑乘不自疗");
        Reset();Dps();f[19]=Color.FromArgb(70,2,1);Want(0,"拒绝没有新自疗字段的v1协议");
        Reset();Dps();F(4,false);Want(0,"引导期间不发伤害键");
        Reset();Dps();B(11);Want(0,"光辉荣耀构筑不会误开主动爆发");
        Reset();Dps();b[6]=Color.Orange;Want(0,"灰烬替换未知不按灰烬");
        Reset();Dps();f[30]=Color.Orange;Want(0,"高优先级CD未知阻止低优先级泄漏");
        Skill(5,false);Want(13,"明确不适用的技能不阻塞");
        Reset();F(3,false);f[6]=Color.FromArgb(102,102,102);F(14);F(15);F(16,false);Want(22,"40%空目标治疗石不等GCD");
        F(10);Check(Decide(new(RetStage.Clear)).Key==22,"清理旧爆发不抢占低血量物品自救");F(10,false);
        F(14,false);Want(23,"治疗石不可用再药水");
        f[6]=Color.Orange;Want(0,"未知血量不触发物品");
        Reset();F(11);F(12);F(13);Skill(9);F(16,false);Want(21,"食物标记责难不等GCD");
        B(28,false);f[5]=Color.Orange;buff=new();Want(21,"责难独立于翅膀10码、敌人数和输出增益");
        f[13]=Color.Orange;Want(21,"沿用防战武器战：可打断性未知不阻止责难");
        F(13,false);Want(21,"沿用防战武器战：不可打断读条仍尝试责难");
        f.Remove(13);Want(21,"可打断诊断字段缺失也不阻止责难");
        foreach(int field in new[]{3,12,38,39,48}) {
            Reset();F(11);F(12);F(13);Skill(9);F(field,false);
            Want(29,"责难任一明确无效条件清理食物："+field);
            f[field]=Color.Orange;Want(0,"责难任一未知条件等待："+field);
            F(field);Want(21,"责难条件恢复可发Shift+3："+field);
        }
        Reset();F(12);F(13);Skill(9);Want(0,"没有食物标记不自动责难");
        Reset();Dps();F(10);buff.Expurgation=O();Want(14,"爆发先补灼烧");
        var prepare=Decide();
        Check(prepare.Request.Sent&&prepare.Request.Stage==RetStage.Prepare,"准备阶段记录公正之剑请求");
        Check(Decide(prepare.Request,.1).Key==0,"公正之剑发键不等于成功，不重复请求准备技能");
        F(16,false);F(34,false);
        var preparedWings=Decide(prepare.Request,.32);
        Check(preparedWings.Key==7&&preparedWings.Request.Stage==RetStage.Wings,
            "复现11:56:58日志：公正之剑进入冷却但异端逐除仍黑，GCD中仍继续翅膀");
        Check(preparedWings.Request.SentAt==.32,"准备阶段计时不能污染翅膀确认计时");
        F(18);F(20,false);F(16);
        Check(Decide(preparedWings.Request,.5).Key==8,"准备完成后翅膀确认仍正常接处决");
        Reset();Dps();F(10);buff.Expurgation=O();prepare=Decide();
        Check(Decide(prepare.Request,2).Key==7,"准备技能未确认超过2秒跳过，不取消军官或假定减益存在");
        F(34,false);Want(7,"公正之剑已在冷却时不等待异端逐除，直接翅膀");
        Reset();Dps();F(10);buff.Expurgation=default;prepare=Decide();
        Check(Decide(prepare.Request,2).Key==7,"异端逐除未配置不能永久挡住翅膀");
        B(28,false);Check(Decide(prepare.Request,2).Key==0,"跳过准备不能绕过翅膀10码门槛");
        B(28);B(30,false);Check(Decide(prepare.Request,2).Key==0,"跳过准备不能绕过翅膀资源门槛");
        B(30);F(21,false);Check(Decide(prepare.Request,2).Key==0,"跳过准备不能绕过翅膀可用性");
        Reset();Dps();F(10);buff.Expurgation=O();prepare=Decide();
        b[14]=Color.FromArgb(2,70,1);Check(Decide(prepare.Request,.3).Key==28,"准备期间换目标仍取消军官");
        Reset();Dps();F(10);
        buff.Expurgation=default;Want(0,"当前目标异端逐除未知不启动爆发");
        buff.Expurgation=O(true);
        var wings=Decide();Check(wings.Key==7&&wings.Request.Sent,"军官第一步翅膀");
        var retry=Decide(wings.Request,.1);Check(retry.Key==7,"发键不等于翅膀成功");
        F(18);F(20,false);var sentence=Decide(wings.Request,.2);
        Check(sentence.Key==8&&sentence.Request.Stage==RetStage.Sentence,"翅膀进入CD不提前取消整组");
        var pending=Decide(sentence.Request,.3);Check(pending.Key==8,"处决同样等待实际确认");
        B(16);F(22,false);var complete=Decide(sentence.Request,.4);
        Check(complete.Key==28&&complete.Request.Stage==RetStage.Clear,"两项实际确认后清理");
        Check(Decide(complete.Request,.5).Key==28,"等待标记真的关闭，不能重开爆发");
        F(10,false);Check(Decide(complete.Request,.6).Key==12,"标记关闭恢复自动灰烬");
        Reset();Dps();F(10);F(22,false);Want(28,"初始处决CD中整组不部分启动");
        Reset();Dps();F(10);var active=Decide();
        Check(Decide(active.Request,2.1).Key==28,"失败发键超时撤销");
        F(10,false);Check(Decide(active.Request,.1).Key==12,"人工取消立即恢复常规输出");
        F(10);b[14]=Color.FromArgb(2,70,1);Check(Decide(active.Request,.2).Key==28,"换目标取消旧请求");
        Reset();Dps();F(10);F(3,false);Check(Decide(active.Request,.1).Key==28,"失去目标取消请求");
        Reset();Dps();F(10);B(11);Check(Decide(active.Request,.1).Key==0,"未适配的光辉荣耀＋处决构筑不继续旧请求");
        Reset();Dps();F(10);F(1,false);Want(28,"脱战清理军官");
        Reset();Dps();F(10);f[19]=Color.Black;Check(Decide(active.Request,.1).Key==0,"旧协议不能发键或清理键");
        Reset();Dps();F(10);B(8,false);B(9,false);Want(28,"未学爆发跳过并清理请求");
        Reset();Dps();F(10);CJQ.Inst.Reset();CJQ.Inst.Buffs=buff;
        var states=new Dictionary<int,bool>();CJQ.Inst.Process(f,b,states);
        Check(states.Keys.SequenceEqual(new[]{28}),"停机/重启清除历史标记请求");
        Check(new[]{0x31,0x32,0x33,0x34,0x35,0x36,0x37,0x38,0x39,0x30,0xDB,0xDD,0xBA,0xDE,0xDC,0xBE,0xBD,0xBB}.All(CJQ.Inst.skipKeys.Contains),"所有绑定键让出手动操作");
        // Radiant Glory without tier sets or Execution Sentence.
        void Radiant(bool aoe=false) { Reset(aoe);B(11);B(9,false);B(43); }
        Radiant();Dps();B(43,false);Want(12,"光辉荣耀自动灰烬，不等军官");
        F(10);Want(12,"军官开启也不发送主动翅膀/处决/清标记");
        f[10]=Color.Orange;f[18]=Color.Orange;f[20]=Color.Orange;f[22]=Color.Orange;f[49]=Color.Orange;
        b[8]=Color.Orange;b[16]=Color.Orange;b[28]=Color.Orange;buff.Sentence=default;
        Want(12,"新构筑独立于军官、主动翅膀冷却、处决减益和翅膀距离");
        Check(Decide(new(RetStage.Wings,0,0,true,1)).Key==12,"切换构筑丢弃旧翅膀请求");
        Skill(5,false);Want(13,"灰烬不可用时接鸣钟");
        Skill(6,false);buff.Art=O(true,true);Want(14,"两层战争艺术优先普通消耗");
        buff.Art=O(true);Want(11,"普通单体最终审判");
        F(7,false);Want(11,"神圣意志免费可用时不硬性要求3圣能");
        B(33,false);Want(14,"实际资源不足才回退公正之剑");
        Skill(7,false);Want(10,"公正之剑不可用回退审判");
        F(17);Want(10,"替换后的愤怒之锤沿用审判键");
        Radiant();Dps();F(8);Want(11,"灰烬自身冷却中满圣能先消耗");
        B(43,false);Want(12,"灰烬自身就绪时满圣能不挡灰烬");
        b[43]=Color.Orange;Want(0,"满圣能但灰烬自身冷却未知不猜测");
        Radiant();Dps();B(6);Skill(2);F(8);Want(12,"满圣能消耗列表优先普通大锤");
        F(8,false);Want(13,"非满圣能普通大锤不抢鸣钟");
        Skill(6,false);buff.Art=O(true,true);Want(14,"非满圣能两层战争艺术先于普通大锤");
        buff.Art=O();Want(12,"其后消耗普通大锤");
        foreach(bool aoe in new[]{false,true}) {
            Radiant(aoe);Skill(aoe?4:3);F(8);Want(aoe?9:11,"满圣能选择无套装终结技");
            F(8,false);Want(aoe?9:11,"普通消耗使用相同目标规则");
        }
        Radiant(true);Skill(3);Skill(4);buff.Legacy=O(true);Want(11,"群体苍穹之遗优先最终审判");
        buff.Legacy=default;Want(0,"群体苍穹之遗未知不猜测");
        Radiant();Dps();Skill(2);B(6);B(7);buff.FreeHammer=O(true,true);buff.Ruling=O(true);
        Want(12,"免费锤临期抢在鸣钟之前");
        buff.FreeHammer=O(true);Want(13,"免费锤未临期先鸣钟");
        Skill(6,false);Skill(7,false);buff.Sentence=O(true,true);
        Want(11,"新构筑不因旧处决点临期提前打免费锤");
        buff.Wings=O(true,true);Want(12,"征伐临期释放免费锤");
        buff.Wings=O();buff.Ruling=O(true,true);Want(12,"公允裁定临期释放免费锤");
        buff.Ruling=O();Want(12,"公允裁定消失不等主动翅膀冷却");
        buff.Ruling=O(true);buff.FreeHammer=new(true,true,"固定点（需重配GCD临期色）");
        Want(0,"旧3秒配置不能冒充2GCD配置");
        buff.FreeHammer=O(true);buff.Ruling=new(true,true,"固定点（需重配GCD临期色）");
        Want(0,"旧公允裁定存在色不能冒充临期色");
        buff.Ruling=O(true);B(6,false);Skill(5,false);Want(11,"免费大锤必须实际替换灰烬按钮");
        foreach(var entry in new[]{(Index:2,Key:12),(Index:3,Key:11),(Index:4,Key:9),(Index:5,Key:12),(Index:6,Key:13),(Index:7,Key:14),(Index:8,Key:10)}) {
            Radiant(entry.Index==4);Skill(entry.Index);
            if(entry.Index==2) B(6);
            Want(entry.Key,"光辉荣耀技能可用："+entry.Index);
            F(39+entry.Index,false);Want(0,"光辉荣耀技能10码外不发键："+entry.Index);
            f[39+entry.Index]=Color.Orange;Want(0,"光辉荣耀距离未知不发键："+entry.Index);
            F(39+entry.Index);B(30+entry.Index,false);Want(0,"光辉荣耀资源不足不发键："+entry.Index);
            B(30+entry.Index);f[21+entry.Index*2]=Color.Orange;Want(0,"光辉荣耀可用性未知不发键："+entry.Index);
        }
        Radiant();Dps();B(43,false);buff.Expurgation=O();
        var radiantPrep=Decide();Check(radiantPrep.Key==14,"灰烬前先建立异端逐除");
        Check(Decide(radiantPrep.Request,.1).Key==0,"准备发键不当作成功也不重复发送");
        F(34,false);var radiantReady=Decide(radiantPrep.Request,.2);
        Check(radiantReady.Key==12,"公正之剑实际冷却确认后进入灰烬");
        F(34);Check(Decide(radiantReady.Request,.3).Key==12,"异端仍黑且公正之剑重置不重复准备");
        B(43);B(6);Skill(2);F(8);
        var afterWake=Decide(radiantReady.Request,.4);
        Check(afterWake.Key==12&&afterWake.Request.Stage==RetStage.Idle,"灰烬实际冷却即清除准备，即使优先发送满能大锤");
        Radiant();Dps();B(43,false);buff.Expurgation=default;var waiting=Decide();
        Check(waiting.Key==0&&Decide(waiting.Request,2).Key==12,"未配异端最多等2秒，不永久卡住");
        b[14]=Color.FromArgb(2,70,1);
        Check(Decide(waiting.Request,3).Request.Started==3,"换目标重新建立有限准备窗口");
        F(3,false);Check(Decide(waiting.Request,.5).Request.Stage==RetStage.Idle,"失去目标清除准备");
        Radiant();Dps();F(16,false);Want(0,"光辉荣耀伤害等待GCD");
        F(11);F(12);Skill(9);Want(21,"光辉荣耀食物责难沿用现有逻辑");
        F(11,false);B(20);B(25);B(26);Want(3,"光辉荣耀圣佑术仍不等GCD");
        f[6]=Color.FromArgb(102,102,102);F(14);Want(22,"光辉荣耀物品自救先于循环");
        Radiant();Dps();f[19]=Color.FromArgb(70,4,2);Want(0,"v5明确拒绝缺少灰烬基础冷却的v4");
        using var pattern=DiagnosticTests.Pattern(1);
        var layout=Calibration.Find(pattern,new Size(1600,900));layout.SpecId=70;
        // Actual sampling/dispatch lifecycle with synthetic pixels and no SendInput.
        Reset();Dps();B(10,false);B(15,false);B(17,false);B(18,false);
        using(var pixels=new Bitmap(layout.ClientWidth,layout.ClientHeight)) {
            var sent=new Dictionary<int,bool>();int calls=0;
            var game=new GameWindow(1,(IntPtr)42,"Synthetic Retribution");
            IntPtr foreground=game.Handle;bool manual=false;
            var runner=new RotationExecution(()=>foreground,()=>manual,(frames,bars)=> {
                calls++;sent.Clear();CJQ.Inst.Process(frames,bars,sent);
            });
            var ret=ClassProfiles.Find(70)!;
            void Tick(bool live=true,double now=0,ClassProfile? profile=null) {
                foreach(var pair in f) pixels.SetPixel(layout.Frames[pair.Key-1].X,layout.Frames[pair.Key-1].Y,pair.Value);
                foreach(var pair in b) pixels.SetPixel(layout.Bars[pair.Key-1].X,layout.Bars[pair.Key-1].Y,pair.Value);
                using var sample=new Captured((Bitmap)pixels.Clone(),new Rectangle(Point.Empty,pixels.Size));
                runner.Tick(game,layout,sample,live,now,profile??ret);
            }
            void Begin() {
                F(10,false);runner.Start(ret);Tick();F(10);Tick();
                Check(sent.ContainsKey(7)&&CJQ.Inst.Request.Stage==RetStage.Wings,"真实执行入口建立翅膀阶段");
            }
            Begin();int before=calls;Tick(false);
            Check(calls==before&&CJQ.Inst.Request.Stage==RetStage.Clear,"心跳失效清除状态且不发键");
            Tick();Check(sent.ContainsKey(28),"心跳恢复仅清理旧军官");
            Begin();foreground=(IntPtr)99;before=calls;Tick();
            Check(calls==before&&CJQ.Inst.Request.Stage==RetStage.Clear,"切窗口取消爆发");
            foreground=game.Handle;Tick();Check(sent.ContainsKey(28),"回前台不重放旧爆发");
            Begin();manual=true;before=calls;Tick(now:1);manual=false;Tick(now:1.49);
            Check(calls==before,"手动按键让出500ms");
            Tick(now:1.51);Check(calls==before+1,"手动让出结束重新采样");
            runner.Stop();Check(CJQ.Inst.Request.Stage==RetStage.Clear,"停机取消爆发");
            Begin();Tick(profile:ClassProfiles.Find(65));
            Check(!runner.Enabled&&CJQ.Inst.Request.Stage==RetStage.Clear,"换专精停机并清请求");
            Radiant();Dps();B(10,false);B(15,false);B(17,false);B(18,false);B(43,false);
            CJQ.Inst.Buffs=buff;F(10);runner.Start(ret);Tick(now:2);
            Check(sent.Keys.SequenceEqual(new[]{12}),"实际采样执行链新构筑启动直接灰烬，忽略旧军官");
            foreground=(IntPtr)99;before=calls;Tick(now:2.1);
            Check(calls==before,"光辉荣耀失焦不发键");
            foreground=game.Handle;Tick(now:2.2);
            Check(sent.Keys.SequenceEqual(new[]{12}),"光辉荣耀回前台不恢复旧翅膀流程");
            runner.Stop();
        }
        int[] expectedAuraIds={406086,387178,433732,432629,454351,343527,383346};
        int[] expectedTalentIds={406064,387170,425518,432626,458359,343527,383344};
        Check(RetBuffs.Auras.Select(a=>a.AuraId).SequenceEqual(expectedAuraIds),"七个光环ID及字段顺序固定");
        Check(RetBuffs.Auras.Select(a=>a.TalentId).SequenceEqual(expectedTalentIds),"光环ID与天赋ID独立");
        Check(RetBuffs.ActivePoints(true).Count()==6&&!RetBuffs.ActivePoints(true).Any(p=>p.Key=="ret_sentence"),"当前构筑只显示六项");
        Check(RetBuffs.Identity("ret_wings").Contains("454351")&&RetBuffs.Identity("ret_wings").Contains("462048"),"灰烬增益与被动替换技能明确区分");
        Check(RetBuffs.Identity("ret_wings",false).Contains("31884")&&!RetBuffs.Identity("ret_wings",false).Contains("454351"),"旧构筑保留主动复仇之怒ID");
        Check(RetBuffs.Identity("ret_free").Contains("433732")&&RetBuffs.Identity("ret_free").Contains("433674"),"免费机会与叠层明确区分");
        Check(RetBuffs.Identity("ret_expurgation").Contains("目标上自己施加的减益"),"异端逐除归属说明");
        foreach(var p in RetBuffs.Points) {
            var c=new SampleColor(144,92,18);
            layout.SpecialPoints[p.Key]=new(){Enabled=true,Point=new(540,8),Comparison=RetBuffs.Comparison(p.Key),ReferenceColor=c};
            Check(RetBuffs.Observe(layout,p.Key,_=>Color.Black).Present==false,p.Key+"缺失");
            Check(RetBuffs.Observe(layout,p.Key,_=>c.ToColor()).Present==true,p.Key+"存在");
            if(RetBuffs.UsesColor(p.Key)) {
                Check(RetBuffs.Observe(layout,p.Key,_=>c.ToColor()).Expiring==true,p.Key+"条件色");
                Check(RetBuffs.Observe(layout,p.Key,_=>Color.Red).Expiring==false,p.Key+"非条件色");
            }
        }
        foreach(string key in new[]{"ret_free","ret_wings","ret_ruling"}) {
            var setting=layout.SpecialPoints[key];setting.Comparison=key=="ret_ruling"?"presence":"expiring";
            var legacy=RetBuffs.Observe(layout,key,_=>setting.ReferenceColor!.ToColor());
            Check(legacy.Present==true&&RetBuffs.RadiantExpiry(legacy) is null,"旧点位存在状态保留，临期语义失效："+key);
            setting.Comparison=RetBuffs.Comparison(key);
            Check(RetBuffs.RadiantExpiry(RetBuffs.Observe(layout,key,_=>setting.ReferenceColor!.ToColor()))==true,"重新取色恢复GCD语义："+key);
        }
        var profiles=new ProfileLayouts(Path.Combine(Path.GetTempPath(),"hiji-ret-test-"+Guid.NewGuid().ToString("N")));
        var profile=ClassProfiles.Find(70)!;profiles.Save(profile,layout);
        Check(profiles.Load(profile,layout).SpecialPoints.Count==7,"惩戒七点持久化");
        Check(profiles.Load(ClassProfiles.Find(65)!,layout).SpecialPoints.Count==0,"不迁移惩戒点给奶骑");
        var moved=layout.Copy();moved.Frames[0]=new(moved.Frames[0].X+1,moved.Frames[0].Y);
        Check(profiles.Load(profile,moved).SpecialPoints.Values.All(p=>!p.Enabled),"几何变更禁用特殊点");
        using var settings=new RetPointSettings();settings.LoadSettings(layout);
        settings.SetRadiant(true);
        Check(settings.Rows["ret_wings"].Name.Text.Contains("454351"),"当前构筑配置显示灰烬增益ID");
        settings.SetRadiant(false);
        Check(settings.Rows["ret_wings"].Name.Text.Contains("31884"),"切换旧构筑更新配置ID");
        settings.SetRadiant(true);
        settings.Stage("ret_art",new(new(550,8),new(60,70,80)));
        settings.Stage("ret_free",new(new(560,8),new(90,100,110)));
        var saved=settings.BuildLayout(layout,"ret_art");settings.LoadSettings(saved,"ret_art");
        Check(settings.Rows["ret_free"].Pending?.Color==new SampleColor(90,100,110),"独立行待保存颜色不会丢失");
        Check(saved.SpecialPoints["ret_art"].ReferenceColor==new SampleColor(60,70,80),"保存只用本行颜色");
        settings.Stage("ret_ruling",new(new(570,8),new(120,130,140)));
        var timedLayout=settings.BuildLayout(saved,"ret_ruling");
        Check(timedLayout.SpecialPoints["ret_ruling"].Comparison=="ret-gcd","实际设置入口保存新GCD模式");
        foreach(SampleColor? invalid in new SampleColor?[]{null,new(0,0,0)}) {
            var invalidLayout=timedLayout.Copy();invalidLayout.SpecialPoints["ret_ruling"].ReferenceColor=invalid;
            bool rejected=false;try {invalidLayout.Validate();} catch(InvalidDataException) {rejected=true;}
            Check(rejected,"GCD模式拒绝缺失/黑色临期参考色");
        }
        // Continuous trace records even without any presentation tick or input dispatch.
        var traceDirectory=Path.Combine(Path.GetTempPath(),"hiji-ret-log-"+Guid.NewGuid().ToString("N"));
        var traceStore=new SavedLogStore(traceDirectory);
        var trace=new RetCombatLog(traceStore);
        Reset();F(27);B(33,false);
        trace.RecordValues(i=>f[i],i=>b[i],true,true,"单体 · 公正之剑");
        string firstTrace=File.ReadAllText(trace.Path);
        Check(trace.Started&&trace.Error is null,"连续惩戒日志可独立于界面刷新落盘");
        Check(firstTrace.Contains("F27=255,255,255")&&firstTrace.Contains("B33=0,0,0"),"日志同时保留可用为真和资源不足的冲突");
        trace.RecordValues(i=>f[i],i=>b[i],true,true,"单体 · 公正之剑");
        Check(File.ReadAllText(trace.Path)==firstTrace,"相同采样不重复写入");
        B(33);trace.RecordValues(i=>f[i],i=>b[i],true,true,"单体 · 最终审判");
        Check(File.ReadAllText(trace.Path).Contains("单体 · 最终审判"),"资源恢复及动作判断变化写入");
        trace.RecordValues(i=>f[i],i=>b[i],false,true,"执行等待有效职业数据 / 心跳");
        Check(File.ReadAllText(trace.Path).Contains("无效采样，以下原始颜色不可用于决策"),"失效采样不会伪装成有效释放依据");
        Check(File.ReadAllText(Path.Combine(traceDirectory,"saved-log-files.json")).Contains("ret-combat-"),"连续日志纳入保存日志索引");
        File.Delete(trace.Path);File.Delete(Path.Combine(traceDirectory,"saved-log-files.json"));
        Directory.Delete(traceStore.DefaultDirectory);Directory.Delete(traceDirectory);
        return count;
    }
    internal static void Render(string path)
    {
        using var pattern=DiagnosticTests.Pattern(1);
        var layout=Calibration.Find(pattern,new Size(1600,900));layout.SpecId=70;
        using var image=new Bitmap(800,20);
        image.SetPixel(layout.Markers[0].X,0,Color.Lime);
        image.SetPixel(layout.Markers[1].X,0,ClassProfiles.Encode(70,2));
        image.SetPixel(layout.Markers[2].X,0,Color.Cyan);
        int x=540;
        foreach(var item in RetBuffs.Points)
        {
            var color=new SampleColor(144,92,18);
            layout.SpecialPoints[item.Key]=new(){Enabled=true,Point=new(x,8),Comparison=RetBuffs.Comparison(item.Key),ReferenceColor=color};
            image.SetPixel(x,8,color.ToColor());x+=24;
        }
        var profiles=new ProfileLayouts(Path.Combine(Path.GetTempPath(),"hiji-ret-ui-"+Guid.NewGuid().ToString("N")));
        profiles.Save(ClassProfiles.Find(70)!,layout);
        using var form=new DiagnosticForm(false,profileLayouts:profiles);
        form.Show();Application.DoEvents();form.FreezeForTesting();form.ShowSynthetic(layout,image);form.Refresh();Application.DoEvents();
        static IEnumerable<Control> All(Control parent)=>parent.Controls.Cast<Control>().SelectMany(c=>new[]{c}.Concat(All(c)));
        var settings=All(form).OfType<RetPointSettings>().Single();
        foreach(var row in settings.Rows.Values)
            if(!row.Pick.Visible||row.Pick.Bottom>row.Pick.Parent!.ClientSize.Height||!row.Disable.Enabled)
                throw new Exception("惩戒七行未完整显示");
        settings.SetRadiant(true);Application.DoEvents();
        if(settings.Rows["ret_sentence"].Pick.Visible||settings.Rows.Values.Count(r=>r.Pick.Visible)!=6||
            !settings.Rows["ret_wings"].Name.Text.Contains("454351"))
            throw new Exception("当前构筑六项光环及ID显示错误");
        settings.SetRadiant(false);Application.DoEvents();
        if(!settings.Rows["ret_sentence"].Pick.Visible||!settings.Rows["ret_wings"].Name.Text.Contains("31884"))
            throw new Exception("旧构筑光环显示未恢复");
        settings.SetRadiant(true);Application.DoEvents();
        foreach(var size in new[]{new Size(1180,800),new Size(950,650)})
        {
            form.ClientSize=size;form.Refresh();Application.DoEvents();
            foreach(var row in settings.Rows.Values.Where(r=>r.Pick.Visible))
                if(row.Save.Right>row.Save.Parent!.ClientSize.Width||row.Pick.Bottom>row.Pick.Parent!.ClientSize.Height)
                    throw new Exception("惩戒设置超出窗口");
        }
        form.ClientSize=new Size(1180,800);Application.DoEvents();
        using var rendered=new Bitmap(form.Width,form.Height);form.DrawToBitmap(rendered,new Rectangle(Point.Empty,rendered.Size));rendered.Save(path,System.Drawing.Imaging.ImageFormat.Png);
        form.Close();Console.WriteLine("惩戒界面验证通过："+path);
    }
}
