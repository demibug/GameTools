#nullable enable
using System.Drawing;

namespace RotationUS.Diagnostics;

// Dedicated palette: old slot 11 food booleans must never create a cast edge.
internal static class HolySacrificeReading
{
    internal static bool? Ready(Color c)=>(c.R,c.G,c.B) switch {
        (0,255,255)=>true,(128,0,255)=>false,_=>null };
    internal static string Decode(Color c)=>Ready(c) switch {
        true=>"牺牲就绪",false=>"牺牲冷却中",_=>"牺牲未知／未学／旧采集器" };
}

internal sealed record HolySacrificeContext(bool Request=false,bool Holding=false);

internal sealed class HolySacrificeProtection
{
    private bool? previousReady;
    private bool? previousProtectionReady;
    private bool? observedRaid;
    private bool pending;
    private int attempts;
    private double lastAt=double.NegativeInfinity,deadline,lastRequest=double.NegativeInfinity;
    private readonly List<PartyLogChange> changes=new();
    internal HolySacrificeContext Current {get;private set;}=new();
    internal string Status {get;private set;}="等待牺牲冷却采样";
    private void State(string value)
    {
        if(Status==value) return;
        Status=value;changes.Add(new("sacrifice", "[牺牲联动圣佑] "+value));
    }
    internal List<PartyLogChange> DrainChanges() {var result=changes.ToList();changes.Clear();return result;}
    internal void Reset(string reason="旧联动请求已清除")
    {
        previousReady=previousProtectionReady=observedRaid=null;pending=false;attempts=0;lastAt=lastRequest=double.NegativeInfinity;deadline=0;Current=new();
        State(reason);
    }
    internal HolySacrificeContext Observe(Dictionary<int,Color> frames,double now,bool live=true,bool executing=true)
    {
        bool? F(int id)=>frames.TryGetValue(id,out var c)?FuryBuffs.Boolean(c):null;
        Current=new();
        if(!live||!double.IsFinite(now)||now<0||F(96)!=true||F(100)!=true||F(1)!=true||F(2)!=true||F(19)!=true||F(41) is null||!executing) {
            Reset(!executing?"执行关闭：不保留联动请求":!live?"采样失效：联动已清除":
                "非战斗／死亡／骑乘／未组队或状态未知：联动已清除");
            return Current;
        }
        if(observedRaid is {} previousMode&&previousMode!=F(41)) Reset("小队／团队模式变化：旧联动已清除");
        if(now<lastAt||now-lastAt>1.5) {
            if(lastAt!=double.NegativeInfinity) Reset("断帧／时间跳变：联动已清除");
            previousReady=null;
        }
        observedRaid=F(41);
        lastAt=now;
        bool? ready=frames.TryGetValue(11,out var color)?HolySacrificeReading.Ready(color):null;
        if(ready is null) {Reset("牺牲冷却未知／未学／旧插件：联动已清除");return Current;}
        bool initial=previousReady is null;
        bool edge=previousReady==true&&ready==false;
        bool protectionEnteredCooldown=previousProtectionReady==true&&F(30)==false;
        previousProtectionReady=F(30);
        previousReady=ready;
        if(edge) {
            pending=true;attempts=0;deadline=now+2;lastRequest=double.NegativeInfinity;
            State("牺牲就绪转冷却：建立2秒圣佑请求，无视自身血量");
        }
        if(!pending) {
            if(ready==true) State("牺牲就绪：等待释放");
            else if(initial) State("牺牲已在冷却：未观察到释放，不补开圣佑");
            return Current;
        }
        // Capture continues during manual-input grace. An observed Protection
        // cooldown edge also completes the link when no automatic key was sent.
        // Check confirmation before timeout so the last allowed sample cannot
        // log a failure despite already showing Protection on cooldown.
        if((attempts>0||protectionEnteredCooldown)&&F(30)==false) {
            pending=false;State("已观察到圣佑进入冷却：本次联动结束");return Current;
        }
        if(now>=deadline) {pending=false;State("2秒请求超时：未确认圣佑进入冷却，取消补开");return Current;}
        if(ready==true) {pending=false;State("牺牲恢复就绪：旧联动已清除");return Current;}
        bool request=F(30)==true&&F(40)==true&&attempts<3&&now-lastRequest>=.5;
        Current=new(request,true);
        if(F(30)!=true||F(40)!=true) State("等待圣佑就绪且可用（未知不授权），最多保留2秒");
        else if(attempts>=3) State("已请求3次：停止重试，等待冷却确认或超时");
        else if(request) State(attempts==0?"牺牲联动待执行：圣佑就绪可用，无视自身血量":$"圣佑仍就绪：允许第{attempts+1}次请求");
        return Current;
    }
    // Called only when Process actually emits action 5, including self rescue.
    internal void Requested(double now)
    {
        if(!pending||!Current.Request||now>=deadline) return;
        attempts++;lastRequest=now;Current=new(false,true);
        State($"已请求圣佑第{attempts}次（-）：等待冷却确认，不代表施放成功");
    }
}
