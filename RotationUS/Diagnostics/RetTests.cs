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
            foreach(int i in new[]{8,9,10,12,13,15,17,18}) B(i);
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
        buff.Legacy=O(true);Want(11,"群体天穹之遗例外");
        F(8,false);Skill(5,false);Skill(6,false);Want(11,"普通消耗也尊重天穹之遗");
        buff.Legacy=default;Want(0,"相关天穹之遗未知不猜测");
        B(15,false);Want(9,"未学天穹之遗无须配置对应点");
        Reset();Skill(2);B(6);Dps();Want(10,"常规锤优先自动灰烬");
        buff.FreeHammer=default;Want(0,"锤形态可用但免费触发未知不误放");
        buff.FreeHammer=O(true);B(7);buff.Ruling=O(true);Want(13,"免费锤可短暂保留同时允许鸣钟，灰烬已被替换");
        buff.FreeHammer=O(true,true);Want(10,"免费锤临期先消耗");
        buff.FreeHammer=O(true);buff.Wings=O(true,true);Want(10,"翅膀临期消耗免费锤");
        buff.Wings=O();buff.Sentence=O(true,true);Want(10,"处决临期消耗免费锤");
        buff.Sentence=O();buff.Ruling=O();Want(10,"未请求翅膀不无限扣住免费锤");
        Reset();Dps();F(5);b[15]=Color.Orange;buff.Legacy=default;F(8);Want(0,"群体增益未知挡住满能量消耗");
        Reset();Dps();f[5]=Color.Orange;Want(0,"目标数未知不猜单体");
        Reset();Dps();F(9,false);Want(0,"资源来源失效");
        Reset();Dps();F(16,false);Want(0,"GCD门槛");
        Reset();Dps();B(19);foreach(int i in new[]{21,22,23,24,25,26,27}) B(i);
        F(3,false);buff=new();Want(2,"低于80%空目标优先瞬发闪现，不依赖输出点");
        B(27,false);Want(1,"非瞬发闪现不读条，回退荣耀圣令");
        B(27);B(24,false);Want(1,"闪现资源不可用回退圣令");
        B(24);B(23,false);Want(1,"闪现自身冷却中回退圣令");
        B(23);b[27]=Color.Orange;Want(0,"瞬发来源未知不误按8");
        B(27,false);B(22,false);Want(0,"两种自疗都不可用不按自疗键");
        B(27);B(24);B(20);Want(3,"低于60%优先9圣佑术");
        F(16,false);Want(3,"圣佑术不等治疗GCD");
        B(25,false);Want(0,"GCD中不释放两种自疗");
        F(16);Want(2,"圣佑术冷却中回到瞬发闪现");
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
        f[13]=Color.Orange;Want(0,"可打断性未知不猜测");
        F(13,false);Want(29,"不可打断清除食物");
        Reset();Dps();F(10);buff.Expurgation=O();Want(14,"爆发先补灼烧");
        buff.Expurgation=default;Want(0,"当前目标灼烧未知不启动爆发");
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
        Reset();Dps();F(10);B(11);Check(Decide(active.Request,.1).Key==28,"天赋变化取消进行中请求");
        Reset();Dps();F(10);F(1,false);Want(28,"脱战清理军官");
        Reset();Dps();F(10);f[19]=Color.Black;Check(Decide(active.Request,.1).Key==0,"旧协议不能发键或清理键");
        Reset();Dps();F(10);B(8,false);B(9,false);Want(28,"未学爆发跳过并清理请求");
        Reset();Dps();F(10);CJQ.Inst.Reset();CJQ.Inst.Buffs=buff;
        var states=new Dictionary<int,bool>();CJQ.Inst.Process(f,b,states);
        Check(states.Keys.SequenceEqual(new[]{28}),"停机/重启清除历史标记请求");
        Check(new[]{0x31,0x32,0x33,0x34,0x35,0x36,0x37,0x38,0x39,0x30,0xDB,0xDD,0xBA,0xDE,0xDC,0xBE,0xBD,0xBB}.All(CJQ.Inst.skipKeys.Contains),"所有绑定键让出手动操作");
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
        }
        foreach(var p in RetBuffs.Points) {
            var c=new SampleColor(144,92,18);
            layout.SpecialPoints[p.Key]=new(){Enabled=true,Point=new(540,8),Comparison=RetBuffs.UsesColor(p.Key)?"expiring":"presence",ReferenceColor=c};
            Check(RetBuffs.Observe(layout,p.Key,_=>Color.Black).Present==false,p.Key+"缺失");
            Check(RetBuffs.Observe(layout,p.Key,_=>c.ToColor()).Present==true,p.Key+"存在");
            if(RetBuffs.UsesColor(p.Key)) {
                Check(RetBuffs.Observe(layout,p.Key,_=>c.ToColor()).Expiring==true,p.Key+"条件色");
                Check(RetBuffs.Observe(layout,p.Key,_=>Color.Red).Expiring==false,p.Key+"非条件色");
            }
        }
        var profiles=new ProfileLayouts(Path.Combine(Path.GetTempPath(),"hiji-ret-test-"+Guid.NewGuid().ToString("N")));
        var profile=ClassProfiles.Find(70)!;profiles.Save(profile,layout);
        Check(profiles.Load(profile,layout).SpecialPoints.Count==7,"惩戒七点持久化");
        Check(profiles.Load(ClassProfiles.Find(65)!,layout).SpecialPoints.Count==0,"不迁移惩戒点给奶骑");
        var moved=layout.Copy();moved.Frames[0]=new(moved.Frames[0].X+1,moved.Frames[0].Y);
        Check(profiles.Load(profile,moved).SpecialPoints.Values.All(p=>!p.Enabled),"几何变更禁用特殊点");
        using var settings=new RetPointSettings();settings.LoadSettings(layout);
        settings.Stage("ret_art",new(new(550,8),new(60,70,80)));
        settings.Stage("ret_free",new(new(560,8),new(90,100,110)));
        var saved=settings.BuildLayout(layout,"ret_art");settings.LoadSettings(saved,"ret_art");
        Check(settings.Rows["ret_free"].Pending?.Color==new SampleColor(90,100,110),"独立行待保存颜色不会丢失");
        Check(saved.SpecialPoints["ret_art"].ReferenceColor==new SampleColor(60,70,80),"保存只用本行颜色");
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
            layout.SpecialPoints[item.Key]=new(){Enabled=true,Point=new(x,8),Comparison=RetBuffs.UsesColor(item.Key)?"expiring":"presence",ReferenceColor=color};
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
        foreach(var size in new[]{new Size(1180,800),new Size(950,650)})
        {
            form.ClientSize=size;form.Refresh();Application.DoEvents();
            foreach(var row in settings.Rows.Values)
                if(row.Save.Right>row.Save.Parent!.ClientSize.Width||row.Pick.Bottom>row.Pick.Parent!.ClientSize.Height)
                    throw new Exception("惩戒设置超出窗口");
        }
        form.ClientSize=new Size(1180,800);Application.DoEvents();
        using var rendered=new Bitmap(form.Width,form.Height);form.DrawToBitmap(rendered,new Rectangle(Point.Empty,rendered.Size));rendered.Save(path,System.Drawing.Imaging.ImageFormat.Png);
        form.Close();Console.WriteLine("惩戒界面验证通过："+path);
    }
}
