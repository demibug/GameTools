#nullable enable
using System.Drawing;

namespace RotationUS.Diagnostics;

internal enum TeamHealthStatus { Empty, Offline, Unknown, Available }
internal readonly record struct TeamHealthReading(TeamHealthStatus Status, double? Percent)
{
    internal static TeamHealthReading Read(Color color) => color.G == 255 && color.B == 0
        ? new(TeamHealthStatus.Available, color.R / 255.0)
        : color.R == 0 && color.G == 0 && color.B == 0 ? new(TeamHealthStatus.Empty, null)
        : color.R == 0 && color.G == 0 && color.B == 255 ? new(TeamHealthStatus.Offline, null)
        : new(TeamHealthStatus.Unknown, null);
    internal static string Decode(Color color)
    {
        var reading = Read(color);
        return reading.Status switch {
            TeamHealthStatus.Available => $"{reading.Percent:P1}",
            TeamHealthStatus.Empty => "空位", TeamHealthStatus.Offline => "离线", _ => "未知（血量采集失败/受限）" };
    }
}
