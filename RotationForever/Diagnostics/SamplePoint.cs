using System.Drawing;

namespace RotationUS.Diagnostics;

internal sealed record SamplePoint(int X, int Y)
{
    public Point Point => new(X, Y);
}
