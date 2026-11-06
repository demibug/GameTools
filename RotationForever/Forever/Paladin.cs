#nullable enable
using System.Diagnostics;
using System.Drawing;
using RotationUS.Diagnostics;

namespace RotationForever;

internal enum PaladinAction { None, Seal, Judgement, HolyStrike }
internal readonly record struct PaladinDecision(PaladinAction Action, string Reason);

internal sealed class ForeverPaladin
{
    internal const int ProfileId = 16002;
    internal static readonly ForeverPaladin Inst = new();
    private readonly double[] retryAt = new double[4];
    private double nextKey;
    internal PaladinDecision LastDecision { get; private set; }
    internal string LastStatus { get; private set; } = "等待永恒圣骑士采样";

    internal static Field[] Fields()
    {
        var fields = Enumerable.Range(1, DisplayProtocol.CellCount).Select(DisplayProtocol.CommonField)
            .Append(new Field("101", "诊断观察点（仅记录RGB）", ValueKind.Raw)).ToArray();
        string[] names =
        [
            "玩家处于战斗", "玩家可操作（存活、未骑乘、未施法、未打字）",
            "目标存活且可攻击", "审判距离内（统一10码）", "正义圣印有效（本地30秒）",
            "正义圣印就绪 · 7", "审判就绪 · 8", "神圣打击就绪 · F11"
        ];
        for (int i = 0; i < names.Length; i++)
            fields[i] = new((i + 1).ToString(), names[i], ValueKind.FuryBoolean);
        fields[8] = new("9", "正义圣印剩余时间（成功施法计时）", ValueKind.Percent);
        fields[9] = new("10", "已学习正义圣印", ValueKind.FuryBoolean);
        fields[10] = new("11", "已学习审判", ValueKind.FuryBoolean);
        fields[11] = new("12", "已学习神圣打击", ValueKind.FuryBoolean);
        fields[13] = new("14", "玩家血量", ValueKind.Percent);
        fields[14] = new("15", "玩家法力", ValueKind.Percent);
        fields[18] = new("19", "处于队伍或团队", ValueKind.FuryBoolean);
        fields[40] = new("41", "处于团队", ValueKind.FuryBoolean);
        fields[49] = new("50", "永恒圣骑士采集完整", ValueKind.FuryBoolean);
        return fields;
    }

    internal static string Decode(Field field, Color color)
    {
        if (field.Id == "9")
            return color.R == color.G && color.G == color.B
                ? $"{color.R / 255.0 * 30:F1} 秒"
                : "未知（计时像素无效）";
        if (field.Kind == ValueKind.FuryBoolean)
            return FuryBuffs.Boolean(color) is { } value ? value ? "是" : "否" : "未知（采集失败）";
        return field.Decode(color);
    }

    internal static PaladinDecision Decide(Func<int, Color> read)
    {
        if (FuryBuffs.Boolean(read(50)) != true)
            return new(PaladinAction.None, "等待完整的永恒圣骑士采集");
        Span<bool> flags = stackalloc bool[8];
        for (int i = 0; i < flags.Length; i++)
        {
            if (FuryBuffs.Boolean(read(i + 1)) is not { } flag)
                return new(PaladinAction.None, $"点位 {i + 1} 无效，等待采集恢复");
            flags[i] = flag;
        }
        if (!flags[0]) return new(PaladinAction.None, "等待进入战斗");
        if (!flags[1]) return new(PaladinAction.None, "等待玩家可操作");
        if (!flags[4] && flags[5]) return new(PaladinAction.Seal, "正义圣印已到期或未计时 · 7");
        if (!flags[2]) return new(PaladinAction.None, "等待存活的敌对目标");
        if (!flags[3]) return new(PaladinAction.None, "等待进入审判10码范围");
        if (flags[4] && flags[6]) return new(PaladinAction.Judgement, "圣印有效，审判就绪 · 8");
        if (flags[7]) return new(PaladinAction.HolyStrike, "神圣打击就绪 · F11");
        return new(PaladinAction.None, "等待技能冷却或资源");
    }

    internal void Process(Dictionary<int, Color> frames, Dictionary<int, Color> bars, Dictionary<int, bool> states)
    {
        states.Clear();
        LastDecision = Decide(id => frames.GetValueOrDefault(id, Color.Orange));
        if (LastDecision.Action != PaladinAction.None) states[(int)LastDecision.Action] = true;
    }

    internal void Dispatch() => Dispatch(Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency, GameKeys.Press);

    internal void Dispatch(double now, Func<PaladinAction, bool> send)
    {
        LastStatus = LastDecision.Reason;
        var action = LastDecision.Action;
        if (action == PaladinAction.None) return;
        if (now < nextKey || now < retryAt[(int)action])
        {
            LastStatus = "等待施法反馈 · " + LastDecision.Reason;
            return;
        }
        if (!send(action)) throw new InvalidOperationException("按键发送失败，请检查游戏与工具的运行权限。");
        nextKey = now + .25;
        retryAt[(int)action] = now + 1;
        LastStatus = "已发送按键 · " + LastDecision.Reason;
    }

    internal void Reset()
    {
        Array.Clear(retryAt);
        nextKey = 0;
        LastDecision = default;
        LastStatus = "等待永恒圣骑士采样";
    }
}

