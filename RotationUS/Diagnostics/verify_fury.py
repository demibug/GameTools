"""Fury Lua behavior with synthetic WoW APIs, no game input.
Run: uv run --with lupa python Diagnostics/verify_fury.py <addon path>
"""
import sys
from pathlib import Path
from lupa import LuaRuntime

root = Path(sys.argv[1])
runtime = LuaRuntime(unpack_returned_tuples=True)
runtime.execute(r'''
UIParent={}; named={}; now=0; spec=72; classID=1; enemies=1; rage=100
cooldowns={}; overrides={}; usable={}; unknownSpells={}; overrideChargesMissing=false; chargeCount=1; auraRestricted=false; auraError=false; auraStacks=nil
castRestricted=false; mounted=false; playerCasting=false; trackingOfficer=false; trackingFood=false
function GetTime() return now end
function UnitClass() return "player","WARRIOR",classID end
C_SpecializationInfo={GetSpecialization=function() return 2 end,GetSpecializationInfo=function() return spec end}
function IsLoggedIn() return true end
function CreateColor(r,g,b,a)
 local c={r=r,g=g,b=b,a=a}; function c:GetRGBA() return self.r,self.g,self.b,self.a end; return c
end
function issecretvalue(v) return type(v)=="table" and v.secret==true end
function reveal(v) if issecretvalue(v) then return v.value end; return v end
Enum={LuaCurveType={Step=1},PowerType={Rage=1}}
C_CurveUtil={}
function C_CurveUtil.CreateColorCurve()
 local c={points={}}
 function c:SetType(v) self.kind=v end
 function c:AddPoint(x,y) table.insert(self.points,{x=x,y=y}) end
 function c:Evaluate(value)
  local x=reveal(value); local result=self.points[1].y
  for _,p in ipairs(self.points) do if x>=p.x then result=p.y end end
  return result
 end
 return c
end
C_CurveUtil.CreateCurve=C_CurveUtil.CreateColorCurve
function C_CurveUtil.EvaluateColorFromBoolean(v,yes,no) if reveal(v) then return yes else return no end end
function CreateFrame(kind,name)
 local f={shown=true,scripts={},textures={}}
 function f:SetSize(w,h) self.w=w;self.h=h end
 function f:SetPoint(...) self.point={...} end
 function f:ClearAllPoints() self.point=nil end
 function f:CreateTexture()
  local t={};function t:SetTexture() end;function t:SetAllPoints() end
  function t:SetVertexColor(r,g,b,a) self.r=r;self.g=g;self.b=b;self.a=a end
  t.SetColorTexture=t.SetVertexColor; table.insert(self.textures,t);return t
 end
 function f:Show() self.shown=true end;function f:Hide() self.shown=false end
 function f:SetStatusBarTexture() self.texture=self:CreateTexture() end
 function f:GetStatusBarTexture() return self.texture end
 function f:SetMinMaxValues(a,b) self.min=a;self.max=b end
 function f:SetValue(value) self.value=value end
 function f:RegisterEvent() end;function f:RegisterUnitEvent() end
 function f:SetScript(event,callback) self.scripts[event]=callback end
 if name then named[name]=f end;return f
end
SlashCmdList={};function print() end
function UnitAffectingCombat() return true end
function UnitCanAttack() return true end
function UnitExists(unit) local n=unit:match("nameplate(%d+)"); return not n or tonumber(n)<=enemies end
function UnitIsDead() return false end
function IsMounted() return mounted end
function UnitCastingInfo(unit) if unit=="player" and playerCasting then return "cast" end end
function UnitChannelInfo() return nil end
function CheckInteractDistance() return true end
function UnitPower() return rage end
function UnitHealthPercent() return .9 end
function UnitGetTotalAbsorbs() return 0 end
function IsPlayerSpell(id) return not unknownSpells[id] end
C_Item={IsItemInRange=function() return true end,GetItemCount=function() return 1 end,GetItemCooldown=function() return 0,0,1 end}
C_Minimap={GetNumTrackingTypes=function() return 2 end,GetTrackingInfo=function(i) return {name=i==1 and "战场军官" or "餐饮供应商",active=i==1 and trackingOfficer or trackingFood} end}
C_SpellBook={IsSpellKnown=function(id) return not unknownSpells[id] end}
C_Spell={}
function C_Spell.GetOverrideSpell(id) return overrides[id] or id end
function C_Spell.IsSpellUsable(id) return usable[id]~=false end
function C_Spell.GetSpellCooldownDuration(id,ignoreGCD)
 local d={};function d:EvaluateRemainingDuration(curve) return curve:Evaluate(cooldowns[id] or 0) end;return d
end
function C_Spell.GetSpellCharges(id)
 if id==335097 and overrideChargesMissing then return nil end
 if id==85288 or id==335097 then return {currentCharges=chargeCount} end
end
C_AssistedCombat={GetNextCastSpell=function() return 0 end}
C_Secrets={ShouldSpellAuraBeSecret=function() return auraRestricted end}
C_UnitAuras={GetPlayerAuraBySpellID=function(id)
 if auraError then error("restricted API") end
 if auraStacks==nil then return nil end
 return {spellId=id,applications=auraStacks}
end}
function white(i) local t=HijiAddon["texture_"..i];return t.r==1 and t.g==1 and t.b==1 end
function black(i) local t=HijiAddon["texture_"..i];return t.r==0 and t.g==0 and t.b==0 end
function unknown(i) local t=HijiAddon["texture_"..i];return t.r==1 and t.g==.5 and t.b==0 end
''')
for file in ("Init.lua", "Frames.lua", "Bars.lua", "Diagnostics.lua", "Classes/Kbz.lua", "ClassDriver.lua"):
    runtime.execute((root / file).read_text(encoding="utf-8-sig"))
runtime.execute(r'''
assert(HijiAddon.extraBgFrame.w==200 and HijiAddon.extraBgFrame.h==20)
assert(named.HijiAddonDiagnosticMarker1.point[4]==740)
assert(named.HijiAddonDiagnosticMarker3.point[4]==760)
local blackRight=HijiAddon.extraBgFrame.point[4]+HijiAddon.extraBgFrame.w
for i=1,3 do assert(named["HijiAddonDiagnosticMarker"..i].point[4]>=blackRight+10) end
HijiAddon.UpdateKbz()
assert(black(29) and white(42)) -- No record is absent, immediately.
assert(black(7)) -- Rage exactly 100.
rage=101;HijiAddon.UpdateKbz();assert(white(7))
rage={secret=true,value=100};HijiAddon.UpdateKbz();assert(black(7))
rage={secret=true,value=101};HijiAddon.UpdateKbz();assert(white(7))
enemies=2;HijiAddon.UpdateKbz();assert(black(2) and black(9))
enemies=3;HijiAddon.UpdateKbz();assert(white(2) and black(9))
enemies=6;HijiAddon.UpdateKbz();assert(white(2) and black(9))
enemies=7;HijiAddon.UpdateKbz();assert(white(2) and white(9))
chargeCount={secret=true,value=1};HijiAddon.UpdateKbz()
assert(white(43) and HijiAddon.bar_1.value.value==1 and HijiAddon.bar_1.max==2)
cooldowns[61304]=1;cooldowns[1719]=0;HijiAddon.UpdateKbz()
assert(black(28) and white(19) and black(35)) -- GCD does not count as own cooldown.
cooldowns[1719]=15;HijiAddon.UpdateKbz();assert(black(19) and white(35))
cooldowns[1719]=0;cooldowns[61304]=0
local event=named.HijiAddonFuryEvents.scripts.OnEvent
event(nil,"UNIT_SPELLCAST_SUCCEEDED","target","guid",1719)
HijiAddon.UpdateKbz();assert(black(29))
event(nil,"UNIT_SPELLCAST_SUCCEEDED","player","guid",1719)
HijiAddon.UpdateKbz();assert(white(29))
now=11.99;HijiAddon.UpdateKbz();assert(white(29))
now=12;HijiAddon.UpdateKbz();assert(black(29))
event(nil,"UNIT_SPELLCAST_SUCCEEDED","player","guid",1719)
now=15;event(nil,"UNIT_SPELLCAST_SUCCEEDED","player","guid",1719)
now=26;HijiAddon.UpdateKbz();assert(white(29))
now=27;HijiAddon.UpdateKbz();assert(black(29))
event(nil,"UNIT_SPELLCAST_SUCCEEDED","player","guid",{secret=true,value=1719})
HijiAddon.UpdateKbz();assert(unknown(29))
event(nil,"PLAYER_DEAD");HijiAddon.UpdateKbz();assert(black(29))
HijiAddon.HandleFuryCommand("fury duration 18")
event(nil,"UNIT_SPELLCAST_SUCCEEDED","player","guid",1719)
now=44.99;HijiAddon.UpdateKbz();assert(white(29))
now=45;HijiAddon.UpdateKbz();assert(black(29))
event(nil,"UNIT_SPELLCAST_SUCCEEDED","player","guid",1719)
event(nil,"TRAIT_CONFIG_UPDATED");HijiAddon.UpdateKbz();assert(black(29))
overrides[6343]=435222;overrides[23881]=335096;overrides[85288]=335097
HijiAddon.UpdateKbz();assert(white(32) and white(33) and white(34))
unknownSpells[335096]=true;unknownSpells[335097]=true;unknownSpells[435222]=true
overrideChargesMissing=true;HijiAddon.UpdateKbz()
assert(white(24) and white(25) and white(27) and white(38) and white(39) and white(41))
assert(white(43) and HijiAddon.bar_1.value.value==1) -- Base spellbook/charge data supports overrides.
unknownSpells={};overrideChargesMissing=false
overrides[6343]=6343;overrides[23881]=23881;overrides[85288]=85288
HijiAddon.UpdateKbz();assert(black(32) and black(33) and black(34))
assert(black(44)) -- No unverified aura ID silently declared exact.
HijiAddon.HandleFuryCommand("fury aura thunder 999999")
for _,stacks in ipairs({0,1,2}) do
 auraStacks=stacks;HijiAddon.UpdateKbz()
 assert(white(44) and HijiAddon.bar_3.value==stacks and HijiAddon.bar_3.max==2)
end
auraRestricted=true;HijiAddon.UpdateKbz();assert(unknown(44))
auraRestricted=false;auraError=true;HijiAddon.UpdateKbz();assert(unknown(44))
auraError=false;HijiAddon.HandleFuryCommand("fury aura hack 999998")
auraStacks=1;HijiAddon.UpdateKbz();assert(white(45) and white(47))
auraStacks=nil;HijiAddon.UpdateKbz();assert(black(45) and white(47))
auraRestricted=true;HijiAddon.UpdateKbz();assert(unknown(45) and black(47))
local driver=named.HijiAddonClassDriver
now=now+.3;driver.scripts.OnUpdate(driver,.1)
assert(named.HijiAddonDiagnosticMarker1.textures[1].g==1)
local pulse=named.HijiAddonDiagnosticMarker3.textures[1].r
local original=HijiAddon.UpdateKbz;HijiAddon.UpdateKbz=function() error("mock exception") end
now=now+.3;assert(not pcall(driver.scripts.OnUpdate,driver,.1))
assert(named.HijiAddonDiagnosticMarker3.textures[1].r==pulse)
HijiAddon.UpdateKbz=original
''')
print("PASS: Fury Lua synthetic layout, rage 100/101 incl. secret display, 2/3/6/7 enemy bounds, charge forwarding, own cooldown vs GCD, successful-cast timer and resets, overrides, aura 0/1/2 and restricted fallbacks, spec-72 driver heartbeat.")
