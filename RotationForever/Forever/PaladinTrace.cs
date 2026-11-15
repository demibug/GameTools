#nullable enable
using System.Drawing;
using RotationUS.Diagnostics;

namespace RotationForever;

// Reads the same sample as execution. Never sends input or changes rotation state.
internal sealed class PaladinTrace
{
    private static readonly int[] points = [1, 2, 3, 4, 5, 6, 7, 8, 10, 11, 12, 16, 17, 18, 26, 27, 28, 50];
    private static readonly Field[] fields = ForeverPaladin.Fields();
    private string lastSignature = "";
    private double lastWrite = double.NegativeInfinity;

    internal void Record(Func<int, Color> read, bool live, bool enabled, bool foreground,
        string execution, double now, Func<string, bool> write)
    {
        string State(int id) => FuryBuffs.Boolean(read(id)) is { } value ? value ? "是" : "否" : "未知";
        string Raw(int id) { var c = read(id); return $"{c.R},{c.G},{c.B}"; }
        string observed = live ? ForeverPaladin.Decide(read).Reason : "采样无效，不推断技能状态";
        var blockers = new List<string>();
        if (!enabled) blockers.Add("执行关闭");
        if (!foreground) blockers.Add("游戏不在前台");
        if (!live) blockers.Add("采样或心跳无效");
        else
        {
            foreach (int id in new[] { 50, 1, 2, 10, 6, 16, 26 })
                if (FuryBuffs.Boolean(read(id)) != true) blockers.Add($"{fields[id - 1].Name}[{id}]={State(id)}");
            if (FuryBuffs.Boolean(read(5)) != false) blockers.Add($"圣印有效[5]={State(5)}");
        }
        string signature = $"采样有效={live}；执行开启={enabled}；游戏前台={foreground}；执行状态={execution}；采样建议={observed}；" +
            "圣印条件=" + (blockers.Count == 0 ? "已满足，是否发键以执行状态为准" : string.Join("、", blockers)) + "；" +
            string.Join("；", points.Select(id => $"{fields[id - 1].Name}[{id}]={State(id)} RGB({Raw(id)})"));
        // Keep timer/health changes out of the signature, but include them in each
        // written snapshot. Long unchanged waits still leave a five-second trace.
        if (signature == lastSignature && now - lastWrite < 5) return;
        string details = $"；圣印剩余[9]={ForeverPaladin.Decode(fields[8], read(9))} RGB({Raw(9)})；法力[15]={fields[14].Decode(read(15))} RGB({Raw(15)})";
        if (write("[永恒战斗诊断] " + signature + details))
        {
            lastSignature = signature;
            lastWrite = now;
        }
    }
}
