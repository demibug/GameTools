#nullable enable
using System.Drawing;

namespace RotationUS.Diagnostics;

// Legacy Holy tests use player/party flags to describe five synthetic actors.
// In raid cases supply the new transport for those same actors; production
// never fabricates raid identity from the party fields.
internal static class RaidTestFixture
{
    private static int revision;
    internal static Dictionary<int,Color> Bars(Dictionary<int,Color> f,int? target=null,int self=1,int epoch=1,int? sequence=null)
    {
        bool F(int id)=>f.TryGetValue(id,out var c)&&FuryBuffs.Boolean(c)==true;
        int current=target??(F(3)&&F(4)?Enumerable.Range(1,5).FirstOrDefault(slot=>F(45+slot)):0);
        var b=Enumerable.Range(1,100).ToDictionary(i=>i,_=>Color.Black);
        for(int i=1;i<=40;i++) b[i]=Color.FromArgb(255,i==current?255:0,i==self?255:0);
        b[41]=b[46]=Color.FromArgb(72,84,1);
        Color Number(int n)=>Color.FromArgb(n&255,(n>>8)&255,(n>>16)&255);
        b[42]=b[44]=Number(epoch);b[43]=b[45]=Number(sequence??++revision);
        b[47]=Number(12);b[48]=Number(34);b[49]=Color.White;
        return b;
    }
    internal static HolyDecision Decide(Dictionary<int,Color> frames,HolyBuffs buff,HealingThresholds? thresholds=null,
        HolyCooldownContext? cooldowns=null,PartyRosterSnapshot? party=null,PartyRunicPower? power=null,int pendingSelection=0)
    {
        bool raid=frames.TryGetValue(41,out var c)&&FuryBuffs.Boolean(c)==true;
        var roster=raid?RaidRosterSnapshot.Read(Bars(frames)):null;
        var sample=raid?new RaidHealingSample(RaidInjury.Read(frames,roster,thresholds??new()),
            TollConfirmed:cooldowns?.Healing?.TollConfirmed==true):null;
        return HOLY.Decide(frames,buff,thresholds,cooldowns,party,power,pendingSelection,roster,sample);
    }
}
