#nullable enable
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text.Json;
using System.Windows.Forms;
using Microsoft.Win32;

namespace RotationUS.Diagnostics;

internal sealed class LoopStatusButton : Button
{
    private bool loopEnabled;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal bool LoopEnabled
    {
        get => loopEnabled;
        set
        {
            if (loopEnabled == value) return;
            loopEnabled = value;
            AccessibleDescription = value ? "循环已激活" : "循环已关闭";
            Invalidate();
        }
    }
    internal Color StatusColor => loopEnabled ? Color.FromArgb(46, 204, 113) : Color.FromArgb(231, 76, 60);

    internal LoopStatusButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Size = new Size(44, 44);
        Margin = new Padding(4, 0, 4, 0);
        Cursor = Cursors.Hand;
        AccessibleName = "循环状态与窗口切换";
        AccessibleDescription = "循环已关闭";
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        using var path = new GraphicsPath();
        path.AddEllipse(ClientRectangle);
        var old = Region;
        Region = new Region(path);
        old?.Dispose();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        float side = Math.Min(ClientSize.Width, ClientSize.Height);
        var ring = new RectangleF(1, 1, side - 3, side - 3);
        using var background = new SolidBrush(Color.FromArgb(36, 42, 50));
        using var border = new Pen(Focused ? Color.White : Color.FromArgb(150, 160, 172), 1.5f);
        using var light = new SolidBrush(StatusColor);
        e.Graphics.FillEllipse(background, ring);
        e.Graphics.DrawEllipse(border, ring);
        float inset = side * .28f;
        e.Graphics.FillEllipse(light, inset, inset, side - inset * 2, side - inset * 2);
    }
}

internal sealed record FloatingPosition(int X, int Y);

internal sealed class FloatingWindowSettings
{
    private readonly string path;
    internal Point? Position { get; private set; }
    internal string? LoadError { get; }

    internal FloatingWindowSettings(string baseDirectory)
    {
        path = Path.Combine(baseDirectory, "floating-window.json");
        if (!File.Exists(path)) return;
        try
        {
            var saved = JsonSerializer.Deserialize<FloatingPosition>(File.ReadAllText(path));
            if (saved is not null) Position = new Point(saved.X, saved.Y);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        { LoadError = "无法读取悬浮按钮位置：" + e.Message; }
    }

    internal string? Save(Point position)
    {
        Position = position;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(new FloatingPosition(position.X, position.Y)));
            File.Move(temporary, path, overwrite: true);
            return null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        { return "无法保存悬浮按钮位置：" + e.Message; }
    }
}

internal static class FloatingPlacement
{
    // Keep the entire button on one working area, including screens with negative coordinates.
    // If it overlaps sampled pixels, try each side of the capture on every available screen.
    internal static Point Place(Point requested, Size size, IReadOnlyList<Rectangle> areas, Rectangle? capture = null)
    {
        if (areas.Count == 0) return requested;
        var candidates = new List<Point>();
        foreach (var area in areas)
        {
            Point Clamp(Point point) => new(
                Math.Clamp(point.X, area.Left, Math.Max(area.Left, area.Right - size.Width)),
                Math.Clamp(point.Y, area.Top, Math.Max(area.Top, area.Bottom - size.Height)));
            candidates.Add(Clamp(requested));
            if (capture is { } region)
            {
                candidates.Add(Clamp(new(requested.X, region.Bottom + 8)));
                candidates.Add(Clamp(new(requested.X, region.Top - size.Height - 8)));
                candidates.Add(Clamp(new(region.Right + 8, requested.Y)));
                candidates.Add(Clamp(new(region.Left - size.Width - 8, requested.Y)));
            }
        }
        double Distance(Point p) => Math.Pow((double)p.X - requested.X, 2) + Math.Pow((double)p.Y - requested.Y, 2);
        var clear = candidates.Where(p => capture is not { } r || !r.IntersectsWith(new Rectangle(p, size)))
            .OrderBy(Distance).ToArray();
        // When no free area exists, the normal capture obstruction check still blocks sampling.
        return clear.Length > 0 ? clear[0] : candidates.OrderBy(Distance).First();
    }
}

internal sealed class FloatingStatusForm : Form
{
    internal LoopStatusButton StatusButton { get; } = new() { Dock = DockStyle.Fill, Margin = Padding.Empty, TabStop = false };
    private readonly FloatingWindowSettings settings;
    private readonly ToolTip tooltip = new();
    private bool positionInitialized;
    private Point? dragOrigin;
    private Point windowOrigin;
    private bool dragged;
    internal event Action? ExpandRequested;
    internal event Action<string>? PersistenceError;
    protected override bool ShowWithoutActivation => true;
    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            parameters.ExStyle |= 0x08000000 | 0x00000080; // WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW
            return parameters;
        }
    }

    internal FloatingStatusForm(FloatingWindowSettings settings)
    {
        this.settings = settings;
        Text = "RotationUS · 循环状态";
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(44, 44);
        StartPosition = FormStartPosition.Manual;
        Controls.Add(StatusButton);
        if (settings.Position is { } saved) { Location = saved; positionInitialized = true; }
        SetLoopEnabled(false);
        StatusButton.MouseDown += (_, e) =>
        {
            if (e.Button != MouseButtons.Left) return;
            dragOrigin = Cursor.Position; windowOrigin = Location; dragged = false;
        };
        StatusButton.MouseMove += (_, e) =>
        {
            if (dragOrigin is not { } origin || e.Button != MouseButtons.Left) return;
            var cursor = Cursor.Position;
            var tolerance = new Rectangle(origin.X - SystemInformation.DragSize.Width / 2,
                origin.Y - SystemInformation.DragSize.Height / 2,
                SystemInformation.DragSize.Width, SystemInformation.DragSize.Height);
            if (!dragged && tolerance.Contains(cursor)) return;
            dragged = true;
            Location = FloatingPlacement.Place(new(windowOrigin.X + cursor.X - origin.X,
                windowOrigin.Y + cursor.Y - origin.Y), Size, WorkingAreas);
        };
        StatusButton.MouseUp += (_, e) =>
        {
            if (e.Button != MouseButtons.Left) return;
            dragOrigin = null;
            if (dragged) SavePosition();
        };
        StatusButton.MouseCaptureChanged += (_, _) => { if (!StatusButton.Capture) dragOrigin = null; };
        StatusButton.Click += (_, _) => { if (!dragged) ExpandRequested?.Invoke(); };
        FormClosing += (_, e) =>
        {
            if (e.CloseReason != CloseReason.UserClosing) return;
            e.Cancel = true;
            ExpandRequested?.Invoke();
        };
        SystemEvents.DisplaySettingsChanged += DisplaySettingsChanged;
    }

    private static Rectangle[] WorkingAreas => Screen.AllScreens.Select(s => s.WorkingArea).ToArray();
    internal void SetLoopEnabled(bool enabled)
    {
        StatusButton.LoopEnabled = enabled;
        tooltip.SetToolTip(StatusButton, (enabled ? "循环已激活" : "循环已关闭") + " · 点击展开 · 拖动移动");
    }

    internal void ShowFloating(Screen screen)
    {
        if (!positionInitialized)
        {
            var area = screen.WorkingArea;
            Location = new Point(area.Right - Width - 20, area.Top + (area.Height - Height) / 2);
            positionInitialized = true;
        }
        Location = FloatingPlacement.Place(Location, Size, WorkingAreas);
        dragged = false;
        Show();
        SavePosition();
    }

    internal void AvoidCapture(Rectangle capture)
    {
        if (!Visible || !Bounds.IntersectsWith(capture)) return;
        var next = FloatingPlacement.Place(Location, Size, WorkingAreas, capture);
        if (next == Location) return;
        Location = next;
        // Do not let an automatic move during a drag jump back on the next mouse move.
        if (dragOrigin is not null) { dragOrigin = Cursor.Position; windowOrigin = next; }
        SavePosition();
    }

    private void SavePosition()
    {
        if (settings.Save(Location) is { } error) PersistenceError?.Invoke(error);
    }

    private void DisplaySettingsChanged(object? sender, EventArgs e)
    {
        if (!IsDisposed && IsHandleCreated)
            BeginInvoke((Action)(() =>
            {
                if (IsDisposed || !positionInitialized) return;
                Location = FloatingPlacement.Place(Location, Size, WorkingAreas);
                SavePosition();
            }));
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        using var path = new GraphicsPath(); path.AddEllipse(ClientRectangle);
        var old = Region; Region = new Region(path); old?.Dispose();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            SystemEvents.DisplaySettingsChanged -= DisplaySettingsChanged;
            tooltip.Dispose();
        }
        base.Dispose(disposing);
    }
}
