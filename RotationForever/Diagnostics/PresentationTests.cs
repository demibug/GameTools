#nullable enable
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace RotationUS.Diagnostics;

internal static class PresentationTests
{
    internal static int Run()
    {
        int count = 0;
        void Check(bool value, string name) { if (!value) throw new Exception("FAIL: " + name); count++; }
        static IEnumerable<Control> All(Control parent) => parent.Controls.Cast<Control>()
            .SelectMany(child => new[] { child }.Concat(All(child)));
        string directory = Path.Combine(Path.GetTempPath(), "RotationUS-presentation-tests-" + Guid.NewGuid().ToString("N"));
        using var form = new DiagnosticForm(false, new SavedLogStore(directory), new ProfileLayouts(directory));
        form.Show();
        form.FreezeForTesting();
        using var pattern = DiagnosticTests.Pattern(1);
        var layout = Calibration.Find(pattern, new Size(1600,900));
        form.ShowSynthetic(layout,pattern);
        var list = All(form).OfType<ListBox>().Single();
        form.ProfileLogChurn(1200,true,flush:false);
        Check(list.Items.Count == 0, "recording does not touch the native log list");
        string export = Path.Combine(directory,"pending");
        Directory.CreateDirectory(directory);
        form.ExportSnapshot(export);
        string text = File.ReadAllText(export+".log.txt");
        Check(text.Contains("测试 200") && text.Contains("测试 1199") && !text.Contains("测试 199\r"),
            "export includes pending log records and keeps original 1000-line retention");
        form.FlushLogDisplay();
        Check(list.Items.Count==1000 && list.Items[0]!.ToString()!.EndsWith("测试 200") &&
            list.Items[999]!.ToString()!.EndsWith("测试 1199"), "deferred display preserves newest entries in order");
        form.ProfileLogChurn(20,flush:false);
        form.ProfileLogChurn(0,true);
        form.FlushLogDisplay();
        Check(list.Items.Count==0,"clear removes pending entries so they cannot reappear");

        var observe = typeof(DiagnosticForm).GetMethod("UpdateLiveRows", BindingFlags.Instance|BindingFlags.NonPublic)!;
        var grid = All(form).OfType<DataGridView>().Single(g=>g.Name=="pointGrid");
        using var sample = new Captured((Bitmap)pattern.Clone(), new Rectangle(Point.Empty,pattern.Size));
        var point = layout.Frames[6];
        sample.Image.SetPixel(point.X,point.Y,Color.FromArgb(80,80,80));
        observe.Invoke(form,new object[]{sample,true,"",true});
        object? shown = grid.Rows[6].Cells[4].Value;
        sample.Image.SetPixel(point.X,point.Y,Color.FromArgb(100,100,100));
        observe.Invoke(form,new object[]{sample,true,"",false});
        sample.Image.SetPixel(point.X,point.Y,Color.FromArgb(120,120,120));
        observe.Invoke(form,new object[]{sample,true,"",false});
        Check(Equals(shown,grid.Rows[6].Cells[4].Value),"observation without rendering does not update the grid");
        form.FlushLogDisplay();
        Check(list.Items.Cast<string>().Any(s=>s.Contains("100, 100, 100")) &&
            list.Items.Cast<string>().Any(s=>s.Contains("120, 120, 120")), "intermediate field transitions survive skipped visual frames");
        observe.Invoke(form,new object[]{sample,true,"",true});
        Check(Equals(grid.Rows[6].Cells[4].Value,"120, 120, 120"),"next render catches up even when observation is unchanged");

        using var tall = new Bitmap(1000,860);
        form.ProfilePresentation(layout,tall);
        form.PerformLayout();
        var preview = All(form).OfType<PixelPreview>().Single();
        Check(preview.Height < 500 && preview.AutoScrollMinSize.Height >= 6880,
            "large images have a viewport-sized control and retain full scroll extent");
        using var output = new Bitmap(form.Width,form.Height);
        form.DrawToBitmap(output,new Rectangle(Point.Empty,output.Size));
        preview.AutoScrollPosition = new Point(0,1000);
        Check(preview.AutoScrollPosition.Y < 0,"preview still scrolls to distant sampling points");
        form.DrawToBitmap(output,new Rectangle(Point.Empty,output.Size));
        return count;
    }
}
