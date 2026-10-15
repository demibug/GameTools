#nullable enable
using System.Drawing;
using System.Text;

namespace RotationUS.Diagnostics;

// Diagnostics only: never starts execution or sends a game key. Each line is
// one coherent captured sample, and invalid/occluded data is marked explicitly.
internal sealed class HolyShieldLog
{
    private readonly SavedLogStore store;
    private readonly Dictionary<int,Color> frames=new(100);
    private readonly List<string> pendingPartyLines=new();
    private double lastSample=double.NegativeInfinity;
    private string lastSignature="";
    private bool remembered;
    internal string Path {get;}
    internal bool Started {get;private set;}
    internal string? Error {get;private set;}

    internal HolyShieldLog(SavedLogStore store)
    {
        this.store=store;
        Path=System.IO.Path.Combine(store.DefaultDirectory,"holy-shield-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N")[..6]+".log.txt");
    }

    internal void Record(Captured sample,DiagnosticLayout layout,bool live,bool executing,string executionReason,double now)
    {
        if(now-lastSample<.25) return;
        lastSample=now;
        for(int i=0;i<layout.Frames.Length;i++) frames[i+1]=sample.At(layout.Frames[i]);
        RecordFrames(frames,layout.HolyThresholds,live,executing,executionReason,now);
    }

    internal void RecordFrames(Dictionary<int,Color> values,HealingThresholds thresholds,bool live,bool executing,string executionReason,double now)
    {
        string signature=$"有效采样={live}；执行开启={executing}；执行判断={executionReason}；"+
            (live?Describe(values,thresholds,true):"采样无效，不能推断盾击阻挡原因");
        if(signature==lastSignature) return;
        if(Write(new[]{signature})) lastSignature=signature;
    }

    internal void RecordPartyChanges(IEnumerable<PartyLogChange> changes)
    {
        string timestamp=DateTimeOffset.Now.ToString("O");
        pendingPartyLines.AddRange(changes.Select(c=>timestamp+" "+c.Message));
        // Observe has already deduplicated these transitions. Keep them until
        // append succeeds, including their original observation timestamps.
        Write(Array.Empty<string>());
    }

    private bool Write(IEnumerable<string> messages)
    {
        var lines=messages.ToArray();
        if(lines.Length==0 && pendingPartyLines.Count==0) return true;
        try {
            Directory.CreateDirectory(store.DefaultDirectory);
            string header=Started?"":"神圣自动诊断（盾击／小队资料／血DK／鸣钟／美德推定窗口／神性之手／牺牲联动圣佑）；仅状态变化写入，无定时重复；记录条件与请求，技能释放仅按冷却推定。"+Environment.NewLine;
            string timestamp=DateTimeOffset.Now.ToString("O");
            File.AppendAllText(Path,header+string.Join(Environment.NewLine,pendingPartyLines.Concat(lines.Select(line=>timestamp+" "+line)))+Environment.NewLine,new UTF8Encoding(false));
            pendingPartyLines.Clear();
            Started=true;
            Error=null;
            // A history-index failure must not make a successfully written
            // state get appended again on every unchanged sample.
            if(!remembered) {
                try {store.Remember(Path);remembered=true;}
                catch(Exception e) when(e is IOException or UnauthorizedAccessException) {Error="日志已写入，但文件索引保存失败："+e.Message;}
            }
            return true;
        } catch(Exception e) when(e is IOException or UnauthorizedAccessException) {Error=e.Message;return false;}
    }

    internal static string Describe(Dictionary<int,Color> values,HealingThresholds thresholds,bool live)
    {
        bool? F(int id)=>values.TryGetValue(id,out var c)?FuryBuffs.Boolean(c):null;
        double? P(int id)=>values.TryGetValue(id,out var c)&&c.R==c.G&&c.G==c.B?c.R/255.0:null;
        string State(int id)=>F(id) is {} b?b?"是":"否":"未知";
        string Raw(int id)=>values.TryGetValue(id,out var c)?$"{c.R},{c.G},{c.B}":"缺失";
        var blockers=new List<string>();
        void Gate(int id,string name,bool required=true) {
            if(F(id)!=required) blockers.Add($"{name}[{id}]={State(id)}");
        }
        Gate(1,"战斗");Gate(2,"存活且未骑乘");Gate(3,"目标存活");Gate(4,"目标为敌对",false);
        Gate(12,"目标可攻击");Gate(95,"近战范围");Gate(97,"盾击已学就绪");Gate(98,"盾击可用");
        Gate(9,"未施法");Gate(20,"GCD就绪");Gate(19,"有队伍");Gate(92,"5圣能");Gate(13,"神圣震击至少2充能");
        if(F(41) is null) blockers.Add("队伍类型[41]=未知");
        var group=HolyTriage.Analyze(values,F(41)==true,thresholds);
        if(!(P(6)>=thresholds.HealBelow)) blockers.Add("自身未达到治疗线或血量未知");
        if(!group.Reliable) blockers.Add("队伍血量不可靠");
        if(group.Unknown>0) blockers.Add($"队伍未知血量={group.Unknown}");
        if(group.Injured>0) blockers.Add($"低于治疗线成员={group.Injured}");
        if(group.Major) blockers.Add("群体严重掉血");
        string gateSummary=live?(blockers.Count==0?"全部盾击条件满足":"盾击阻挡="+string.Join("、",blockers)):"采样无效，不能推断盾击阻挡原因";
        var fields=new[]{1,2,3,4,9,12,13,19,20,41,92,95,97,98,96,100};
        string raw=string.Join("；",fields.Select(id=>$"{id}={State(id)}({Raw(id)})"));
        string probes=string.Join("；",HolyItemRange.Probes.Select(p=>$"{p.Range}码档物品{p.Item}[{p.Id}]="+
            (values.TryGetValue(p.Id,out var c)?HolyItemRange.Decode(c):"缺失")+$"({Raw(p.Id)})"));
        int count=F(41)==true?40:5;
        string members=string.Join(",",Enumerable.Range(1,count).Select(slot=>{
            var reading=values.TryGetValue(50+slot,out var c)?TeamHealthReading.Read(c):new(TeamHealthStatus.Unknown,null);
            return $"{slot}="+(reading.Percent is {} p?(p*100).ToString("F1")+"%":reading.Status.ToString());
        }));
        return gateSummary+$"；自身HP={P(6)?.ToString("P1")??"未知"}；治疗线={thresholds.HealBelow:P0}；"+raw+"；"+probes+"；成员HP="+members;
    }

    internal static int RunTests()
    {
        int checks=0;
        void Check(bool ok,string message) {if(!ok) throw new Exception("FAIL Shield log: "+message);checks++;}
        var values=Enumerable.Range(1,100).ToDictionary(i=>i,_=>Color.Black);
        foreach(int id in new[]{1,2,3,9,12,13,19,20,92,95,96,97,98,100}) values[id]=Color.White;
        values[6]=Color.White;
        for(int slot=1;slot<=5;slot++) values[50+slot]=Color.FromArgb(255,255,0);
        Check(Describe(values,new(),true).StartsWith("全部盾击条件满足"),"healthy enemy sample passes every existing shield gate");
        values[13]=Color.Black;
        Check(Describe(values,new(),true).Contains("神圣震击至少2充能[13]=否"),"insufficient Shock charges explain blocked Shield");
        values[13]=Color.Orange;
        Check(Describe(values,new(),true).Contains("神圣震击至少2充能[13]=未知"),"unknown Shock charges explain blocked Shield");
        values[13]=Color.White;
        foreach(var (color,label) in new[]{(Color.Lime,"范围内"),(Color.Red,"范围外"),
            (Color.FromArgb(255,128,0),"nil或无效返回"),(Color.FromArgb(128,0,255),"秘密值"),
            (Color.Magenta,"查询报错"),(Color.Blue,"未查询"),(Color.Cyan,"物品距离接口缺失"),
            (Color.Black,"旧采集器或未显示")}) {
            values[14]=color;
            Check(Describe(values,new(),true).Contains("2码档物品168948[14]="+label),"item evidence persisted: "+label);
            var field=HolyBuffs.Fields().Single(f=>f.Id=="14");
            Check(HolyBuffs.Decode(new(),field,color).StartsWith(label),"UI and trace agree on item evidence: "+label);
        }
        values[14]=Color.Red;values[15]=Color.Lime;values[18]=Color.Magenta;
        string itemTrace=Describe(values,new(),true);
        Check(itemTrace.Contains("2码档物品168948[14]=范围外")&&itemTrace.Contains("2码档物品194718[15]=范围内")&&itemTrace.Contains("5码档物品8149[18]=查询报错"),"each probe keeps its own independent result");
        values[95]=Color.FromArgb(255,128,0);
        Check(Describe(values,new(),true).Contains("近战范围[95]=未知"),"actual unknown range is identified");
        Check(Describe(values,new(),true).Contains("95=未知(255,128,0)"),"source RGB is recorded");
        values[4]=Color.White;values[12]=Color.Black;values[53]=Color.FromArgb(180,255,0);
        string blocked=Describe(values,new(),true);
        Check(blocked.Contains("目标为敌对[4]=是")&&blocked.Contains("低于治疗线成员=1"),"friendly target and injured member are both recorded");
        Check(!Describe(values,new(),false).Contains("盾击阻挡="),"invalid capture cannot claim a gameplay blocker");
        string directory=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"hiji-shield-log-"+Guid.NewGuid().ToString("N"));
        try {
            var writer=new HolyShieldLog(new SavedLogStore(directory));
            writer.RecordFrames(values,new(),true,false,"执行已关闭",0);
            string first=File.ReadAllText(writer.Path);
            Check(writer.Started&&writer.Error is null&&first.Contains("执行开启=False")&&first.Contains("近战范围"),"logging works without enabling input");
            writer.RecordFrames(values,new(),true,false,"执行已关闭",1);
            Check(File.ReadAllText(writer.Path)==first,"unchanged samples are deduplicated");
            writer.RecordFrames(values,new(),true,false,"执行已关闭",6);
            Check(File.ReadAllText(writer.Path)==first,"unchanged states never get periodic duplicate lines");
            writer.RecordFrames(values,new(),true,false,"改变",7);
            writer.RecordFrames(values,new(),true,false,"执行已关闭",8);
            Check(File.ReadAllLines(writer.Path).Length==4,"A to B to A records recovery");
            writer.RecordFrames(values,new(),false,false,"采样无效",9);
            string invalid=File.ReadAllText(writer.Path);values[14]=Color.Yellow;
            writer.RecordFrames(values,new(),false,false,"采样无效",30);
            Check(File.ReadAllText(writer.Path)==invalid,"invalid pixel noise cannot repeat the same invalid state");
            Check(File.ReadAllText(System.IO.Path.Combine(directory,"saved-log-files.json")).Contains("holy-shield"),"automatic trace participates in saved-log history");
            string blockedDirectory=System.IO.Path.Combine(directory,"blocked-index");
            string indexTemporary=System.IO.Path.Combine(blockedDirectory,"saved-log-files.json.tmp");
            Directory.CreateDirectory(indexTemporary);
            var indexFailure=new HolyShieldLog(new SavedLogStore(blockedDirectory));
            indexFailure.RecordFrames(values,new(),false,false,"采样无效",0);
            string written=File.ReadAllText(indexFailure.Path);
            Check(indexFailure.Started && indexFailure.Error is not null,"successful append reports history-index failure separately");
            indexFailure.RecordFrames(values,new(),false,false,"采样无效",60);
            Check(File.ReadAllText(indexFailure.Path)==written,"index failure cannot cause unchanged states to be appended repeatedly");
            Directory.Delete(indexTemporary);
            indexFailure.RecordFrames(values,new(),true,false,"恢复",61);
            Check(indexFailure.Error is null,"history index can recover on the next change");
            foreach(string file in Directory.GetFiles(System.IO.Path.Combine(blockedDirectory,"captures"))) File.Delete(file);
            Directory.Delete(System.IO.Path.Combine(blockedDirectory,"captures"));
            foreach(string file in Directory.GetFiles(blockedDirectory)) File.Delete(file);
            Directory.Delete(blockedDirectory);
        } finally {
            foreach(string file in Directory.GetFiles(System.IO.Path.Combine(directory,"captures"))) File.Delete(file);
            Directory.Delete(System.IO.Path.Combine(directory,"captures"));
            foreach(string file in Directory.GetFiles(directory)) File.Delete(file);
            Directory.Delete(directory);
        }
        return checks;
    }
}
