#nullable enable
using System.Drawing;

namespace RotationUS.Diagnostics;

internal static class HolyPriorityTests
{
    internal static int Run()
    {
        int count=0;
        void Check(bool ok,string why) {if(!ok) throw new Exception("FAIL Unified Holy priority: "+why);count++;}
        int Spell(int key)=>key switch {1 or 11=>11,2 or 9=>9,3 or 12=>12,_=>key};
        Dictionary<int,Color> Frame(bool self,bool raid,double hp)
        {
            var f=Enumerable.Range(1,100).ToDictionary(i=>i,_=>Color.Black);
            foreach(int id in new[]{1,2,3,4,5,9,10,19,20,42,43,44,45,96,100}) f[id]=Color.White;
            f[41]=raid?Color.White:Color.Black;f[self?46:47]=Color.White;
            int n=(int)Math.Round(hp*255);
            f[6]=self?Color.FromArgb(n,n,n):Color.White;f[7]=Color.White;f[8]=Color.FromArgb(n,n,n);
            for(int slot=1;slot<=(raid?40:5);slot++) f[50+slot]=Color.FromArgb(slot==(self?1:2)?n:255,255,0);
            f[93]=f[94]=Color.Orange;
            return f;
        }
        void Skill(Dictionary<int,Color> f,int n) {f[20+n]=f[30+n]=Color.White;}
        foreach(bool raid in new[]{false,true})
        foreach(double hp in new[]{.15,.55,.70})
        foreach(int power in new[]{0,3,5})
        foreach(int charges in new[]{0,1,2})
        foreach(bool infusion in new[]{false,true})
        foreach(bool hand in new[]{false,true})
        foreach(bool free in new[]{false,true}) {
            var keys=new List<int>();
            foreach(bool self in new[]{false,true}) {
                var f=Frame(self,raid,hp);
                foreach(int n in new[]{3,5,6}) Skill(f,n);
                if(charges>0) Skill(f,2);
                f[13]=charges==2?Color.White:Color.Black;
                f[91]=power>=3?Color.White:Color.Black;f[92]=power==5?Color.White:Color.Black;
                var buff=new HolyBuffs {Infusion=infusion,HandOfDivinity=hand,DivinePurpose=free};
                keys.Add(Spell(HOLY.Decide(f,buff).Key));
            }
            bool selfRescue=hp<.2&&(power>=3||free);
            if(selfRescue) Check(keys[1]==9,"critical self spends available Holy Power before other direct healing");
            else Check(keys[0]==keys[1],$"same spell for self/ally outside self-spender rescue: raid={raid}, hp={hp}, power={power}, charges={charges}, infusion={infusion}, hand={hand}, free={free}");
            if(!free&&power<5&&charges==2) Check(keys[0]==8,"ally two charges precede infused Flash and ordinary spenders");
            if(!free&&power<5&&charges==1&&infusion) Check(keys[0]==11,"ally one charge follows infused Flash");
        }
        foreach(bool raid in new[]{false,true}) {
            var f=Frame(true,raid,.15);Skill(f,2);Skill(f,3);Skill(f,5);
            f[13]=f[91]=Color.White;
            var buff=new HolyBuffs {Infusion=true,DivinePurpose=false};
            Check(HOLY.Decide(f,buff).Key==2,"self rescue spends three HP ahead of two-charge Shock and infused Flash");
            foreach(int id in new[]{23,33}) {
                foreach(var color in new[]{Color.Black,Color.Orange}) {
                    f[id]=color;
                    Check(HOLY.Decide(f,buff).Key==8,"unavailable or unknown self spender immediately falls back to Shock");
                }
                f[id]=Color.White;
            }
            f[91]=Color.Black;Check(HOLY.Decide(f,buff).Key==8,"insufficient power uses another immediate self heal");
            f[91]=Color.Orange;Check(HOLY.Decide(f,buff).Key==8,"unknown power cannot authorize self spender");
            buff.DivinePurpose=true;Check(HOLY.Decide(f,buff).Key==2,"confirmed free proc authorizes self spender with unknown power");
            f[3]=f[4]=f[5]=f[46]=Color.Black;
            Check(HOLY.Decide(f,buff).Key==2,"self spender macro needs no friendly target selection");
        }
        foreach(bool self in new[]{false,true}) {
            var f=Frame(self,false,.70);Skill(f,6);f[10]=Color.Black;
            var buff=new HolyBuffs {Infusion=false,HandOfDivinity=true};
            Check(HOLY.Decide(f,buff).Key==0,"moving with ordinary injury preserves Hand instead of spending it merely because it exists");
            Skill(f,9);f[53]=Color.FromArgb(179,255,0);
            Check(HOLY.Decide(f,buff).Key!=15,"reserved Hand alone cannot authorize Virtue without executable follow-up");
            f=Frame(self,false,.55);Skill(f,6);f[10]=Color.Black;
            Check(Spell(HOLY.Decide(f,buff).Key)==12,"major injury permits Hand while moving");
            Check(HOLY.Decide(f,buff,new(Major:.50)).Key==0,"Hand follows configured major-injury threshold");
        }
        // Group cooldowns must not be preempted solely by an early self macro.
        foreach(int cooldown in new[]{4,9}) {
            var keys=new List<int>();
            foreach(bool self in new[]{false,true}) {
                var f=Frame(self,false,.70);f[53]=f[54]=Color.FromArgb(191,255,0);
                Skill(f,cooldown);Skill(f,5);
                keys.Add(HOLY.Decide(f,new(){Infusion=true},cooldowns:new(Healing:new(new PartyTollReading(true),TollConfirmed:true))).Key);
            }
            Check(keys.All(key=>key==(cooldown==4?4:15)),"same group cooldown priority for self and ally");
        }
        return count;
    }
}
