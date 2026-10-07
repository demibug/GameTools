#nullable enable
using System.Drawing;

namespace RotationUS.Diagnostics;

internal sealed class RotationExecution
{
    private double manualPauseUntil;
    private readonly Func<IntPtr> foreground;
    private readonly Func<bool>? manualInput;
    private readonly Action<Dictionary<int, Color>, Dictionary<int, Color>>? execute;
    private int runningSpec;
    private readonly Dictionary<int, Color> frames = new(50);
    private readonly Dictionary<int, Color> bars = new(50);

    internal RotationExecution(Func<IntPtr>? foreground = null, Func<bool>? manualInput = null,
        Action<Dictionary<int, Color>, Dictionary<int, Color>>? execute = null)
    {
        this.foreground = foreground ?? global::Program.GetForegroundWindow;
        this.manualInput = manualInput;
        this.execute = execute;
    }
    internal bool Enabled { get; private set; }
    internal event Action? EnabledChanged;
    internal void Start(ClassProfile? profile = null)
    {
        manualPauseUntil = 0; runningSpec = (profile ?? ClassProfiles.Protection).SpecId;
        if (Enabled) return;
        Enabled = true; EnabledChanged?.Invoke();
    }
    internal void Stop()
    {
        manualPauseUntil = 0; runningSpec = 0;
        if (!Enabled) return;
        Enabled = false; EnabledChanged?.Invoke();
    }
    internal string Tick(GameWindow game, DiagnosticLayout layout, Captured sample, bool live, double now, ClassProfile? profile = null)
    {
        if (!Enabled) return "执行已关闭";
        profile ??= ClassProfiles.Protection;
        if (profile.SpecId != runningSpec || (layout.SpecId != 0 && layout.SpecId != profile.SpecId))
        { Stop(); return "职业 / 专精变化，执行已关闭"; }
        if (!live) return "执行等待有效职业数据 / 心跳";
        if (foreground() != game.Handle) return "执行等待游戏处于前台";
        if (manualInput?.Invoke() ?? global::Program.ClassManualInputPressed(profile)) manualPauseUntil = now + .5;
        if (now < manualPauseUntil) return "执行让出手动按键（500 ms）";
        for (int i = 0; i < layout.Frames.Length; i++) frames[i + 1] = sample.At(layout.Frames[i]);
        for (int i = 0; i < layout.Bars.Length; i++) bars[i + 1] = sample.At(layout.Bars[i]);
        if (layout.SpecialEnabled) frames[51] = sample.At(layout.Special);
        else frames.Remove(51);
        if (profile.SpecId == 73) FZ.Inst.IgnorePainReferenceColor = layout.SpecialEnabled ? layout.SpecialReferenceColor?.ToColor() : null;
        if (profile.SpecId == 581 && !layout.SpecialEnabled) return "执行等待复仇专精特殊点配置";
        if (profile.SpecId == 71) WQZ.Inst.Buffs = ArmsBuffs.Read(layout, sample.At);
        if (profile.SpecId == 72) KBZ.Inst.Buffs = FuryBuffs.Read(layout, sample.At);
        // Recheck immediately before sending keys, after assembling this sample.
        if (foreground() != game.Handle) return "执行等待游戏处于前台";
        if (execute is not null) execute(frames, bars);
        else global::Program.ExecuteClassSample(profile, frames, bars);
        return profile.SpecId == 71 ? $"{WQZ.Inst.LastDecision.Branch} · {WQZ.Inst.LastDecision.Reason}" : profile.SpecId == 72 ? $"{KBZ.Inst.LastDecision.Branch} · {KBZ.Inst.LastDecision.Reason} · {(KBZ.Inst.LastDecision.Mode.Length > 0 ? KBZ.Inst.LastDecision.Mode : layout.ThunderMode)}" : "执行已开启 · " + profile.DisplayName;
    }
}
