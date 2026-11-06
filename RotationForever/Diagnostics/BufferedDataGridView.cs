#nullable enable
using System.Windows.Forms;

namespace RotationUS.Diagnostics;

// A changing row must not erase/repaint each cell directly on the desktop.
internal sealed class BufferedDataGridView : DataGridView
{
    internal BufferedDataGridView() { DoubleBuffered = true; }
}
