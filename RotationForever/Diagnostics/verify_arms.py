"""Exercise the real Arms collector with native-display mocks, without game input.
Run: uv run --with lupa python Diagnostics/verify_arms.py <addon directory>
"""
import re
import runpy
import sys
from pathlib import Path

context = runpy.run_path(str(Path(__file__).with_name("verify_addon.py")))
runtime = context["runtime"]
root = Path(sys.argv[1])
runtime.execute(r'''
local previousCreateFrame=CreateFrame
function CreateFrame(...)
    local frame=previousCreateFrame(...)
    function frame:SetAllPoints() end
    return frame
end
function CreateColor(r,g,b,a)
    return {GetRGBA=function() return r,g,b,a end}
end
Enum={LuaCurveType={Step=1},PowerType={Rage=1}}
local function curve()
    local object={points={}}
    function object:SetType(value) self.type=value end
    function object:AddPoint(x,y) table.insert(self.points,{x,y}) end
    return object
end
C_CurveUtil={CreateCurve=curve,CreateColorCurve=curve,
    EvaluateColorFromBoolean=function(v,yes,no) return v and yes or no end}
-- Secret values may be handed to native displays, never to Lua arithmetic.
local secretMeta={__lt=function() error("secret comparison") end,
    __le=function() error("secret comparison") end,__add=function() error("secret arithmetic") end}
function secret(v) return setmetatable({value=v},secretMeta) end
function issecretvalue(v) return getmetatable(v)==secretMeta end
health={player=0.5,target=0.35};rage=40;charges=1;enemyCount=1
overrides={};cooldowns={};unusable={};unknownSpells={};chargeFail=false;healthFail=false
tracking={};trackingFail=false;targetCasting=false;targetChanneling=false;itemCounts={}
function UnitHealthPercent(unit,predicted,c)
    if healthFail then error("health unavailable") end
    -- This models native curve evaluation, not addon access to health.
    local value=health[unit]
    if c.type==Enum.LuaCurveType.Step then
        local result=c.points[1][2]
        for _,p in ipairs(c.points) do if value>=p[1] then result=p[2] end end
        return result
    end
    return value
end
function UnitPower() return secret(rage) end
function UnitGetTotalAbsorbs() return secret(0) end
function UnitAffectingCombat() return true end
function UnitCanAttack() return true end
function UnitExists(unit)
    local index=unit:match("nameplate(%d+)")
    return not index or tonumber(index)<=enemyCount
end
function UnitIsDead() return false end
function IsMounted() return false end
function UnitCastingInfo(unit) if unit=="target" and targetCasting then return "spell" end end
function UnitChannelInfo(unit) if unit=="target" and targetChanneling then return "spell" end end
function CheckInteractDistance() return true end
C_Item={IsItemInRange=function() return true end,GetItemCount=function(id) return itemCounts[id] or 0 end,GetItemCooldown=function() return 0,0,1 end}
C_Minimap={GetNumTrackingTypes=function() if trackingFail then error("tracking unavailable") end;return #tracking end,
    GetTrackingInfo=function(i) return tracking[i] end}
C_SpellBook={IsSpellKnown=function(id) return not unknownSpells[id] end}
function IsPlayerSpell(id) return not unknownSpells[id] end
C_AssistedCombat={GetNextCastSpell=function() return 0 end}
C_Spell={
    GetOverrideSpell=function(id) return overrides[id] or id end,
    IsSpellUsable=function(id) return not unusable[id] end,
    GetSpellCharges=function() if chargeFail then error("charges unavailable") end;return {currentCharges=secret(charges)} end,
    GetSpellCooldownDuration=function(id,ignoreGCD)
        assert(ignoreGCD==true or id==61304)
        return {EvaluateRemainingDuration=function(_,c) return c.points[(cooldowns[id] or 0)>0 and 2 or 1][2] end}
    end
}
''')
runtime.execute((root / "Classes/Wqz.lua").read_text(encoding="utf-8-sig"))
runtime.execute(r'''
HijiAddon.ClearDiagnosticData()
HijiAddon.UpdateWqz()
assert(rgb(HijiAddon.texture_39,1,1,1))
assert(rgb(HijiAddon.texture_50,1,1,1)) -- Loaded utility collector generation.
assert(rgb(HijiAddon.texture_42,0,0,0)) -- exactly 35% is not execute
assert(rgb(HijiAddon.texture_43,0,0,0)) -- exactly 50% does not use Die by the Sword
assert(HijiAddon.armsThresholdBars[7].min==40 and HijiAddon.armsThresholdBars[7].max==41)
assert(issecretvalue(HijiAddon.armsThresholdBars[7].value))
assert(HijiAddon.bar_1.min==0 and HijiAddon.bar_1.max==2 and issecretvalue(HijiAddon.bar_1.value))
assert(rgb(HijiAddon.texture_38,1,1,1) and not rgb(HijiAddon.texture_37,1,1,1))
health.target=.34999;health.player=.49999;enemyCount=2;charges=2
overrides[1464]=1269383;overrides[163201]=281000;overrides[227847]=446035
HijiAddon.UpdateWqz()
assert(rgb(HijiAddon.texture_42,1,1,1) and rgb(HijiAddon.texture_43,1,1,1))
assert(rgb(HijiAddon.texture_2,1,1,1) and rgb(HijiAddon.texture_37,1,1,1))
assert(HijiAddon.bar_1.value.value==2)
health.target=.35001;health.player=.50001;enemyCount=1
chargeFail=true;healthFail=true;overrides[1464]=secret(1269383)
HijiAddon.UpdateWqz()
assert(rgb(HijiAddon.texture_38,1,.5,0))
assert(rgb(HijiAddon.texture_37,1,.5,0) and rgb(HijiAddon.texture_30,1,.5,0))
assert(rgb(HijiAddon.texture_6,1,.5,0) and rgb(HijiAddon.texture_42,1,.5,0))
assert(HijiAddon.bar_1.value==0) -- no stale charges after a failed sample
chargeFail=false;healthFail=false;overrides[1464]=nil
HijiAddon.UpdateWqz()
assert(rgb(HijiAddon.texture_38,1,1,1) and rgb(HijiAddon.texture_37,0,0,0))
local oldBar=HijiAddon.bar_1
HijiAddon.ClearDiagnosticData()
assert(not HijiAddon.armsThresholdBars[7].shown and not oldBar.shown)
assert(HijiAddon.bar_1==nil)
HijiAddon.UpdateWqz()
assert(HijiAddon.bar_1==oldBar and oldBar.shown)
-- Utility collection follows Fury and does not require fixed DPS points.
tracking={{name="餐饮供应商",active=true},{name="战场军官",active=true}}
targetCasting=true;cooldowns[107574]=5;itemCounts[5512]=1;itemCounts[211879]=1
HijiAddon.UpdateWqz()
for _,i in ipairs({11,12,44,45,46,47,48,49}) do assert(rgb(HijiAddon["texture_"..i],1,1,1)) end
targetCasting=false;targetChanneling=true;cooldowns[6552]=2;cooldowns[312411]=2
HijiAddon.UpdateWqz()
assert(rgb(HijiAddon.texture_45,1,1,1) and rgb(HijiAddon.texture_46,0,0,0) and rgb(HijiAddon.texture_47,0,0,0))
trackingFail=true
HijiAddon.UpdateWqz()
assert(rgb(HijiAddon.texture_44,1,.5,0) and rgb(HijiAddon.texture_48,1,.5,0))
trackingFail=false;tracking={};targetChanneling=false;cooldowns[107574]=0
unknownSpells[202168]=true;unusable[34428]=true
HijiAddon.UpdateWqz()
assert(rgb(HijiAddon.texture_31,0,0,0)) -- fallback observes actual Victory Rush usability.
unusable[34428]=false
HijiAddon.UpdateWqz()
assert(rgb(HijiAddon.texture_31,1,1,1) and rgb(HijiAddon.texture_21,1,1,1))
for _,i in ipairs({44,45,48,49}) do assert(rgb(HijiAddon["texture_"..i],0,0,0)) end
''')

# Confirm shared action numbers still send the user's actual virtual keys.
program = (Path(__file__).parent.parent / "Program.cs").read_text(encoding="utf-8-sig")
expected = {1: (False, "VK_7"), 2: (False, "VK_8"), 3: (False, "VK_9"),
            4: (False, "VK_0"), 5: (False, "VK_Jian"), 6: (False, "VK_Deng"),
            15: (True, "VK_Xiegang"), 18: (True, "VK_Juhao"),
            28: (True, "VK_0"), 29: (True, "VK_Jian"), 7: (False, "VK_FangkuohaoZuo"),
            8: (False, "VK_FangkuohaoYou"), 9: (False, "VK_Xiegang"), 10: (False, "VK_Fenhao"),
            11: (False, "VK_Danyinhao"), 13: (True, "VK_FangkuohaoZuo"),
            14: (True, "VK_FangkuohaoYou"), 16: (True, "VK_Fenhao"), 12: (False, "VK_Juhao")}
for action, (shift, key) in expected.items():
    method = "SimulateShiftKeyPress" if shift else "SimulateKeyPress"
    assert re.search(rf"CheckPt\({action}\)\)\s*\{{\s*{method}\({key}\)", program), action
assert re.search(r"VK_Juhao\s*=\s*0xBE", program)
print(f"PASS: Arms collector boundaries, replacements, secret resources, charges, utilities/tracking, source failures/recovery, display cleanup and {len(expected)} keyboard mappings; no game input sent.")
