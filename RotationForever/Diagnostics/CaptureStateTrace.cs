#nullable enable
using System.Drawing;

namespace RotationUS.Diagnostics;

// Diagnostic evidence only. Never validates an action or reuses a previous capture.
internal sealed class CaptureStateTrace
{
    private string last="";
    private double lastAt=double.NegativeInfinity;
    private double? pausedAt;
    internal static SamplePoint[] Points(DiagnosticLayout layout)
    {
        if(layout.Frames.Length<2||layout.Bars.Length<2) return Array.Empty<SamplePoint>();
        int x=(int)Math.Round(layout.Frames[0].X-(layout.Frames[^1].X-layout.Frames[0].X)/(double)(layout.Frames.Length-1));
        var points=new[]{new SamplePoint(x,layout.Frames[0].Y),new SamplePoint(x,layout.Bars[0].Y)};
        return points.All(p=>p.X>=0&&p.X<layout.ClientWidth&&p.Y>=0&&p.Y<layout.ClientHeight)?points:Array.Empty<SamplePoint>();
    }
    internal static string Environment(Color? upper,Color? lower,bool includeAlpha=true)
    {
        if(upper is not {} a||lower is not {} b||a.R!=208||a.B is not (1 or 3)||b.R!=209||b.G>101||
            Enumerable.Range(0,4).Any(i=>((a.G>>(i*2))&3)>2)||(b.B&15)>8||(b.B>>4)>8)
            return "警告框环境=未知（旧插件、未输出或像素被遮挡）";
        string State(int shift)=>((a.G>>shift)&3) switch {1=>"否",2=>"是",_=>"未知"};
        string Strata(int id)=>id switch {1=>"BACKGROUND",2=>"LOW",3=>"MEDIUM",4=>"HIGH",5=>"DIALOG",6=>"FULLSCREEN",7=>"FULLSCREEN_DIALOG",8=>"TOOLTIP",_=>"未知"};
        return $"警告框IsShown={State(0)}；禁用低血量警告CVar={State(2)}；UIParent可见={State(4)}；取色层可见={State(6)}；插件采集Lua错误={(a.B==3?"有（原文见HijiCaptureLog）":"无已报告错误")}；"+
            $"取色层级={Strata(b.B&15)}；警告层级={Strata(b.B>>4)}"+
            (includeAlpha?$"；警告有效Alpha={(b.G==101?"未知":(b.G/100.0).ToString("F2"))}":"");
    }
    internal string? Observe(Captured sample,DiagnosticLayout layout,bool accepted,bool heartbeat,double? age,
        IntPtr foreground,GameWindow game,bool executing,double now)
    {
        var points=Points(layout);
        Color? Read(int n)=>points.Length==2&&sample.Bounds.Contains(points[n].Point)?sample.At(points[n]):null;
        var upper=Read(0);var lower=Read(1);
        var mode=sample.At(layout.Markers[0]);var identity=sample.At(layout.Markers[1]);var pulse=sample.At(layout.Markers[2]);
        string cause=accepted?"采样校验通过":Colors.Kind(mode)!=2?"模式标记异常":!heartbeat?"心跳未确认或过期":"职业标记无效或等待专精确认";
        if(!accepted) pausedAt??=now;
        double? duration=accepted&&pausedAt is {} start?Math.Max(0,now-start):null;
        if(accepted) pausedAt=null;
        string head=$"{cause}；前台为游戏={foreground==game.Handle}；执行开启={executing}；";
        string key=head+Environment(upper,lower,false);
        if(key==last&&(accepted||now-lastAt<1)) return null;
        last=key;lastAt=now;
        string Rgb(Color c)=>$"{c.R},{c.G},{c.B}";
        string Point(SamplePoint p)=>$"{p.X},{p.Y}";
        return "[采集诊断] "+head+Environment(upper,lower)+
            $"；模式RGB={Rgb(mode)}；职业RGB={Rgb(identity)}；心跳RGB={Rgb(pulse)}；心跳最近变化距今={(age is {} seconds?seconds.ToString("F3")+"秒":"未知")}；"+
            $"前台句柄=0x{foreground.ToInt64():X}；游戏句柄=0x{game.Handle.ToInt64():X}；PID={game.Pid}；客户区={layout.ClientWidth}x{layout.ClientHeight}；"+
            $"采样区域={sample.Bounds}；标记点={string.Join("/",layout.Markers.Select(Point))}；"+
            $"当帧自身HP原始RGB={Rgb(sample.At(layout.Frames[5]))}；治疗石RGB={Rgb(sample.At(layout.Frames[15]))}；药水RGB={Rgb(sample.At(layout.Frames[16]))}"+
            (!accepted?"；以上原始像素未通过校验，不推断实际血量或可用性":"")+
            (duration is {} d?$"；本次采样暂停={d:F3}秒":"");
    }
    internal static int RunTests()
    {
        int n=0;
        void Check(bool ok,string name){if(!ok)throw new Exception("FAIL capture trace: "+name);n++;}
        Check(Environment(Color.Black,Color.Black).Contains("未知"),"legacy collectors are unknown");
        var a=Color.FromArgb(208,166,1);var b=Color.FromArgb(209,75,120);
        Check(Environment(a,b).Contains("警告框IsShown=是")&&Environment(a,b).Contains("取色层级=TOOLTIP")&&Environment(a,b).Contains("警告有效Alpha=0.75"),"warning visibility and strata decode");
        Check(Environment(a,Color.FromArgb(209,101,120)).Contains("Alpha=未知"),"restricted alpha unknown");
        Check(Environment(Color.FromArgb(208,255,1),b).Contains("旧插件"),"invalid flags rejected");
        Check(Environment(Color.FromArgb(208,166,3),b).Contains("有（原文见HijiCaptureLog）"),"collector Lua failure is reported independently of heartbeat");
        using var pixels=DiagnosticTests.Pattern(1);
        var layout=Calibration.Find(pixels,new Size(1600,900));
        var points=Points(layout);
        Check(points.Length==2&&layout.CaptureBounds.Contains(points[0].Point)&&layout.CaptureBounds.Contains(points[1].Point),"column zero included in capture bounds");
        pixels.SetPixel(points[0].X,points[0].Y,a);pixels.SetPixel(points[1].X,points[1].Y,b);
        using var sample=new Captured((Bitmap)pixels.Clone(),new Rectangle(Point.Empty,pixels.Size));
        var trace=new CaptureStateTrace();var game=new GameWindow(1,new IntPtr(2),"test");
        Check(trace.Observe(sample,layout,false,true,.1,new IntPtr(2),game,true,0)!.Contains("不推断实际血量"),"invalid captures retain raw evidence only");
        Check(trace.Observe(sample,layout,false,true,.2,new IntPtr(2),game,true,.1) is null,"repeated invalid samples throttled");
        Check(trace.Observe(sample,layout,false,true,.3,new IntPtr(2),game,true,1.1) is not null,"ongoing pause retains evidence once a second");
        Check(trace.Observe(sample,layout,true,true,0,new IntPtr(2),game,true,4.4)!.Contains("本次采样暂停=4.400秒"),"recovery duration recorded");
        Check(trace.Observe(sample,layout,true,true,.1,new IntPtr(2),game,true,5) is null,"stable valid captures deduplicated");
        Check(trace.Observe(sample,layout,true,true,.2,new IntPtr(3),game,true,6)!.Contains("前台为游戏=False"),"focus loss recorded");
        return n;
    }
}
