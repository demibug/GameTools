"""Execute the real Retribution collector with native API mocks; never sends input."""
import re
import runpy
import sys
from pathlib import Path

runtime = runpy.run_path(str(Path(__file__).with_name("verify_addon.py")))["runtime"]
root = Path(sys.argv[1])
runtime.execute(r'''
local create=CreateFrame
function CreateFrame(...)
    local f=create(...)
    function f:SetAllPoints() end
    function f:SetValue(v) assert(v~=nil);self.value=v end
    return f
end
function CreateColor(r,g,b,a) return {GetRGBA=function() return r,g,b,a end} end
Enum={LuaCurveType={Step=1},PowerType={HolyPower=9}}
local function curve()
    return {points={},SetType=function(self,t) self.type=t end,
        AddPoint=function(self,x,y) table.insert(self.points,{x,y}) end}
end
local meta={__lt=function() error('secret comparison') end,__le=function() error('secret comparison') end,
    __add=function() error('secret arithmetic') end}
function secret(v) return setmetatable({value=v},meta) end
function issecretvalue(v) return getmetatable(v)==meta end
C_CurveUtil={CreateCurve=curve,CreateColorCurve=curve,EvaluateColorFromBoolean=function(v,yes,no)
    if issecretvalue(v) then v=v.value end;return v and yes or no end}
power=5;enemyCount=1;health=.8;tracking={};overrides={};cooldowns={};charges={};unusable={};ranges={};castTime=1500
unknown={[458359]=true};targetCast=false;locked=false;items={};itemEnabled=1
function UnitPower(_,kind) assert(kind==9);if power==nil then error('power unavailable') end;return secret(power) end
function UnitHealthPercent(_,_,c)
    if health==nil then error('health unavailable') end
    if c.type==Enum.LuaCurveType.Step then
        local result=c.points[1][2]
        for _,p in ipairs(c.points) do if health>=p[1] then result=p[2] end end
        return result
    end
    return health
end
function UnitExists(unit) local n=unit:match('nameplate(%d+)');return not n or tonumber(n)<=enemyCount end
function UnitIsDeadOrGhost() return false end
function UnitAffectingCombat() return true end
function IsMounted() return false end
function UnitCanAttack() return true end
function CheckInteractDistance() return true end
function UnitCastingInfo(unit) if unit=='target' and targetCast then return 'spell',nil,nil,nil,nil,nil,nil,locked end end
function UnitChannelInfo() end
C_Minimap={GetNumTrackingTypes=function() if tracking==nil then error('tracking unavailable') end;return #tracking end,
    GetTrackingInfo=function(i) return tracking[i] end}
C_SpellBook={IsSpellKnown=function(id) return not unknown[id] end}
function IsPlayerSpell(id) return not unknown[id] end
C_Item={GetItemCount=function(id) return items[id] or 0 end,GetItemCooldown=function() return 0,0,itemEnabled end,
    IsItemInRange=function() return true end}
C_Spell={GetOverrideSpell=function(id) return overrides[id] or id end,
    GetSpellInfo=function(id) assert(id==19750 or id==999);return {castTime=castTime} end,
    IsSpellUsable=function(id) return not unusable[id] end,
    IsSpellInRange=function(id) if ranges[id]=='nil' then return nil end;return ranges[id]==nil or ranges[id] end,
    GetSpellCharges=function(id) if charges[id] then return {currentCharges=secret(charges[id])} end end,
    GetSpellCooldownDuration=function(id,ignoreGCD)
        assert(ignoreGCD==true or id==61304)
        if cooldowns[id]=='nil' then return nil end
        return {EvaluateRemainingDuration=function(_,c)
            local value=cooldowns[id] or 0;local result=c.points[1][2]
            for _,p in ipairs(c.points) do if value>=p[1] then result=p[2] end end
            return result
        end}
    end}
''')
assert "Classes/Cjq.lua" in (root / "HijiAddon.toc").read_text(encoding="utf-8-sig").replace("\\", "/")
runtime.execute((root / "Classes/Cjq.lua").read_text(encoding="utf-8-sig"))
runtime.execute(r'''
local targetEvent=frames[#frames]
local function f(i,r,g,b) assert(rgb(HijiAddon['texture_'..i],r,g or r,b or r),'frame '..i) end
local function flag(i,r,g,b) assert(rgb(HijiAddon['bar_'..i]:GetStatusBarTexture(),r,g or r,b or r),'bar '..i) end
HijiAddon.ClearDiagnosticData();HijiAddon.UpdateCjq()
f(19,70/255,2/255,2/255);f(50,1);f(5,0);f(9,1)
flag(19,0);flag(20,0);flag(21,1);flag(22,1);flag(27,0)
health=.79999;castTime=0;HijiAddon.UpdateCjq();flag(19,1);flag(20,0);flag(27,1)
health=.6;HijiAddon.UpdateCjq();flag(19,1);flag(20,0)
health=.59999;HijiAddon.UpdateCjq();flag(19,1);flag(20,1)
castTime=secret(0);HijiAddon.UpdateCjq();flag(27,1,.5,0)
castTime=nil;HijiAddon.UpdateCjq();flag(27,1,.5,0)
castTime=0;overrides[19750]=999;cooldowns[999]=2;unusable[85673]=true
HijiAddon.UpdateCjq();flag(23,0);flag(24,1);flag(22,0);flag(27,1)
overrides[19750]=nil;unusable[85673]=nil;castTime=1500;health=.8
assert(HijiAddon.bar_1.max==5 and HijiAddon.bar_1.value.value==5)
assert(HijiAddon.retThresholdBars[7].min==2 and HijiAddon.retThresholdBars[8].min==4)
assert(issecretvalue(HijiAddon.retThresholdBars[8].value))
f(24,0);f(30,1);flag(6,0);flag(7,0);flag(11,0);flag(12,1)
cooldowns[61304]=1;cooldowns[31884]=20;cooldowns[343527]=30;enemyCount=2
HijiAddon.UpdateCjq();f(16,0);f(18,1);f(20,0);f(22,0);flag(16,1);f(30,1);f(49,1);f(5,1)
-- Charges remain usable during recharge and use native secret display.
charges[184575]=1;cooldowns[184575]=8
HijiAddon.UpdateCjq();assert(HijiAddon.retThresholdBars[34].shown and HijiAddon.retThresholdBars[34].value.value==1)
-- Temporary hammer does not have to be known independently.
unknown[427453]=true;unknown[429826]=true;overrides[255937]=427453
HijiAddon.UpdateCjq();f(24,1);f(30,0);flag(6,1);flag(7,0)
overrides[255937]=429826;overrides[20271]=24275
HijiAddon.UpdateCjq();f(24,1);f(30,0);flag(7,1);f(17,1)
-- Native range bool is displayed directly, never compared in Lua.
ranges[24275]=secret(false);HijiAddon.UpdateCjq();f(47,0)
overrides[255937]=255937;ranges[255937]='nil';ranges[53385]='nil';ranges[343527]='nil'
HijiAddon.UpdateCjq();f(44,1);f(43,1);f(40,1,.5,0)
power=nil;health=nil;tracking=nil;overrides[255937]=secret(427453)
HijiAddon.UpdateCjq();f(7,1,.5,0);f(8,1,.5,0);f(9,1,.5,0);f(6,1,.5,0);f(10,1,.5,0);f(11,1,.5,0)
f(24,1,.5,0);f(30,1,.5,0);flag(6,1,.5,0);f(50,1)
flag(19,1,.5,0);flag(20,1,.5,0)
assert(not HijiAddon.retThresholdBars[7].shown and not HijiAddon.retThresholdBars[8].shown)
assert(HijiAddon.bar_1.value==0)
-- Recover all sources; interrupt restrictions must remain unknown.
power=3;health=.4;overrides[255937]=255937
tracking={{name='战场军官',active=true},{name='餐饮供应商',active=true}}
targetCast=true;locked=secret(false);items[5512]=1;items[211879]=1
HijiAddon.UpdateCjq();f(10,1);f(11,1);f(12,1);f(13,1,.5,0);f(14,1);f(15,1)
locked=false;itemEnabled=false;HijiAddon.UpdateCjq();f(13,1);f(14,0);f(15,0)
locked=true;itemEnabled=true;HijiAddon.UpdateCjq();f(13,0);f(14,1)
itemEnabled=0;HijiAddon.UpdateCjq();f(14,0)
itemEnabled=secret(1);HijiAddon.UpdateCjq();f(14,1,.5,0)
cooldowns[31884]='nil';HijiAddon.UpdateCjq();f(18,1,.5,0);f(20,1,.5,0)
flag(14,1/255,70/255,1/255);targetEvent.scripts.OnEvent(targetEvent,'PLAYER_TARGET_CHANGED')
HijiAddon.UpdateCjq();flag(14,2/255,70/255,1/255)
local threshold=HijiAddon.retThresholdBars[8];local flagBar=HijiAddon.bar_6
HijiAddon.ClearDiagnosticData();assert(not threshold.shown and not flagBar.shown and HijiAddon.bar_6==nil)
HijiAddon.UpdateCjq();assert(threshold.shown and HijiAddon.bar_6.shown)
''')
program = (Path(__file__).parent.parent / "Program.cs").read_text(encoding="utf-8-sig")
for action,key in [(1,"7"),(2,"8"),(3,"9")]:
    assert re.search(rf"CheckPt\({action}\)\)\s*\{{\s*SimulateKeyPress\(VK_{key}\)",program),action
punctuation = ["FangkuohaoZuo", "FangkuohaoYou", "Xiegang", "Fenhao", "Danyinhao", "Juhao"]
for action in range(7, 31):
    key = punctuation[(action - 7) % 6] if action < 19 else ["1", "2", "3", "4", "5", "6", "7", "8", "9", "0", "Jian", "Deng"][action - 19]
    method = "SimulateKeyPress" if action < 13 else "SimulateShiftKeyPress"
    assert re.search(rf"CheckPt\({action}\)\)\s*\{{\s*{method}\(VK_{key}\)", program), action
print("PASS: Retribution collector, secret resources/native display, overrides, own CD/GCD, charges, unknown/recovery, range fallback, interruptability, items, target epoch, strict 60/80 health thresholds, instant Flash, cleanup and 27 keyboard mappings.")
