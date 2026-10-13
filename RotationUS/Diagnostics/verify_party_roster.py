"""Exercise actual Lua roster lifecycle and export a packet for the C# decoder.
uv run --with lupa python Diagnostics/verify_party_roster.py E:/MyWork/HijiAddon [packet.json]
"""
import json
import runpy
import sys
from pathlib import Path

context = runpy.run_path(str(Path(__file__).with_name("verify_addon.py")))
runtime = context["runtime"]
runtime.execute(r'''
now=100
SlashCmdList.HIJIADDONDIAGNOSTIC("locate off")
class="PALADIN"; spec=1
raid=false; grouped=true; combat=false
requests={}; inspectBusy=false; inspected=nil
roster={player={guid="self",class=2,spec=65,role="HEALER"},
    party1={guid="tank",class=1,spec=73,role="DAMAGER"},
    party2={guid="mage",class=8,spec=63,role="DAMAGER"}}
function GetServerTime() return 123 end
function GetTimePreciseSec() return .456 end
function IsInGroup() return grouped end
function IsInRaid() return raid end
function InCombatLockdown() return combat end
function UnitExists(unit) return roster[unit]~=nil end
function UnitGUID(unit)
    assert(not combat,"identity requested in combat")
    return roster[unit] and roster[unit].guid
end
function UnitClass(unit)
    assert(not combat,"class requested in combat")
    if roster[unit].classError then error("unavailable class") end
    return "class","CLASS",roster[unit].class
end
function UnitGroupRolesAssigned(unit)
    assert(not combat,"role requested in combat")
    return roster[unit].role
end
function UnitIsConnected(unit) return not roster[unit].offline end
function CanInspect(unit) return not roster[unit].unreachable end
function NotifyInspect(unit)
    assert(not combat,"inspect requested in combat")
    inspected=unit; requests[#requests+1]={unit=unit,at=now}
end
function ClearInspectPlayer() inspected=nil end
function hooksecurefunc(name,hook)
    local original=_G[name]
    _G[name]=function(...) local value=original(...);hook(...);return value end
end
C_SpecializationInfo.GetInspectSpecialization=function(unit)
    assert(inspected==unit,"read another addon's inspect data")
    return roster[unit].spec
end
InspectFrame={IsShown=function() return inspectBusy end}
local secret=setmetatable({}, {__eq=function() error("secret compare") end,__lt=function() error("secret compare") end})
function issecretvalue(value) return rawequal(value,secret) end
secretValue=secret
function PartyPacket()
    local p={}
    for _, frame in ipairs(frames) do
        if frame.shown and frame.point and frame.point[5]==-1 and frame.w==5 then
            local index=frame.point[4]/5
            if index>=51 and index<=76 then
                local t=frame.textures[1]
                p[index]={math.floor(t.r*255+.5),math.floor(t.g*255+.5),math.floor(t.b*255+.5)}
            end
        end
    end
    return p
end
function Num(index) local c=PartyPacket()[index]; return c[1]+c[2]*256+c[3]*65536 end
function Step(seconds) now=now+(seconds or .1);HijiAddon.UpdatePartyRoster() end
function Event(event,arg) local f=named.HijiAddonPartyRoster; f.scripts.OnEvent(f,event,arg) end
''')
runtime.execute((context["root"] / "PartyRoster.lua").read_text(encoding="utf-8-sig"))
runtime.execute(r'''
Step(0)
assert(Num(56)==2 and Num(60)>0 and #requests==1 and inspected=="party1")
assert(PartyPacket()[61][1]==1 and PartyPacket()[61][2]==3 and Num(62)==0)
Event("INSPECT_READY","tank")
assert(Num(62)==73 and inspected==nil)
Step(2)
assert(inspected=="party2")
Event("INSPECT_READY","mage")
assert(Num(65)==63)
exportedPacket=PartyPacket()

-- Combat freezes verified data and performs no identity/class/spec calls.
local before=#requests
combat=true;Event("PLAYER_REGEN_DISABLED")
Step(90)
assert(Num(56)==1 and Num(62)==73 and #requests==before)

-- Same slot replacement invalidates immediately, even without reading identity in combat.
local oldEpoch=Num(54);local oldID=Num(60)
roster.party1={guid="replacement",class=8,spec=64,role="DAMAGER"}
Event("GROUP_ROSTER_UPDATE")
assert(Num(56)==3 and Num(54)~=oldEpoch and Num(60)==0 and Num(62)==0)
Event("INSPECT_READY","tank")
assert(Num(56)==3 and Num(62)==0)
combat=false;Event("PLAYER_REGEN_ENABLED");Step(2)
assert(Num(56)==2 and Num(60)~=oldID and Num(62)==0)
Event("INSPECT_READY","tank")
assert(Num(62)==0)
Event("INSPECT_READY","replacement")
assert(Num(62)==64)

-- Delayed result after leaving, and switching into/out of a raid.
grouped=false;Event("GROUP_ROSTER_UPDATE")
assert(Num(56)==0 and Num(60)==0)
Event("INSPECT_READY","replacement");assert(Num(62)==0)
grouped=true;raid=true;Event("GROUP_ROSTER_UPDATE");Step(2)
assert(Num(56)==0 and Num(60)==0)
raid=false;Event("GROUP_ROSTER_UPDATE");Step(2)
assert(Num(56)==2 and Num(62)==0)

-- Other consumers own the shared inspection channel after their request.
NotifyInspect("party2")
Event("INSPECT_READY","replacement");assert(Num(62)==0 and inspected=="party2")
Event("INSPECT_READY","mage");assert(Num(65)==0)
inspectBusy=true;before=#requests;Step(12);assert(#requests==before)
inspectBusy=false;Step(2);assert(#requests==before+1)
Step(5);assert(Num(62)==0) -- timeout, not a fake zero specialization success

-- Polling alone detects a reorder, even if a roster event was missed.
oldEpoch=Num(54);roster.party1,roster.party2=roster.party2,roster.party1
Step(3)
assert(Num(54)~=oldEpoch and Num(62)==0 and Num(65)==0)

-- Unreachable members cannot starve other members' inspection.
roster.party1.unreachable=true
Event("GROUP_ROSTER_UPDATE");Step(2)
assert(inspected=="party2")
Event("INSPECT_READY","replacement");assert(Num(65)==64)

-- Failed/secret values are explicitly unknown, and stale spec data is cleared.
roster.party2.classError=true;Step(3)
assert(PartyPacket()[64][1]==0 and Num(65)==0)
roster.party2.classError=false;roster.party2.class=secretValue;Step(3)
assert(PartyPacket()[64][1]==0 and Num(65)==0)
roster.party2.class=8
roster.party1.role=secretValue;Step(3)
assert(PartyPacket()[61][2]==0)
roster.party1.role="DAMAGER";roster.party1.unreachable=false
Event("GROUP_ROSTER_UPDATE");Step(2);Event("INSPECT_READY","mage")
assert(Num(62)==63)
roster.party1.unreachable=true;Step(61)
assert(Num(62)==0)

-- Disconnect/reconnect and specialization notifications revoke old metadata.
roster.party2.offline=true;Event("UNIT_CONNECTION","party2")
assert(Num(56)==3 and Num(65)==0)
Step(3);assert(PartyPacket()[64][3]==0 and Num(65)==0)
roster.party2.offline=false;Event("UNIT_CONNECTION","party2");Step(3)
Event("INSPECT_READY","replacement");assert(Num(65)==64)
roster.party2.spec=62;Event("PLAYER_SPECIALIZATION_CHANGED","party2")
assert(Num(56)==3 and Num(65)==0)
Step(3);Event("INSPECT_READY","replacement");assert(Num(65)==62)

-- Shared display resets / calibration hide the packet and invalidate the session roster.
SlashCmdList.HIJIADDONDIAGNOSTIC("locate on")
local p=PartyPacket();assert(p[51][1]==255 and p[51][3]==255)
Step(3);p=PartyPacket();assert(p[51][1]==255 and p[51][3]==255)
SlashCmdList.HIJIADDONDIAGNOSTIC("locate off");Step(3)
assert(Num(56)==2 and Num(65)==0)
Event("PLAYER_LEAVING_WORLD");assert(Num(56)==3)

-- Production class driver continues the health collector and metadata together.
now=now+3;warFrame.scripts.OnUpdate(warFrame,.2)
assert(Num(56)==2 and HijiAddon.texture_51.g==1)
-- A collector-level failure clears the packet while preserving live health output.
InspectFrame.IsShown=function() error("inspect UI unavailable") end
now=now+3;warFrame.scripts.OnUpdate(warFrame,.2)
assert(PartyPacket()[51]==nil and HijiAddon.texture_51.g==1)
InspectFrame.IsShown=function() return false end
Step(3);assert(Num(56)==2)
''')
if len(sys.argv) > 2:
    packet = runtime.globals().exportedPacket
    Path(sys.argv[2]).write_text(json.dumps([[packet[i][c] for c in range(1, 4)] for i in range(51, 77)]), encoding="utf-8")
print("PASS: party metadata lifecycle, combat invalidation, inspect ownership, missing data and calibration")
