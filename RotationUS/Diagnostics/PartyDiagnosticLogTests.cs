#nullable enable
using System.Drawing;

namespace RotationUS.Diagnostics;

internal static class PartyDiagnosticLogTests
{
    internal static int Run()
    {
        int count=0;void Check(bool ok,string why) {if(!ok) throw new Exception("FAIL Party diagnostic log: "+why);count++;}
        var gate=new StateChangeLog();
        Check(gate.Changed("A","yes") && gate.Changed("B","yes") && !gate.Changed("A","yes"),"independent streams do not break deduplication");
        Check(gate.Changed("A","no") && gate.Changed("A","yes"),"A B A recovery is recorded");
        var trace=new PartyDiagnosticLog();var roster=PartyRunicPowerTests.Example;
        PartyRunicPower? power=new(roster,new bool?[]{null,true,null,null,null});
        int hp=115;Color range=Color.White;bool? combat=true;bool executing=false;
        string powerStatus="实时能量有效",rosterStatus="战斗中：沿用脱战确认资料";
        Color Frame(int id)=>id==52?Color.FromArgb(hp,255,0):id is >=51 and <=55?Color.FromArgb(255,255,0):range;
        List<PartyLogChange> Observe()=>trace.Observe(roster,power,Frame,combat,executing,new(),rosterStatus,powerStatus);
        var initial=Observe();
        Check(initial.Count==8 && initial.Any(c=>c.Message.Contains("职业=死亡骑士(6)")&&c.Message.Contains("专精=鲜血(250)")),"initial snapshot includes each slot, class/spec/role and identity");
        Check(initial.Any(c=>c.Key=="priority:2" && c.Message.Contains("降权生效") && c.Message.Contains("实际血量=")),"activation has reason and evidence");
        for(int i=0;i<300;i++) {
            roster=roster with {Members=roster.Members.ToArray()};
            power=new(roster,new bool?[]{null,true,null,null,null});
            Check(Observe().Count==0,"identical new frame does not log "+i);
        }
        hp=120;Check(Observe().Count==0,"HP movement within same rule state does not repeat RP/discount messages");
        Check(trace.Snapshot.Contains("实际血量="),"export snapshot retains current evidence without new log entries");
        power=new(roster,new bool?[]{null,false,null,null,null});
        var low=Observe();Check(low.Count==2 && low.Any(c=>c.Message.Contains("符能不足45点")),"45 down-crossing logs energy and rule transition");
        power=new(roster,new bool?[]{null,true,null,null,null});Check(Observe().Count==2,"energy recovery recorded");
        hp=102;Check(Observe().Count==0,"exact 40% remains eligible");
        hp=101;var protectedChange=Observe();
        Check(protectedChange.Count==1 && protectedChange[0].Message.Contains("低于40%保护线"),"below 40 logs only rule reason");
        hp=95;Check(Observe().Count==0,"unchanged protection reason is silent");
        hp=30;Check(Observe().Single().Message.Contains("进入危急档"),"urgent rescue distinct from ordinary protection");
        hp=115;Check(Observe().Single().Message.Contains("降权生效"),"health recovery restores discount with one transition");
        power=null;powerStatus="能量超过0.5秒未刷新";
        var expired=Observe();Check(expired.Count==2 && expired.Any(c=>c.Message.Contains("超过0.5秒")),"stale signal records exact cause and fallback");
        Check(Observe().Count==0,"unchanged expiry silent");
        powerStatus="能量信号缺失／队伍或采样版本不匹配";
        Check(Observe().Count==1,"changed unknown cause logs energy without repeating same fallback rule");
        power=new(roster,new bool?[]{null,true,null,null,null});Observe();
        range=Color.Black;Check(Observe().Single().Message.Contains("治疗范围外"),"range excluded from actual selection explained");
        range=Color.White;Observe();hp=255;
        Check(Observe().Single().Message.Contains("当前无需单体治疗"),"healthy tank does not falsely claim active healing discount");
        hp=115;Observe();combat=false;
        Check(Observe().Single().Message.Contains("非战斗"),"OOC diagnosis does not claim active discount");
        combat=true;Observe();executing=true;
        Check(Observe().Single().Key=="mode","execution switch reports once without repeating member states");
        roster=roster with {Members=roster.Members.Select((m,i)=>i==1?m with {SpecId=0}:m).ToArray()};
        Check(Observe().Any(c=>c.Message.Contains("鲜血专精未确认")),"inspection loss described");
        roster=PartyRunicPowerTests.Example;power=new(roster,new bool?[]{null,true,null,null,null});Observe();
        var replacement=roster with {Epoch=2,Members=roster.Members.Select((m,i)=>i==1?m with{Id=90,ClassId=8,SpecId=63,AssignedRole=3}:m).ToArray()};
        var replaced=trace.Observe(replacement,null,Frame,true,true,new(),rosterStatus,"能量已清除");
        Check(replaced.Any(c=>c.Key=="power:2" && c.Message.Contains("旧状态已清除")) && replaced.Any(c=>c.Message.Contains("成员ID=90")),"same-slot replacement clears old power and logs new identity");
        var cleared=trace.Observe(null,null,Frame,true,false,new(),"团队模式：小队功能停用","能量已清除");
        Check(cleared.Any(c=>c.Message.Contains("旧状态已清除")) && !trace.Snapshot.Contains("成员ID=90"),"raid clears old snapshot and states");
        Check(trace.Observe(null,null,Frame,true,false,new(),"团队模式：小队功能停用","能量已清除").Count==0,"stable raid inactive state silent");
        Check(Observe().Count==8,"reacquisition after reset records complete baseline");

        string directory=Path.Combine(Path.GetTempPath(),"hiji-party-log-"+Guid.NewGuid().ToString("N"));
        try {
            var writer=new HolyShieldLog(new SavedLogStore(directory));var history=new PartyDiagnosticLog();
            List<PartyLogChange> Step(bool enough)=>history.Observe(roster,new(roster,new bool?[]{null,enough,null,null,null}),Frame,true,false,new(),rosterStatus,"实时");
            writer.RecordPartyChanges(Step(true));string first=File.ReadAllText(writer.Path);
            writer.RecordPartyChanges(Step(true));Check(File.ReadAllText(writer.Path)==first,"automatic file uses same deduplicated party transitions as window");
            for(int i=0;i<550;i++) writer.RecordPartyChanges(Step(i%2!=0));
            string all=File.ReadAllText(writer.Path);
            Check(File.ReadAllLines(writer.Path).Length>1000 && all.StartsWith(first),"automatic file retains initial identity beyond 1000-line window");
            Check(all.Contains("执行=关闭（仅诊断）") && all.Contains("符能不足45点") && all.Contains("降权生效"),"execution-off file includes power and reasons");
            writer.RecordPartyChanges(history.Observe(null,null,Frame,null,false,new(),"采样中断","能量清除"));
            Check(File.ReadAllText(writer.Path).Contains("旧状态已清除：采样中断"),"reset without a valid frame still persists cleanup");

            writer.RecordPartyChanges(Step(true));
            string beforeFailure=File.ReadAllText(writer.Path);
            var queued=new List<PartyLogChange>();
            using(var locked=new FileStream(writer.Path,FileMode.Open,FileAccess.ReadWrite,FileShare.None)) {
                var down=Step(false);queued.AddRange(down);writer.RecordPartyChanges(down);
                Check(writer.Error is not null,"exclusive file lock reports append failure");
                writer.RecordPartyChanges(Step(false));
                var up=Step(true);queued.AddRange(up);writer.RecordPartyChanges(up);
                var cleanup=history.Observe(null,null,Frame,null,false,new(),"锁定期间退队","能量清除");
                queued.AddRange(cleanup);writer.RecordPartyChanges(cleanup);
            }
            // No further state change is needed to retry all intervening events.
            var unchanged=history.Observe(null,null,Frame,null,false,new(),"锁定期间退队","能量清除");
            Check(unchanged.Count==0,"retry frame itself has no new transitions");
            writer.RecordPartyChanges(unchanged);
            string recovered=File.ReadAllText(writer.Path);
            string[] appended=recovered[beforeFailure.Length..].Split(Environment.NewLine,StringSplitOptions.RemoveEmptyEntries);
            Check(writer.Error is null && recovered.StartsWith(beforeFailure),"unlock recovers append without rewriting old history");
            Check(appended.Select(line=>line[(line.IndexOf(' ')+1)..]).SequenceEqual(queued.Select(c=>c.Message)),"failed changes and cleanup replay exactly once in observation order");
            writer.RecordPartyChanges(unchanged);
            Check(File.ReadAllText(writer.Path)==recovered,"successful retry is removed from queue and unchanged state stays silent");
        } finally {
            foreach(string file in Directory.GetFiles(Path.Combine(directory,"captures"))) File.Delete(file);
            Directory.Delete(Path.Combine(directory,"captures"));
            foreach(string file in Directory.GetFiles(directory)) File.Delete(file);
            Directory.Delete(directory);
        }
        return count;
    }
}
