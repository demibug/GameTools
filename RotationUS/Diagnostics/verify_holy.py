"""Exercise the real Holy collector and native secret-value display without game input."""
import runpy
import sys
from pathlib import Path

# Reuse the existing display/API mocks and run the warrior regression once.
context = runpy.run_path(str(Path(__file__).with_name("verify_arms.py")))
runtime = context["runtime"]
root = Path(sys.argv[1])
runtime.execute(r'''
SlashCmdList.HIJIADDONDIAGNOSTIC("locate off")
Enum.PowerType.HolyPower=9;Enum.PowerType.Mana=0
holy=5;infusionPresent=true;handPresent=false;mana=.7;restricted=false;dead={};ranges={target=true,party1=true,party2=false,party3=true,party4=true}
health={player=.8,target=.3};tracking={};targetCasting=false
local originalCasting,originalChanneling=UnitCastingInfo,UnitChannelInfo
function UnitCastingInfo(unit) assert(unit=="player","removed target cast observation queried");return originalCasting(unit) end
function UnitChannelInfo(unit) assert(unit=="player","removed target channel observation queried");return originalChanneling(unit) end
function UnitIsConnected() return true end
function UnitIsDeadOrGhost(unit) return dead[unit] or false end
function UnitIsFriend() return true end
function UnitIsUnit(a,b) return a=="target" and b=="party1" end
function UnitPower(_,power) assert(power==9);return secret(holy) end
function UnitPowerPercent(_,power,_,c) assert(power==0 and c);return secret(mana) end
speed=0
function GetUnitSpeed() return secret(speed),secret(7),secret(7),secret(7) end
local originalCreateFrame=CreateFrame
CreateFrame=function(kind,...)
    local frame=originalCreateFrame(kind,...)
    if kind=="StatusBar" then
        local originalSetValue=frame.SetValue
        frame.SetValue=function(self,value,...)
            assert(select("#",...)==0,"multiple GetUnitSpeed returns leaked into native SetValue")
            return originalSetValue(self,value)
        end
    end
    return frame
end
function IsInGroup() return true end
function IsInRaid() return false end
C_Spell.IsSpellInRange=function(id,unit)
    assert(id~=96231 and id~=35395 and id~=53600,"Holy range must not depend on unavailable melee skills")
    return secret(ranges[unit] or false)
end
C_Item.IsItemInRange=function(id,unit)
    assert(id==168948 or id==194718 or id==8149,"unexpected melee range item")
    assert(unit=="target")
    return true
end
-- Mock only the native evaluator; addon must never branch on these booleans.
C_CurveUtil.EvaluateColorFromBoolean=function(v,yes,no)
    if issecretvalue(v) then return v.value and yes or no end
    return v and yes or no
end
C_Secrets={ShouldSpellAuraBeSecret=function() return restricted end}
C_UnitAuras={GetPlayerAuraBySpellID=function(id)
    assert(id~=25771,"manual Forbearance must never be queried")
    assert(id~=31884,"removed Wings aura must never be queried")
    assert(not restricted)
    if (id==54149 and infusionPresent) or (id==414273 and handPresent) then
        return setmetatable({}, {__index=function() error("proc presence must never read stack counts") end})
    end
    if id==53576 then error("never use passive talent as a proc") end
end}
''')
runtime.execute((root / "Classes/Holy.lua").read_text(encoding="utf-8-sig"))
runtime.execute(r'''
HijiAddon.ClearDiagnosticData();HijiAddon.UpdateHoly()
-- Real C_Item uses boolean cooldown flags; older clients/mocks used 0/1.
-- Exercise both items through the actual collector, including source failures.
local savedItemCooldown,savedItemCount=C_Item.GetItemCooldown,C_Item.GetItemCount
local itemFlag,itemStart,itemDuration,itemCount=true,0,0,1
local failedItem
C_Item.GetItemCount=function(id) if id==failedItem then return itemCount end;return savedItemCount(id) end
C_Item.GetItemCooldown=function(id)
    if id==failedItem then return itemStart,itemDuration,itemFlag end
    return savedItemCooldown(id)
end
for _,entry in ipairs({{16,5512},{17,211879}}) do
    local index,id=entry[1],entry[2];failedItem=id
    for _,flag in ipairs({true,1}) do
        itemFlag,itemCount,itemStart,itemDuration=flag,1,0,0
        HijiAddon.UpdateHoly();assert(rgb(HijiAddon["texture_"..index],1,1,1),"ready item flag "..tostring(flag).." id="..id)
        itemCount=0;HijiAddon.UpdateHoly();assert(rgb(HijiAddon["texture_"..index],0,0,0),"missing item "..id)
        itemCount=1;itemStart=GetTime();itemDuration=60
        HijiAddon.UpdateHoly();assert(rgb(HijiAddon["texture_"..index],0,0,0),"active item cooldown "..id)
        itemStart=GetTime()-60
        HijiAddon.UpdateHoly();assert(rgb(HijiAddon["texture_"..index],1,1,1),"expired item cooldown "..id)
    end
    itemStart,itemDuration=0,0
    for _,flag in ipairs({false,0}) do
        itemFlag=flag;HijiAddon.UpdateHoly();assert(rgb(HijiAddon["texture_"..index],0,0,0),"disabled item flag "..tostring(flag).." id="..id)
    end
    for _,flag in ipairs({secret(true),secret(1),"unknown",2}) do
        itemFlag=flag;HijiAddon.UpdateHoly();assert(rgb(HijiAddon["texture_"..index],1,.5,0),"restricted/invalid item flag "..id)
    end
    itemFlag=nil;HijiAddon.UpdateHoly();assert(rgb(HijiAddon["texture_"..index],1,.5,0),"missing item flag "..id)
    itemFlag=true;itemStart=secret(0)
    HijiAddon.UpdateHoly();assert(rgb(HijiAddon["texture_"..index],1,.5,0),"restricted item start "..id)
    itemStart=0;itemDuration=secret(0)
    HijiAddon.UpdateHoly();assert(rgb(HijiAddon["texture_"..index],1,.5,0),"restricted item duration "..id)
    itemDuration=0;itemCount=secret(1)
    HijiAddon.UpdateHoly();assert(rgb(HijiAddon["texture_"..index],1,.5,0),"restricted item count "..id)
    itemCount=1
    C_Item.GetItemCooldown=function(itemID)
        if itemID==id then error("item cooldown unavailable") end
        return savedItemCooldown(itemID)
    end
    HijiAddon.UpdateHoly();assert(rgb(HijiAddon["texture_"..index],1,.5,0),"item cooldown API failure "..id)
    C_Item.GetItemCooldown=function(itemID)
        if itemID==failedItem then return itemStart,itemDuration,itemFlag end
        return savedItemCooldown(itemID)
    end
    HijiAddon.UpdateHoly();assert(rgb(HijiAddon["texture_"..index],1,1,1),"item recovers on next sample "..id)
end
C_Item.GetItemCooldown,C_Item.GetItemCount=savedItemCooldown,savedItemCount
HijiAddon.UpdateHoly()
assert(HijiAddon.frame_11.shown and rgb(HijiAddon.texture_11,0,0,0),"food collection retained when inactive")
tracking={{name="餐饮供应商",active=true}};HijiAddon.UpdateHoly()
assert(HijiAddon.frame_11.shown and rgb(HijiAddon.texture_11,1,1,1),"food collection retained when active")
tracking[1].active=false;HijiAddon.UpdateHoly();assert(rgb(HijiAddon.texture_11,0,0,0))
trackingFail=true;HijiAddon.UpdateHoly();assert(rgb(HijiAddon.texture_11,1,.5,0),"food collection failures stay unknown")
trackingFail=false;tracking={};HijiAddon.UpdateHoly()
for _,index in ipairs({14,15,18}) do assert(rgb(HijiAddon["texture_"..index],0,1,0) and HijiAddon["frame_"..index].shown,"item range evidence slots visible") end
assert(HijiAddon.frame_13.shown and HijiAddon.holyThresholdBars[13].shown)
assert(HijiAddon.holyThresholdBars[13].min==1 and HijiAddon.holyThresholdBars[13].max==2 and issecretvalue(HijiAddon.holyThresholdBars[13].value))
assert(named.HijiAddonHolyEvents==nil and HijiAddon.ResetHolyTimer==nil)
assert(rgb(HijiAddon.texture_97,1,1,1) and HijiAddon.frame_97.shown)
assert(rgb(HijiAddon.texture_98,1,1,1) and HijiAddon.frame_98.shown)
assert(rgb(HijiAddon.texture_96,1,1,1))
assert(rgb(HijiAddon.texture_100,1,1,1) and HijiAddon.frame_95.shown and rgb(HijiAddon.texture_95,1,1,1))
assert(HijiAddon.frame_41.shown and rgb(HijiAddon.texture_41,0,0,0),"shield tail fields do not overwrite raid mode")
-- Both short probes must confirm; the broader 5-yard probe never authorizes melee.
local savedItemRange=C_Item.IsItemInRange
local probes={{14,168948},{15,194718},{18,8149}}
local cases={"in","out","nil","secret-in","secret-out","error","invalid"}
local palette={['in']={0,1,0},['out']={1,0,0},['nil']={1,.5,0},
    ['secret-in']={.5,0,1},['secret-out']={.5,0,1},['error']={1,0,1},['invalid']={1,.5,0}}
local answers,calls={},{}
C_SpellBook.IsSpellBookItemInRange=function() error("Holy must not query spellbook melee range") end
C_Item.IsItemInRange=function(id,unit)
    assert(unit=="target");calls[id]=(calls[id] or 0)+1
    local answer=answers[id]
    if answer=="in" then return true end
    if answer=="out" then return false end
    if answer=="secret-in" then return secret(true) end
    if answer=="secret-out" then return secret(false) end
    if answer=="error" then error("item range unavailable") end
    if answer=="invalid" then return 1 end
    return nil
end
for _,a in ipairs(cases) do for _,b in ipairs(cases) do for _,c in ipairs(cases) do
    answers={[168948]=a,[194718]=b,[8149]=c};calls={}
    HijiAddon.UpdateHoly()
    local expected=(a=="out" or b=="out") and {0,0,0}
        or (a=="in" and b=="in") and {1,1,1} or {1,.5,0}
    assert(rgb(HijiAddon.texture_95,table.unpack(expected)),"short probe consensus: "..a..","..b..","..c)
    for _,entry in ipairs(probes) do
        assert(calls[entry[2]]==1,"each item must be queried exactly once")
        assert(rgb(HijiAddon["texture_"..entry[1]],table.unpack(palette[answers[entry[2]]])),"independent item status")
    end
end end end
local savedAttackable=UnitCanAttack
local function assertSkipped()
    assert(next(calls)==nil,"invalid/protected target must not enter item range API")
    for _,entry in ipairs(probes) do assert(rgb(HijiAddon["texture_"..entry[1]],0,0,1),"previous target item evidence cleared") end
end
for _,case in ipairs({"friendly","secret","missing","error"}) do
    -- Start each gate case with positive enemy evidence to detect stale values.
    UnitCanAttack=savedAttackable;answers={[168948]="in",[194718]="in",[8149]="in"};HijiAddon.UpdateHoly();calls={}
    UnitCanAttack=function()
        if case=="friendly" then return false end
        if case=="secret" then return secret(true) end
        if case=="error" then error("attackability unavailable") end
        return nil
    end
    HijiAddon.UpdateHoly();assertSkipped()
    local expected=case=="friendly" and {0,0,0} or {1,.5,0}
    assert(rgb(HijiAddon.texture_95,table.unpack(expected)),"target gate: "..case)
end
UnitCanAttack=savedAttackable;calls={};dead.target=true
HijiAddon.UpdateHoly();assertSkipped();assert(rgb(HijiAddon.texture_95,0,0,0))
dead.target=false
C_Item.IsItemInRange=nil;HijiAddon.UpdateHoly()
assert(rgb(HijiAddon.texture_95,1,.5,0),"missing item API remains unknown")
for _,entry in ipairs(probes) do assert(rgb(HijiAddon["texture_"..entry[1]],0,1,1),"missing API evidence") end
C_Item.IsItemInRange=savedItemRange;C_SpellBook.IsSpellBookItemInRange=nil
-- Holy has neither Rebuke nor Crusader Strike; range must still work.
unknownSpells[96231]=true;unknownSpells[35395]=true
HijiAddon.UpdateHoly();assert(rgb(HijiAddon.texture_95,1,1,1),"short item range works without melee skills")
local savedSpellRange=C_Spell.IsSpellInRange
C_Spell.IsSpellInRange=nil;HijiAddon.UpdateHoly()
assert(rgb(HijiAddon.texture_95,1,1,1),"melee item range does not need spell API")
C_Spell.IsSpellInRange=savedSpellRange
HijiAddon.UpdateHoly()
-- Reproduce the live restricted-speed failure: only a native StatusBar may
-- receive the value. Its black fill hides white when moving, including tiny
-- nonzero speed; no Lua comparison or color-curve evaluation is permitted.
local motion=HijiAddon.holyThresholdBars[10]
assert(motion and motion.shown and motion.min==0 and motion.max==.000000001)
assert(rgb(HijiAddon.texture_10,1,1,1) and issecretvalue(motion.value) and motion.value.value==0)
assert(rgb(motion:GetStatusBarTexture(),0,0,0))
speed=7;HijiAddon.UpdateHoly();assert(motion.shown and motion.value.value==7)
speed=.00001;HijiAddon.UpdateHoly();assert(motion.shown and motion.value.value==.00001)
speed=0;HijiAddon.UpdateHoly();assert(motion.shown and motion.value.value==0)
local originalSpeed=GetUnitSpeed
GetUnitSpeed=function() error("speed unavailable") end
HijiAddon.UpdateHoly();assert(not motion.shown and rgb(HijiAddon.texture_10,1,.5,0))
GetUnitSpeed=originalSpeed;HijiAddon.UpdateHoly();assert(motion.shown and motion.value.value==0)
assert(rgb(HijiAddon.texture_1,1,1,1) and rgb(HijiAddon.texture_2,1,1,1))
assert(rgb(HijiAddon.texture_41,0,0,0))
assert(rgb(HijiAddon.texture_42,1,1,1) and rgb(HijiAddon.texture_43,0,0,0))
assert(rgb(HijiAddon.texture_47,1,1,1) and rgb(HijiAddon.texture_46,0,0,0))
assert(rgb(HijiAddon.texture_99,1,1,1));ranges.target=false;HijiAddon.UpdateHoly();assert(rgb(HijiAddon.texture_99,0,0,0));ranges.target=true
assert(HijiAddon.texture_7.r.value==.7 and HijiAddon.texture_7.r==HijiAddon.texture_7.g)
assert(HijiAddon.holyThresholdBars[91].min==2 and HijiAddon.holyThresholdBars[91].max==3)
assert(HijiAddon.holyThresholdBars[92].min==4 and HijiAddon.holyThresholdBars[92].max==5)
assert(HijiAddon.holyThresholdBars[91].value.value==5)
assert(HijiAddon.holyThresholdBars[22].min==0 and HijiAddon.holyThresholdBars[22].max==1 and issecretvalue(HijiAddon.holyThresholdBars[22].value))
assert(rgb(HijiAddon.texture_93,1,1,1) and rgb(HijiAddon.texture_94,0,0,0))
handPresent=true;HijiAddon.UpdateHoly();assert(rgb(HijiAddon.texture_94,1,1,1))
handPresent=false;HijiAddon.UpdateHoly();assert(rgb(HijiAddon.texture_94,0,0,0))
assert(rgb(HijiAddon.texture_18,0,1,0) and HijiAddon.frame_18.shown)
assert(HijiAddon.frame_30.shown and HijiAddon.frame_40.shown,"Protection cooldown/usability visible")
-- Every unlearned priority spell is black in both fields, without querying it.
local originalKnown,originalPlayerSpell=C_SpellBook.IsSpellKnown,IsPlayerSpell
local originalCooldown,originalCharges,originalUsable=C_Spell.GetSpellCooldownDuration,C_Spell.GetSpellCharges,C_Spell.IsSpellUsable
local unlearned
local rangeSpells={}
local originalRange=C_Spell.IsSpellInRange
C_Spell.IsSpellInRange=function(id,unit) assert(id~=unlearned,"unlearned range queried");rangeSpells[id]=true;return originalRange(id,unit) end
C_SpellBook.IsSpellKnown=function(id) return id~=unlearned and originalKnown(id) end
IsPlayerSpell=function(id) return id~=unlearned and originalPlayerSpell(id) end
C_Spell.GetSpellCooldownDuration=function(id,...) assert(id~=96231,"removed Rebuke cooldown queried");assert(id~=633,"manual Lay cooldown queried");assert(id~=unlearned,"unlearned cooldown queried");return originalCooldown(id,...) end
C_Spell.GetSpellCharges=function(id,...) assert(id~=unlearned,"unlearned charges queried");return originalCharges(id,...) end
C_Spell.IsSpellUsable=function(id,...) assert(id~=633,"manual Lay usability queried");assert(id~=unlearned,"unlearned usability queried");return originalUsable(id,...) end
for i,id in ipairs({20271,20473,85673,375576,19750,82326,31884,31821,200025,498,53600}) do
    unlearned=id;HijiAddon.UpdateHoly()
    local readyIndex,usableIndex=i==11 and 97 or 20+i,i==11 and 98 or 30+i
    assert(rgb(HijiAddon["texture_"..readyIndex],0,0,0) and rgb(HijiAddon["texture_"..usableIndex],0,0,0))
    if i==11 then assert(rgb(HijiAddon.texture_95,1,1,1),"range remains independent of unlearned Shield readiness") end
    if i==2 then assert(not HijiAddon.holyThresholdBars[22].shown and not HijiAddon.holyThresholdBars[13].shown and rgb(HijiAddon.texture_13,0,0,0)) end
    if i==5 then assert(rangeSpells[82326] and rgb(HijiAddon.texture_5,1,1,1),"learned Holy Light replaces missing Flash range") end
end
-- A spell learned again is immediately reconsidered on the next sample.
unlearned=nil;HijiAddon.UpdateHoly();assert(HijiAddon.holyThresholdBars[22].shown)
C_SpellBook.IsSpellKnown,IsPlayerSpell=originalKnown,originalPlayerSpell
C_Spell.GetSpellCooldownDuration,C_Spell.GetSpellCharges,C_Spell.IsSpellUsable=originalCooldown,originalCharges,originalUsable
C_Spell.IsSpellInRange=originalRange
-- Missing inactive duration handles must not disable Flash/Holy Light/GCD.
-- Use public cooldown flags only; timing numbers remain forbidden to Lua logic.
local originalInfo=C_Spell.GetSpellCooldown
local cooldownSlots={[61304]=20,[20271]=21,[20473]=22,[85673]=23,[375576]=24,[19750]=25,[82326]=26,[31884]=27,[31821]=28,[200025]=29,[498]=30,[53600]=97}
local missingDuration,info={},{}
for id in pairs(cooldownSlots) do missingDuration[id]=true;info[id]={isEnabled=true,isActive=false} end
C_Spell.GetSpellCooldownDuration=function(id,...)
    assert(id~=96231,"removed Rebuke cooldown queried")
    if missingDuration[id] then return nil end
    return originalCooldown(id,...)
end
C_Spell.GetSpellCooldown=function(id)
    assert(id~=96231,"removed Rebuke cooldown fallback queried")
    assert(missingDuration[id],"fallback only for missing handles")
    return info[id] and setmetatable(info[id],{__index=function() error("must not read secret cooldown timing") end})
end
C_Spell.GetSpellCharges=function() return nil end -- learned non-charge Holy Shock
HijiAddon.UpdateHoly();assert(rgb(HijiAddon.texture_13,0,0,0) and not HijiAddon.holyThresholdBars[13].shown,"nil charges never imply two charges even when Shock is ready")
for id,index in pairs(cooldownSlots) do
    info[id]={isEnabled=true,isActive=false};HijiAddon.UpdateHoly();assert(rgb(HijiAddon["texture_"..index],1,1,1),"inactive recovery "..id)
    info[id]={isEnabled=true,isActive=true};HijiAddon.UpdateHoly();assert(rgb(HijiAddon["texture_"..index],0,0,0),"active recovery "..id)
    info[id]={isEnabled=false,isActive=false};HijiAddon.UpdateHoly();assert(rgb(HijiAddon["texture_"..index],0,0,0),"disabled cooldown "..id)
    info[id]={isEnabled=true,isActive=secret(false)};HijiAddon.UpdateHoly();assert(rgb(HijiAddon["texture_"..index],1,.5,0),"restricted flag "..id)
    info[id]={isEnabled=true};HijiAddon.UpdateHoly();assert(rgb(HijiAddon["texture_"..index],1,.5,0),"missing active flag "..id)
    info[id]=nil;HijiAddon.UpdateHoly();assert(rgb(HijiAddon["texture_"..index],1,.5,0),"missing cooldown data "..id)
    info[id]={isEnabled=true,isActive=false}
end
-- Charge data is authoritative even while another charge is recharging; lack
-- of the charge API result uses ordinary cooldowns, but API errors stay unknown.
C_Spell.GetSpellCharges=originalCharges
charges=1;info[20473]={isEnabled=true,isActive=true};HijiAddon.UpdateHoly()
assert(HijiAddon.holyThresholdBars[22].shown and HijiAddon.holyThresholdBars[22].value.value==1)
assert(HijiAddon.holyThresholdBars[13].shown and HijiAddon.holyThresholdBars[13].value.value==1)
charges=0;HijiAddon.UpdateHoly();assert(HijiAddon.holyThresholdBars[22].value.value==0 and HijiAddon.holyThresholdBars[13].value.value==0)
charges=2;HijiAddon.UpdateHoly();assert(HijiAddon.holyThresholdBars[22].value.value==2 and HijiAddon.holyThresholdBars[13].value.value==2)
chargeFail=true;HijiAddon.UpdateHoly();assert(rgb(HijiAddon.texture_22,1,.5,0) and not HijiAddon.holyThresholdBars[22].shown)
assert(rgb(HijiAddon.texture_13,1,.5,0) and not HijiAddon.holyThresholdBars[13].shown,"charge failure clears stale two-charge fill")
chargeFail=false;charges=1;HijiAddon.UpdateHoly();assert(HijiAddon.holyThresholdBars[13].shown and HijiAddon.holyThresholdBars[13].value.value==1)
C_Spell.GetSpellCooldownDuration,C_Spell.GetSpellCooldown=originalCooldown,originalInfo
restricted=true;HijiAddon.UpdateHoly()
assert(rgb(HijiAddon.texture_18,0,1,0) and HijiAddon.frame_18.shown)
for i=93,94 do assert(rgb(HijiAddon["texture_"..i],1,.5,0) and HijiAddon.holyThresholdBars[i]==nil) end
restricted=false;infusionPresent=false;handPresent=false;HijiAddon.UpdateHoly()
for i=93,94 do assert(rgb(HijiAddon["texture_"..i],0,0,0)) end
dead.party1=true;HijiAddon.UpdateHoly();assert(rgb(HijiAddon.texture_42,0,0,0))
-- New tail observations survive the shared HP update, and are cleared on spec switch.
class,spec="PALADIN",1
HijiAddon.UpdateTeamHealth();assert(HijiAddon.frame_96.shown and HijiAddon.frame_99.shown and rgb(HijiAddon.texture_99,1,1,1) and rgb(HijiAddon.texture_100,1,1,1))
for i=91,92 do assert(HijiAddon.holyThresholdBars[i].shown) end
class,spec="WARRIOR",1;HijiAddon.ClearDiagnosticData();HijiAddon.UpdateTeamHealth()
for i=91,100 do assert(not HijiAddon["frame_"..i].shown) end
for i=91,92 do assert(not HijiAddon.holyThresholdBars[i].shown) end
assert(not HijiAddon.holyThresholdBars[13].shown,"two-charge bar cleared after spec switch")
-- Corpses and ghosts remain valid zero-health slots, never low-health candidates.
function UnitExists(unit) return unit=="player" end
dead.player=true;HijiAddon.UpdateTeamHealth();assert(rgb(HijiAddon.texture_51,0,1,0),tostring(HijiAddon.texture_51.r)..","..tostring(HijiAddon.texture_51.g)..","..tostring(HijiAddon.texture_51.b))
''')
print("PASS: Holy short item range: 343 combinations, both short probes required, broad probe cannot authorize Shield, invalid/secret/error/missing results never grant range, target changes clear evidence, no Rebuke/Crusader Strike or spell API dependency; existing Holy regressions preserved.")
