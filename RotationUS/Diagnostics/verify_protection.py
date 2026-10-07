"""Exercise Protection raw-rage threshold with existing native-display mocks; no input."""
import runpy
from pathlib import Path

context = runpy.run_path(str(Path(__file__).with_name("verify_arms.py")))
runtime, root = context["runtime"], context["root"]
runtime.execute(r'''
Enum.DurationTimeModifier={RealTime=1}
local originalCurve=C_CurveUtil.CreateCurve
C_CurveUtil.CreateCurve=function()
    local value=originalCurve()
    function value:ClearPoints() self.points={} end
    return value
end
powerMaximum=130;powerFail=false
function UnitPowerPercent() return rage/powerMaximum end
local originalPower=UnitPower
function UnitPower(...)
    if powerFail then error("raw power unavailable") end
    return originalPower(...)
end
function IsInGroup() return false end
function IsInRaid() return false end
C_Container={GetContainerNumSlots=function() return 0 end}
C_Spell.GetSpellCooldown=function(id) return {startTime=0,duration=cooldowns[id] or 0} end
C_Spell.GetSpellCooldownDuration=function(id,ignoreGCD)
    if ownCooldownFail and ignoreGCD then error("own cooldown source failed") end
    local duration=cooldowns[id] or 0
    if not ignoreGCD and (cooldowns[61304] or 0)>duration then duration=cooldowns[61304] end
    return {EvaluateRemainingPercent=function() return duration==0 and 1 or 0 end,
        EvaluateRemainingDuration=function(_,curve) return curve.points[duration==0 and 1 or 2][2] end}
end
''')
runtime.execute((root / "Classes/Fz.lua").read_text(encoding="utf-8-sig"))
runtime.execute(r'''
HijiAddon.ClearDiagnosticData()
rage=80;HijiAddon.UpdateFz()
local bar=HijiAddon.protectionThresholdBars[30]
assert(bar.min==79 and bar.max==80 and bar.shown)
assert(issecretvalue(bar.value) and bar.value.value==80)
assert(rgb(HijiAddon.texture_7,80/130,80/130,80/130))
assert(rgb(HijiAddon.texture_30,0,0,0)) -- black base, white native bar overlay.
rage=79;HijiAddon.UpdateFz();assert(bar.value.value==79)
rage=81;powerMaximum=100;HijiAddon.UpdateFz();assert(bar.value.value==81)
powerFail=true;HijiAddon.UpdateFz()
assert(not bar.shown and rgb(HijiAddon.texture_30,1,.5,0))
powerFail=false;rage=80;HijiAddon.UpdateFz();assert(bar.shown and bar.value.value==80)
HijiAddon.ClearDiagnosticData();assert(not bar.shown)
HijiAddon.UpdateFz();assert(bar.shown and HijiAddon.protectionThresholdBars[30]==bar)
cooldowns[107574]=0;cooldowns[385952]=0;cooldowns[61304]=1
HijiAddon.UpdateFz()
assert(rgb(HijiAddon.texture_15,0,0,0) and rgb(HijiAddon.texture_18,0,0,0))
assert(rgb(HijiAddon.texture_31,0,0,0) and rgb(HijiAddon.texture_32,0,0,0)) -- GCD is not a consumed request.
cooldowns[107574]=30;HijiAddon.UpdateFz()
assert(rgb(HijiAddon.texture_31,0,0,0) and rgb(HijiAddon.texture_32,1,1,1))
cooldowns[385952]=30;HijiAddon.UpdateFz()
assert(rgb(HijiAddon.texture_31,1,1,1) and rgb(HijiAddon.texture_32,1,1,1))
ownCooldownFail=true;HijiAddon.UpdateFz()
assert(rgb(HijiAddon.texture_31,1,.5,0) and rgb(HijiAddon.texture_32,1,.5,0))
ownCooldownFail=false;unknownSpells[385952]=true;HijiAddon.UpdateFz()
assert(rgb(HijiAddon.texture_31,1,1,1)) -- An unlearned spell must not hold the officer request forever.
''')
print("PASS: Protection raw rage 79/80/81, independent maximum, secret native display, failure recovery, specialization cleanup and own cooldown/GCD officer confirmation; no input sent.")
