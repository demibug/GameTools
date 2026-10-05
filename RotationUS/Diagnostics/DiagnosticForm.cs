#nullable enable
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Text;
using System.Windows.Forms;

namespace RotationUS.Diagnostics;

internal sealed class DiagnosticForm : Form
{
    private readonly ComboBox games = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 330 };
    private readonly Label status = new() { AutoSize = true, ForeColor = Color.DarkSlateBlue, Padding = new Padding(5) };
    private readonly DataGridView grid = new() { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, RowHeadersVisible = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false, BackgroundColor = Color.White, BorderStyle = BorderStyle.None };
    // A positive extent prevents a whole-list width scan on each eviction.
    private readonly ListBox changes = new() { Dock = DockStyle.Fill, HorizontalScrollbar = true, HorizontalExtent = 1 };
    private readonly PixelPreview preview = new();
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 100 };
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private readonly Heartbeat heartbeat = new();
    private readonly CaptureBuffer captureBuffer = new();
    private readonly PresentationSchedule presentation = new();
    private DiagnosticLayout? samplingLayout;
    private Rectangle samplingBounds;
    private string lastUnknownReason = "";
    private bool batchingLogs;
    private readonly CheckBox specialEnabled = new() { Text = "启用特殊点 51", AutoSize = true };
    private readonly FuryPointSettings furyPoints = new();
    private readonly FlowLayoutPanel legacySettings = new() { Dock=DockStyle.Fill,WrapContents=true };
    private readonly TableLayoutPanel furySettings = new() { Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,Visible=false };
    private readonly FlowLayoutPanel furyOptions = new() { Dock=DockStyle.Fill,WrapContents=false };
    private readonly RowStyle settingsHeight = new(SizeType.Absolute,100);
    private readonly RowStyle previewHeight = new(SizeType.Absolute,125);
    private readonly RowStyle logsHeight = new(SizeType.Absolute,155);
    private readonly ComboBox thunderMode = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120, Visible = false };
    private readonly NumericUpDown specialX = new() { Minimum = 0, Maximum = 16000, Value = 610, Width = 70 };
    private readonly NumericUpDown specialY = new() { Minimum = 0, Maximum = 16000, Value = 8, Width = 60 };
    private readonly Label specialColor = new() { AutoSize = true, Padding = new Padding(0, 5, 0, 0) };
    private readonly Label classStatus = new() { Text = "当前职业：等待插件识别", AutoSize = true, ForeColor = Color.DarkGreen, Padding = new Padding(0, 5, 0, 0) };
    private readonly Button pickSpecial = new() { Text = "截图取点 / 取色", AutoSize = true, Enabled = false };
    private readonly ProfileLayouts profileLayouts;
    private ClassIdentity identity;
    private ClassProfile? activeProfile;
    private Field[] fields = ClassProfiles.RawFields;
    private readonly Button saveSpecial = new() { Text = "保存特殊点", AutoSize = true, Enabled = false };
    private PickedPixel? pendingSpecialPick;
    private readonly Button pause = new() { Text = "暂停查看", AutoSize = true };
    private readonly Button locate = new() { Text = "定位测试", AutoSize = true };
    private readonly Button run = new() { Text = "开启执行（中键）", AutoSize = true };
    private readonly Label executionStatus = new() { Text = "执行已关闭", AutoSize = true, ForeColor = Color.DarkSlateBlue, Padding = new Padding(0, 5, 0, 0) };
    private readonly RotationExecution execution;
    private readonly LoopStatusButton loopStatus = new();
    private readonly ToolTip loopTooltip = new();
    private readonly FloatingStatusForm floatingWindow;
    private bool inFloatingMode;
    private bool minimizeQueued;
    private FormWindowState expandedWindowState = FormWindowState.Normal;
    private MouseToggle? mouseToggle;
    private bool modalOperation;
    private string exportDirectory;
    private readonly SavedLogStore savedLogs;
    private readonly StableLocator locator = new();
    private string layoutPath => activeProfile is null ? profileLayouts.DiscoveryPath : profileLayouts.PathFor(activeProfile);
    private readonly Dictionary<string, string> lastValues = new();
    private readonly Dictionary<string, string> changedTimes = new();
    private readonly Queue<string> log = new();
    private DiagnosticLayout? layout;
    private Captured? captured;
    private DiagnosticLayout? capturedLayout;
    private DateTime capturedAt;
    private bool frozen;
    private bool updatingControls;
    private bool locating;
    private double lastLocateScan = -10;
    private bool locationVerified;
    private string previousStatus = "";

    public DiagnosticForm(bool listenForMouse = true, SavedLogStore? savedLogs = null, ProfileLayouts? profileLayouts = null,
        RotationExecution? execution = null)
    {
        this.savedLogs = savedLogs ?? new SavedLogStore(AppContext.BaseDirectory);
        this.profileLayouts = profileLayouts ?? new ProfileLayouts(AppContext.BaseDirectory);
        this.execution = execution ?? new RotationExecution();
        var floatingSettings = new FloatingWindowSettings(Path.GetDirectoryName(this.profileLayouts.DiscoveryPath)!);
        floatingWindow = new FloatingStatusForm(floatingSettings);
        floatingWindow.ExpandRequested += ExpandFromFloating;
        floatingWindow.PersistenceError += message => SetStatus(message);
        this.execution.EnabledChanged += SyncLoopStatus;
        SyncLoopStatus();
        exportDirectory = this.savedLogs.LastDirectory;
        Text = "RotationUS · 职业执行与点位诊断";
        Font = new Font("Microsoft YaHei UI", 9);
        changes.FontChanged += (_, _) => UpdateLogWidth();
        ClientSize = new Size(1180, 800); MinimumSize = new Size(950, 650);
        StartPosition = FormStartPosition.Manual;
        var area = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);
        Location = new Point(Math.Max(area.Left, area.Left + (area.Width - Width) / 2), area.Top + 170);
        TopMost = true;
        var content = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6, Padding = new Padding(10) };
        content.RowStyles.Add(new(SizeType.Absolute, 128));
        content.RowStyles.Add(new(SizeType.Absolute, 52));
        content.RowStyles.Add(settingsHeight);
        content.RowStyles.Add(previewHeight);
        content.RowStyles.Add(new(SizeType.Percent, 100));
        content.RowStyles.Add(logsHeight);
        Controls.Add(content);
        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true };
        loopStatus.Click += (_, _) => CollapseToFloating(); toolbar.Controls.Add(loopStatus);
        toolbar.Controls.Add(games);
        toolbar.Controls.Add(Button("刷新游戏", RefreshGames));
        locate.Click += (_, _) => ToggleLocator(); toolbar.Controls.Add(locate);
        pause.Click += (_, _) => TogglePause(); toolbar.Controls.Add(pause);
        run.Click += (_, _) => ToggleExecution(); toolbar.Controls.Add(run);
        toolbar.Controls.Add(Button("保存截图与日志", Export));
        toolbar.Controls.Add(Button("清空窗口日志", ClearCurrentLog));
        toolbar.Controls.Add(Button("清空已保存日志", ClearSavedLogs));
        var top = new CheckBox { Text = "置顶", Checked = true, AutoSize = true, Padding = new Padding(0, 5, 0, 0) };
        top.CheckedChanged += (_, _) => TopMost = top.Checked; toolbar.Controls.Add(top);
        toolbar.Controls.Add(classStatus);
        content.Controls.Add(toolbar, 0, 0);
        var instructions = new Label { Dock = DockStyle.Fill, Text = "定位：游戏 /hiji locate on → 外部点击定位测试 → 查看逐点校验；分辨率或 UI 缩放变化后自动重新定位。\n通过后：游戏 /hiji locate off → 外部退出定位测试，查看当前专精数据。窗口避开顶部色条；首次更新插件需 /reload。", AutoEllipsis = true };
        content.Controls.Add(instructions, 0, 1);
        var settings = legacySettings;
        thunderMode.Items.AddRange(new[] { "仅有无", "精确层数接口" }); thunderMode.SelectedIndex = 0;
        thunderMode.SelectedIndexChanged += (_, _) =>
        {
            if (updatingControls || layout is null || activeProfile?.SpecId != 72) return;
            StopExecution(); var updated = layout.Copy(); updated.ThunderMode = thunderMode.SelectedIndex == 1 ? "exact-stacks" : "presence-only";
            this.profileLayouts.Save(activeProfile!, updated); layout = updated; samplingLayout = null; lastValues.Clear();
            AddLog("雷霆轰击模式：" + updated.ThunderMode + "；特殊点只判断有无，精确模式需验证层数接口。");
        };
        settings.Controls.Add(specialEnabled); settings.Controls.Add(new Label { Text = "X", AutoSize = true, Padding = new Padding(0, 5, 0, 0) }); settings.Controls.Add(specialX);
        settings.Controls.Add(new Label { Text = "Y", AutoSize = true, Padding = new Padding(0, 5, 0, 0) }); settings.Controls.Add(specialY);
        pickSpecial.Click += (_, _) => PickSpecial(); settings.Controls.Add(pickSpecial);
        saveSpecial.Click += (_, _) => SaveSpecial(); settings.Controls.Add(saveSpecial);
        specialX.ValueChanged += (_, _) => InvalidateSpecialPick();
        specialY.ValueChanged += (_, _) => InvalidateSpecialPick();
        settings.Controls.Add(executionStatus);
        settings.SetFlowBreak(executionStatus, true);
        specialColor.Text = "无视苦痛：纯黑或等于取色 RGB 时需要施放；尚未取色。";
        settings.Controls.Add(specialColor);
        furyPoints.PickRequested+=key=>PickSpecial(key);
        furyPoints.SaveRequested+=key=>SaveSpecial(key);
        furyPoints.DisableRequested+=key=>
        {
            if(layout is null || activeProfile?.SpecId!=72) return;
            StopExecution();var updated=layout.Copy();
            if(updated.SpecialPoints.TryGetValue(key,out var point)) point.Enabled=false;
            SaveSpecialLayout(updated,key);
        };
        furySettings.RowStyles.Add(new(SizeType.Absolute,180));
        furySettings.RowStyles.Add(new(SizeType.Percent,100));
        furySettings.Controls.Add(furyPoints,0,0);
        furyOptions.Controls.Add(new Label {Text="雷霆模式",AutoSize=true,Padding=new Padding(0,5,0,0)});
        furyOptions.Controls.Add(thunderMode);
        furySettings.Controls.Add(furyOptions,0,1);
        var settingsHost=new Panel {Dock=DockStyle.Fill};
        settingsHost.Controls.Add(settings);settingsHost.Controls.Add(furySettings);
        content.Controls.Add(settingsHost, 0, 2);
        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.FromArgb(245, 247, 250) };
        scroll.Controls.Add(preview); content.Controls.Add(scroll, 0, 3);
        string[] headings = { "点位", "职业字段", "客户区坐标", "颜色", "原始 RGB", "解码 / 旧规则判断", "最近变化" };
        foreach (var title in headings) grid.Columns.Add(title, title);
        grid.Columns[0].FillWeight = 35; grid.Columns[1].FillWeight = 150; grid.Columns[2].FillWeight = 80;
        grid.Columns[3].FillWeight = 35; grid.Columns[4].FillWeight = 90; grid.Columns[5].FillWeight = 220; grid.Columns[6].FillWeight = 85;
        grid.RowTemplate.Height = 25;
        RestoreLiveRows();
        grid.SelectionChanged += (_, _) => { preview.Selected = grid.CurrentRow?.Index ?? -1; preview.Invalidate(); };
        content.Controls.Add(grid, 0, 4);
        var bottom = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        bottom.RowStyles.Add(new(SizeType.Absolute, 38)); bottom.RowStyles.Add(new(SizeType.Percent, 100));
        bottom.Controls.Add(status, 0, 0); bottom.Controls.Add(changes, 0, 1); content.Controls.Add(bottom, 0, 5);
        games.SelectedIndexChanged += (_, _) => { InvalidateSpecialPick(); StopExecution(); identity = default; activeProfile = null; fields = ClassProfiles.RawFields; classStatus.Text = "当前职业：等待插件识别"; if (!locating) RestoreLiveRows(); ApplyControls(); heartbeat.Reset(); locator.Reset(); locationVerified = false; lastValues.Clear(); SetUnknown("游戏窗口已切换，等待有效采样"); };
        timer.Tick += (_, _) => TickCapture();
        Shown += (_, _) =>
        {
            LoadLayout(); RefreshGames(); timer.Start();
            if (this.savedLogs.LoadError is not null) AddLog(this.savedLogs.LoadError);
            if (floatingSettings.LoadError is not null) AddLog(floatingSettings.LoadError);
            if (listenForMouse)
            {
                try
                {
                    mouseToggle = new MouseToggle(() =>
                    {
                        if (!IsDisposed && IsHandleCreated && !modalOperation)
                            BeginInvoke((Action)(() => { if (!IsDisposed && !modalOperation) ToggleExecution(); }));
                    });
                }
                catch (Exception e) { SetStatus(e.Message); }
            }
        };
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            execution.EnabledChanged -= SyncLoopStatus;
            floatingWindow.Dispose(); loopTooltip.Dispose();
            execution.Stop(); mouseToggle?.Dispose(); mouseToggle = null;
            timer.Stop(); timer.Dispose(); captureBuffer.Dispose();
            preview.Source = null; captured?.Dispose(); captured = null;
        }
        base.Dispose(disposing);
    }
    private void SyncLoopStatus()
    {
        loopStatus.LoopEnabled = execution.Enabled;
        floatingWindow.SetLoopEnabled(execution.Enabled);
        loopTooltip.SetToolTip(loopStatus, (execution.Enabled ? "循环已激活" : "循环已关闭") + " · 点击收起为悬浮按钮");
        run.Text = execution.Enabled ? "关闭执行（中键）" : "开启执行（中键）";
        if (!execution.Enabled) executionStatus.Text = "执行已关闭";
        timer.Interval = execution.Enabled ? 30 : 100;
        presentation.Reset();
    }

    internal void CollapseToFloating()
    {
        if (inFloatingMode || modalOperation || !IsHandleCreated) return;
        var screen = Screen.FromControl(this);
        if (WindowState != FormWindowState.Minimized) expandedWindowState = WindowState;
        inFloatingMode = true;
        Hide();
        if (WindowState == FormWindowState.Minimized) WindowState = expandedWindowState;
        floatingWindow.ShowFloating(screen);
    }

    internal void ExpandFromFloating()
    {
        if (!inFloatingMode || IsDisposed) return;
        inFloatingMode = false;
        floatingWindow.Hide();
        presentation.Reset(); lastUnknownReason = "";
        if (WindowState == FormWindowState.Minimized) WindowState = expandedWindowState;
        var restoreState = expandedWindowState;
        Show();
        // A hidden window can retain native minimized placement even when its managed state is normal.
        WindowState = restoreState;
        Activate();
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x0112 && (m.WParam.ToInt64() & 0xFFF0) == 0xF020 && floatingWindow is not null)
        { CollapseToFloating(); return; } // WM_SYSCOMMAND / SC_MINIMIZE
        base.WndProc(ref m);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (floatingWindow is null || inFloatingMode) return;
        if (WindowState == FormWindowState.Minimized && !minimizeQueued && IsHandleCreated)
        {
            // Restore only after the native minimize has finished updating its bounds/state.
            minimizeQueued = true;
            BeginInvoke((Action)(() =>
            {
                minimizeQueued = false;
                if (!IsDisposed && WindowState == FormWindowState.Minimized) CollapseToFloating();
            }));
        }
        else if (WindowState != FormWindowState.Minimized) expandedWindowState = WindowState;
    }
    private static Button Button(string label, Action action)
    {
        var button = new Button { Text = label, AutoSize = true };
        button.Click += (_, _) => action(); return button;
    }
    private GameWindow SelectedGame => games.SelectedItem as GameWindow ?? throw new InvalidOperationException("未找到游戏窗口，请启动 Wow.exe 后刷新游戏列表。");
    private Rectangle OwnBounds => floatingWindow.Visible ? floatingWindow.Bounds
        : Visible && WindowState != FormWindowState.Minimized ? Bounds : Rectangle.Empty;
    private void AvoidFloatingCapture(GameWindow game, Rectangle region, Size size)
    {
        if (floatingWindow.Visible)
            floatingWindow.AvoidCapture(GameCapture.ScreenRegion(game, region, Rectangle.Empty, size));
    }
    private void RefreshGames()
    {
        int? old = (games.SelectedItem as GameWindow)?.Pid;
        games.Items.Clear(); games.Items.AddRange(GameCapture.Windows());
        if (games.Items.Count > 0)
        {
            int found = Enumerable.Range(0, games.Items.Count).FirstOrDefault(i => (games.Items[i] as GameWindow)?.Pid == old, -1);
            if (found >= 0 || games.Items.Count == 1) games.SelectedIndex = found >= 0 ? found : 0;
            else SetUnknown("发现多个游戏窗口，请选择目标窗口。");
        }
        else SetUnknown("未找到 Wow.exe；可以先启动游戏，再刷新。");
    }
    private void LoadLayout()
    {
        if (!File.Exists(profileLayouts.DiscoveryPath)) return;
        try
        {
            var saved = DiagnosticLayout.Load(profileLayouts.DiscoveryPath);
            if (saved.MarkerLayout != MarkerLayouts.Wide)
            { AddLog("已停用旧版定位坐标。所有职业使用统一右侧布局，请 /reload 后重新定位。"); return; }
            layout = saved; ApplyControls(); AddLog("已载入统一右侧定位坐标，等待识别当前职业。");
        }
        catch (Exception e) { SetUnknown(e.Message); }
    }
    private void ApplyControls(string? committedFuryKey=null)
    {
        pendingSpecialPick = null; saveSpecial.Enabled = false;
        bool special = activeProfile?.HasSpecial == true;
        specialEnabled.Enabled = specialX.Enabled = specialY.Enabled = pickSpecial.Enabled = special;
        bool fury = activeProfile?.SpecId == 72;
        furySettings.Visible=fury;legacySettings.Visible=!fury;thunderMode.Visible=fury;
        settingsHeight.Height=fury?218:100;previewHeight.Height=fury?60:125;logsHeight.Height=fury?110:155;
        Control executionParent=fury?furyOptions:legacySettings;
        if(executionStatus.Parent!=executionParent) executionParent.Controls.Add(executionStatus);
        furyPoints.LoadSettings(fury?layout:null,committedFuryKey);
        if (layout is null) return;
        updatingControls = true;
        specialEnabled.Text = "启用特殊点 51";
        specialEnabled.Checked = layout.SpecialEnabled;
        specialX.Value = Math.Min(specialX.Maximum, layout.Special.X); specialY.Value = Math.Min(specialY.Maximum, layout.Special.Y);
        thunderMode.SelectedIndex = layout.ThunderMode == "exact-stacks" ? 1 : 0;
        specialColor.Text = !special ? "当前专精不使用特殊点。" : activeProfile?.SpecId == 581
            ? $"复仇特殊点：沿用非黑判断；取色 RGB：{layout.SpecialReferenceColor?.ToString() ?? "未采集"}。"
            : $"无视苦痛：纯黑或等于取色 RGB 时需要施放；取色 RGB：{layout.SpecialReferenceColor?.ToString() ?? "未采集（只判断纯黑）"}。";
        updatingControls = false;
    }
    private bool SelectClass(ClassIdentity next, DiagnosticLayout geometry, bool force = false)
    {
        if (!force && identity == next) return false;
        StopExecution(); InvalidateSpecialPick(); heartbeat.Reset();
        var profile = ClassProfiles.Find(next.SpecId);
        if (profile?.ClassId != next.ClassId) profile = null;
        var selectedLayout = profile is null ? geometry.Copy() : profileLayouts.Load(profile, geometry);
        if (profile is null) { selectedLayout.SpecialEnabled = false; selectedLayout.SpecialReferenceColor = null; selectedLayout.SpecialPoints.Clear(); selectedLayout.SpecId = next.SpecId; }
        else profileLayouts.Save(profile, selectedLayout);
        identity = next; activeProfile = profile; layout = selectedLayout; samplingLayout = null;
        fields = profile?.Fields ?? ClassProfiles.RawFields;
        classStatus.Text = $"当前职业：{next.DisplayName}" + (profile is null ? "" : $" · 配置 {profile.Key}.json");
        ApplyControls(); lastValues.Clear(); changedTimes.Clear();
        if (!locating) RestoreLiveRows();
        AddLog($"已切换 {next.DisplayName}；{(profile is null ? "显示原始颜色" : "配置 " + layoutPath)}；执行已关闭。");
        return true;
    }
    private void PreserveSpecial(DiagnosticLayout found)
    {
        if (layout is null) return;
        found.Special = layout.Special;
        found.SpecId = layout.SpecId;
        found.SpecialReferenceColor = layout.SpecialReferenceColor;
        // External point 51 cannot be inferred when game size or UI scale changes.
        bool compatible = PositionVerification.CompatibleSpecialGeometry(found, layout);
        found.SpecialEnabled = layout.SpecialEnabled && compatible;
        found.SpecialPoints = layout.SpecialPoints.ToDictionary(p => p.Key, p => p.Value.Copy());
        found.ThunderMode = layout.ThunderMode;
        if (!compatible)
        {
            found.SpecialReferenceColor = null;
            foreach (var point in found.SpecialPoints.Values)
            { point.Enabled = false; point.ReferenceColor = null; }
        }
        if (layout.SpecialEnabled && !found.SpecialEnabled) AddLog("标准点位已变化，特殊点 51 已停用，请单独重新确认坐标。");
    }
    private void RestoreLiveRows()
    {
        presentation.Reset(); lastUnknownReason = "";
        grid.Rows.Clear(); grid.Columns[1].HeaderText = "职业字段"; grid.Columns[5].HeaderText = "解码 / 旧规则判断"; grid.Columns[6].HeaderText = "最近变化";
        foreach (var field in fields) grid.Rows.Add(field.Id, field.Name, "—", "", "—", "等待有效数据", "—");
        preview.LocationMode = false;
        preview.LiveFields = fields;
    }
    private void ToggleLocator()
    {
        if (locating)
        {
            locating = false; locate.Text = "定位测试"; RestoreLiveRows(); heartbeat.Reset(); lastValues.Clear();
            SetUnknown("已退出定位测试。游戏输入 /hiji locate off 恢复真实数据。");
        }
        else BeginLocator("定位测试：游戏输入 /hiji locate on，工具将自动寻找两行色块并保存稳定坐标。");
    }
    private void BeginLocator(string reason)
    {
        InvalidateSpecialPick();
        StopExecution();
        locating = true; locate.Text = "退出定位测试"; frozen = false; pause.Text = "暂停查看";
        locator.Reset(); locationVerified = false; lastLocateScan = -10; heartbeat.Reset();
        grid.Rows.Clear(); grid.Columns[1].HeaderText = "定位测试点"; grid.Columns[5].HeaderText = "预期颜色 / 校验结果"; grid.Columns[6].HeaderText = "校验时间";
        preview.LocationMode = true; SetUnknown(reason);
    }
    private Captured ReadScan(GameWindow game, Size size)
    {
        var region = new Rectangle(0, 0, size.Width, Math.Min(128, size.Height));
        AvoidFloatingCapture(game, region, size);
        return GameCapture.Read(game, region, OwnBounds);
    }
    private void TickLocator()
    {
        if (clock.Elapsed.TotalSeconds - lastLocateScan < .5) return;
        lastLocateScan = clock.Elapsed.TotalSeconds;
        try
        {
            var game = SelectedGame; var size = GameCapture.ClientSize(game);
            using var scan = ReadScan(game, size);
            DiagnosticLayout found;
            try { found = Calibration.Find(scan.Image, size); }
            catch (InvalidDataException e) when (layout is not null && size.Width == layout.ClientWidth && size.Height == layout.ClientHeight)
            {
                // Keep failing points visible while waiting for a complete pattern.
                ShowPositionChecks(layout, scan, PositionVerification.Check(layout, scan.At));
                locator.Reset(); locationVerified = false;
                SetStatus("定位图案不完整，已按旧坐标标出失败点：" + e.Message);
                return;
            }
            var checks = PositionVerification.Check(found, scan.At);
            if (checks.Any(c => !c.Passed)) throw new InvalidDataException("定位色块校验失败，请确认测试模式及色条可见性。");
            bool stable = locator.Observe(found);
            var nextIdentity = ClassProfiles.Identify(scan.At(found.Markers[1]));
            if (stable && (layout is null || !PositionVerification.SameGeometry(layout, found) || identity != nextIdentity))
            {
                PreserveSpecial(found); SelectClass(nextIdentity, found, force: true); profileLayouts.SaveGeometry(layout!);
                AddLog($"重新定位并保存：{size.Width}×{size.Height}，第一点 ({found.Frames[0].X}, {found.Frames[0].Y})，第二行 Y={found.Bars[0].Y}。");
            }
            ShowPositionChecks(found, scan, checks);
            string message = stable ? $"定位校验通过 103/103 · 坐标已保存 · {size.Width}×{size.Height} · 持续监测分辨率和 UI 缩放" : "色块校验通过 103/103 · 等待第二次位置一致后保存";
            if (stable && !locationVerified) AddLog(message);
            locationVerified = stable; SetStatus(message, false);
        }
        catch (Exception e)
        {
            locator.Reset(); locationVerified = false;
            SetUnknown("定位等待：" + e.Message + " 游戏输入 /hiji locate on。");
        }
    }
    private void ShowPositionChecks(DiagnosticLayout found, Captured scan, PointCheck[] checks)
    {
        if (inFloatingMode) return;
        var displayed = found.Copy(); displayed.SpecialEnabled = false;
        ReplaceCapture(PositionVerification.Crop(scan, found), displayed);
        if (grid.Rows.Count != checks.Length)
        {
            grid.Rows.Clear();
            foreach (var check in checks) grid.Rows.Add(check.Id, check.Name, "", "", "", "", "");
        }
        for (int index = 0; index < checks.Length; index++)
        {
            var check = checks[index]; var row = grid.Rows[index];
            row.Cells[2].Value = $"{check.Point.X}, {check.Point.Y}";
            row.Cells[3].Style.BackColor = check.Actual; row.Cells[4].Value = Colors.Rgb(check.Actual);
            row.Cells[5].Value = $"预期 {check.Expected} · {(check.Passed ? "通过" : "失败")}";
            row.Cells[6].Value = capturedAt.ToString("HH:mm:ss.fff");
            row.DefaultCellStyle.BackColor = check.Passed ? Color.FromArgb(232, 246, 239) : Color.MistyRose;
        }
    }
    private void SaveSpecial(string? furyKey=null)
    {
        var pending=furyKey is null?pendingSpecialPick:furyPoints.Rows[furyKey].Pending;
        if (updatingControls || modalOperation || pending is not { } selected) return;
        StopExecution();
        try
        {
            if (layout is null) throw new InvalidOperationException("先校准标准点位，再设置特殊点。");
            var game = SelectedGame;
            if (GameCapture.ClientSize(game) != new Size(layout.ClientWidth, layout.ClientHeight))
                throw new InvalidOperationException("游戏尺寸已变化，请重新定位并取点。");
            var marker = layout.Markers[1];
            using var currentIdentity = GameCapture.Read(game, new Rectangle(marker.X, marker.Y, 1, 1), OwnBounds);
            if (ClassProfiles.Identify(currentIdentity.At(marker)) != identity)
            {
                InvalidateSpecialPick();
                throw new InvalidOperationException("职业 / 专精已变化，请在当前职业重新取点。");
            }
            SaveSpecialLayout(furyKey is null?BuildSpecialLayout(selected):furyPoints.BuildLayout(layout,furyKey),furyKey);
        }
        catch (Exception e) { SetUnknown(e.Message); }
    }
    internal DiagnosticLayout BuildSpecialLayout(PickedPixel selected)
    {
        if(layout is null) throw new InvalidOperationException("先校准标准点位，再设置特殊点。");
        var updated=layout.Copy();
        updated.Special=selected.Point;updated.SpecialReferenceColor=selected.Color;updated.SpecialEnabled=specialEnabled.Checked;
        return updated;
    }
    private void InvalidateSpecialPick()
    {
        if (updatingControls) return;
        pendingSpecialPick = null; saveSpecial.Enabled = false;
        furyPoints.InvalidatePicks();
        if (layout is not null && activeProfile?.SpecId != 72)
            specialColor.Text = $"已保存取色 RGB：{layout.SpecialReferenceColor?.ToString() ?? "未采集"}；请截图取点 / 取色后保存。";
    }
    internal void StageSpecialPick(PickedPixel selected)
    {
        updatingControls = true;
        try
        {
            specialX.Value = selected.Point.X; specialY.Value = selected.Point.Y; specialEnabled.Checked = true;
        }
        finally { updatingControls = false; }
        pendingSpecialPick = selected; saveSpecial.Enabled = true;
        specialColor.Text = $"待保存：({selected.Point.X}, {selected.Point.Y})，取色 RGB：{selected.Color}；点击保存特殊点后生效。";
        SetStatus("取点取色完成，尚未写入配置，请点击保存特殊点。");
    }
    internal void SaveSpecialLayout(DiagnosticLayout updated,string? furyKey=null)
    {
        if (activeProfile is null || !activeProfile.HasSpecial) throw new InvalidOperationException("当前职业没有特殊点配置。");
        profileLayouts.Save(activeProfile, updated); layout = updated; ApplyControls(furyKey);
        samplingLayout = null; presentation.Reset();
        AddLog(activeProfile.SpecId == 72 ? $"已保存 {(furyKey is null?"狂暴特殊点":FuryBuffs.Label(furyKey))} · {layout.ThunderMode} · {layoutPath}" : $"{activeProfile.DisplayName}特殊点 51：({updated.Special.X}, {updated.Special.Y})，取色 RGB {updated.SpecialReferenceColor?.ToString() ?? "未采集"}，{(updated.SpecialEnabled ? "启用" : "停用")}；已保存 {layoutPath}");
        lastValues.Clear(); changedTimes.Clear();
        frozen = false; pause.Text = "暂停查看"; heartbeat.Reset();
    }
    private async void PickSpecial(string? furyKey=null)
    {
        if (modalOperation) return;
        if(furyKey is null) InvalidateSpecialPick(); else furyPoints.InvalidatePick(furyKey);
        StopExecution();
        modalOperation = true; timer.Stop();
        try
        {
            if (locating) throw new InvalidOperationException("先退出定位测试，并在游戏输入 /hiji locate off，再选取实际图标。");
            if (layout is null || activeProfile?.HasSpecial != true) throw new InvalidOperationException("先定位并识别支持特殊点的职业，再截图取点。");
            var game = SelectedGame; var size = GameCapture.ClientSize(game);
            if (size.Width != layout.ClientWidth || size.Height != layout.ClientHeight)
                throw new InvalidOperationException("游戏尺寸已变化，请先重新校准。");
            var bounds = GameCapture.ScreenRegion(game, new Rectangle(Point.Empty, size), Rectangle.Empty);
            Hide(); GameCapture.Activate(game);
            await System.Threading.Tasks.Task.Delay(250);
            if (IsDisposed) return;
            if (global::Program.GetForegroundWindow() != game.Handle)
                throw new InvalidOperationException("游戏未处于前台，无法取点，请重试。");
            using var screenshot = GameCapture.Read(game, new Rectangle(Point.Empty, size), Rectangle.Empty);
            if (ClassProfiles.Identify(screenshot.At(layout.Markers[1])) != identity)
                throw new InvalidOperationException("职业 / 专精已变化，请等待识别后重新取点。");
            using var picker = new SpecialPointPicker(screenshot.Image, bounds, activeProfile.DisplayName,
                furyKey is not null ? $"{FuryBuffs.Label(furyKey)}："+(FuryBuffs.UsesTimeColor(furyKey)?"请在临近结束时取非黑参考色；纯黑=缺失":"纯黑=缺失；任何非黑=存在；所取颜色仅记录") : activeProfile.SpecId == 73 ? "纯黑或等于取色 RGB 时，判定需要施放" : "沿用原复仇代码的非黑像素判断");
            if (picker.ShowDialog() != DialogResult.OK || picker.Selection is not { } selected) return;
            if (GameCapture.ClientSize(game) != size)
                throw new InvalidOperationException("选点过程中游戏尺寸变化，请重新选取。");
            if(furyKey is null) StageSpecialPick(selected);
            else { furyPoints.Stage(furyKey,selected);SetStatus(FuryBuffs.Label(furyKey)+"取点完成，请点击该行的保存。"); }
        }
        catch (Exception e) { if (!IsDisposed) SetStatus("取点失败：" + e.Message); }
        finally
        {
            modalOperation = false;
            if (!IsDisposed) { Show(); Activate(); timer.Start(); heartbeat.Reset(); }
        }
    }
    private void TogglePause()
    {
        StopExecution();
        frozen = !frozen; pause.Text = frozen ? "继续采样" : "暂停查看";
        if (frozen) SetStatus($"已暂停查看 · 最后采样 {capturedAt:HH:mm:ss.fff}");
        else { heartbeat.Reset(); TickCapture(); }
    }
    private void ToggleExecution()
    {
        if (modalOperation) return;
        if (execution.Enabled) { StopExecution(); AddLog("执行已关闭（中键 / 侧键 / 按钮）。"); return; }
        try
        {
            if (locating) throw new InvalidOperationException("先退出定位测试，并在游戏输入 /hiji locate off。");
            if (layout is null) throw new InvalidOperationException("先完成点位校准，再开启执行。");
            if (activeProfile is null) throw new InvalidOperationException("当前职业尚未识别，或没有对应执行代码。");
            if (activeProfile.SpecId == 581 && !layout.SpecialEnabled) throw new InvalidOperationException("先设置复仇专精的特殊判断点。");
            var size = GameCapture.ClientSize(SelectedGame);
            if (size.Width != layout.ClientWidth || size.Height != layout.ClientHeight)
                throw new InvalidOperationException("游戏尺寸已变化，请重新定位后开启执行。");
            frozen = false; pause.Text = "暂停查看"; heartbeat.Reset();
            execution.Start(activeProfile); timer.Interval = 30;
            presentation.Reset();
            run.Text = "关闭执行（中键）"; executionStatus.Text = "执行等待有效职业数据 / 心跳";
            AddLog($"执行已开启：{activeProfile.DisplayName}；使用对应配置、执行代码与原键位。");
        }
        catch (Exception e) { SetStatus("无法开启执行：" + e.Message); }
    }
    private void StopExecution()
    {
        execution.Stop(); timer.Interval = 100;
        presentation.Reset();
        run.Text = "开启执行（中键）"; executionStatus.Text = "执行已关闭";
    }
    private void TickCapture()
    {
        if (frozen) return;
        if (locating) { TickLocator(); return; }
        try
        {
            if (layout is null) { SetUnknown("等待定位：游戏输入 /hiji locate on，再点击定位测试。"); return; }
            var game = SelectedGame; var size = GameCapture.ClientSize(game);
            if (size.Width != layout.ClientWidth || size.Height != layout.ClientHeight)
            {
                BeginLocator("游戏客户区尺寸已变化，进入定位测试。游戏输入 /hiji locate on 自动重新定位。"); return;
            }
            if (!ReferenceEquals(samplingLayout, layout))
            {
                samplingBounds = layout.CaptureBounds; samplingLayout = layout;
                presentation.Reset();
            }
            AvoidFloatingCapture(game, samplingBounds, size);
            var next = captureBuffer.Read(game, samplingBounds, OwnBounds, size);
            int mode = Colors.Kind(next.At(layout.Markers[0]));
            var nextIdentity = ClassProfiles.Identify(next.At(layout.Markers[1]));
            if (SelectClass(nextIdentity, layout)) return;
            bool alive = heartbeat.Observe(next.At(layout.Markers[2]), clock.Elapsed.TotalSeconds);
            bool live = mode == 2 && activeProfile is not null && alive;
            string reason = mode == 4 ? "定位图案" : activeProfile is null ? identity.DisplayName : mode != 2 ? "插件尚未输出 / 需重载 / 坐标不匹配" : !alive ? "心跳等待或超过 1.5 秒未变化" : "";
            // Execute from the fresh sample before any table, log or preview work.
            string executionText = activeProfile is null ? "执行已关闭" : execution.Tick(game, layout, next, live, clock.Elapsed.TotalSeconds, activeProfile);
            if (!presentation.Due(clock.Elapsed.TotalSeconds, Visible && !inFloatingMode && WindowState != FormWindowState.Minimized)) return;
            // The preview/export owns a stable copy; it never triggers another screen capture.
            ReplaceCapture(new Captured((Bitmap)next.Image.Clone(), next.Bounds));
            UpdateLiveRows(next, live, reason);
            if (live && activeProfile?.SpecId == 72)
            {
                var f = Enumerable.Range(1,50).ToDictionary(i => i, i => next.At(layout.Frames[i-1]));
                var b = Enumerable.Range(1,50).ToDictionary(i => i, i => next.At(layout.Bars[i-1]));
                var decision = KBZ.Decide(f,b,FuryBuffs.Read(layout,next.At));
                string explanation = $"{decision.Branch} · {decision.Reason} · {(decision.Mode.Length > 0 ? decision.Mode : layout.ThunderMode)} · 鲁莽来源=成功施法计时";
                if (!execution.Enabled) executionStatus.Text = "诊断（执行关闭）：" + explanation;
            }
            int charges = next.At(layout.Bars[1]).R == 255 ? 2 : next.At(layout.Bars[0]).R == 255 ? 1 : 0;
            string extra = activeProfile?.SpecId == 73 ? $" · 盾牌格挡充能 {charges}" : "";
            SetStatus(live ? $"{identity.DisplayName} · 心跳正常{extra} · {size.Width}×{size.Height} · {capturedAt:HH:mm:ss.fff}" : $"{reason} · 原始画面 {capturedAt:HH:mm:ss.fff}", false);
            if (execution.Enabled || activeProfile?.SpecId != 72 || !live) executionStatus.Text = executionText;
        }
        catch (Exception e)
        {
            heartbeat.Reset();
            SetUnknown(e.Message);
        }
    }
    private void UpdateLiveRows(Captured next, bool live, string reason)
    {
        if (layout is null) return;
        lastUnknownReason = "";
        changes.BeginUpdate(); batchingLogs = true;
        try
        {
            for (int index = 0; index < fields.Length; index++)
            {
                var field = fields[index]; var row = grid.Rows[index];
                SamplePoint? point = FuryBuffs.Point(layout, field);
                if (point is null) { row.Cells[2].Value = "未设置"; row.Cells[4].Value = "—"; row.Cells[5].Value = layout.SpecId==72?"未知 · 特殊点未配置/停用":"未启用（手动校准）"; row.Cells[3].Style.BackColor = Color.White; continue; }
                var color = next.At(point);
                string rgb = Colors.Rgb(color), value = live ? FuryBuffs.Decode(layout, field, color,next.At) : $"未知 · {reason}";
                string coordinates = $"{point.X}, {point.Y}";
                if (!Equals(row.Cells[2].Value, coordinates)) row.Cells[2].Value = coordinates;
                string signature = $"{rgb}|{value}";
                bool hadValue = lastValues.TryGetValue(field.Id, out var old);
                if (hadValue && old == signature) continue;
                row.Cells[3].Style.BackColor = color; row.Cells[4].Value = rgb; row.Cells[5].Value = value;
                row.DefaultCellStyle.BackColor = live && field.Kind == ValueKind.Boolean && color.R == 255 ? Color.FromArgb(232, 246, 239) : Color.White;
                if (hadValue)
                {
                    changedTimes[field.Id] = DateTime.Now.ToString("HH:mm:ss.fff");
                    // Undefined slots still show their current RGB; log meaningful fields only.
                    if (field.Kind != ValueKind.Raw || field.Id.StartsWith("S:")) AddLog($"[{field.Id}] {field.Name}: {old} → {signature}");
                }
                lastValues[field.Id] = signature;
                row.Cells[6].Value = changedTimes.GetValueOrDefault(field.Id, "—");
            }
        }
        finally
        {
            batchingLogs = false;
            changes.TopIndex = Math.Max(0, changes.Items.Count - 1); changes.EndUpdate();
        }
    }
    internal void ProfilePresentation(DiagnosticLayout example, Bitmap image)
    {
        layout = example;
        ReplaceCapture(new Captured((Bitmap)image.Clone(), new Rectangle(Point.Empty, image.Size)));
        UpdateLiveRows(captured!, true, "");
    }
    internal void ProfileLogChurn(int lines, bool reset = false)
    {
        _ = changes.Handle;
        if (reset) { log.Clear(); changes.Items.Clear(); changes.HorizontalExtent = 1; }
        changes.BeginUpdate(); batchingLogs = true;
        try
        {
            for (int i = 0; i < lines; i++)
                AddLog($"[17] 雷霆一击冷却: 128, 128, 128|编码 50.2%；旧规则未就绪 → 129, 129, 129|编码 50.6%；旧规则未就绪 · 测试 {i}");
        }
        finally
        {
            batchingLogs = false;
            changes.TopIndex = Math.Max(0, changes.Items.Count - 1); changes.EndUpdate();
        }
    }
    private void ReplaceCapture(Captured next, DiagnosticLayout? displayed = null)
    {
        var old = captured; captured = next; capturedAt = DateTime.Now;
        capturedLayout = (displayed ?? layout)?.Copy();
        preview.Source = next; preview.SamplingLayout = capturedLayout; preview.Size = new Size(next.Image.Width * 2, Math.Max(90, next.Image.Height * 8 + 28));
        preview.Invalidate(); old?.Dispose();
    }
    private void SetUnknown(string reason)
    {
        executionStatus.Text = execution.Enabled ? "执行等待有效采样" : "执行已关闭";
        if (inFloatingMode) { SetStatus(reason); return; }
        if (lastUnknownReason == reason) return;
        lastUnknownReason = reason;
        foreach (DataGridViewRow row in grid.Rows)
        {
            row.Cells[4].Value = "—"; row.Cells[5].Value = "未知（无有效采样）";
            row.Cells[3].Style.BackColor = Color.White; row.DefaultCellStyle.BackColor = Color.White;
        }
        lastValues.Clear(); SetStatus(captured is null ? reason : reason + $" · 保留上次画面 {capturedAt:HH:mm:ss.fff}");
    }
    private void SetStatus(string text, bool record = true)
    {
        status.Text = text;
        if (record && previousStatus != text) AddLog(text);
        previousStatus = text;
    }
    private void AddLog(string message)
    {
        string line = $"{DateTime.Now:HH:mm:ss.fff} {message}";
        int width = MeasureLogWidth(line);
        if (width > changes.HorizontalExtent) changes.HorizontalExtent = width;
        log.Enqueue(line); changes.Items.Add(line);
        while (log.Count > 1000) { log.Dequeue(); changes.Items.RemoveAt(0); }
        if (!batchingLogs) changes.TopIndex = Math.Max(0, changes.Items.Count - 1);
    }
    private int MeasureLogWidth(string line) => TextRenderer.MeasureText(line, changes.Font, Size.Empty,
        TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Width + 12;
    private void UpdateLogWidth()
    {
        int width = 1;
        foreach (string line in log) width = Math.Max(width, MeasureLogWidth(line));
        changes.HorizontalExtent = width;
    }
    private void ClearCurrentLog()
    {
        log.Clear(); changes.Items.Clear();
        changes.HorizontalExtent = 1;
        status.Text = "窗口日志已清空。继续采样后会记录新的变化。";
    }
    private void ClearSavedLogs()
    {
        modalOperation = true;
        bool wasFrozen = frozen; frozen = true;
        StopExecution();
        try
        {
            var result = savedLogs.ClearContents();
            ClearCurrentLog();
            string message = result.Cleared == 0 && result.Errors.Length == 0
                ? "没有已保存日志需要清空。窗口日志已清空。"
                : $"已清空 {result.Cleared} 个已保存日志的内容。窗口日志已清空。";
            if (result.Errors.Length > 0) message += $"；{result.Errors.Length} 个失败：" + string.Join("；", result.Errors);
            SetStatus(message);
        }
        catch (Exception e) { SetStatus("清空日志失败：" + e.Message); }
        finally { modalOperation = false; frozen = wasFrozen; if (!frozen) heartbeat.Reset(); }
    }
    private void Export()
    {
        modalOperation = true;
        bool wasFrozen = frozen;
        frozen = true;
        StopExecution();
        try
        {
            if (captured is null || capturedLayout is null) throw new InvalidOperationException("还没有采样画面。");
            Directory.CreateDirectory(exportDirectory);
            using var dialog = new SaveFileDialog { Filter = "原始采样 PNG|*.png", InitialDirectory = exportDirectory, FileName = $"points-{capturedAt:yyyyMMdd-HHmmss-fff}.png" };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            string path = Path.ChangeExtension(dialog.FileName, null);
            exportDirectory = Path.GetDirectoryName(Path.GetFullPath(dialog.FileName))!;
            ExportSnapshot(path);
            AddLog("已保存截图、字段 CSV、日志及布局：" + path);
        }
        catch (Exception e) { SetStatus(e.Message); }
        finally { modalOperation = false; frozen = wasFrozen; if (!frozen) heartbeat.Reset(); }
    }
    internal void ExportSnapshot(string path)
    {
        if (captured is null || capturedLayout is null) throw new InvalidOperationException("还没有采样画面。");
        captured.Image.Save(path + ".png", ImageFormat.Png);
        static string Quote(object? value) => "\"" + (value?.ToString() ?? "").Replace("\"", "\"\"") + "\"";
        var csv = new StringBuilder(); csv.AppendLine(string.Join(",", grid.Columns.Cast<DataGridViewColumn>().Where(c => c.Index != 3).Select(c => Quote(c.HeaderText))));
        foreach (DataGridViewRow row in grid.Rows) csv.AppendLine(string.Join(",", row.Cells.Cast<DataGridViewCell>().Where(c => c.ColumnIndex != 3).Select(c => Quote(c.Value))));
        File.WriteAllText(path + ".csv", csv.ToString(), new UTF8Encoding(true));
        File.WriteAllText(path + ".log.txt", $"最后画面时间：{capturedAt:O}\n状态：{status.Text}\n截图客户区范围：{captured.Bounds}\n" + string.Join(Environment.NewLine, log), Encoding.UTF8);
        savedLogs.Remember(path + ".log.txt");
        capturedLayout.Save(path + ".layout.json");
    }
    internal void ShowSynthetic(DiagnosticLayout example, Bitmap image)
    {
        SelectClass(ClassProfiles.Identify(image.GetPixel(example.Markers[1].X, example.Markers[1].Y)), example, force: true);
        ApplyControls(); ReplaceCapture(new((Bitmap)image.Clone(), new Rectangle(Point.Empty, image.Size)));
        for (int i = 0; i < fields.Length; i++)
        {
            var field = fields[i]; var point = FuryBuffs.Point(layout!, field);
            if (point is null) continue;
            var color = image.GetPixel(point.X, point.Y); var row = grid.Rows[i];
            row.Cells[2].Value = $"{point.X}, {point.Y}"; row.Cells[3].Style.BackColor = color; row.Cells[4].Value = Colors.Rgb(color); row.Cells[5].Value = FuryBuffs.Decode(layout!, field, color,p=>image.GetPixel(p.X,p.Y));
        }
        SetStatus("合成画面，仅供窗口布局检查 · 当前专精字段与原始 RGB");
        if (activeProfile?.SpecId == 72) AddLog("[S:enrage] 激怒：存在 · 临近结束 · 固定点；鲁莽=成功施法计时；雷霆配置=" + layout!.ThunderMode);
        else { AddLog("[21] 建议：盾牌猛击：否 → 是"); AddLog("[7] 怒气比例：45.1% → 62.7%"); }
    }
    internal void FreezeForTesting() { StopExecution(); timer.Stop(); frozen = true; }
    internal void ShowSyntheticPosition(DiagnosticLayout example, Bitmap image)
    {
        SelectClass(ClassProfiles.Identify(image.GetPixel(example.Markers[1].X, example.Markers[1].Y)), example, force: true);
        layout = example; BeginLocator("合成定位测试"); frozen = true;
        using var scan = new Captured((Bitmap)image.Clone(), new Rectangle(Point.Empty, image.Size));
        ShowPositionChecks(example, scan, PositionVerification.Check(example, scan.At));
        SetStatus("合成定位画面 · 校验通过 103/103 · 专门用于检查点位位置");
    }
}

internal sealed class PixelPreview : Control
{
    internal Captured? Source;
    internal DiagnosticLayout? SamplingLayout;
    internal int Selected = -1;
    internal bool LocationMode;
    internal Field[]? LiveFields;
    public PixelPreview() { DoubleBuffered = true; Size = new Size(1000, 90); }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e); var source = Source; var layout = SamplingLayout;
        if (source is null || layout is null) { e.Graphics.DrawString("校准后显示像素放大图，黄色标记对应表格选中行。", Font, Brushes.DimGray, 8, 15); return; }
        e.Graphics.InterpolationMode = InterpolationMode.NearestNeighbor; e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;
        e.Graphics.DrawImage(source.Image, new Rectangle(0, 24, source.Image.Width * 2, source.Image.Height * 8), 0, 0, source.Image.Width, source.Image.Height, GraphicsUnit.Pixel);
        using var small = new Font(Font.FontFamily, 7);
        for (int i = 0; i < layout.Frames.Length; i++)
        {
            var p = layout.Frames[i]; int x = (p.X - source.Bounds.X) * 2;
            e.Graphics.DrawString((i + 1).ToString(), small, Brushes.Black, x - 7, 3);
        }
        for (int i = 0; i < 3; i++) e.Graphics.DrawString($"M{i + 1}", small, Brushes.Black, (layout.Markers[i].X - source.Bounds.X) * 2 - 7, 3);
        SamplePoint? selected = Selected is >= 0 and < 50 ? layout.Frames[Selected] : LocationMode
            ? Selected is >= 50 and < 100 ? layout.Bars[Selected - 50] : Selected is >= 100 and < 103 ? layout.Markers[Selected - 100] : null
            : LiveFields is { } fields && Selected >= 0 && Selected < fields.Length ? FuryBuffs.Point(layout, fields[Selected]) : null;
        if (selected is not null)
        {
            int x = (selected.X - source.Bounds.X) * 2, y = 24 + (selected.Y - source.Bounds.Y) * 8;
            using var pen = new Pen(Color.Gold, 2); e.Graphics.DrawRectangle(pen, x - 4, y - 2, 9, 11);
        }
    }
}
