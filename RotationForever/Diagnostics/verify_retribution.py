"""Execute the real Retribution collector with native API mocks; never sends input."""
import re
import runpy
import sys
from pathlib import Path

runtime = runpy.run_path(str(Path(__file__).with_name("verify_addon.py")))["runtime"]
root = Path(sys.argv[1])
runtime.execute(r'''
local create=CreateFrame
function CreateFrame(...)
    local f=create(...)
    function f:SetAllPoints() end
    function f:SetValue(v) assert(v~=nil);self.value=v end
    return f
end
function CreateColor(r,g,b,a) return {GetRGBA=function() return r,g,b,a end} end
Enum={LuaCurveType={Step=1},PowerType={HolyPower=9}}
local function curve()
    return {points={},SetType=function(self,t) self.type=t end,
        AddPoint=function(self,x,y) table.insert(self.points,{x,y}) end}
end
local meta={__lt=function() error('secret comparison') end,__le=function() error('secret comparison') end,
    __add=function() error('secret arithmetic') end}
function secret(v) return setmetatable({value=v},meta) end
function issecretvalue(v) return getmetatable(v)==meta end
C_CurveUtil={CreateCurve=curve,CreateColorCurve=curve,EvaluateColorFromBoolean=function(v,yes,no)
    if issecretvalue(v) then v=v.value end;return v and yes or no end}
power=5;enemyCount=1;health=.8;tracking={};overrides={};cooldowns={};charges={};unusable={};insufficient={};ranges={};castTime=1500
unknown={[458359]=true};targetCast=false;targetChannel=false;locked=false;items={};itemEnabled=1
unitCombat={};unitRanges={};unitDead={};unitAttackable={}
local function observed(values,unit,default)
    local value=values[unit]
    if value==nil then return default end
    if value=='nil' then return nil end
    if value=='error' then error('unit state unavailable') end
    return value
end
function UnitPower(_,kind) assert(kind==9);if power==nil then error('power unavailable') end;return secret(power) end
function UnitHealthPercent(_,_,c)
    if health==nil then error('health unavailable') end
    if c.type==Enum.LuaCurveType.Step then
        local result=c.points[1][2]
        for _,p in ipairs(c.points) do if health>=p[1] then result=p[2] end end
        return result
    end
    return health
end
function UnitExists(unit) local n=unit:match('nameplate(%d+)');return not n or tonumber(n)<=enemyCount end
function UnitIsDeadOrGhost(unit) return observed(unitDead,unit,false) end
function UnitAffectingCombat(unit) return observed(unitCombat,unit,true) end
function IsMounted() return false end
function UnitCanAttack(_,unit) return observed(unitAttackable,unit,true) end
function CheckInteractDistance() error('Retribution must not use interact distance') end
function UnitCastingInfo(unit) if unit=='target' and targetCast then return 'spell',nil,nil,nil,nil,nil,nil,locked end end
function UnitChannelInfo(unit) if unit=='target' and targetChannel then return 'channel',nil,nil,nil,nil,nil,locked end end
C_Minimap={GetNumTrackingTypes=function() if tracking==nil then error('tracking unavailable') end;return #tracking end,
    GetTrackingInfo=function(i) return tracking[i] end}
C_SpellBook={IsSpellKnown=function(id) return not unknown[id] end}
function IsPlayerSpell(id) return not unknown[id] end
C_Item={GetItemCount=function(id) return items[id] or 0 end,GetItemCooldown=function() return 0,0,itemEnabled end,
    IsItemInRange=function() error('Retribution must not use item range') end}
C_Spell={GetOverrideSpell=function(id) return overrides[id] or id end,
    GetSpellInfo=function(id) assert(id==19750 or id==999);return {castTime=castTime} end,
    IsSpellUsable=function(id) return not (unusable[id] or insufficient[id]),not not insufficient[id] end,
    IsSpellInRange=function(id,unit)
        if unit:match('^nameplate%d+$') then
            assert(id==853)
            return observed(unitRanges,unit,true)
        end
        assert(unit=='target')
        if ranges[id]=='error' then error('range unavailable') end
        if ranges[id]=='nil' then return nil end
        return ranges[id]==nil or ranges[id]
    end,
    GetSpellCharges=function(id) if charges[id] then return {currentCharges=secret(charges[id])} end end,
    GetSpellCooldownDuration=function(id,ignoreGCD)
        assert(ignoreGCD==true or id==61304)
        if cooldowns[id]=='nil' then return nil end
        return {EvaluateRemainingDuration=function(_,c)
            local value=cooldowns[id] or 0;local result=c.points[1][2]
            for _,p in ipairs(c.points) do if value>=p[1] then result=p[2] end end
            return result
        end}
    end}
''')
assert "Classes/Cjq.lua" in (root / "HijiAddon.toc").read_text(encoding="utf-8-sig").replace("\\", "/")
runtime.execute((root / "Classes/Cjq.lua").read_text(encoding="utf-8-sig"))
runtime.execute(r'''
local targetEvent=frames[#frames]
local function f(i,r,g,b) assert(rgb(HijiAddon['texture_'..i],r,g or r,b or r),'frame '..i) end
local function flag(i,r,g,b) assert(rgb(HijiAddon['bar_'..i]:GetStatusBarTexture(),r,g or r,b or r),'bar '..i) end
HijiAddon.ClearDiagnosticData();HijiAddon.UpdateCjq()
f(19,70/255,5/255,2/255);f(50,1);f(5,0);f(9,1);flag(28,1)
-- Radiant Glory observes Wake's own cooldown independently of its overridden button.
unknown[458359]=nil;unknown[343527]=true
overrides[255937]=427453;cooldowns[255937]=12;cooldowns[427453]=0
HijiAddon.UpdateCjq();flag(11,1);flag(9,0);flag(43,1);f(24,1);f(30,0)
cooldowns[255937]=0;cooldowns[61304]=1
HijiAddon.UpdateCjq();flag(43,0);f(16,0)
cooldowns[255937]='nil';HijiAddon.UpdateCjq();flag(43,1,.5,0)
cooldowns[255937]=0;overrides[255937]=429826
HijiAddon.UpdateCjq();flag(43,0);flag(7,1)
unknown[458359]=true;unknown[343527]=nil;overrides={};cooldowns={}
-- IsSpellUsable returns TWO booleans. Insufficient power must never invert the first.
power=1;insufficient[85256]=true;insufficient[53385]=true;insufficient[85673]=true
HijiAddon.UpdateCjq();f(27,0);f(29,0);flag(22,0)
-- A genuine free spender remains usable even at low power.
insufficient[85256]=nil;insufficient[53385]=nil
HijiAddon.UpdateCjq();f(27,1);f(29,1);flag(22,0)
-- The effective replacement spell must retain the same two-result semantics.
overrides[85256]=383328;insufficient[383328]=true
HijiAddon.UpdateCjq();f(27,0)
insufficient[383328]=nil;HijiAddon.UpdateCjq();f(27,1)
overrides[85256]=nil;insufficient={};power=5
-- Secret usability results also reach native display without interpreting the second return.
local originalUsable=C_Spell.IsSpellUsable
for _,usable in ipairs({false,true}) do
    C_Spell.IsSpellUsable=function(id)
        if id==85256 then return secret(usable),secret(not usable) end
        return originalUsable(id)
    end
    HijiAddon.UpdateCjq();f(27,usable and 1 or 0)
end
C_Spell.IsSpellUsable=originalUsable
-- Audit every automatic spell family, including native replacement forms and self-help flags.
local auditedSpells={
    {id=31884,slot=21},{id=231895,slot=21,base=31884},
    {id=343527,slot=23},
    {id=427453,slot=25,base=255937},{id=429826,slot=25,base=255937},
    {id=85256,slot=27},{id=383328,slot=27,base=85256},
    {id=53385,slot=29},{id=255937,slot=31},{id=375576,slot=33},
    {id=184575,slot=35},{id=20271,slot=37},{id=24275,slot=37,base=20271},
    {id=96231,slot=39},
    {id=85673,slot=22,bar=true},{id=19750,slot=24,bar=true},
    {id=999,slot=24,bar=true,base=19750},{id=498,slot=26,bar=true},
}
local usabilityCases={
    {true,false,1},{false,false,0},{false,true,0},{true,true,1},
    {secret(false),secret(true),0},{secret(true),secret(false),1},
    {false,secret(false),0},{true,nil,1},
    {nil,true,1,.5,0},{'invalid',true,1,.5,0},{'error',false,1,.5,0},
}
local auditCount=0
for _,entry in ipairs(auditedSpells) do
    overrides={};if entry.base then overrides[entry.base]=entry.id end
    local check=entry.bar and flag or f
    for _,values in ipairs(usabilityCases) do
        local queried=false
        C_Spell.IsSpellUsable=function(id)
            if id~=entry.id then return originalUsable(id) end
            queried=true
            if values[1]=='error' then error('usability unavailable') end
            return values[1],values[2]
        end
        HijiAddon.UpdateCjq()
        assert(queried,'effective spell not queried: '..entry.id)
        check(entry.slot,values[3],values[4],values[5])
        local resourceSlot=math.floor(entry.bar and (40+(entry.slot-22)/2) or (30+(entry.slot-21)/2))
        local noPower=values[2]
        if values[1]=='error' or noPower==nil then flag(resourceSlot,1,.5,0)
        else
            if issecretvalue(noPower) then noPower=noPower.value end
            flag(resourceSlot,noPower and 0 or 1)
        end
        -- Resource failure must not corrupt the independent cooldown field.
        check(entry.slot-1,1)
        auditCount=auditCount+1
    end
    C_Spell.IsSpellUsable=originalUsable
    cooldowns[entry.id]=7;HijiAddon.UpdateCjq();check(entry.slot-1,0);check(entry.slot,1)
    cooldowns[entry.id]='nil';HijiAddon.UpdateCjq();check(entry.slot-1,1,.5,0);check(entry.slot,1)
    cooldowns[entry.id]=nil;HijiAddon.UpdateCjq();check(entry.slot-1,1)
end
overrides={};C_Spell.IsSpellUsable=originalUsable
print('PASS: '..#auditedSpells..' Retribution spell/form paths, '..auditCount..' usability cases, independent cooldown failure/recovery.')
-- AoE requires two living, attackable, engaged enemies within Hammer of Justice range.
enemyCount=0;HijiAddon.UpdateCjq();f(5,0)
enemyCount=2;unitCombat.nameplate1=false;unitCombat.nameplate2=false
HijiAddon.UpdateCjq();f(5,0)
unitCombat.nameplate1=true;HijiAddon.UpdateCjq();f(5,0)
unitCombat.nameplate2=true;unitRanges.nameplate2=false;HijiAddon.UpdateCjq();f(5,0)
unitRanges.nameplate2=true;cooldowns[853]=45;unusable[853]=true
HijiAddon.UpdateCjq();f(5,1)
unitDead.nameplate2=true;HijiAddon.UpdateCjq();f(5,0)
unitDead.nameplate2=false;unitAttackable.nameplate2=false;HijiAddon.UpdateCjq();f(5,0)
unitAttackable.nameplate2=true
for _,value in ipairs({'nil','error',secret(true),secret(false)}) do
    unitCombat.nameplate2=value;HijiAddon.UpdateCjq();f(5,1,.5,0)
end
unitCombat.nameplate2=true
for _,value in ipairs({'nil','error',secret(true),secret(false)}) do
    unitRanges.nameplate2=value;HijiAddon.UpdateCjq();f(5,1,.5,0)
end
-- Idle enemies never query range, even when their range source would fail.
unitCombat.nameplate2=false;unitRanges.nameplate2='error';HijiAddon.UpdateCjq();f(5,0)
-- Two confirmed enemies suffice even if an earlier unit has unknown combat state.
enemyCount=3;unitCombat.nameplate1=secret(true);unitCombat.nameplate2=true;unitRanges.nameplate2=true
HijiAddon.UpdateCjq();f(5,1)
unitCombat.nameplate3=false;HijiAddon.UpdateCjq();f(5,1,.5,0)
unitCombat={};unitRanges={};unitDead={};unitAttackable={};enemyCount=1
cooldowns[853]=nil;unusable[853]=nil;HijiAddon.UpdateCjq();f(5,0)
flag(19,0);flag(20,0);flag(21,1);flag(22,1);flag(27,0)
health=.79999;castTime=0;HijiAddon.UpdateCjq();flag(19,1);flag(20,0);flag(27,1)
health=.6;HijiAddon.UpdateCjq();flag(19,1);flag(20,0)
health=.59999;HijiAddon.UpdateCjq();flag(19,1);flag(20,1)
castTime=secret(0);HijiAddon.UpdateCjq();flag(27,1,.5,0)
castTime=nil;HijiAddon.UpdateCjq();flag(27,1,.5,0)
castTime=0;overrides[19750]=999;cooldowns[999]=2;unusable[85673]=true
HijiAddon.UpdateCjq();flag(23,0);flag(24,1);flag(22,0);flag(27,1)
overrides[19750]=nil;unusable[85673]=nil;castTime=1500;health=.8
assert(HijiAddon.bar_1.max==5 and HijiAddon.bar_1.value.value==5)
assert(HijiAddon.retThresholdBars[7].min==2 and HijiAddon.retThresholdBars[8].min==4)
assert(issecretvalue(HijiAddon.retThresholdBars[8].value))
f(24,0);f(30,1);flag(6,0);flag(7,0);flag(11,0);flag(12,1)
cooldowns[61304]=1;cooldowns[31884]=20;cooldowns[343527]=30;enemyCount=2
HijiAddon.UpdateCjq();f(16,0);f(18,1);f(20,0);f(22,0);flag(16,1);f(30,1);f(49,1);f(5,1)
-- Charges remain usable during recharge and use native secret display.
charges[184575]=1;cooldowns[184575]=8
HijiAddon.UpdateCjq();assert(HijiAddon.retThresholdBars[34].shown and HijiAddon.retThresholdBars[34].value.value==1)
-- Temporary hammer does not have to be known independently.
unknown[427453]=true;unknown[429826]=true;overrides[255937]=427453
HijiAddon.UpdateCjq();f(24,1);f(30,0);flag(6,1);flag(7,0)
overrides[255937]=429826;overrides[20271]=24275
HijiAddon.UpdateCjq();f(24,1);f(30,0);flag(7,1);f(17,1)
-- All offensive families use Hammer of Justice range, including free Hammer and Judgment's override.
local closeRanges={40,41,42,43,45,46,47}
local function close(r,g,b)
    for _,slot in ipairs(closeRanges) do f(slot,r,g,b) end
    flag(28,r,g,b)
end
ranges[853]=secret(false);HijiAddon.UpdateCjq();close(0)
-- Rebuke alone retains its own range probe.
f(48,1)
f(44,0) -- Wake is still replaced by Hammer here.
for _,id in ipairs({343527,427453,429826,85256,53385,255937,375576,184575,20271,24275}) do ranges[id]=false end
cooldowns[853]=45;unusable[853]=true
ranges[853]=secret(true);HijiAddon.UpdateCjq();close(1)
-- Nil/errors must remain unknown instead of falling back to the spells' longer ranges.
for _,id in ipairs({343527,427453,429826,85256,53385,255937,375576,184575,20271,24275}) do ranges[id]=true end
ranges[853]='nil';HijiAddon.UpdateCjq();close(1,.5,0)
ranges[853]='error';HijiAddon.UpdateCjq();close(1,.5,0)
ranges[853]=false;HijiAddon.UpdateCjq();close(0)
ranges[853]=true;HijiAddon.UpdateCjq();close(1)
overrides[20271]=nil;ranges[20271]=false;HijiAddon.UpdateCjq();f(47,1);f(17,0)
overrides[20271]=24275;cooldowns[853]=nil;unusable[853]=nil
-- Normal Hammer and untransformed Wake both follow the same gate, without item fallback.
for _,shape in ipairs({427453,255937}) do
    overrides[255937]=shape
    local slot=shape==427453 and 41 or 44
    ranges[shape]='nil';ranges[53385]='nil'
    ranges[853]=secret(false);HijiAddon.UpdateCjq();f(slot,0);f(43,0)
    ranges[853]='nil';HijiAddon.UpdateCjq();f(slot,1,.5,0);f(43,1,.5,0)
    ranges[853]='error';HijiAddon.UpdateCjq();f(slot,1,.5,0);f(43,1,.5,0)
    ranges[853]=secret(true);HijiAddon.UpdateCjq();f(slot,1);f(43,1)
end
ranges[96231]=false;HijiAddon.UpdateCjq();f(48,0)
ranges[96231]='nil';HijiAddon.UpdateCjq();f(48,1,.5,0)
ranges[96231]=true;ranges[853]=false;HijiAddon.UpdateCjq();f(48,1)
ranges[853]=true
-- Wings uses an independent target range field even without Execution Sentence learned.
unknown[343527]=true;overrides[31884]=231895
HijiAddon.UpdateCjq();flag(28,1);f(40,0)
unitAttackable.target=false;HijiAddon.UpdateCjq();flag(28,0)
unitAttackable.target='error';HijiAddon.UpdateCjq();flag(28,1,.5,0)
unitAttackable.target=nil;unknown[343527]=nil;overrides[31884]=nil
overrides[255937]=255937;ranges[255937]='nil';ranges[53385]='nil';ranges[343527]='nil'
HijiAddon.UpdateCjq();f(44,1);f(43,1);f(40,1)
power=nil;health=nil;tracking=nil;overrides[255937]=secret(427453)
HijiAddon.UpdateCjq();f(7,1,.5,0);f(8,1,.5,0);f(9,1,.5,0);f(6,1,.5,0);f(10,1,.5,0);f(11,1,.5,0)
f(24,1,.5,0);f(30,1,.5,0);flag(6,1,.5,0);f(50,1)
flag(19,1,.5,0);flag(20,1,.5,0)
assert(not HijiAddon.retThresholdBars[7].shown and not HijiAddon.retThresholdBars[8].shown)
assert(HijiAddon.bar_1.value==0)
-- Recover sources; secret interruptibility is rendered natively, not rejected as unknown.
power=3;health=.4;overrides[255937]=255937
tracking={{name='战场军官',active=true},{name='餐饮供应商',active=true}}
targetCast=true;locked=secret(false);items[5512]=1;items[244838]=1
HijiAddon.UpdateCjq();f(10,1);f(11,1);f(12,1);f(13,1);f(14,1);f(15,1)
for _,channel in ipairs({false,true}) do
    targetCast=not channel;targetChannel=channel
    for _,value in ipairs({false,secret(false)}) do locked=value;HijiAddon.UpdateCjq();f(12,1);f(13,1) end
    for _,value in ipairs({true,secret(true)}) do locked=value;HijiAddon.UpdateCjq();f(12,1);f(13,0) end
    locked=nil;HijiAddon.UpdateCjq();f(13,1,.5,0)
    locked='invalid';HijiAddon.UpdateCjq();f(13,1,.5,0)
end
targetCast=false;targetChannel=false;HijiAddon.UpdateCjq();f(12,0);f(13,0)
targetCast=true
locked=false;itemEnabled=false;HijiAddon.UpdateCjq();f(13,1);f(14,0);f(15,0)
locked=true;itemEnabled=true;HijiAddon.UpdateCjq();f(13,0);f(14,1)
itemEnabled=0;HijiAddon.UpdateCjq();f(14,0)
itemEnabled=secret(1);HijiAddon.UpdateCjq();f(14,1,.5,0)
cooldowns[31884]='nil';HijiAddon.UpdateCjq();f(18,1,.5,0);f(20,1,.5,0)
flag(14,1/255,70/255,1/255);targetEvent.scripts.OnEvent(targetEvent,'PLAYER_TARGET_CHANGED')
HijiAddon.UpdateCjq();flag(14,2/255,70/255,1/255)
local threshold=HijiAddon.retThresholdBars[8];local flagBar=HijiAddon.bar_6
HijiAddon.ClearDiagnosticData();assert(not threshold.shown and not flagBar.shown and HijiAddon.bar_6==nil)
HijiAddon.UpdateCjq();assert(threshold.shown and HijiAddon.bar_6.shown)
''')
program = (Path(__file__).parent.parent / "Program.cs").read_text(encoding="utf-8-sig")
# Pin the reviewed identities and ensure both applications keep the same catalog.
expected_auras = [
    ("ret_art", "战争艺术", 406086, 406064, "player"),
    ("ret_legacy", "苍穹之遗", 387178, 387170, "player"),
    ("ret_free", "圣光裁决", 433732, 425518, "player"),
    ("ret_ruling", "公允裁定", 432629, 432626, "player"),
    ("ret_wings", "复仇之怒", 454351, 458359, "player"),
    ("ret_sentence", "处决宣判", 343527, 343527, "target"),
    ("ret_expurgation", "异端逐除", 383346, 383344, "target"),
]
cs = (Path(__file__).with_name("RetObservation.cs")).read_text(encoding="utf-8-sig")
for i, (key, name, aura_id, talent_id, unit) in enumerate(expected_auras, 1):
    entry = runtime.globals().HijiAddon.retAuras[i]
    assert (entry.key, entry.name, entry.auraID, entry.talentID, entry.unit) == (key, name, aura_id, talent_id, unit)
    assert f'new("{key}","{name}",{aura_id},{talent_id},"{unit}",' in cs
runtime.execute(r'''
local savedPrint=print
local lines={}
print=function(line) table.insert(lines,line) end
C_Spell.GetSpellName=function(id) return "client-name-"..id end
assert(HijiAddon.HandleRetCommand("ret auras"))
assert(#lines==9 and lines[2]:find("client%-name%-406086") and lines[6]:find("454351"))
assert(not HijiAddon.HandleRetCommand("ret unrelated"))
C_Spell.GetSpellName=function() error("unavailable") end
assert(HijiAddon.HandleRetCommand("ret auras"))
C_Spell.GetSpellName=function() return secret("restricted") end
assert(HijiAddon.HandleRetCommand("ret auras"))
print=savedPrint
''')
for action,key in [(1,"7"),(2,"8"),(3,"9")]:
    assert re.search(rf"CheckPt\({action}\)\)\s*\{{\s*SimulateKeyPress\(VK_{key}\)",program),action
punctuation = ["FangkuohaoZuo", "FangkuohaoYou", "Xiegang", "Fenhao", "Danyinhao", "Juhao"]
for action in range(7, 31):
    key = punctuation[(action - 7) % 6] if action < 19 else ["1", "2", "3", "4", "5", "6", "7", "8", "9", "0", "Jian", "Deng"][action - 19]
    method = "SimulateKeyPress" if action < 13 else "SimulateShiftKeyPress"
    assert re.search(rf"CheckPt\({action}\)\)\s*\{{\s*{method}\(VK_{key}\)", program), action
print("PASS: Retribution v5 collector; Radiant Glory and independent base Wake cooldown under normal/free overrides; 18 spell/form paths and 198 dual-return usability/resource cases; independent cooldown failure/recovery; secret resources/native display, overrides, own CD/GCD, charges, unknown/recovery, Hammer of Justice 10-yard gates and engaged AoE count, interruptability, items, target epoch, strict 60/80 health thresholds, instant Flash, cleanup and 27 keyboard mappings.")
print("PASS: 7 aura/talent identities match across Lua and C#; client-name diagnostics tolerate unavailable/restricted names.")
