"""Actual raid Lua, secure state snippet, secret transport and binding lifecycle.
python Diagnostics/verify_raid_healing.py <HijiAddon directory> [packet.json]
"""
import json
import sys
from pathlib import Path
from lupa import LuaRuntime

runtime = LuaRuntime(unpack_returned_tuples=True)
runtime.execute(r'''
UIParent={}; HijiAddon={cellWidth=5}; named={}; cells={}; combat=false; raid=false; spec=65; epoch=1
roster={}; target="raid30"; self="raid4"; secretMode=false; failure=nil
function GetServerTime() return 123 end
function GetTime() return .456 end
function UnitClass() return "paladin","PALADIN",2 end
function InCombatLockdown() return combat end
function IsInRaid() return raid end
function UnitExists(unit) return roster[unit]~=nil end
function UnitIsConnected(unit) return roster[unit].online~=false end
function UnitIsDeadOrGhost(unit) return roster[unit].dead==true end
function HijiAddon.GetDiagnosticIdentity() return spec,2 end
local mt={__eq=function() error("secret compared") end,__lt=function() error("secret compared") end,
 __le=function() error("secret compared") end,__add=function() error("secret arithmetic") end,__mul=function() error("secret arithmetic") end}
function secret(value) return setmetatable({value=value},mt) end
function issecretvalue(value) return getmetatable(value)==mt end
function observation(value) if secretMode then return secret(value) end;return value end
function UnitIsUnit(a,b)
 if a=="target" then return observation(b==target) end
 return observation(a==self)
end
function CreateColor(r,g,b,a) return {GetRGBA=function() return r,g,b,a end} end
C_CurveUtil={EvaluateColorFromBoolean=function(v,yes,no)
 if issecretvalue(v) then v=v.value end;return v and yes or no end}
C_SpellBook={IsSpellKnown=function() return true end}
C_Spell={IsSpellInRange=function(id,unit)
 if unit==failure then error("range unavailable") end
 return observation(roster[unit].range~=false) end}
function CreateFrame(kind,name,parent,template)
 if template then assert(not combat,"protected creation during combat") end
 local f={attrs={},scripts={},shown=true,bindings={},name=name,template=template}
 function f:SetSize() end
 function f:SetPoint(_,_,_,x,y) self.index=x/5; assert(y==-1);cells[self.index]=self end
 function f:CreateTexture()
  local t={};self.texture=t
  function t:SetAllPoints() end
  function t:SetColorTexture(r,g,b) self.r=r;self.g=g;self.b=b end
  return t
 end
 function f:Show() self.shown=true end
 function f:Hide() self.shown=false end
 function f:RegisterForClicks(...) self.clicks={...} end
 function f:SetAttribute(k,v) assert(not combat);self.attrs[k]=v end
 function f:GetAttribute(k) return self.attrs[k] end
 function f:RegisterEvent() end
 function f:SetScript(k,v) self.scripts[k]=v end
 function f:ClearBindings() self.bindings={} end
 function f:SetBindingClick(priority,key,name,button) assert(priority and button=="LeftButton");self.bindings[key]=name end
 if name then named[name]=f end
 return f
end
function Drive()
 local owner=named.HijiAddonRaidBindings
 if not owner then return end
 local state=raid and spec==65 and "raid" or "none"
 owner.attrs["state-raid"]=state
 local run=assert(load("return function(self,newstate) "..owner.attrs["_onstate-raid"].." end"))()
 run(owner,state)
end
function RegisterStateDriver(owner,state,condition)
 assert(not combat and state=="raid" and condition=="[group:raid,spec:1] raid; none")
 Drive()
end
function Event(name) named.HijiAddonRaidHealingEvents.scripts.OnEvent(nil,name) end
function Packet()
 local result={}
 for i=1,49 do
  local c=cells[i];assert(c and c.shown);local t=c.texture
  result[i]={math.floor(t.r*255+.5),math.floor(t.g*255+.5),math.floor(t.b*255+.5)}
 end
 return result
end
''')
root = Path(sys.argv[1])
runtime.execute((root / "RaidHealing.lua").read_text(encoding="utf-8-sig"))
runtime.execute(r'''
combat=true;Event("PLAYER_LOGIN");assert(named.HijiAddonRaidBindings==nil)
combat=false;Event("PLAYER_REGEN_ENABLED");assert(named.HijiAddonRaidBindings)
assert(next(named.HijiAddonRaidBindings.bindings)==nil)
raid=true;Drive();Event("GROUP_ROSTER_UPDATE")
local expected={}
for i=1,12 do expected[i]="SHIFT-F"..i end
for i,key in ipairs({"1","2","3","4","5","6","7","8","9","0","-","=","[","]","\\",";","'","."}) do expected[12+i]="CTRL-"..key end
local count=0
for _ in pairs(named.HijiAddonRaidBindings.bindings) do count=count+1 end
assert(count==30)
for i,key in ipairs(expected) do
 local name=named.HijiAddonRaidBindings.bindings[key];assert(name=="HijiAddonRaidTarget"..i)
 local b=named[name];assert(b.attrs["unit"]=="raid"..i and b.attrs["*type1"]=="target")
 assert(b.clicks[1]=="AnyDown" and b.clicks[2]=="AnyUp")
end
for i=1,40 do roster["raid"..i]={} end
roster.raid2.online=false;roster.raid3.dead=true;roster.raid5.range=false
HijiAddon.UpdateRaidHealing();p=Packet()
assert(p[1][1]==255 and p[2][1]==0 and p[3][1]==0 and p[5][1]==0)
assert(p[30][2]==255 and p[4][3]==255 and p[49][1]==255)
local old=p[42][1];combat=true;Event("GROUP_ROSTER_UPDATE");HijiAddon.UpdateRaidHealing();assert(Packet()[42][1]~=old)
secretMode=true;HijiAddon.UpdateRaidHealing();p=Packet();assert(p[30][2]==255 and p[4][3]==255 and p[1][1]==255)
failure="raid7";HijiAddon.UpdateRaidHealing();assert(Packet()[7][1]==128);failure=nil
raid=false;Drive();HijiAddon.UpdateRaidHealing();assert(next(named.HijiAddonRaidBindings.bindings)==nil and not cells[41].shown)
raid=true;spec=66;Drive();HijiAddon.UpdateRaidHealing();assert(next(named.HijiAddonRaidBindings.bindings)==nil and not cells[41].shown)
spec=65;Drive();HijiAddon.UpdateRaidHealing();p=Packet()
HijiAddon.diagnosticTestMode=true;local before=p[43][1];HijiAddon.UpdateRaidHealing();assert(Packet()[43][1]==before)
HijiAddon.ResetRaidHealing();assert(not cells[41].shown)
HijiAddon.diagnosticTestMode=false;HijiAddon.UpdateRaidHealing();p=Packet()
''')
if len(sys.argv) > 2:
    p = runtime.globals().p
    Path(sys.argv[2]).write_text(json.dumps({str(i): [p[i][j] for j in (1, 2, 3)] for i in range(1, 50)}), encoding="utf-8")
print("PASS: 30 raid bindings, combat deferral, secure activation/restoration, opaque booleans, range/death/offline, roster epoch, calibration and spec cleanup")
