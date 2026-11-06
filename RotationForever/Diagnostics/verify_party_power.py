"""Exercise live party RP through native StatusBars, with hostile secret mocks.
uv run --with lupa python Diagnostics/verify_party_power.py E:/MyWork/HijiAddon [fixture.json]
"""
import json
import runpy
import sys
from pathlib import Path

context = runpy.run_path(str(Path(__file__).with_name("verify_party_roster.py")))
runtime = context["runtime"]
runtime.execute(r'''
combat=false;grouped=true;raid=false
roster={player={guid="self",class=2,spec=65,role="HEALER"},
    party1={guid="blood",class=6,spec=250,role="TANK"},
    party2={guid="mage",class=8,spec=63,role="DAMAGER"}}
Enum={PowerType={RunicPower=6}}
local originalCreate=CreateFrame
powerFailure=false;nativeFailure=false;rp=44;powerCalls=0
function CreateFrame(kind,...)
    local f=originalCreate(kind,...)
    function f:SetAllPoints() end
    local originalSet=f.SetValue
    function f:SetValue(value)
        if nativeFailure then error("native value unavailable") end
        return originalSet(self,value)
    end
    return f
end
function UnitPower(unit,kind)
    assert(unit=="party1" and kind==6,"power queried for unconfirmed Blood DK")
    powerCalls=powerCalls+1
    if powerFailure then error("restricted power API") end
    return rp
end
local originalSecret=issecretvalue
function issecretvalue(value)
    return originalSecret(value) or (type(value)=="table" and rawget(value,"secretPower")==true)
end
local function forbidden() error("Lua touched restricted power") end
function SecretPower(value)
    return setmetatable({secretPower=true,value=value},{__eq=forbidden,__lt=forbidden,__le=forbidden,
        __add=forbidden,__sub=forbidden,__mul=forbidden,__div=forbidden,__tostring=forbidden})
end
function PowerPacket()
    local p={}
    for _, f in ipairs(frames) do
        if f.shown and f.point and f.point[5]==-1 and f.w==5 then
            local index=f.point[4]/5
            if index>=77 and index<=94 then
                local t=f.textures[1]
                p[index]={math.floor(t.r*255+.5),math.floor(t.g*255+.5),math.floor(t.b*255+.5)}
                -- Emulate only native rendering, which can consume restricted values.
                for _,bar in ipairs(frames) do
                    if bar.shown and bar.parent==f then
                        local v=bar.value
                        if issecretvalue(v) then v=rawget(v,"value") end
                        if v>=bar.max then p[index]={255,255,255} end
                    end
                end
            end
        end
    end
    return p
end
local wrappedCreate=CreateFrame
function CreateFrame(kind,name,parent)
    local f=wrappedCreate(kind,name,parent);f.parent=parent;return f
end
function IsPower(r,g,b)
    local c=PowerPacket()[85]
    return c and c[1]==r and c[2]==g and c[3]==b
end
Event("GROUP_ROSTER_UPDATE");Step(3)
assert(powerCalls==0 and IsPower(255,128,0))
Event("INSPECT_READY","blood")
assert(Num(62)==250 and IsPower(0,0,0))
rp=45;Step();assert(IsPower(255,255,255))
combat=true;Event("PLAYER_REGEN_DISABLED")
rp=SecretPower(44);Step();assert(IsPower(0,0,0))
rp=SecretPower(45);Step();assert(IsPower(255,255,255))
rp=SecretPower(125);Step();assert(IsPower(255,255,255))
rp=0;Step();assert(IsPower(0,0,0))
rp=nil;Step();assert(IsPower(255,128,0))
rp=45;Step();assert(IsPower(255,255,255))
powerFailure=true;Step();assert(IsPower(255,128,0))
powerFailure=false;Step();assert(IsPower(255,255,255))
nativeFailure=true;Step();assert(IsPower(255,128,0))
nativeFailure=false;Step();assert(IsPower(255,255,255))
local before=powerCalls
roster.party1={guid="replacement",class=8,spec=64,role="DAMAGER"}
Event("GROUP_ROSTER_UPDATE")
assert(PowerPacket()[77]==nil and Num(56)==3)
Event("INSPECT_READY","blood");Step();assert(powerCalls==before and PowerPacket()[77]==nil)
combat=false;Event("PLAYER_REGEN_ENABLED");Step(3)
assert(IsPower(255,128,0) and powerCalls==before)
raid=true;Event("GROUP_ROSTER_UPDATE");Step();assert(PowerPacket()[77]==nil)
raid=false;roster.party1={guid="blood2",class=6,spec=250,role="TANK"}
Event("GROUP_ROSTER_UPDATE");Step(3);Event("INSPECT_READY","blood2")
assert(IsPower(255,255,255))
-- Save the real Lua public headers plus native-rendered pixels for C#.
powerFixture={roster=PartyPacket(),power=PowerPacket()}
roster.party1.offline=true;Event("UNIT_CONNECTION","party1")
assert(PowerPacket()[77]==nil)
Step(3);assert(IsPower(255,128,0))
SlashCmdList.HIJIADDONDIAGNOSTIC("locate on")
-- Calibration's own test pixels are intentionally present; native bars are hidden.
for _,f in ipairs(frames) do if f.parent and f.parent.point and f.parent.point[4]==85*5 then assert(not f.shown) end end
SlashCmdList.HIJIADDONDIAGNOSTIC("locate off")
assert(PowerPacket()[77]==nil)
''')
if len(sys.argv) > 2:
    fixture = runtime.globals().powerFixture
    def colors(key, start, end):
        return [[fixture[key][i][c] for c in range(1, 4)] for i in range(start, end)]
    Path(sys.argv[2]).write_text(json.dumps({"roster":colors("roster",51,77),"power":colors("power",77,95)}),encoding="utf-8")
print("PASS: live Blood DK RP 44/45, opaque secrets, nil/API/native failure, replacement, raid, disconnect, calibration and packet export")
