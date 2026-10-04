"""Mock WoW frame APIs to verify calibration lifecycle and successful-update heartbeat.
Run: uv run --with lupa python Diagnostics/verify_addon.py <HijiAddon directory>
"""
import sys
from pathlib import Path
from lupa import LuaRuntime

runtime = LuaRuntime(unpack_returned_tuples=True)
runtime.execute(r'''
UIParent = {}
frames = {}
named = {}
now = 1
spec = 3
updates = 0
class = "WARRIOR"
function GetTime() return now end
classIDs = {WARRIOR=1, PALADIN=2, ROGUE=4, DEATHKNIGHT=6, MONK=10, DRUID=11, DEMONHUNTER=12, MAGE=8}
specIDs = {WARRIOR={71,72,73},PALADIN={65,66,70},ROGUE={259,260,261},DEATHKNIGHT={250,251,252},MONK={268,270,269},DRUID={102,103,104,105},DEMONHUNTER={577,581,1480},MAGE={62,63,64}}
function UnitClass() return "player", class, classIDs[class] end
function IsLoggedIn() return true end
C_SpecializationInfo = {GetSpecialization = function() return spec end, GetSpecializationInfo = function(index) return specIDs[class][index] end}
SlashCmdList = {}
function print() end
function CreateFrame(kind, name, parent)
    local frame = {shown = true, textures = {}, scripts = {}}
    function frame:SetSize(w,h) self.w=w self.h=h end
    function frame:SetPoint(...) self.point={...} end
    function frame:ClearAllPoints() self.point=nil end
    function frame:CreateTexture()
        local texture = {}
        function texture:SetTexture(...) end
        function texture:SetAllPoints(...) end
        function texture:SetVertexColor(r,g,b,a) self.r=r self.g=g self.b=b self.a=a end
        texture.SetColorTexture = texture.SetVertexColor
        table.insert(self.textures, texture)
        return texture
    end
    function frame:Show() self.shown=true end
    function frame:Hide() self.shown=false end
    function frame:SetStatusBarTexture() self.statusTexture = self:CreateTexture() end
    function frame:GetStatusBarTexture() return self.statusTexture end
    function frame:SetMinMaxValues(min,max) self.min=min self.max=max end
    function frame:SetValue(v) self.value=v end
    function frame:SetScript(event,fn) self.scripts[event]=fn end
    function frame:RegisterEvent() end
    function frame:RegisterUnitEvent() end
    table.insert(frames, frame)
    if name then named[name]=frame end
    return frame
end
function rgb(texture,r,g,b) return texture.r==r and texture.g==g and texture.b==b end
function displaceLayout()
    HijiAddon.extraBgFrame:SetPoint("TOPLEFT",UIParent,"TOPLEFT",400,0)
    HijiAddon.extraBgFrame:SetSize(20,20)
    HijiAddon.extraBgFrame:Hide()
    for i=1,3 do
        local frame=named["HijiAddonDiagnosticMarker"..i]
        frame:SetPoint("TOPLEFT",UIParent,"TOPLEFT",550+i*10,0)
        frame:Hide()
    end
end
function assertSharedLayout()
    local bg=HijiAddon.extraBgFrame
    assert(bg.point[4]==530 and bg.point[5]==0 and bg.w==200 and bg.h==20 and bg.shown)
    for i=1,3 do
        local frame=named["HijiAddonDiagnosticMarker"..i]
        assert(frame.point[4]==740+(i-1)*10 and frame.point[5]==0 and frame.w==10 and frame.h==3 and frame.shown)
    end
end
''')
root = Path(sys.argv[1])
assert "Classes/Kbz.lua" in (root / "HijiAddon.toc").read_text(encoding="utf-8-sig").replace("\\", "/")
for filename in ("Init.lua", "Frames.lua", "Bars.lua", "Diagnostics.lua"):
    runtime.execute((root / filename).read_text(encoding="utf-8-sig"))
runtime.execute(r'''
heartbeatFrame = frames[#frames]
HijiAddon.UpdateFz = function()
    updates = updates + 1
    if not HijiAddon.bar_1 then HijiAddon.bar_1=HijiAddon.SetBar("bar_1",1,2) end
    HijiAddon.bar_1:SetValue(2)
    HijiAddon.frame_21:Show()
end
HijiAddon.UpdateWqz = function() updates=updates+1 end
HijiAddon.UpdateKbz = function() updates=updates+1;HijiAddon.frame_42:Show() end
timerResets=0
HijiAddon.ResetFuryTimer=function() timerResets=timerResets+1 end
HijiAddon.UpdateCat = function()
    updates=updates+1
    if not HijiAddon.bar_1 then HijiAddon.bar_1=HijiAddon.SetBar("bar_1",1,5) end
    HijiAddon.bar_1:SetValue(5)
end
HijiAddon.UpdateBear = function() updates=updates+1 end
''')
runtime.execute((root / "ClassDriver.lua").read_text(encoding="utf-8-sig"))
runtime.execute(r'''
warFrame = named.HijiAddonClassDriver
for _, name in ipairs({"UpdateFQ","UpdateKtz","UpdateUDK","UpdateJiuMonk","UpdateWindMonk","UpdateDht"}) do
    HijiAddon[name] = function() updates=updates+1 end
end
warFrame.scripts.OnUpdate(warFrame, 1.1)
assertSharedLayout()
assert(updates == 1 and HijiAddon.bar_1.value == 2)
local protectionBar=HijiAddon.bar_1
assert(rgb(named.HijiAddonDiagnosticMarker1.textures[1],0,1,0))
assert(rgb(named.HijiAddonDiagnosticMarker2.textures[1],73/255,0,1/255))
displaceLayout()
SlashCmdList.HIJIADDONDIAGNOSTIC(" locate on ")
assertSharedLayout()
assert(HijiAddon.diagnosticTestMode)
assert(not protectionBar.shown and HijiAddon.bar_1==nil)
for i=1,50 do
    assert(HijiAddon["frame_"..i].shown)
    local mode=i%3
    assert(rgb(HijiAddon["texture_"..i],mode==0 and 1 or 0,mode==1 and 1 or 0,mode==2 and 1 or 0))
end
warFrame.scripts.OnUpdate(warFrame,.2)
assert(updates == 1)
local old=named.HijiAddonDiagnosticMarker3.textures[1].b
now=now+.3
heartbeatFrame.scripts.OnUpdate(heartbeatFrame,.3)
assert(named.HijiAddonDiagnosticMarker3.textures[1].b~=old)
SlashCmdList.HIJIADDONDIAGNOSTIC("locate off")
assert(not HijiAddon.diagnosticTestMode)
assert(not protectionBar.shown and protectionBar.value==0 and HijiAddon.bar_1==nil)
for i=1,50 do
    assert(not HijiAddon["frame_"..i].shown)
    assert(rgb(HijiAddon["texture_"..i],1,1,1))
end
now=now+.3
warFrame.scripts.OnUpdate(warFrame,.1)
assert(updates==2 and HijiAddon.bar_1.value==2 and HijiAddon.frame_21.shown)
assert(HijiAddon.bar_1==protectionBar and protectionBar.shown)
local updateProtection=HijiAddon.UpdateFz
local lastColor=named.HijiAddonDiagnosticMarker3.textures[1].r
HijiAddon.UpdateFz=function() error("mock data failure") end
now=now+1
local ok=pcall(warFrame.scripts.OnUpdate,warFrame,.1)
assert(not ok and named.HijiAddonDiagnosticMarker3.textures[1].r==lastColor)
spec=1
warFrame.scripts.OnEvent(warFrame,"PLAYER_SPECIALIZATION_CHANGED","player")
assert(not HijiAddon.frame_21.shown and not protectionBar.shown and HijiAddon.bar_1==nil)
assert(rgb(named.HijiAddonDiagnosticMarker3.textures[1],0,0,0))
warFrame.scripts.OnUpdate(warFrame,1.1)
assert(updates==3 and rgb(named.HijiAddonDiagnosticMarker2.textures[1],71/255,0,1/255))
local cases={{"DRUID",2,103},{"DRUID",3,104},{"PALADIN",2,66},{"ROGUE",2,260},{"DEATHKNIGHT",3,252},{"MONK",1,268},{"MONK",3,269},{"DEMONHUNTER",2,581}}
for _,case in ipairs(cases) do
    displaceLayout()
    class,spec=case[1],case[2]
    local before=updates
    now=now+.3
    warFrame.scripts.OnUpdate(warFrame,.2)
    assertSharedLayout()
    assert(updates==before+1)
    assert(rgb(named.HijiAddonDiagnosticMarker2.textures[1],(case[3]%256)/255,math.floor(case[3]/256)/255,classIDs[class]/255))
end
class,spec="WARRIOR",2
displaceLayout()
HijiAddon.frame_21:Show()
local resetsBefore=timerResets
now=now+.3;warFrame.scripts.OnUpdate(warFrame,.2)
assertSharedLayout()
assert(not HijiAddon.frame_21.shown and HijiAddon.frame_42.shown and timerResets==resetsBefore+1)
assert(rgb(named.HijiAddonDiagnosticMarker2.textures[1],72/255,0,1/255))
assert(rgb(named.HijiAddonDiagnosticMarker1.textures[1],0,1,0))
local furyPulse=named.HijiAddonDiagnosticMarker3.textures[1].r
local updateFury=HijiAddon.UpdateKbz
HijiAddon.UpdateKbz=function() error("mock fury update failure") end
now=now+.3;assert(not pcall(warFrame.scripts.OnUpdate,warFrame,.2))
assert(named.HijiAddonDiagnosticMarker3.textures[1].r==furyPulse)
HijiAddon.UpdateKbz=updateFury
-- Different resource ranges must not share a hidden or two-charge status bar.
class,spec="DRUID",2
warFrame.scripts.OnUpdate(warFrame,.2)
local catBar=HijiAddon.bar_1
assert(catBar~=protectionBar and catBar.shown and catBar.max==5 and catBar.w==50 and catBar.value==5)
assert(not HijiAddon.frame_42.shown)
local frameCount=#frames
HijiAddon.UpdateFz=updateProtection
class,spec="WARRIOR",3
warFrame.scripts.OnUpdate(warFrame,.2)
assert(HijiAddon.bar_1==protectionBar and protectionBar.shown and protectionBar.max==2 and protectionBar.w==20)
assert(not catBar.shown and #frames==frameCount)
class,spec="DRUID",2
warFrame.scripts.OnUpdate(warFrame,.2)
assert(HijiAddon.bar_1==catBar and catBar.shown and not protectionBar.shown and #frames==frameCount)
class,spec="MAGE",1
displaceLayout()
local before=updates
warFrame.scripts.OnUpdate(warFrame,.2)
assertSharedLayout()
assert(updates==before and not HijiAddon.frame_21.shown and HijiAddon.bar_1==nil and not catBar.shown)
assert(rgb(named.HijiAddonDiagnosticMarker2.textures[1],62/255,0,8/255))
displaceLayout()
warFrame.scripts.OnEvent(warFrame,"TRAIT_CONFIG_UPDATED")
assertSharedLayout()
SlashCmdList.HIJIADDONDIAGNOSTIC("locate on")
displaceLayout()
warFrame.scripts.OnEvent(warFrame,"PLAYER_SPECIALIZATION_CHANGED","player")
assertSharedLayout()
''')
print("PASS: shared right-side layout restored across classes/specs/talent changes and locate mode; calibration lifecycle, successful-data heartbeat, eleven class routes including Fury, automatic spec switching/timer resets, resource-bar range/visibility/cache, and unsupported class clearing.")
