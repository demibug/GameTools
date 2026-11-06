#nullable enable
using System.Drawing;

namespace RotationUS.Diagnostics;

internal sealed record PartyRunicPower(PartyRosterSnapshot Roster, bool?[] Enough)
{
    internal bool? At(PartyRosterSnapshot? roster, int slot) =>
        roster is not null && Roster.Matches(roster) && slot is >= 1 and <= 5 ? Enough[slot-1] : null;
}

// Live resource observations are never stored on disk or on the OOC identity cache.
internal sealed class PartyRunicPowerCache
{
    internal const int Threshold = 45;
    internal const double Lifetime = .5;
    internal PartyRunicPower? Current { get; private set; }
    internal string Status { get; private set; } = "未取得实时能量";
    private (int A, int B, int Epoch, int Revision)? stamp;
    private double changedAt = double.NegativeInfinity;
    internal void Reset(string reason = "未取得实时能量")
    {
        Current=null; stamp=null; changedAt=double.NegativeInfinity; Status=reason;
    }
    internal PartyRunicPower? Observe(Func<int,Color> read, PartyRosterSnapshot? roster, double now)
    {
        Current=null;
        if (roster is null) { Reset("小队资料无效：能量已清除"); return null; }
        int N(int id) { var c=read(id); return c.R | c.G<<8 | c.B<<16; }
        bool Magic(int id) { var c=read(id); return c.R==72 && c.G==82 && c.B==1; }
        int revision=N(81);
        if (!Magic(77) || !Magic(94) || N(78)!=roster.SessionA || N(79)!=roster.SessionB ||
            N(80)!=roster.Epoch || N(93)!=roster.Epoch || revision==0 || N(92)!=revision || N(73)!=revision ||
            Enumerable.Range(0,5).Any(i=>N(82+i*2)!=roster.Members[i].Id))
        { Status="能量信号缺失／队伍或采样版本不匹配"; return null; }
        var next=(roster.SessionA,roster.SessionB,roster.Epoch,revision);
        if (now < changedAt) { Reset("采样时钟变化"); return null; }
        if (stamp != next) { stamp=next; changedAt=now; }
        if (now-changedAt > Lifetime) { Status="能量超过0.5秒未刷新"; return null; }
        var enough=Enumerable.Range(0,5).Select(i=>roster.Members[i].IsBloodTank ? FuryBuffs.Boolean(read(83+i*2)) : null).ToArray();
        Status=!roster.Members.Any(m=>m.IsBloodTank)?"没有已确认的血DK" :
            enough.All(v=>v is null)?"符文能量未知：取消血DK降权":"实时能量已接收（45点门槛，逐成员显示）";
        return Current=new(roster,enough);
    }
}
