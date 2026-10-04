#nullable enable
using System.Drawing;

namespace RotationUS.Diagnostics;

internal static class FuryTests
{
    internal static void Render(string path)
    {
        using var pattern=DiagnosticTests.Pattern(1,markerLayout:MarkerLayouts.Wide);
        var layout=Calibration.Find(pattern,new Size(1600,900));layout.SpecId=72;
        using var image=new Bitmap(800,20);
        using(var g=Graphics.FromImage(image)) g.Clear(Color.Black);
        foreach(int i in new[]{1,3,4,5,28,37,23,42}) image.SetPixel(layout.Frames[i-1].X,0,Color.White);
        image.SetPixel(layout.Frames[5].X,0,Color.FromArgb(230,230,230));
        image.SetPixel(layout.Markers[0].X,0,Color.Lime);
        image.SetPixel(layout.Markers[1].X,0,ClassProfiles.Encode(72,1));
        image.SetPixel(layout.Markers[2].X,0,Color.Cyan);
        for(int i=0;i<FuryBuffs.Points.Length;i++) layout.SpecialPoints[FuryBuffs.Points[i].Key]=new()
            { Point=new(540+i*20,8),Enabled=true,Comparison=FuryBuffs.UsesTimeColor(FuryBuffs.Points[i].Key)?"expiring":"presence",ReferenceColor=new((byte)(90+i),30,10) };
        image.SetPixel(540,8,Color.FromArgb(90,30,10));
        string folder=Path.Combine(Path.GetTempPath(),"RotationUS-fury-ui-"+Guid.NewGuid().ToString("N"));
        using var form=new DiagnosticForm(false,new SavedLogStore(folder),new ProfileLayouts(folder));
        form.Show();System.Windows.Forms.Application.DoEvents();form.FreezeForTesting();
        form.ShowSynthetic(layout,image);form.ProfilePresentation(layout,image);form.Refresh();
        string samplePath=Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path))!,"fury-synthetic-sample");
        form.ExportSnapshot(samplePath);
        string csv=File.ReadAllText(samplePath+".csv");
        if(FuryBuffs.Points.Any(p=>!csv.Contains("S:"+p.Key) || !csv.Contains(p.Label)) || !csv.Contains("固定点"))
            throw new Exception("Named point export missing name or source");
        if(DiagnosticLayout.Load(samplePath+".layout.json").SpecialPoints.Count!=FuryBuffs.Points.Length)
            throw new Exception("Exported layout lost named points");
        var editor=Descendants(form).OfType<FuryPointSettings>().Single();
        if(editor.Rows.Count!=5 || editor.Rows.Values.Any(row=>!row.Pick.Visible || !row.Save.Visible || !row.Color.Visible))
            throw new Exception("Five independent Fury rows must all be visible");
        using var rendered=new Bitmap(form.Width,form.Height);
        form.DrawToBitmap(rendered,new Rectangle(Point.Empty,rendered.Size));rendered.Save(Path.GetFullPath(path));
        form.ClientSize=new Size(950,650);form.PerformLayout();System.Windows.Forms.Application.DoEvents();
        if(editor.Rows.Values.Any(row=>!editor.ClientRectangle.Contains(editor.RectangleToClient(row.Save.RectangleToScreen(row.Save.ClientRectangle)))))
            throw new Exception("Fury settings clipped at minimum size");
        using(var minimum=new Bitmap(form.Width,form.Height))
        { form.DrawToBitmap(minimum,new Rectangle(Point.Empty,minimum.Size));minimum.Save(Path.ChangeExtension(Path.GetFullPath(path),"minimum.png")); }
        var replacement=new PickedPixel(new(550,8),new(12,34,56));
        editor.Stage("enrage",replacement);editor.Stage("hack",new(new(610,8),new(40,50,60)));
        form.SaveSpecialLayout(editor.BuildLayout(layout,"enrage"),"enrage");
        var persisted=DiagnosticLayout.Load(Path.Combine(folder,"profiles","kbz.json"));
        if(persisted.SpecialPoints["enrage"].ReferenceColor!=new SampleColor(12,34,56) || editor.Rows["enrage"].Save.Enabled || !editor.Rows["hack"].Save.Enabled ||
            FuryBuffs.Points.Skip(1).Any(p=>persisted.SpecialPoints[p.Key].Point!=layout.SpecialPoints[p.Key].Point || persisted.SpecialPoints[p.Key].ReferenceColor!=layout.SpecialPoints[p.Key].ReferenceColor))
            throw new Exception("Saving enrage modified another point or retained a pending save");
        form.SaveSpecialLayout(editor.BuildLayout(persisted,"hack"),"hack");
        persisted=DiagnosticLayout.Load(Path.Combine(folder,"profiles","kbz.json"));
        if(persisted.SpecialPoints["hack"].ReferenceColor!=new SampleColor(40,50,60) || persisted.SpecialPoints["hack"].Comparison!="presence")
            throw new Exception("Presence-only buff lost independently sampled reference color");
        form.Close();Console.WriteLine("Fury UI rendered: "+Path.GetFullPath(path));
    }
    private static IEnumerable<System.Windows.Forms.Control> Descendants(System.Windows.Forms.Control parent) => parent.Controls.Cast<System.Windows.Forms.Control>()
        .SelectMany(child=>new[]{child}.Concat(Descendants(child)));
    internal static void Run(Action<bool,string> check, Action<Action,string> reject)
    {
        var profile = ClassProfiles.Find(72)!;
        check(FuryBuffs.Points.Select(p=>p.Label).SequenceEqual(new[]{"激怒","雷霆轰击","劈斩","旋风斩","天神下凡"}),"exactly five named buff points");
        using(var editor=new FuryPointSettings())
        {
            using var pixels=DiagnosticTests.Pattern(1);
            var configuration=Calibration.Find(pixels,new Size(1600,900));configuration.SpecId=72;
            foreach(var definition in FuryBuffs.Points)
            {
                editor.Stage(definition.Key,new(new(540,8),new(20,40,60)));
                configuration=editor.BuildLayout(configuration,definition.Key);
                var matching=FuryBuffs.Observe(configuration,definition.Key,_=>Color.FromArgb(20,40,60));
                var other=FuryBuffs.Observe(configuration,definition.Key,_=>Color.Green);
                check(matching.Present==true && matching.Expiring==(FuryBuffs.UsesTimeColor(definition.Key)?true:null),"reference color semantics for "+definition.Key);
                check(other.Present==true && other.Expiring==(FuryBuffs.UsesTimeColor(definition.Key)?false:null),"other nonblack color semantics for "+definition.Key);
                check(FuryBuffs.Observe(configuration,definition.Key,_=>Color.Black).Present==false,"black absence for "+definition.Key);
            }
            check(configuration.SpecialPoints.Count==5 && configuration.SpecialPoints.Values.All(p=>p.ReferenceColor==new SampleColor(20,40,60)),"all five points retain sampled colors");
            editor.Stage("thunder",new(new(540,8),new(0,0,0)));configuration=editor.BuildLayout(configuration,"thunder");
            check(FuryBuffs.Observe(configuration,"thunder",_=>Color.Blue).Present==true,"black reference never changes presence-only nonblack rule");
            foreach(string key in new[]{"enrage","avatar"})
            { editor.Stage(key,new(new(540,8),new(0,0,0)));reject(()=>editor.BuildLayout(configuration,key),"black time reference rejected for "+key); }
            configuration.SpecialPoints["thunder-two"]=new() {Point=new(560,8),Enabled=true};
            check(FuryBuffs.Read(configuration,_=>Color.White).ThunderTwo is null,"retired sixth point cannot infer two layers");
        }
        foreach (double scale in new[] { .8,1,1.142,1.25,1.5,2 })
        {
            using var wide = DiagnosticTests.Pattern(scale,3,2,markerLayout:MarkerLayouts.Wide);
            var found = Calibration.Find(wide,new Size(1600,900));
            check(found.MarkerLayout == MarkerLayouts.Wide && PositionVerification.Check(found,p => wide.GetPixel(p.X,p.Y)).All(c => c.Passed),"wide scaled calibration " + scale);
            check(found.Markers[0].X>3+730*scale,"markers outside black background " + scale);
            using var previous=DiagnosticTests.Pattern(scale,3,2,markerLayout:MarkerLayouts.PreviousWide);
            reject(()=>Calibration.Find(previous,new Size(1600,900)),"previous wide layout requires unified calibration " + scale);
        }
        using var old = DiagnosticTests.Pattern(1,markerLayout:MarkerLayouts.Legacy);
        using var newer = DiagnosticTests.Pattern(1,markerLayout:MarkerLayouts.Wide);
        reject(()=>Calibration.Find(old,new Size(1600,900)),"legacy layout requires unified calibration");
        var layout = Calibration.Find(newer,new Size(1600,900)); layout.SpecId=72;
        var legacy = layout.Copy(); legacy.MarkerLayout=MarkerLayouts.Legacy;
        legacy.Markers=MarkerLayouts.Points(10,10,0,MarkerLayouts.Legacy);
        check(!PositionVerification.SameGeometry(legacy,layout) && !PositionVerification.CompatibleSpecialGeometry(legacy,layout),"old special points require fresh picking under unified layout");
        foreach (var registered in ClassProfiles.All)
        {
            using var classPattern=DiagnosticTests.Pattern(1);
            using (var g=Graphics.FromImage(classPattern))
            using (var identityBrush=new SolidBrush(ClassProfiles.Encode(registered.SpecId,registered.ClassId)))
                g.FillRectangle(identityBrush,750,0,10,3);
            var classLayout=Calibration.Find(classPattern,new Size(1600,900));
            check(classLayout.MarkerLayout==MarkerLayouts.Wide && PositionVerification.SameGeometry(layout,classLayout),"same right-side geometry for " + registered.Key);
        }
        var locator = new StableLocator(); locator.Observe(legacy);
        check(!locator.Observe(layout),"marker migration requires two consistent full layouts");
        using (var conflict = DiagnosticTests.Pattern(1,markerLayout:MarkerLayouts.Wide))
        {
            using var g=Graphics.FromImage(conflict);
            g.FillRectangle(Brushes.Red,560,0,10,3);g.FillRectangle(Brushes.Blue,570,0,10,3);g.FillRectangle(Brushes.Cyan,580,0,10,3);
            reject(()=>Calibration.Find(conflict,new Size(1600,900)),"ambiguous legacy/wide markers");
        }
        using (var conflict=DiagnosticTests.Pattern(1,markerLayout:MarkerLayouts.Wide))
        {
            using var g=Graphics.FromImage(conflict);
            g.FillRectangle(Brushes.Red,690,0,10,3);g.FillRectangle(Brushes.Blue,700,0,10,3);g.FillRectangle(Brushes.Cyan,710,0,10,3);
            reject(()=>Calibration.Find(conflict,new Size(1600,900)),"ambiguous previous/current wide markers");
        }
        using (var narrow=DiagnosticTests.Pattern(1,width:760,markerLayout:MarkerLayouts.Wide))
            reject(()=>Calibration.Find(narrow,new Size(760,900)),"wide marker clipped by client edge");
        layout.SpecialPoints["enrage"]=new() { Point=new(900,35),Enabled=true,Comparison="expiring",ReferenceColor=new(90,30,10) };
        check(layout.CaptureBounds.Contains(new Point(900,35)),"all enabled named points included in single capture");
        var copy=layout.Copy(); copy.SpecialPoints["enrage"].Enabled=false;
        check(layout.SpecialPoints["enrage"].Enabled,"named point copies independent");
        var black=copy.Copy();black.SpecialPoints["enrage"].ReferenceColor=new(0,0,0);
        reject(black.Validate,"black expiration reference");
        var outside=layout.Copy();outside.SpecialPoints["enrage"].Point=new(1600,0);
        reject(outside.Validate,"named point out of bounds");
        check(FuryBuffs.Observe(layout,"enrage",_=>Color.Black).Present==false,"black enrage absent");
        var ending=FuryBuffs.Observe(layout,"enrage",_=>Color.FromArgb(90,30,10));
        check(ending.Present==true && ending.Expiring==true,"exact enrage color expires");
        check(FuryBuffs.Observe(layout,"enrage",_=>Color.Blue).Expiring==false,"other nonblack enrage normal");
        check(FuryBuffs.Observe(layout,"avatar",_=>Color.Black).Present is null,"unconfigured point is unknown");
        var layerField=FuryBuffs.Fields().Single(field=>field.Id=="B4");
        check(FuryBuffs.Decode(layout,layerField,Color.Black,_=>Color.Black).StartsWith("未知"),"disabled aura bar is not diagnosed as zero layers");
        check(FuryBuffs.Decode(layout,layerField,Color.White,_=>Color.FromArgb(255,128,0)).StartsWith("未知"),"restricted aura bar cannot display stale two layers");
        check(FuryBuffs.Decode(layout,layerField,Color.Black,_=>Color.White).StartsWith("否"),"valid aura bar may report fewer than two layers");
        string config=Path.Combine(Path.GetTempPath(),"fury-profile-test-"+Guid.NewGuid().ToString("N"));
        try
        {
            var store=new ProfileLayouts(config);store.Save(profile,layout);
            var reloaded=store.Load(profile,layout);
            check(reloaded.SpecialPoints["enrage"].ReferenceColor==new SampleColor(90,30,10),"named config persists independently");
            var protection=legacy.Copy();protection.SpecId=73;protection.SpecialEnabled=true;protection.Special=new(610,8);protection.SpecialReferenceColor=new(30,40,50);
            store.Save(ClassProfiles.Protection,protection);
            var migrated=store.Load(ClassProfiles.Protection,layout);
            check(!migrated.SpecialEnabled && migrated.SpecialReferenceColor is null && migrated.SpecialPoints.Count==0 && PositionVerification.SameGeometry(migrated,layout),"defense uses shared geometry and requires fresh coordinates and color");
            var previousGeometry=layout.Copy();previousGeometry.MarkerLayout=MarkerLayouts.PreviousWide;
            previousGeometry.Markers=MarkerLayouts.Points(10,10,0,MarkerLayouts.PreviousWide);
            previousGeometry.SpecId=72;previousGeometry.SpecialPoints=layout.Copy().SpecialPoints;
            store.Save(profile,previousGeometry);
            var currentGeometry=store.Load(profile,layout);
            check(!currentGeometry.SpecialPoints["enrage"].Enabled && currentGeometry.SpecialPoints["enrage"].ReferenceColor is null,"previous wide config requires fresh named points and colors");
            store.SaveGeometry(layout);
            check(DiagnosticLayout.Load(store.DiscoveryPath).SpecialPoints.Count==0,"shared config strips all named points");
            var moved=layout.Copy();moved.Frames[0]=new(moved.Frames[0].X+3,moved.Frames[0].Y);
            check(!store.Load(profile,moved).SpecialPoints["enrage"].Enabled,"actual standard geometry change invalidates named points");
        }
        finally
        {
            if(Directory.Exists(config)) { foreach(var path in Directory.GetFiles(config,"*",SearchOption.AllDirectories)) File.Delete(path); Directory.Delete(Path.Combine(config,"profiles"));Directory.Delete(config); }
        }
        Dictionary<int,Color> f=new(),b=new();FuryBuffs buffs=new();
        void Reset(bool aoe=false,bool burst=false)
        {
            f=Enumerable.Range(1,50).ToDictionary(i=>i,_=>Color.Black);b=Enumerable.Range(1,50).ToDictionary(i=>i,_=>Color.Black);
            foreach(int i in new[]{1,3,4,5,28,42}) f[i]=Color.White;
            f[6]=Color.White;f[2]=aoe?Color.White:Color.Black;f[29]=burst?Color.White:Color.Black;
            buffs=new() { Enrage=new(true,false,"test"),Avatar=new(false,false,"test"),Thunder=false,ThunderTwo=false,Hack=false,Whirlwind=true };
        }
        void Skill(int usable,int ready,bool yes=true) { f[usable]=f[ready]=yes?Color.White:Color.Black; }
        FuryDecision D()=>KBZ.Decide(f,b,buffs);
        Reset();Skill(23,37);Skill(24,38);f[7]=Color.White;check(D().Key==7,"ST outside rage>100 rampage beats blood");
        f[7]=Color.Black;buffs.Enrage=new(true,true,"test");check(D().Key==7,"ST outside expiration rampage");
        Reset(false,true);Skill(23,37);Skill(24,38);Skill(25,39);buffs.Hack=true;
        check(D().Key==9,"ST burst hack raging before blood, no tier preparation");
        f[7]=Color.White;check(D().Key==7,"ST burst high rage first");
        f[7]=Color.Black;Skill(27,41);f[32]=Color.White;buffs.Thunder=true;buffs.ThunderTwo=true;buffs.ThunderMode="exact-stacks";
        check(D().Key==11,"ST burst exact two thunder before hack");
        buffs.ThunderMode="presence-only";check(D().Key==9,"presence-only does not pretend two layers");
        buffs.Avatar=new(true,true,"test");check(D().Key==11,"avatar ending thunder early even presence-only");
        buffs.Avatar=new(false,true,"test");check(D().Key==9,"absent avatar not ending");
        buffs.Avatar=new(true,null,"test");check(D().Key==9,"optional avatar ending unknown disables only ending rule");
        buffs.ThunderMode="exact-stacks";buffs.ThunderTwo=null;
        check(D().Key==9 && D().Mode.Contains("已降级"),"unknown exact layers explicitly degrades when presence known");
        Reset();Skill(23,37);Skill(27,41);buffs.Thunder=true;f[32]=Color.White;
        check(D().Key==11,"ST outside blast before ordinary rampage");
        Reset(false,true);Skill(23,37);Skill(24,38);check(D().Key==8,"ST burst blood before normal rampage");
        Reset();Skill(23,37);Skill(24,38);check(D().Key==7,"ST outside ordinary rampage before blood");
        Reset(true,true);Skill(23,37);Skill(27,41);buffs.Enrage=new(false,false,"test");buffs.Thunder=true;f[32]=Color.White;
        check(D().Key==7,"AOE burst absent enrage before thunder");
        buffs.Enrage=new(true,false,"test");check(D().Key==11,"AOE burst thunder before rampage");
        buffs.Thunder=false;f[32]=Color.Black;f[7]=Color.White;
        check(D().Key==7,"six enemies high rage before ordinary clap");
        f[9]=Color.White;check(D().Key==11,"seven enemies ordinary clap before high rage");
        Reset(true);Skill(23,37);Skill(24,38);Skill(27,41);buffs.Whirlwind=false;
        check(D().Key==11,"AOE outside missing cleave buff replenishes clap");
        buffs.Whirlwind=true;check(D().Key==8,"AOE outside blood before ordinary rampage");
        Reset(true);Skill(25,39);Skill(26,40);check(D().Key==10,"AOE outside execute before ordinary raging");
        buffs.Hack=true;check(D().Key==9,"AOE outside hack raging before execute");
        Reset(true);Skill(24,38);Skill(25,39);buffs.Hack=true;check(D().Key==8,"AOE outside blood before hack raging");
        Reset();Skill(24,38);Skill(25,39);buffs.Hack=true;check(D().Key==9,"ST outside hack raging before blood");
        Reset(false,true);Skill(25,39);f[39]=Color.Black;f[43]=Color.White;b[1]=Color.White;
        check(D().Key==9,"raging still available with one remaining charge while recharging");
        Reset();Skill(24,38);buffs.Avatar=new(true,false,"test");check(D().Key==8,"avatar-only burst uses base blood family");
        Reset();Skill(23,37);buffs.Enrage=new(null,null,"unconfigured");check(D().Waiting,"unknown enrage cannot silently become absent");
        f[7]=Color.White;check(D().Key==7,"known rage condition makes enrage irrelevant");
        Reset(true,true);Skill(27,41);f[32]=Color.White;buffs.Thunder=true;buffs.Enrage=new(null,null,"unknown");
        check(D().Key==11,"unusable rampage does not block thunder on unknown enrage");
        Reset();Skill(23,37);buffs.Enrage=new(null,null,"unknown");f[22]=Color.White;
        check(D().Waiting && D().Key!=16,"heroic fallback cannot bypass unresolved high priority");
        Reset();f[29]=Color.FromArgb(255,128,0);check(D().Waiting,"invalid timer channel blocks unknown branch");
        buffs.Avatar=new(true,false,"test");Skill(24,38);check(D().Key==8,"known avatar proves burst despite timer unknown");
        Reset();f[8]=Color.White;Skill(30,19);Skill(31,20);check(D().Key==17,"officer recklessness first");
        f[29]=Color.White;f[19]=Color.Black;f[35]=Color.White;check(D().Key==3,"after reckless success avatar still requested");
        f[20]=Color.Black;f[36]=Color.White;check(D().Key==28,"both own cooldowns clear officer");
        f[35]=f[36]=Color.Black;f[28]=Color.Black;check(D().Key!=28,"GCD alone never clears officer");
        f[1]=Color.Black;check(D().Key==28,"out of combat clears officer");
        Reset();f[8]=f[30]=f[19]=f[31]=Color.White;f[36]=Color.White;
        check(D().Key==17,"only recklessness ready executes without waiting for avatar");
        f[19]=Color.Black;f[35]=Color.White;f[20]=Color.White;f[36]=Color.Black;
        check(D().Key==3,"only avatar ready executes without waiting for recklessness");
        Reset();f[6]=Color.FromArgb(89,89,89);foreach(int i in new[]{11,12,13,14,16})f[i]=Color.White;
        check(D().Key==1,"utility recovery victory first");f[13]=Color.Black;check(D().Key==18,"fox before consumables");
        f[16]=Color.Black;check(D().Key==5,"healthstone before potion");f[11]=Color.Black;check(D().Key==6,"potion recovery");
        Reset();f[13]=f[14]=Color.White;f[6]=Color.FromArgb(178,178,178);check(D().Key==1,"victory below encoded 70 percent boundary");
        f[6]=Color.FromArgb(179,179,179);check(D().Key!=1,"victory above encoded 70 percent boundary");
        Reset();f[11]=Color.White;f[6]=Color.FromArgb(102,102,102);check(D().Key==5,"healthstone at encoded 40 percent boundary");
        f[6]=Color.FromArgb(103,103,103);check(D().Key!=5,"healthstone above encoded 40 percent boundary");
        Reset();f[10]=f[21]=Color.White;check(D().Key==15,"wrecking enabled for fury absorption");
        f[10]=Color.Black;check(D().Key!=15,"wrecking not used without absorption");
        f[10]=Color.White;f[21]=Color.Black;check(D().Key!=15,"wrecking not used during own cooldown");
        f[21]=Color.White;f[5]=Color.Black;check(D().Key!=15,"wrecking not used outside range condition");
        foreach(bool aoe in new[]{false,true}) foreach(bool burst in new[]{false,true})
        {
            Reset(aoe,burst);Skill(27,41);check(D().Key==11,"ordinary clap available as final fallback "+aoe+"/"+burst);
            Skill(26,40);check(D().Key==(aoe&&burst?11:10),"execute vs ordinary clap priority "+aoe+"/"+burst);
        }
        Reset();f[17]=f[18]=f[15]=Color.White;check(D().Key==4,"food requested interrupt");
        f[18]=Color.Black;check(D().Key==29,"food cleanup when cast ends");
        Reset();f[3]=Color.Black;f[22]=Color.White;check(D().Key==16,"heroic range fallback uses Shift semicolon key action");
        check(FuryBuffs.Boolean(Color.FromArgb(255,128,0)) is null,"unknown wire color not white boolean");
        check(FuryBuffs.Fields().All(field=>field.Id!="S:recklessness"),"recklessness uses timer not special point");
        using(var pixels=new Bitmap(1000,50))
        {
            using(var g=Graphics.FromImage(pixels))g.Clear(Color.Black);
            foreach(int i in new[]{1,3,4,5,7,23,28,29,37,42}) pixels.SetPixel(layout.Frames[i-1].X,layout.Frames[i-1].Y,Color.White);
            pixels.SetPixel(layout.Frames[5].X,layout.Frames[5].Y,Color.White);
            pixels.SetPixel(900,35,Color.FromArgb(90,30,10));
            using var sample=new Captured((Bitmap)pixels.Clone(),new Rectangle(Point.Empty,pixels.Size));
            var game=new GameWindow(1,(IntPtr)42,"Fury synthetic");bool manual=false;int calls=0;
            Dictionary<int,bool> selected=new();
            var runner=new RotationExecution(()=>game.Handle,()=>manual,(frames,bars)=>{ calls++;selected.Clear();profile.Process(frames,bars,selected); });
            runner.Start(profile);runner.Tick(game,layout,sample,false,0,profile);
            check(calls==0,"fury execution requires valid data and heartbeat");
            string result=runner.Tick(game,layout,sample,true,0,profile);
            check(calls==1 && selected.Count(kv=>kv.Value)==1 && selected.GetValueOrDefault(7) && !selected.GetValueOrDefault(12),"fury registered handler selects one action without whirlwind");
            check(KBZ.Inst.Buffs.Enrage.Expiring==true && result.Contains(KBZ.Inst.LastDecision.Reason),"fury execution reads named points from same capture and reports decision");
            manual=true;runner.Tick(game,layout,sample,true,.1,profile);manual=false;runner.Tick(game,layout,sample,true,.59,profile);
            check(calls==1,"fury manual input yields for 500ms");
            runner.Tick(game,layout,sample,true,.61,profile);check(calls==2,"fury resumes after manual input pause");
            runner.Tick(game,layout,sample,true,1,ClassProfiles.Protection);
            check(!runner.Enabled && calls==2,"switching fury to protection stops before processing");
        }
        var defense=Enumerable.Range(1,51).ToDictionary(i=>i,_=>Color.Black);
        var defenseBars=Enumerable.Range(1,50).ToDictionary(i=>i,_=>Color.Black);
        defense[6]=Color.White;foreach(int i in new[]{1,5,10,19})defense[i]=Color.White;
        var actions=new Dictionary<int,bool>();FZ.Inst.Process(defense,defenseBars,actions);
        check(actions.GetValueOrDefault(15),"protection retains enabled wrecking on absorption");
        actions.Clear();defense[10]=Color.Black;FZ.Inst.Process(defense,defenseBars,actions);
        check(!actions.GetValueOrDefault(15),"protection no wrecking without absorb");
        foreach(int i in new[]{3,8,18})defense[i]=Color.White;
        actions.Clear();FZ.Inst.Process(defense,defenseBars,actions);check(actions.GetValueOrDefault(3),"protection avatar waits for thunder cooldown");
        defense[17]=Color.White;actions.Clear();FZ.Inst.Process(defense,defenseBars,actions);
        check(!actions.GetValueOrDefault(3),"protection still withholds avatar when thunder ready");
    }
}
