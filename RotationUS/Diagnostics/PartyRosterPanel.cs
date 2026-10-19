#nullable enable
using System.Drawing;
using System.Windows.Forms;

namespace RotationUS.Diagnostics;

internal sealed class PartyRosterPanel : UserControl
{
    private readonly Label status=new() {Dock=DockStyle.Top,Height=48,Padding=new Padding(8),AutoEllipsis=true};
    internal readonly DataGridView Members=new BufferedDataGridView() {Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,
        AllowUserToDeleteRows=false,AllowUserToResizeRows=false,RowHeadersVisible=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,
        BackgroundColor=Color.White,BorderStyle=BorderStyle.None,SelectionMode=DataGridViewSelectionMode.FullRowSelect,MultiSelect=false};
    internal PartyRosterPanel()
    {
        Dock=DockStyle.Fill;
        var note=new Label {Dock=DockStyle.Bottom,Height=52,Padding=new Padding(8),Text=
            "位置对应治疗绑定：自己 / party1～party4（游戏队伍框体可能另行排序）。\n血DK仅在符能≥45且血量≥40%时降权；能量未知恢复T优先；资料失效按最低血量。团队停用。"};
        string[] columns={"位置","职业","专精","队伍职责","识别结果","资料状态","血量","实时符能","治疗排序"};
        foreach(string title in columns) Members.Columns.Add(title,title);
        float[] widths={65,85,75,70,105,120,60,110,190};
        for(int i=0;i<widths.Length;i++) {Members.Columns[i].FillWeight=widths[i];Members.Columns[i].SortMode=DataGridViewColumnSortMode.NotSortable;}
        Members.RowTemplate.Height=32;
        Members.DefaultCellStyle.WrapMode=DataGridViewTriState.True;
        Members.ColumnHeadersHeight=30;
        for(int i=0;i<5;i++) Members.Rows.Add();
        Controls.Add(Members);Controls.Add(note);Controls.Add(status);
        Clear("等待脱战取得小队资料");
    }
    internal void Clear(string reason) => Present(null,null,_=>Color.Black,false,reason,"能量已清除");
    internal void Present(PartyRosterSnapshot? roster, PartyRunicPower? power, Func<int,Color> health, bool combat, string rosterStatus, string powerStatus, HealingThresholds? thresholds=null)
    {
        status.Text=rosterStatus+"\n"+powerStatus;
        for(int i=0;i<5;i++) {
            var m=roster?.Members[i]; var row=Members.Rows[i]; int slot=i+1;
            bool occupied=m is {Id:>0};
            var hp=occupied?TeamHealthReading.Read(health(slot)):new(TeamHealthStatus.Empty,null);
            bool? enough=power?.At(roster,slot);
            bool reduced=combat && hp.Percent is {} p && p>=(thresholds??new()).Critical && PartyHealingPriority.BloodReduced(p,slot,roster,power);
            string validity=!occupied?(roster is null?"无有效资料":"空位") : !m!.Online?"离线，不参与" : m.ClassId==0?"职业未知" :
                m.Role==0?"职责未知／资料冲突" : !m.HasKnownSpecialization?"部分有效：专精未知" : combat?"有效：战前确认":"有效";
            string treatment=!occupied?"—" : !m!.Online?"不参与" : m.Role==0?"最低血量" : reduced?"血DK降权（排序+5）" :
                m.IsBloodTank && hp.Percent is >0 and <.40?"低于40%：T优先" : m.Role==1?"T优先（排序−5）" : m.Role==2?"治疗优先（排序−3）":"按实际血量";
            string recognition=!occupied?"—":m!.Role==1?(m.HasKnownSpecialization?"T：专精确认":"T：职责推断"):RoleName(m.Role);
            object[] values={i==0?"自己":"party"+i,occupied?ClassName(m!.ClassId):"—",occupied?SpecName(m!.SpecId):"—",
                occupied?RoleName(m!.AssignedRole):"—",recognition,validity,hp.Percent is {} h?$"{h:P0}":hp.Status==TeamHealthStatus.Offline?"离线":"—",
                m?.IsBloodTank==true?enough==true?"≥45（实时）":enough==false?"<45（实时）":"未知／已失效":"—",treatment};
            for(int j=0;j<values.Length;j++) if(!Equals(row.Cells[j].Value,values[j])) row.Cells[j].Value=values[j];
            var background=reduced?Color.FromArgb(255,244,218):m?.Role==1?Color.FromArgb(227,242,253):Color.White;
            if(row.DefaultCellStyle.BackColor!=background) row.DefaultCellStyle.BackColor=background;
        }
    }
    internal static string RoleName(int role)=>role switch {1=>"坦克",2=>"治疗",3=>"输出",_=>"未知"};
    internal static string ClassName(int id)=>id switch {1=>"战士",2=>"圣骑士",3=>"猎人",4=>"潜行者",5=>"牧师",6=>"死亡骑士",7=>"萨满祭司",8=>"法师",9=>"术士",10=>"武僧",11=>"德鲁伊",12=>"恶魔猎手",13=>"唤魔师",_=>"未知"};
    internal static string SpecName(int id)=>id switch {
        71=>"武器",72=>"狂怒",73=>"防护",65=>"神圣",66=>"防护",70=>"惩戒",253=>"野兽控制",254=>"射击",255=>"生存",
        259=>"奇袭",260=>"狂徒",261=>"敏锐",256=>"戒律",257=>"神圣",258=>"暗影",250=>"鲜血",251=>"冰霜",252=>"邪恶",
        262=>"元素",263=>"增强",264=>"恢复",62=>"奥术",63=>"火焰",64=>"冰霜",265=>"痛苦",266=>"恶魔学识",267=>"毁灭",
        268=>"酒仙",270=>"织雾",269=>"踏风",102=>"平衡",103=>"野性",104=>"守护",105=>"恢复",577=>"浩劫",581=>"复仇",1480=>"噬灭",
        1467=>"湮灭",1468=>"恩护",1473=>"增辉",0=>"未知",_=>$"未知({id})"};
}
