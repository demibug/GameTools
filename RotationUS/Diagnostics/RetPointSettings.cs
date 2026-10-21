#nullable enable
using System.Drawing;
using System.Windows.Forms;

namespace RotationUS.Diagnostics;

// Each row owns its pending selection; saving one buff never borrows another row's color.
internal sealed class RetPointSettings : UserControl
{
    internal sealed class PointRow
    {
        internal readonly Label Name = new() { Dock=DockStyle.Fill, TextAlign=ContentAlignment.MiddleLeft };
        internal readonly NumericUpDown X = new() { Maximum=16000, Dock=DockStyle.Fill };
        internal readonly NumericUpDown Y = new() { Maximum=16000, Dock=DockStyle.Fill };
        internal readonly Label Color = new() { Dock=DockStyle.Fill, TextAlign=ContentAlignment.MiddleLeft };
        internal readonly Label Rule = new() { Dock=DockStyle.Fill, TextAlign=ContentAlignment.MiddleLeft };
        internal readonly Button Pick = new() { Text="取点 / 取色", Dock=DockStyle.Fill };
        internal readonly Button Save = new() { Text="保存", Dock=DockStyle.Fill, Enabled=false };
        internal readonly Button Disable = new() { Text="停用", Dock=DockStyle.Fill, Enabled=false };
        internal PickedPixel? Pending;
        internal string Status = "";
    }
    internal readonly Dictionary<string,PointRow> Rows = new();
    internal event Action<string>? PickRequested, SaveRequested, DisableRequested;
    private bool updating;
    private readonly TableLayoutPanel table;
    private bool? radiant;
    private readonly ToolTip identityTip = new() { AutoPopDelay=20000 };

    internal RetPointSettings()
    {
        Dock=DockStyle.Fill;
        table=new TableLayoutPanel { Dock=DockStyle.Fill, ColumnCount=8, RowCount=9, Margin=Padding.Empty };
        foreach(float width in new[]{220f,65,65,120}) table.ColumnStyles.Add(new(SizeType.Absolute,width));
        table.ColumnStyles.Add(new(SizeType.Percent,100));
        foreach(float width in new[]{108f,65,65}) table.ColumnStyles.Add(new(SizeType.Absolute,width));
        table.RowStyles.Add(new(SizeType.Absolute,23));
        for(int i=0;i<7;i++) table.RowStyles.Add(new(SizeType.Absolute,30));
        table.RowStyles.Add(new(SizeType.Percent,100));
        string[] headers={"光环 / 状态","X","Y","参考 RGB","判断方式","","",""};
        for(int c=0;c<headers.Length;c++) table.Controls.Add(new Label {Text=headers[c],Dock=DockStyle.Fill},c,0);
        for(int i=0;i<RetBuffs.Points.Length;i++)
        {
            var definition=RetBuffs.Points[i]; string key=definition.Key;
            var row=new PointRow(); Rows.Add(key,row);
            row.Rule.Text=RetBuffs.Rule(key);
            Control[] controls={row.Name,row.X,row.Y,row.Color,row.Rule,row.Pick,row.Save,row.Disable};
            for(int c=0;c<controls.Length;c++) table.Controls.Add(controls[c],c,i+1);
            row.X.ValueChanged+=(_,_)=> { if(!updating) InvalidatePick(key); };
            row.Y.ValueChanged+=(_,_)=> { if(!updating) InvalidatePick(key); };
            row.Pick.Click+=(_,_)=>PickRequested?.Invoke(key);
            row.Save.Click+=(_,_)=>SaveRequested?.Invoke(key);
            row.Disable.Click+=(_,_)=>DisableRequested?.Invoke(key);
        }
        Controls.Add(table);
        LoadSettings(null);
    }
    internal void SetRadiant(bool value)
    {
        if(radiant==value) return;
        radiant=value;
        table.SuspendLayout();
        try {
            int index=Array.FindIndex(RetBuffs.Points,p=>p.Key=="ret_sentence")+1;
            foreach(Control control in table.Controls)
                if(table.GetRow(control)==index) control.Visible=!value;
            table.RowStyles[index].Height=value?0:30;
            Rows["ret_free"].Rule.Text=value?"触发看B7；指定色=≤2GCD":RetBuffs.Rule("ret_free");
            RefreshNames();
        }
        finally {table.ResumeLayout();}
    }
    internal void LoadSettings(DiagnosticLayout? layout,string? committedKey=null)
    {
        updating=true;
        try
        {
            foreach(var definition in RetBuffs.Points)
            {
                var row=Rows[definition.Key];
                if(committedKey is not null && committedKey!=definition.Key && row.Pending is not null) continue;
                var setting=layout?.SpecialPoints.GetValueOrDefault(definition.Key);
                row.Pending=null;row.Save.Enabled=false;
                row.Status=setting?.Enabled==true?"已启用":setting is null?"未设置":"已停用";
                row.X.Value=Math.Min(row.X.Maximum,setting?.Point.X??530);
                row.Y.Value=Math.Min(row.Y.Maximum,setting?.Point.Y??8);
                row.Color.Text=setting?.ReferenceColor?.ToString()??"未取色";
                row.Pick.Enabled=layout is not null;row.Disable.Enabled=setting?.Enabled==true;
            }
            RefreshNames();
        }
        finally { updating=false; }
    }
    internal void InvalidatePick(string key)
    {
        var row=Rows[key];row.Pending=null;row.Save.Enabled=false;
    }
    internal void InvalidatePicks() { foreach(var key in Rows.Keys) InvalidatePick(key); }
    internal void Stage(string key,PickedPixel picked)
    {
        var row=Rows[key];updating=true;
        try { row.X.Value=picked.Point.X;row.Y.Value=picked.Point.Y; }
        finally { updating=false; }
        row.Pending=picked;row.Color.Text=picked.Color.ToString();row.Save.Enabled=true;
        row.Status="待保存";RefreshNames();
    }
    private void RefreshNames()
    {
        foreach(var (key,row) in Rows) {
            row.Name.Text=RetBuffs.Label(key,radiant!=false)+" · "+row.Status;
            string detail=RetBuffs.Identity(key,radiant!=false);
            identityTip.SetToolTip(row.Name,detail);identityTip.SetToolTip(row.Pick,detail);
        }
    }
    protected override void Dispose(bool disposing)
    {
        if(disposing) identityTip.Dispose();
        base.Dispose(disposing);
    }
    internal DiagnosticLayout BuildLayout(DiagnosticLayout layout,string key)
    {
        if(Rows[key].Pending is not { } picked) throw new InvalidOperationException("请先为该 Buff 取点 / 取色。");
        var updated=layout.Copy();
        updated.SpecialPoints[key]=new() { Point=picked.Point,Enabled=true,
            Comparison=RetBuffs.Comparison(key),ReferenceColor=picked.Color };
        updated.Validate();return updated;
    }
}
