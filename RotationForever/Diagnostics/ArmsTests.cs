#nullable enable
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace RotationUS.Diagnostics;

internal static class ArmsTests
{
    internal static int Run()
    {
        int count=0;
        void Check(bool ok,string why) { if(!ok) throw new Exception("FAIL: 武器 · "+why); count++; }
        using var pattern=DiagnosticTests.Pattern(1);
        var layout=Calibration.Find(pattern,new Size(1600,900)); layout.SpecId=71;
        var reference=new SampleColor(144,92,18);
        foreach(var point in ArmsBuffs.Points)
        {
            layout.SpecialPoints[point.Key]=new() {Enabled=true,Point=new(540,8),Comparison=ArmsBuffs.UsesColor(point.Key)?"expiring":"presence",ReferenceColor=reference};
            var absent=ArmsBuffs.Observe(layout,point.Key,_=>Color.Black);
            var matched=ArmsBuffs.Observe(layout,point.Key,_=>reference.ToColor());
            var other=ArmsBuffs.Observe(layout,point.Key,_=>Color.Red);
            Check(absent.Present==false&&absent.Expiring==false,point.Label+"黑色缺失");
            Check(matched.Present==true&&(!ArmsBuffs.UsesColor(point.Key)||matched.Expiring==true),point.Label+"参考色");
            Check(other.Present==true&&(!ArmsBuffs.UsesColor(point.Key)||other.Expiring==false),point.Label+"其他非黑色");
            layout.SpecialPoints[point.Key].Enabled=false;
            Check(ArmsBuffs.Observe(layout,point.Key,_=>Color.Black).Present is null,point.Label+"停用未知");
            layout.SpecialPoints[point.Key].Enabled=true;
        }
        var f=Enumerable.Range(1,50).ToDictionary(i=>i,_=>Color.Black);
        var b=Enumerable.Range(1,50).ToDictionary(i=>i,_=>Color.Black);
        var buff=new ArmsBuffs();
        BuffObservation State(bool present=false,bool matches=false)=>new(present,matches,"合成");
        void F(int id,bool value=true)=>f[id]=value?Color.White:Color.Black;
        void Skill(int usable,int ready,bool value=true) { F(usable,value);F(ready,value); }
        void Reset(bool aoe=false,bool execute=false)
        {
            for(int i=1;i<=50;i++) { f[i]=Color.Black;b[i]=Color.Black; }
            foreach(int id in new[]{1,3,4,5,20,38,39,50}) F(id);
            F(2,aoe);F(42,execute);f[6]=Color.White;
            buff=new() {RendTen=State(true),RendFive=State(true),Smash=false,SuddenDeath=State(),Demise=State(),Precision=State(),Collateral=State()};
        }
        void Want(int key,string why)
        {
            var decision=WQZ.Decide(f,b,buff);
            Check(decision.Key==key,why+"；实际="+decision.Reason);
        }
        void AllDps()
        {
            foreach(var pair in new[]{(22,13),(23,14),(24,15),(25,16),(26,17),(27,33),(28,34),(29,35),(30,36)}) Skill(pair.Item1,pair.Item2);
            b[1]=b[2]=Color.White;
        }
        Reset();AllDps();buff.RendTen=State(true,true);Want(12,"单体不足10秒先补撕裂");
        buff.RendTen=State();Want(12,"单体缺失撕裂先补");
        buff.RendTen=State(true);Want(8,"单体无军官标记不自动天神");
        Skill(22,13,false);Want(8,"单体巨人先于输出");
        Skill(23,14,false);buff.SuddenDeath=State(true,true);Want(14,"猝死两层先于风暴");
        buff.SuddenDeath=State(true);buff.Smash=true;Want(14,"风暴前消耗猝死");
        buff.Demise=State(true,true);Want(7,"殒命满三层直接风暴");
        buff.Smash=false;F(37);Want(13,"英勇打击先于致死");
        F(37,false);Want(11,"普通猛击不抢致死");
        Skill(26,17,false);Want(14,"猝死一层低位斩杀");
        buff.SuddenDeath=State();F(35,false);Want(10,"一充能可用不被恢复冷却挡住");
        F(29,false);buff.RendFive=State(true,true);Want(12,"不足5秒低位刷新");
        buff.RendFive=State(true);Want(13,"单体猛击兜底");
        Reset(execute:true);AllDps();Want(8,"斩杀无军官标记不自动天神");
        Skill(22,13,false);Want(8,"斩杀巨人");
        Skill(23,14,false);F(37);Want(13,"斩杀英勇优先");
        F(37,false);buff.Smash=true;Want(7,"斩杀巨人期间风暴");
        Skill(25,16,false);buff.Precision=State(true,true);Want(11,"精准两层致死");
        buff.Precision=State(true);buff.SuddenDeath=State(true);Want(14,"精准一层让位猝死");
        buff.SuddenDeath=State();F(7);Want(14,"怒气>40高位斩杀");
        F(7,false);Want(10,"怒气未超40先压制");
        F(29,false);Want(14,"斩杀阶段末位斩杀");
        Reset(aoe:true);AllDps();Want(9,"多目标横扫第一");
        Skill(24,15,false);buff.RendTen=State();Want(12,"多目标补缺失撕裂");
        buff.RendTen=State(true,true);Want(8,"多目标无军官标记不自动天神且撕裂已存在不提前刷新");
        Skill(22,13,false);Want(8,"多目标巨人");
        Skill(23,14,false);buff.Collateral=State(true,true);Want(12,"间接三层先于风暴");
        buff.Collateral=State(true);Want(7,"多目标无巨人也风暴");
        Skill(25,16,false);buff.SuddenDeath=State(true,true);Want(14,"两层猝死先于普通顺劈");
        buff.SuddenDeath=State(true);Want(12,"普通顺劈先于一层猝死");
        Skill(27,33,false);Want(10,"两充能压制先于一层猝死");
        b[2]=Color.Black;Want(14,"一层猝死先于单充能压制");
        buff.SuddenDeath=State();Want(10,"普通压制");
        F(29,false);Want(14,"可用斩杀先于致死");
        Skill(28,34,false);Want(11,"多目标末位致死");
        Skill(26,17,false);Want(13,"多目标猛击兜底");
        Reset();Skill(22,13);Skill(26,17);Skill(27,33);F(14);buff.RendTen=default;Want(0,"未知撕裂挡住后续输出");
        F(27,false);Want(11,"顺劈不可用时未知撕裂不阻塞");
        Reset();Skill(32,19);F(43);f[6]=Color.FromArgb(127,127,127);Want(2,"低于50%剑在人在");
        F(43,false);Want(0,"未低于50%不剑在人在");
        F(43);F(4,false);Want(0,"防御保留原距离前提");
        Reset();F(10);F(18);Want(15,"碎裂投掷沿用狂暴Shift+反斜杠");
        Reset();Skill(31,21);f[6]=Color.FromArgb(191,191,191);buff.RendTen=default;
        Want(1,"乘胜追击低于75%且不依赖输出光环");
        f[6]=Color.FromArgb(192,192,192);Want(0,"乘胜追击达到75%不触发");
        f[6]=Color.FromArgb(191,191,191);F(3,false);Want(0,"乘胜追击需要5码");
        F(3);F(20,false);Want(0,"乘胜追击等待GCD");
        Reset();F(47);f[6]=Color.FromArgb(203,203,203);Want(18,"袋里乾坤低于80%");
        f[6]=Color.FromArgb(204,204,204);Want(0,"袋里乾坤80%不触发");
        f[6]=Color.FromArgb(100,100,100);F(47,false);F(11);F(12);Want(5,"治疗石优先药水");
        F(11,false);Want(6,"治疗药水继承");F(47);Want(18,"袋里乾坤优先治疗石药水");
        Skill(31,21);Want(1,"乘胜追击优先袋里乾坤");
        Reset();F(44);F(45);F(46);F(20,false);buff.RendTen=default;
        Want(4,"食物标记拳击不等待GCD或输出光环");
        F(45,false);Want(29,"目标停止施法清理食物标记");
        F(45);F(46,false);Want(29,"拳击冷却清理食物标记");
        F(46);F(3,false);Want(29,"离开近战清理食物标记");
        F(3);F(1,false);Want(29,"脱战清理食物标记");
        Reset();F(45);F(46);Want(0,"无食物标记不自动拳击");
        Reset();F(48);Skill(22,13);Want(3,"军官标记请求武器天神");
        buff.RendTen=default;Want(3,"军官请求不依赖未知输出光环");
        F(49);Want(28,"天神自身冷却确认后清理军官");
        F(49,false);F(1,false);Want(28,"脱战清理军官");
        foreach (var phase in new[]{(false,false),(false,true),(true,false)})
        {
            Reset(phase.Item1,phase.Item2);Skill(22,13);
            Want(0,"三套列表军官OFF且天神就绪也不发9");
            f[48]=Color.Orange;Want(0,"三套列表未知军官标记不自动天神");
            F(48);Want(3,"三套列表军官ON才发9");
            F(22,false);Want(0,"军官ON但天神不可用不发9");
        }
        Reset();F(44);F(45);F(46);F(48);Skill(22,13);
        Want(4,"同时开启两标记时拳击优先且不会当作天神请求");
        F(44,false);Want(3,"食物OFF后军官仍单独请求天神");
        Reset();f[44]=Color.Orange;F(45);F(46);Want(0,"未知食物标记不误发拳击");
        Reset();F(39,false);Want(0,"协议失效不发键");
        Reset();F(44);F(45);F(46);F(50,false);Want(0,"旧插件通用字段未加载时不发键");
        Check(WQZ.Decide(f,b,buff).Reason.Contains("/reload"),"明确提示重载插件");
        F(50);Want(4,"重载新采集代码后食物请求拳击");
        Reset();F(20,false);AllDps();Want(0,"公共冷却中无输出");
        Reset();AllDps();buff.RendTen=State(true);buff.SuddenDeath=State(true,true);
        Check(WQZ.Decide(f,b,buff).Branch=="单体","猝死不切换血量阶段");
        var directory=Path.Combine(Path.GetTempPath(),"hiji-arms-tests-"+Guid.NewGuid().ToString("N"));
        var profiles=new ProfileLayouts(directory);var arms=ClassProfiles.Find(71)!;
        profiles.Save(arms,layout);
        var furyGeometry=layout.Copy();furyGeometry.SpecId=72;furyGeometry.SpecialPoints.Clear();
        furyGeometry.SpecialPoints["enrage"]=new(){Enabled=true,Point=new(570,8)};
        var loaded=profiles.Load(arms,furyGeometry);
        Check(loaded.SpecialPoints.Count==7&&!loaded.SpecialPoints.ContainsKey("enrage"),"武器独立重载七点");
        var missing=new ProfileLayouts(Path.Combine(directory,"fresh"));
        Check(missing.Load(arms,furyGeometry).SpecialPoints.Count==0,"新武器不继承狂暴点");
        var shifted=furyGeometry.Copy();shifted.ClientWidth++;
        Check(profiles.Load(arms,shifted).SpecialPoints.Values.All(p=>!p.Enabled),"尺寸变化停用七点");
        using var settings=new ArmsPointSettings();settings.LoadSettings(layout);
        settings.Stage("rend10",new(new(550,8),new(50,60,70)));
        settings.Stage("rend5",new(new(560,8),new(80,90,100)));
        var saved=settings.BuildLayout(layout,"rend10");settings.LoadSettings(saved,"rend10");
        Check(settings.Rows["rend5"].Pending is not null,"保存一行保留其他待保存色");
        Check(saved.SpecialPoints["rend5"].ReferenceColor==reference,"保存一行不修改其他参考色");
        profiles.Save(arms,saved);Check(profiles.Load(arms,layout).SpecialPoints["rend10"].ReferenceColor==new SampleColor(50,60,70),"新色重载");
        return count;
    }

    internal static void Render(string path)
    {
        using var pattern=DiagnosticTests.Pattern(1);
        var layout=Calibration.Find(pattern,new Size(1600,900));layout.SpecId=71;
        using var image=new Bitmap(800,20);
        image.SetPixel(layout.Markers[0].X,0,Color.Lime);
        image.SetPixel(layout.Markers[1].X,0,ClassProfiles.Encode(71,1));
        image.SetPixel(layout.Markers[2].X,0,Color.Cyan);
        int x=540;
        foreach(var item in ArmsBuffs.Points)
        {
            var color=new SampleColor(144,92,18);
            layout.SpecialPoints[item.Key]=new(){Enabled=true,Point=new(x,8),Comparison=ArmsBuffs.UsesColor(item.Key)?"expiring":"presence",ReferenceColor=color};
            image.SetPixel(x,8,color.ToColor());x+=24;
        }
        var profiles=new ProfileLayouts(Path.Combine(Path.GetTempPath(),"hiji-arms-ui-"+Guid.NewGuid().ToString("N")));
        profiles.Save(ClassProfiles.Find(71)!,layout);
        using var form=new DiagnosticForm(false,profileLayouts:profiles);
        form.Show();Application.DoEvents();form.FreezeForTesting();form.ShowSynthetic(layout,image);form.Refresh();Application.DoEvents();
        static IEnumerable<Control> All(Control parent)=>parent.Controls.Cast<Control>().SelectMany(c=>new[]{c}.Concat(All(c)));
        var settings=All(form).OfType<ArmsPointSettings>().Single();
        foreach(var row in settings.Rows.Values)
            if(!row.Pick.Visible||row.Pick.Bottom>row.Pick.Parent!.ClientSize.Height||!row.Disable.Enabled)
                throw new Exception("武器七行未完整显示");
        foreach(var size in new[]{new Size(1180,800),new Size(950,650)})
        {
            form.ClientSize=size;form.Refresh();Application.DoEvents();
            foreach(var row in settings.Rows.Values)
                if(row.Save.Right>row.Save.Parent!.ClientSize.Width||row.Pick.Bottom>row.Pick.Parent!.ClientSize.Height)
                    throw new Exception("武器设置超出窗口");
        }
        form.ClientSize=new Size(1180,800);Application.DoEvents();
        using var rendered=new Bitmap(form.Width,form.Height);form.DrawToBitmap(rendered,new Rectangle(Point.Empty,rendered.Size));rendered.Save(path,ImageFormat.Png);
        form.Close();Console.WriteLine("武器界面验证通过："+path);
    }
}
