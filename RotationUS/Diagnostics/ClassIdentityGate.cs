#nullable enable
namespace RotationUS.Diagnostics;

// Never execute a sample whose identity is unconfirmed. A rejected sample does
// not change the selected profile or the user's activation state.
internal sealed class ClassIdentityGate
{
    private ClassIdentity pending;
    private double since = double.NaN, previous = double.NaN;
    private int samples;
    internal bool Observe(ClassIdentity current, ClassIdentity next, bool liveMarker, double now)
    {
        bool knownPair = new PartyMember(1,next.ClassId,next.SpecId,0,true).Role != 0;
        if (!liveMarker || !knownPair || !double.IsFinite(now)) { Reset(); return false; }
        if (next == current) { Reset(); return true; }
        if (next != pending || !double.IsFinite(previous) || now < previous || now-previous > .5) {
            pending=next;since=now;samples=0;
        }
        previous=now;
        return ++samples >= 3 && now-since >= .3;
    }
    internal void Reset() {pending=default;since=previous=double.NaN;samples=0;}
    internal static int RunTests()
    {
        int n=0;
        void Check(bool value,string why) {if(!value) throw new Exception("FAIL identity gate: "+why);n++;}
        var gate=new ClassIdentityGate();var holy=new ClassIdentity(65,2);var ret=new ClassIdentity(70,2);
        Check(gate.Observe(holy,holy,true,0),"current identity accepted immediately");
        Check(!gate.Observe(holy,default,true,.1),"unknown identity rejected");
        Check(gate.Observe(holy,holy,true,.2),"recovery keeps current identity");
        Check(!gate.Observe(holy,ret,true,.3),"single different sample rejected");
        Check(gate.Observe(holy,holy,true,.4),"single-frame false change does not switch");
        Check(!gate.Observe(holy,new(65,1),true,.5),"mismatched class/spec rejected");
        Check(!gate.Observe(holy,ret,false,.6),"invalid mode cannot confirm a change");
        Check(!gate.Observe(holy,ret,true,1),"new candidate starts confirmation");
        Check(!gate.Observe(holy,ret,true,1.1),"too brief");
        Check(gate.Observe(holy,ret,true,1.31),"three stable samples confirm actual switch");
        gate.Reset();
        Check(!gate.Observe(holy,ret,true,2),"reset clears previous evidence");
        Check(!gate.Observe(holy,ret,true,3),"long capture gap resets evidence");
        Check(!gate.Observe(holy,ret,true,2.9),"backward clock resets evidence");
        Check(!gate.Observe(holy,ret,true,double.NaN),"invalid clock rejected");
        Check(!gate.Observe(holy,new(62,8),true,4),"unsupported but real spec must be confirmed");
        Check(!gate.Observe(holy,new(62,8),true,4.1),"unsupported candidate remains pending");
        Check(gate.Observe(holy,new(62,8),true,4.31),"real unsupported spec also closes old execution");
        return n;
    }
}
