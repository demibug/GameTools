#nullable enable
using System.Drawing;

namespace RotationUS.Diagnostics;

internal sealed class RotationExecution
{
    private double manualPauseUntil;
    private string manualReason="";
    internal string LastInputDetails {get;private set;}="";
    private readonly Func<IntPtr> foreground;
    private readonly Func<bool>? manualInput;
    private readonly Action<Dictionary<int, Color>, Dictionary<int, Color>>? execute;
    private int runningSpec;
    private readonly Dictionary<int, Color> frames = new(DisplayProtocol.CellCount + 1);
    private readonly Dictionary<int, Color> bars = new(DisplayProtocol.CellCount);

    internal RotationExecution(Func<IntPtr>? foreground = null, Func<bool>? manualInput = null,
        Action<Dictionary<int, Color>, Dictionary<int, Color>>? execute = null)
    {
        this.foreground = foreground ?? global::Program.GetForegroundWindow;
        this.manualInput = manualInput;
        this.execute = execute;
    }
    internal bool Enabled { get; private set; }
    internal string LastStopReason { get; private set; } = "尚未开启";
    internal event Action<bool,string>? StateChanged;
    internal event Action? EnabledChanged;
    internal void Start(ClassProfile? profile = null,string reason="用户开启执行")
    {
        RotationForever.ForeverPaladin.Inst.Reset();
        CJQ.Inst.Reset();
        HOLY.Inst.CancelExecution();
        manualPauseUntil = 0; runningSpec = (profile ?? ClassProfiles.Protection).SpecId;
        if (Enabled) return;
        Enabled = true; LastStopReason=""; EnabledChanged?.Invoke(); StateChanged?.Invoke(true,reason);
    }
    internal void Stop(string reason="未指定来源")
    {
        RotationForever.ForeverPaladin.Inst.Reset();
        CJQ.Inst.Reset();
        HOLY.Inst.CancelExecution();
        manualPauseUntil = 0; runningSpec = 0;
        if (!Enabled) return;
        Enabled = false; LastStopReason=reason; EnabledChanged?.Invoke(); StateChanged?.Invoke(false,reason);
    }
    internal string Tick(GameWindow game, DiagnosticLayout layout, Captured sample, bool live, double now, ClassProfile? profile = null)
    {
        LastInputDetails="";
        if (!Enabled) return "执行已关闭："+LastStopReason;
        if (!layout.CurrentProtocol) { Stop("布局协议过期，需重新定位"); return "布局已升级，请重新定位100个色块"; }
        profile ??= ClassProfiles.Protection;
        if (profile.SpecId != runningSpec || (layout.SpecId != 0 && layout.SpecId != profile.SpecId))
        { Stop("执行专精与已确认的采集专精不一致"); HOLY.Inst.Reset();return "职业 / 专精变化，执行已关闭"; }
        if (!live) { CJQ.Inst.Cancel(); if(profile.SpecId==65) {HOLY.Inst.Sacrifice.Reset("采样失效：联动已清除");HOLY.Inst.Raid.Reset();} return "执行等待有效职业数据 / 心跳"; }
        if (foreground() != game.Handle) { CJQ.Inst.Cancel(); if(profile.SpecId==65) {HOLY.Inst.Sacrifice.Reset("游戏不在前台：联动已清除");HOLY.Inst.Raid.Reset();} return "执行等待游戏处于前台"; }
        if (manualInput?.Invoke() ?? global::Program.ClassManualInputPressed(profile)) {
            manualPauseUntil = now + .5;
            manualReason=manualInput is not null?"测试输入":global::Program.ClassManualKeyDescription(profile);
        }
        if (now < manualPauseUntil) {
            LastInputDetails=$"触发键状态={manualReason}；剩余让出窗口={(manualPauseUntil-now)*1000:F0}ms（系统按键状态，不证明物理操作来源）";
            if(profile.SpecId==65) HOLY.Inst.CancelExecution(clearSacrifice:false);return "执行让出手动按键（500 ms）";
        }
        for (int i = 0; i < layout.Frames.Length; i++) frames[i + 1] = sample.At(layout.Frames[i]);
        for (int i = 0; i < layout.Bars.Length; i++) bars[i + 1] = sample.At(layout.Bars[i]);
        if (layout.SpecialEnabled) frames[DisplayProtocol.SpecialStart] = sample.At(layout.Special);
        else frames.Remove(DisplayProtocol.SpecialStart);
        if (profile.SpecId == 73) FZ.Inst.IgnorePainReferenceColor = layout.SpecialEnabled ? layout.SpecialReferenceColor?.ToColor() : null;
        if (profile.SpecId == 581 && !layout.SpecialEnabled) return "执行等待复仇专精特殊点配置";
        if (profile.SpecId == 71) WQZ.Inst.Buffs = ArmsBuffs.Read(layout, sample.At);
        if (profile.SpecId == 70) CJQ.Inst.Buffs = RetBuffs.Read(layout, sample.At);
        if (profile.SpecId == 72) KBZ.Inst.Buffs = FuryBuffs.Read(layout, sample.At);
        if (profile.SpecId == 65) { HOLY.Inst.Buffs = HolyBuffs.Read(layout, sample.At); HOLY.Inst.Thresholds = layout.HolyThresholds; }
        // Recheck immediately before sending keys, after assembling this sample.
        if (foreground() != game.Handle) { CJQ.Inst.Cancel(); if(profile.SpecId==65) {HOLY.Inst.Sacrifice.Reset("游戏不在前台：联动已清除");HOLY.Inst.Raid.Reset();} return "执行等待游戏处于前台"; }
        try
        {
            if (execute is not null) execute(frames, bars);
            else global::Program.ExecuteClassSample(profile, frames, bars);
        }
        catch (InvalidOperationException)
        {
            Stop("按键发送失败");
            throw;
        }
        if (profile.SpecId == RotationForever.ForeverPaladin.ProfileId)
            return RotationForever.ForeverPaladin.Inst.LastStatus;
        if (profile.SpecId == 70) return $"{CJQ.Inst.LastDecision.Branch} · {CJQ.Inst.LastDecision.Reason}";
        if (profile.SpecId == 65) {
            string hint=HolyBuffs.InstantSourceHint(layout,frames,HOLY.Inst.Buffs);
            return HOLY.Inst.LastDecision.Reason+(hint.Length>0?" · "+hint:"");
        }
        return profile.SpecId == 71 ? $"{WQZ.Inst.LastDecision.Branch} · {WQZ.Inst.LastDecision.Reason}" : profile.SpecId == 72 ? $"{KBZ.Inst.LastDecision.Branch} · {KBZ.Inst.LastDecision.Reason} · {(KBZ.Inst.LastDecision.Mode.Length > 0 ? KBZ.Inst.LastDecision.Mode : layout.ThunderMode)}" : "执行已开启 · " + profile.DisplayName;
    }
}
