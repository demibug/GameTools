#nullable enable
using System.Drawing;

namespace RotationUS.Diagnostics;

internal sealed record PartyMember(int Id, int ClassId, int SpecId, int AssignedRole, bool Online)
{
    internal bool IsBloodTank => ClassId == 6 && SpecId == 250 && Role == 1;
    internal bool HasKnownSpecialization => SpecId != 0 && (this with {AssignedRole=0}).Role != 0;
    // Only validated class/spec pairs may override the assigned role.
    internal int Role
    {
        get
        {
            if (!Online || Id == 0 || ClassId is < 1 or > 13) return 0;
            var spec = SpecId switch
            {
                71 or 72 => (1,3), 73 => (1,1),
                65 => (2,2), 66 => (2,1), 70 => (2,3),
                253 or 254 or 255 => (3,3), 259 or 260 or 261 => (4,3),
                256 or 257 => (5,2), 258 => (5,3),
                250 => (6,1), 251 or 252 => (6,3),
                262 or 263 => (7,3), 264 => (7,2),
                62 or 63 or 64 => (8,3), 265 or 266 or 267 => (9,3),
                268 => (10,1), 270 => (10,2), 269 => (10,3),
                102 or 103 => (11,3), 104 => (11,1), 105 => (11,2),
                577 or 1480 => (12,3), 581 => (12,1),
                1467 or 1473 => (13,3), 1468 => (13,2),
                _ => (0,0)
            };
            if (spec.Item1 != 0) return spec.Item1 == ClassId ? spec.Item2 : 0;
            return AssignedRole;
        }
    }
}

internal sealed record PartyRosterSnapshot(int SessionA, int SessionB, int Epoch, PartyMember[] Members)
{
    internal bool Matches(PartyRosterSnapshot other) => SessionA == other.SessionA && SessionB == other.SessionB &&
        Epoch == other.Epoch && Members.SequenceEqual(other.Members);
    internal double Bonus(int slot) => slot is >= 1 and <= 5 ? Members[slot-1].Role switch { 1 => .05, 2 => .03, _ => 0 } : 0;
    internal string Summary => "小队资料：" + string.Join("，", Members.Select((m,i) =>
        m.Id == 0 ? null : $"{(i == 0 ? "自己" : "party"+i)}=" + (m.Role switch {1 => "T", 2 => "治疗", 3 => "输出", _ => "未知"}))
        .Where(s => s is not null));
}

internal static class PartyRosterProtocol
{
    internal const int Start = 51, Count = 26;
    internal static (int Mode, PartyRosterSnapshot Snapshot)? Decode(Func<int, Color> read)
    {
        var p = Enumerable.Range(Start, Count).Select(read).ToArray();
        bool Magic(Color c) => c.R == 72 && c.G == 80 && c.B == 1;
        int Number(int i) => p[i].R | p[i].G << 8 | p[i].B << 16;
        if (!Magic(p[0]) || !Magic(p[24]) || Number(4) == 0 || Number(3) == 0 ||
            Number(4) != Number(22) || Number(3) != Number(23) || Number(5) != Number(25) ||
            p[5].G != 0 || p[5].B != 0 || p[5].R > 3) return null;
        int checksum = 0;
        for (int i=0;i<21;i++)
            checksum = (checksum + p[i].R*((i+1)*3+1) + p[i].G*((i+1)*3+2) + p[i].B*((i+1)*3+3)) % 16777216;
        if (checksum != Number(21)) return null;
        var members = new PartyMember[5];
        for (int slot=0;slot<5;slot++)
        {
            int pos = 6+slot*3, id = Number(pos), spec = Number(pos+2);
            var info = p[pos+1];
            if (info.R > 13 || info.G > 3 || info.B > 1 || spec > 65535 ||
                (id == 0 && (Number(pos+1) != 0 || spec != 0))) return null;
            members[slot] = new(id,info.R,spec,info.G,info.B == 1);
        }
        if (p[5].R is 1 or 2 && (members[0].Id == 0 || members.Count(m=>m.Id!=0) < 2 ||
            members.Where(m=>m.Id!=0).Select(m=>m.Id).Distinct().Count() != members.Count(m=>m.Id!=0))) return null;
        return (p[5].R, new(Number(1),Number(2),Number(3),members));
    }
}

// Bound to the selected game session; no persistence, and no acquisition in combat.
internal sealed class PartyRosterCache
{
    internal PartyRosterSnapshot? Current { get; private set; }
    internal int Generation { get; private set; }
    internal string Status { get; private set; } = "等待脱战取得小队资料";
    private (int A,int B,int Epoch)? identity;
    private double lastAt = double.NegativeInfinity;
    internal void Reset(string reason = "资料已清除，等待脱战刷新")
    {
        if (Current is not null || identity is not null) Generation++;
        Current = null; identity = null; lastAt = double.NegativeInfinity; Status=reason;
    }
    internal PartyRosterSnapshot? Observe(Func<int,Color> readBar, bool live, bool? inParty, bool? raid, bool? combat, double now)
    {
        if (!live || inParty != true || raid != false || combat is null) {
            Reset(!live ? "采集／心跳无效，旧资料已清除" : raid == true ? "团队模式：小队功能停用" :
                inParty == false ? "未在小队：旧资料已清除" : "队伍／战斗状态未知：按最低血量"); return null;
        }
        if (now < lastAt || now-lastAt > 1.5) Reset();
        lastAt = now;
        var packet = PartyRosterProtocol.Decode(readBar);
        if (packet is null) { Reset("资料包无效／插件未更新：按最低血量"); return null; }
        var snapshot = packet.Value.Snapshot;
        var nextIdentity = (snapshot.SessionA,snapshot.SessionB,snapshot.Epoch);
        if (identity != nextIdentity) { Current=null; identity=nextIdentity; Generation++; }
        if (packet.Value.Mode != (combat.Value ? 1 : 2))
        {
            if (Current is not null) Generation++;
            Current=null; Status="队伍变动／资料待刷新：按最低血量"; return null;
        }
        if (combat.Value)
        {
            if (Current is null || !Current.Matches(snapshot)) Current = null;
        }
        else Current = snapshot;
        Status=Current is null ? "等待脱战重新确认队伍：按最低血量" : combat.Value ? "战斗中：沿用脱战确认资料" : "非战斗：每3秒检查队伍，专精异步刷新";
        return Current;
    }
}

internal static class PartyHealingPriority
{
    internal const double BloodProtection = .40;
    internal static bool BloodReduced(double hp, int slot, PartyRosterSnapshot? roster, PartyRunicPower? power) =>
        hp >= BloodProtection && hp <= 1 && slot is >=1 and <=5 &&
        roster?.Members[slot-1].IsBloodTank == true && power?.At(roster,slot) == true;
    internal static double Bonus(double hp, int slot, PartyRosterSnapshot roster, PartyRunicPower? power) =>
        BloodReduced(hp,slot,roster,power) ? -.05 : roster.Bonus(slot);
    // Actual health defines urgency and all spell gates. Bonuses only sort within a tier.
    internal static int Compare(double hp, int slot, double otherHp, int otherSlot, HealingThresholds t, PartyRosterSnapshot? roster, PartyRunicPower? power=null)
    {
        if (roster is null) return hp.CompareTo(otherHp);
        int Tier(double health) => health < t.Critical ? 0 : health < t.Major ? 1 : 2;
        int tier = Tier(hp).CompareTo(Tier(otherHp));
        if (tier != 0) return tier;
        if (hp < t.Critical) return hp.CompareTo(otherHp);
        int score = (hp-Bonus(hp,slot,roster,power)).CompareTo(otherHp-Bonus(otherHp,otherSlot,roster,power));
        return score != 0 ? score : hp.CompareTo(otherHp);
    }
}
