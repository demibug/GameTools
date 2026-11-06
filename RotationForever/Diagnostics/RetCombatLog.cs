#nullable enable
using System.Drawing;
using System.Text;

namespace RotationUS.Diagnostics;

// Records coherent samples before presentation throttling, including floating mode.
// Execution judgments are requests, never evidence of successful spell casts.
internal sealed class RetCombatLog
{
    private readonly SavedLogStore store;
    private string last="";
    private bool remembered;
    internal string Path {get;}
    internal bool Started {get;private set;}
    internal string? Error {get;private set;}
    internal RetCombatLog(SavedLogStore store)
    {
        this.store=store;
        Path=System.IO.Path.Combine(store.DefaultDirectory,"ret-combat-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N")[..6]+".log.txt");
    }
    internal void Record(Captured sample,DiagnosticLayout layout,bool live,bool executing,string reason)
    {
        if(layout.SpecId!=70) return;
        RecordValues(i=>sample.At(layout.Frames[i-1]),i=>sample.At(layout.Bars[i-1]),live,executing,reason,
            string.Join(";",RetBuffs.ActivePoints(RetBuffs.IsRadiant(layout,sample.At)).Select(p=>p.Key+"="+(layout.SpecialPoints.TryGetValue(p.Key,out var point)&&point.Enabled?Rgb(sample.At(point.Point)):"未配置"))));
    }
    private static string Rgb(Color c)=>$"{c.R},{c.G},{c.B}";
    internal void RecordValues(Func<int,Color> frame,Func<int,Color> bar,bool live,bool executing,string reason,string buffs="")
    {
        // The two API results are retained separately: F21..39 and B30..42.
        string line=$"有效采样={live}；执行开启={executing}；执行判断={reason}；"+
            (live?"":"无效采样，以下原始颜色不可用于决策；")+
            string.Join(";",Enumerable.Range(1,50).Select(i=>$"F{i}={Rgb(frame(i))}"))+"；"+
            string.Join(";",Enumerable.Range(1,43).Select(i=>$"B{i}={Rgb(bar(i))}"))+"；"+buffs;
        if(line==last) return;
        try {
            Directory.CreateDirectory(store.DefaultDirectory);
            string header=Started?"":"惩戒连续诊断：浮窗／最小化仍记录；仅状态变化写入。执行判断为按键请求，不代表施法成功。"+Environment.NewLine+
                string.Join("；",RetBuffs.Fields().Where(f=>f.Bar||int.TryParse(f.Id,out var id)&&id<=50).Select(f=>f.Id+"="+f.Name))+Environment.NewLine;
            File.AppendAllText(Path,header+DateTimeOffset.Now.ToString("O")+" "+line+Environment.NewLine,new UTF8Encoding(false));
            Started=true;last=line;Error=null;
            if(!remembered) {
                try {store.Remember(Path);remembered=true;}
                catch(Exception e) when(e is IOException or UnauthorizedAccessException) {Error="日志已写入，索引保存失败："+e.Message;}
            }
        } catch(Exception e) when(e is IOException or UnauthorizedAccessException) {Error=e.Message;}
    }
}
