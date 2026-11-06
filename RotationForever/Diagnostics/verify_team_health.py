"""Verify shared 40-member HP collection, roster changes, and native secret display."""
import runpy
from pathlib import Path

context = runpy.run_path(str(Path(__file__).with_name("verify_addon.py")))
runtime = context["runtime"]
runtime.execute(r'''
SlashCmdList.HIJIADDONDIAGNOSTIC("locate off")
raid=false
roster={player={health=.75},party1={health=.25},party2={health=0},party3={offline=true},party4={failure=true}}
function IsInRaid() return raid end
function UnitExists(unit) return roster[unit]~=nil end
function UnitIsConnected(unit) return not roster[unit].offline end
local secretMeta={__lt=function() error("secret comparison") end,__le=function() error("secret comparison") end,
    __add=function() error("secret arithmetic") end,__div=function() error("secret arithmetic") end}
function UnitHealthPercent(unit,predicted,curve)
    assert(predicted and curve)
    if roster[unit].failure then error("mock unavailable health") end
    if roster[unit].secret then return setmetatable({nativeHealth=roster[unit].health},secretMeta) end
    return roster[unit].health
end
local addon=HijiAddon
addon.UpdateTeamHealth()
assert(rgb(addon.texture_51,.75,1,0) and rgb(addon.texture_52,.25,1,0))
assert(rgb(addon.texture_53,0,1,0)) -- zero health is not empty black
assert(rgb(addon.texture_54,0,0,1)) -- offline
assert(rgb(addon.texture_55,1,.5,0)) -- unknown
for i=56,90 do assert(rgb(addon["texture_"..i],0,0,0) and addon["frame_"..i].shown) end
for i=91,100 do assert(not addon["frame_"..i].shown) end
assert(addon.cellCount==100 and addon.cellWidth==5)
for i=1,100 do
    local frame=addon["frame_"..i]
    assert(frame.w==5 and frame.h==1 and frame.point[4]==i*5 and frame.point[5]==0)
end
assertSharedLayout()
raid=true;roster={}
for i=1,40 do roster["raid"..i]={health=i/40} end
addon.UpdateTeamHealth()
for i=1,40 do assert(rgb(addon["texture_"..(50+i)],i/40,1,0)) end
roster.raid40={health=.2,secret=true}
addon.UpdateTeamHealth()
assert(addon.texture_90.r.nativeHealth==.2 and addon.texture_90.g==1 and addon.texture_90.b==0)
roster.raid1=nil;roster.raid2.health=.125
addon.UpdateTeamHealth()
assert(rgb(addon.texture_51,0,0,0) and rgb(addon.texture_52,.125,1,0))
roster.raid3={failure=true};addon.UpdateTeamHealth();assert(rgb(addon.texture_53,1,.5,0))
roster.raid3={health=.5};addon.UpdateTeamHealth();assert(rgb(addon.texture_53,.5,1,0))
raid=false;roster={player={health=.9}}
addon.UpdateTeamHealth()
for i=52,90 do assert(rgb(addon["texture_"..i],0,0,0)) end
for _,specIndex in ipairs({1,2,3}) do
    class,spec="WARRIOR",specIndex
    now=now+.3
    warFrame.scripts.OnUpdate(warFrame,.2)
    assert(rgb(addon.texture_51,.9,1,0))
    assertSharedLayout()
end
SlashCmdList.HIJIADDONDIAGNOSTIC("locate on")
local before=addon.texture_51.r
addon.UpdateTeamHealth()
assert(addon.texture_51.r==before) -- never overwrite localization pattern
local upper,lower=0,0
for _,frame in ipairs(frames) do
    if frame.shown and frame.w==5 and frame.h==1 and frame.point then
        if frame.point[5]==0 then upper=upper+1 end
        if frame.point[5]==-1 then lower=lower+1 end
    end
end
assert(upper==100 and lower==100)
SlashCmdList.HIJIADDONDIAGNOSTIC("locate off")
for i=51,100 do assert(not addon["frame_"..i].shown) end
''')
print("PASS: 100x5 cells, unchanged large area/markers, 40-member HP, solo/party/raid roster transitions, empty/offline/zero/unknown/recovery/secret health, all three warrior driver routes and 100+100 calibration pattern.")
