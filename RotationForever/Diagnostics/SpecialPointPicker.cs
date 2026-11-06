#nullable enable
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace RotationUS.Diagnostics;

internal sealed record PickedPixel(SamplePoint Point, SampleColor Color);

// The frozen client screenshot is displayed 1:1; clicks never reach the game.
internal sealed class SpecialPointPicker : Form
{
    private readonly Bitmap image;
    private readonly string title;
    private readonly string rule;
    private Point pixel;
    internal PickedPixel? Selection { get; private set; }

    internal SpecialPointPicker(Bitmap image, Rectangle screenBounds, string title = "无视苦痛", string rule = "纯黑或等于取色 RGB 时，判定需要施放")
    {
        this.image = image;
        this.title = title; this.rule = rule;
        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = screenBounds;
        TopMost = true; KeyPreview = true; DoubleBuffered = true;
        Cursor = Cursors.Cross;
        Text = title + "：截图选点取色";
        Font = new Font("Microsoft YaHei UI", 10);
        pixel = new Point(image.Width / 2, image.Height / 2);
    }

    internal static PickedPixel ReadPixel(Bitmap image, Point point)
    {
        if (!new Rectangle(Point.Empty, image.Size).Contains(point))
            throw new ArgumentOutOfRangeException(nameof(point), "点位超出游戏截图。");
        return new(new(point.X, point.Y), SampleColor.From(image.GetPixel(point.X, point.Y)));
    }

    private void Pick(Point point)
    {
        Selection = ReadPixel(image, point);
        DialogResult = DialogResult.OK;
        Close();
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        pixel = new(Math.Clamp(e.X, 0, image.Width - 1), Math.Clamp(e.Y, 0, image.Height - 1));
        Invalidate();
    }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left && new Rectangle(Point.Empty, image.Size).Contains(e.Location)) Pick(e.Location);
        else if (e.Button == MouseButtons.Right) { DialogResult = DialogResult.Cancel; Close(); }
    }
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); return true; }
        if (keyData == Keys.Enter) { Pick(pixel); return true; }
        var delta = keyData switch { Keys.Left => new Point(-1, 0), Keys.Right => new Point(1, 0),
            Keys.Up => new Point(0, -1), Keys.Down => new Point(0, 1), _ => Point.Empty };
        if (delta != Point.Empty)
        {
            pixel = new(Math.Clamp(pixel.X + delta.X, 0, image.Width - 1), Math.Clamp(pixel.Y + delta.Y, 0, image.Height - 1));
            Invalidate(); return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.DrawImageUnscaled(image, Point.Empty);
        var hud = new Rectangle(16, pixel.Y < 230 && pixel.X < 510 ? Math.Max(16, Height - 210) : 16, 480, 180);
        using var backdrop = new SolidBrush(Color.FromArgb(240, 20, 24, 32));
        e.Graphics.FillRectangle(backdrop, hud);
        e.Graphics.DrawString(title + "：点击选点 · Esc / 右键取消\n方向键微调，Enter 选取；返回窗口后点击保存\n" + rule, Font, Brushes.White, hud.X + 12, hud.Y + 10);
        var picked = ReadPixel(image, pixel);
        using var color = new SolidBrush(picked.Color.ToColor());
        e.Graphics.FillRectangle(color, hud.X + 12, hud.Y + 95, 32, 32);
        e.Graphics.DrawRectangle(Pens.White, hud.X + 12, hud.Y + 95, 32, 32);
        e.Graphics.DrawString($"客户区 X={pixel.X}, Y={pixel.Y}\nRGB {picked.Color}", Font, Brushes.White, hud.X + 58, hud.Y + 95);
        var src = Rectangle.Intersect(new Rectangle(pixel.X - 5, pixel.Y - 5, 11, 11), new Rectangle(Point.Empty, image.Size));
        var dest = new Rectangle(hud.Right - 110, hud.Y + 80, src.Width * 8, src.Height * 8);
        e.Graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;
        e.Graphics.DrawImage(image, dest, src, GraphicsUnit.Pixel);
        e.Graphics.DrawRectangle(Pens.Yellow, dest.X + (pixel.X - src.X) * 8, dest.Y + (pixel.Y - src.Y) * 8, 7, 7);
        e.Graphics.DrawRectangle(Pens.Yellow, pixel.X - 3, pixel.Y - 3, 6, 6);
    }
}
