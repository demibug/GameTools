#nullable enable
using System.Drawing;

namespace RotationUS.Diagnostics;

internal sealed record PartyLogChange(string Key,string Message);

// Observe every captured frame, independent of execution and panel visibility.
// HP is evidence attached to a transition, not part of the RP/priority state key.
internal sealed class PartyDiagnosticLog
{
    private Dictionary<string,(string State,string Text)> previous=new();
    internal bool Observed {get;private set;}
    internal string Snapshot {get;private set;}="小队诊断尚未采样";
    internal List<PartyLogChange> Observe(PartyRosterSnapshot? roster,PartyRunicPower? power,
        Func<int,Color> frame,bool? combat,bool executing,HealingThresholds thresholds,string rosterStatus,string powerStatus)
    {
        var current=new Dictionary<string,(string State,string Text)>();
        void Add(string key,string state,string text)=>current[key]=(state,text);
        string identity=roster is null?"无有效队伍":$"会话={roster.SessionA}/{roster.SessionB}，队伍版本={roster.Epoch}";
        string mode=$"{rosterStatus}；{identity}；执行={(executing?"开启":"关闭（仅诊断）")}";
        Add("mode",mode,mode);
        if(roster is not null) for(int i=0;i<5;i++) {
            int slot=i+1;var m=roster.Members[i];string name=i==0?"自己":"party"+i;
            string memberIdentity=$"{identity}，成员ID={m.Id}";
            string details=m.Id==0?$"{name}：空位":$"{name}：{memberIdentity}；职业={PartyRosterPanel.ClassName(m.ClassId)}({m.ClassId})；"+
                $"专精={PartyRosterPanel.SpecName(m.SpecId)}({m.SpecId})；队伍职责={PartyRosterPanel.RoleName(m.AssignedRole)}；"+
                $"识别={PartyRosterPanel.RoleName(m.Role)}；依据={(m.HasKnownSpecialization?"专精确认":m.Role!=0?"职责推断":"资料不足／冲突")}；在线={m.Online}";
            Add("member:"+slot,memberIdentity+"|"+details,details);
            if(m.Id==0 || m.ClassId!=6) continue;
            var hp=TeamHealthReading.Read(frame(50+slot));bool? enough=power?.At(roster,slot);
            string energy=!m.IsBloodTank?"未确认在线鲜血专精，不使用符能":enough==true?"≥45（实时）":enough==false?"<45（实时）":
                power is null?"未知／已失效："+powerStatus:!power.Roster.Matches(roster)?"未知：资料与能量身份不匹配":"未知：该成员能量采集无效／受限";
            Add("power:"+slot,memberIdentity+"|"+energy,$"{name}：{memberIdentity}；符文能量={energy}");
            string why;
            if(!m.Online || hp.Status==TeamHealthStatus.Offline) why="未生效：成员离线，不参与治疗排序";
            else if(!m.IsBloodTank) why="未生效：鲜血专精未确认，按有效职责／最低血量处理";
            else if(combat!=true) why="未生效：非战斗或战斗状态未知";
            else if(hp.Percent is null) why="未生效：血量未知，不参与选人";
            else if(hp.Percent<=0) why="未生效：成员死亡，不参与选人";
            else if(hp.Percent<thresholds.Critical) why="未生效：进入危急档，按实际血量抢救";
            else if(hp.Percent<PartyHealingPriority.BloodProtection) why="未生效：血量低于40%保护线，恢复普通T优先（排序−5）";
            else if(enough is null) why="未生效：实时能量未知／过期，恢复普通T优先（排序−5）";
            else if(enough==false) why="未生效：符能不足45点，恢复普通T优先（排序−5）";
            else if(hp.Percent>=thresholds.HealBelow) why="条件满足，当前无需单体治疗：血量已达到治疗线";
            else if(slot>1 && FuryBuffs.Boolean(frame(40+slot))!=true) why="未参与选人：治疗范围外／范围未知";
            else why="降权生效：鲜血专精确认、符能≥45、血量≥40%，同一伤势档内排序+5；危急抢救不变";
            string evidence=$"实际血量={(hp.Percent is {} h?h.ToString("P1"):hp.Status.ToString())}；符能={energy}；"+
                $"危急线={thresholds.Critical:P1}；治疗线={thresholds.HealBelow:P1}";
            Add("priority:"+slot,memberIdentity+"|"+why,$"{name}：{memberIdentity}；{why}；{evidence}");
        }
        var changes=new List<PartyLogChange>();
        string Label(string key)=>key.StartsWith("member:")?"小队成员":key.StartsWith("power:")?"血DK能量":key.StartsWith("priority:")?"血DK降权":"小队状态";
        foreach(var item in current) {
            if(previous.TryGetValue(item.Key,out var old) && old.State==item.Value.State) continue;
            string before=previous.TryGetValue(item.Key,out old)?"；此前="+old.Text:"";
            changes.Add(new(item.Key,$"[{Label(item.Key)}] {item.Value.Text}"+before));
        }
        foreach(var item in previous.Where(p=>!current.ContainsKey(p.Key)))
            changes.Add(new(item.Key,$"[{Label(item.Key)}] 旧状态已清除：{rosterStatus}；此前={item.Value.Text}"));
        previous=current;Observed=true;
        Snapshot=string.Join(Environment.NewLine,current.Values.Select(v=>v.Text));
        return changes;
    }
}
