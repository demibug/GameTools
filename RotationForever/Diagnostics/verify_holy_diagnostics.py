"""Real Holy Lua: detailed point26/36 telemetry and level-sync disabled gates.
Run: uv run --with lupa python Diagnostics/verify_holy_diagnostics.py <addon directory>
"""
import runpy
from pathlib import Path

context = runpy.run_path(str(Path(__file__).with_name("verify_holy.py")))
runtime = context["runtime"]
runtime.execute(r'''
class,spec="PALADIN",1
dead={};unknownSpells={};unusable={};overrides={};cooldowns={};charges=1
local oldDuration=C_Spell.GetSpellCooldownDuration
local oldInfo=C_Spell.GetSpellCooldown
local oldUsable=C_Spell.IsSpellUsable
local oldOverride=C_Spell.GetOverrideSpell
local oldKnown=C_SpellBook.IsSpellKnown
local oldPlayerSpell=IsPlayerSpell
function UnitExists() return true end
function UnitLevel() return 90 end
function UnitEffectiveLevel() return 60 end
C_Spell.GetSpellLevelLearned=function(id) assert(id==82326 or id==123456);return 70 end
local disabled={}
C_Spell.IsSpellDisabled=function(id) return disabled[id] or false end
local function cell(index)
    for i=#frames,1,-1 do
        local f=frames[i]
        if f.shown and f.point and f.point[4]==index*HijiAddon.cellWidth and f.point[5]==-1 then
            return f.textures[1]
        end
    end
end
local function code(index,stage,reason)
    local t=cell(index)
    assert(t and t.r==stage/255 and t.g==reason/255,"wrong diagnostic cell "..index.." expected="..stage.."/"..reason)
end
local function observations(known,disabled)
    assert(cell(96).b==known/255 and cell(97).b==disabled/255,"wrong independent knowledge / disabled observations")
end
local function baseline()
    C_Spell.GetSpellCooldownDuration=oldDuration;C_Spell.GetSpellCooldown=oldInfo
    C_Spell.IsSpellUsable=oldUsable;C_Spell.GetOverrideSpell=oldOverride
    C_SpellBook.IsSpellKnown=oldKnown;IsPlayerSpell=oldPlayerSpell
    C_Spell.IsSpellDisabled=function(id) return disabled[id] or false end
    unknownSpells={};disabled={};unusable={};overrides={}
end
HijiAddon.UpdateHoly()
assert(rgb(cell(95),72/255,68/255,2/255) and rgb(cell(100),72/255,68/255,2/255))
assert(rgb(cell(98),150/255,65/255,1/255) and rgb(cell(99),90/255,60/255,70/255))
code(96,0,0);code(97,0,0);observations(1,2)

-- No shared failure string: each interface is identified independently.
C_Spell.GetSpellCooldownDuration=function(id,...)
    if id==82326 then error("duration exploded") end;return oldDuration(id,...)
end
C_Spell.IsSpellUsable=function(id) if id==82326 then return nil end;return oldUsable(id) end
HijiAddon.UpdateHoly();code(96,4,1);code(97,9,2)
assert(rgb(HijiAddon.texture_26,1,.5,0) and rgb(HijiAddon.texture_36,1,.5,0))
baseline();HijiAddon.UpdateHoly();code(96,0,0);code(97,0,0)

C_Spell.GetSpellCooldownDuration=function(id,...) if id==82326 then return nil end;return oldDuration(id,...) end
C_Spell.GetSpellCooldown=function(id) if id==82326 then return nil end;return oldInfo(id) end
HijiAddon.UpdateHoly();code(96,5,2);code(97,0,0)
for _,test in ipairs({{nil,6,2},{secret(false),6,3},{1,6,4}}) do
    C_Spell.GetSpellCooldown=function(id)
        if id==82326 then return {isEnabled=test[1],isActive=false} end;return oldInfo(id)
    end
    HijiAddon.UpdateHoly();code(96,test[2],test[3])
end
C_Spell.GetSpellCooldown=function(id)
    if id==82326 then return {isEnabled=true,isActive=secret(false)} end;return oldInfo(id)
end
HijiAddon.UpdateHoly();code(96,7,3)
C_Spell.GetSpellCooldown=function(id)
    if id==82326 then return setmetatable({}, {__index=function() error("field access failed") end}) end;return oldInfo(id)
end
HijiAddon.UpdateHoly();code(96,6,1)
baseline()
C_Spell.GetSpellCooldownDuration=function(id,...)
    if id==82326 then return {EvaluateRemainingDuration=function() error("native evaluate failed") end} end
    return oldDuration(id,...)
end
HijiAddon.UpdateHoly();code(96,8,1)

for _,test in ipairs({{"nil",2},{"secret",3},{"invalid",4},{"error",1}}) do
    baseline()
    C_Spell.GetOverrideSpell=function(id)
        if id~=82326 then return oldOverride(id) end
        if test[1]=="nil" then return nil end
        if test[1]=="secret" then return secret(82326) end
        if test[1]=="invalid" then return "82326" end
        error("override failed")
    end
    HijiAddon.UpdateHoly();code(96,1,test[2]);code(97,1,test[2])
end
baseline();C_SpellBook.IsSpellKnown=function(id) if id==82326 then return secret(true) end;return oldKnown(id) end
HijiAddon.UpdateHoly();code(96,0,0);code(97,0,0);observations(19,2)
assert(rgb(HijiAddon.texture_26,1,.5,0) and rgb(HijiAddon.texture_36,1,1,1),"knowledge failure cannot hide usable evidence or grant readiness")
baseline();unknownSpells[82326]=true;HijiAddon.UpdateHoly();code(96,2,5);code(97,2,5)
baseline()
C_Spell.IsSpellUsable=function(id) if id==82326 then return false,true end;return oldUsable(id) end
HijiAddon.UpdateHoly();code(97,9,8);assert(rgb(HijiAddon.texture_36,0,0,0))
C_Spell.IsSpellUsable=function(id) if id==82326 then return false,false end;return oldUsable(id) end
HijiAddon.UpdateHoly();code(97,9,9)
C_Spell.IsSpellUsable=function(id) if id==82326 then return secret(true),secret(false) end;return oldUsable(id) end
HijiAddon.UpdateHoly();code(97,0,0);assert(rgb(HijiAddon.texture_36,1,1,1),"native secret display still works")
baseline()

-- Disabled skills must not query their cooldown, charges or usability. Clear
-- per-frame decisions when restrictions disappear, without retaining a blacklist.
for i,id in ipairs({20271,20473,85673,375576,19750,82326,31884,31821,200025,498,53600}) do
    disabled={[id]=true}
    C_Spell.GetSpellCooldownDuration=function(spell,...)
        assert(spell~=id,"disabled cooldown queried");return oldDuration(spell,...)
    end
    C_Spell.IsSpellUsable=function(spell)
        assert(spell~=id,"disabled usability queried");return oldUsable(spell)
    end
    HijiAddon.UpdateHoly()
    local ready,usable=i==11 and 97 or 20+i,i==11 and 98 or 30+i
    assert(rgb(HijiAddon["texture_"..ready],0,0,0) and rgb(HijiAddon["texture_"..usable],0,0,0))
    if id==82326 then code(96,3,6);code(97,3,6) end
    baseline();HijiAddon.UpdateHoly()
    assert(rgb(HijiAddon["texture_"..usable],1,1,1),"restriction recovery must be immediate")
end
for _,test in ipairs({{"nil",2},{"secret",3},{"invalid",4},{"error",1}}) do
    C_Spell.IsSpellDisabled=function(id)
        if id~=82326 then return false end
        if test[1]=="nil" then return nil end
        if test[1]=="secret" then return secret(false) end
        if test[1]=="invalid" then return 0 end
        error("disabled check failed")
    end
    HijiAddon.UpdateHoly();code(96,0,0);code(97,0,0);observations(1,16+test[2])
    assert(rgb(HijiAddon.texture_26,1,1,1) and rgb(HijiAddon.texture_36,1,1,1),"auxiliary failure retains independent ready and usable evidence")
    local called={}
    C_Spell.GetSpellCooldownDuration=function(id,...)
        if id==82326 then called.cooldown=true;error("cooldown failed too") end;return oldDuration(id,...)
    end
    C_Spell.IsSpellUsable=function(id)
        if id==82326 then called.usable=true;return false,true end;return oldUsable(id)
    end
    HijiAddon.UpdateHoly();code(96,4,1);code(97,9,8);observations(1,16+test[2])
    assert(called.cooldown and called.usable and rgb(HijiAddon.texture_26,1,.5,0) and rgb(HijiAddon.texture_36,0,0,0),"auxiliary failure never waives failed primary evidence")
    C_Spell.GetSpellCooldownDuration=oldDuration;C_Spell.IsSpellUsable=oldUsable
end
-- All four queries can fail independently in the same update.
baseline()
C_SpellBook.IsSpellKnown=function(id) if id==82326 then error("knowledge failed") end;return oldKnown(id) end
C_Spell.IsSpellDisabled=function(id) if id==82326 then return secret(false) end;return false end
C_Spell.GetSpellCooldownDuration=function(id,...) if id==82326 then error("duration failed") end;return oldDuration(id,...) end
C_Spell.IsSpellUsable=function(id) if id==82326 then return nil end;return oldUsable(id) end
HijiAddon.UpdateHoly();code(96,4,1);code(97,9,2);observations(17,19)
assert(rgb(HijiAddon.texture_26,1,.5,0) and rgb(HijiAddon.texture_36,1,.5,0))
baseline();HijiAddon.UpdateHoly();code(96,0,0);code(97,0,0);observations(1,2)
baseline();disabled[19750]=true
local oldRange=C_Spell.IsSpellInRange
C_Spell.IsSpellInRange=function(id,unit) assert(id~=19750,"disabled Flash must not block healing range");return oldRange(id,unit) end
HijiAddon.UpdateHoly();assert(rgb(HijiAddon.texture_5,1,1,1))
C_Spell.IsSpellInRange=oldRange;baseline()
overrides[82326]=123456;HijiAddon.UpdateHoly();assert(rgb(cell(98),64/255,226/255,1/255),"effective override ID")
baseline();C_Spell.IsSpellDisabled=nil;HijiAddon.UpdateHoly();code(96,0,0);code(97,0,0)
observations(1,26)
for i,id in ipairs({20271,20473,85673,375576,19750,82326,31884,31821,200025,498,53600}) do
    local ready,usable=i==11 and 97 or 20+i,i==11 and 98 or 30+i
    local readyObserved=i==2 and HijiAddon.holyThresholdBars[22].shown and HijiAddon.holyThresholdBars[22].value.value>=1 or rgb(HijiAddon["texture_"..ready],1,1,1)
    assert(readyObserved and rgb(HijiAddon["texture_"..usable],1,1,1),"missing auxiliary API must not disable learned usable spell "..id)
end
HijiAddon.ClearDiagnosticData()
assert(not cell(95) and not cell(100),"switching specs or locate mode must clear diagnostic overlays")
''')
print("PASS: Holy point26/36 detailed diagnostics, native secret display, disabled-spell filtering/recovery and range fallback; no game input sent.")
