#nullable enable
namespace RotationUS.Diagnostics;

internal sealed class PresentationSchedule
{
    internal const double IntervalSeconds = .25;
    private double nextUpdate = double.NegativeInfinity;
    internal void Reset() => nextUpdate = double.NegativeInfinity;
    internal bool Due(double now, bool visible = true)
    {
        if (!visible || now < nextUpdate) return false;
        nextUpdate = now + IntervalSeconds;
        return true;
    }
}
