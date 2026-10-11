"""Exercise all three real warrior item collectors; no game input or logs.
Run: uv run --with lupa python Diagnostics/verify_warrior_items.py <addon directory>
"""
import runpy
from pathlib import Path

context = runpy.run_path(str(Path(__file__).with_name("verify_protection.py")))
runtime, root = context["runtime"], context["root"]
runtime.execute((root / "Classes/Kbz.lua").read_text(encoding="utf-8-sig"))
runtime.execute(r'''
local originalCount,originalCooldown=C_Item.GetItemCount,C_Item.GetItemCooldown
local itemCount,itemStart,itemDuration,itemFlag=1,0,0,true
local selectedItem,apiFailure
C_Item.GetItemCount=function(id)
    if id==selectedItem then return itemCount end
    return originalCount(id)
end
C_Item.GetItemCooldown=function(id)
    if id==selectedItem then
        if apiFailure then error("item cooldown unavailable") end
        return itemStart,itemDuration,itemFlag
    end
    return originalCooldown(id)
end
-- Include a backpack fixture to reproduce the old Protection implementation.
C_Container.GetContainerNumSlots=function(bagID) return bagID==0 and 2 or 0 end
C_Container.GetContainerItemInfo=function(_,slotID)
    return {itemID=slotID==1 and 5512 or 211879,stackCount=1}
end
warriorItemChecks=0
for _,spec in ipairs({{"Fury",HijiAddon.UpdateKbz},{"Arms",HijiAddon.UpdateWqz},{"Protection",HijiAddon.UpdateFz}}) do
    local name,update=spec[1],spec[2]
    HijiAddon.ClearDiagnosticData()
    for _,entry in ipairs({{11,5512},{12,211879}}) do
        local index,id=entry[1],entry[2];selectedItem=id
        local function check(r,g,b,label)
            update()
            local frame,texture=HijiAddon["frame_"..index],HijiAddon["texture_"..index]
            -- Protection's legacy reader checks only red: unknown must be false.
            if name=="Protection" and r==1 and g==.5 and b==0 then r,g,b=0,0,0 end
            local matches=(r==0 and g==0 and b==0 and not frame.shown) or (frame.shown and rgb(texture,r,g,b))
            assert(matches,name.." "..label.." id="..id)
            if name=="Protection" then
                assert((frame.shown and texture.r==1)==(r==1),name.." legacy reader "..label)
            end
            warriorItemChecks=warriorItemChecks+1
        end
        for _,flag in ipairs({true,1}) do
            itemFlag,itemCount,itemStart,itemDuration=flag,1,0,0
            check(1,1,1,"ready flag="..tostring(flag))
            itemCount=0;check(0,0,0,"missing inventory")
            itemCount=1;itemStart=GetTime();itemDuration=60
            check(0,0,0,"active cooldown")
            itemStart=GetTime()-60;check(1,1,1,"expired cooldown")
        end
        itemStart,itemDuration=0,0
        for _,flag in ipairs({false,0}) do
            itemFlag=flag;check(0,0,0,"disabled flag="..tostring(flag))
        end
        for _,flag in ipairs({secret(true),secret(1),"unknown",2}) do
            itemFlag=flag;check(1,.5,0,"restricted/invalid flag")
        end
        itemFlag=nil;check(1,.5,0,"missing flag")
        itemFlag=true;itemStart=secret(0);check(1,.5,0,"restricted start")
        itemStart=0;itemDuration=secret(0);check(1,.5,0,"restricted duration")
        itemDuration=0;itemCount=secret(1);check(1,.5,0,"restricted count")
        itemCount=1;apiFailure=true;check(1,.5,0,"cooldown API failure")
        apiFailure=false;check(1,1,1,"recovery on next sample")
    end
end
C_Item.GetItemCount,C_Item.GetItemCooldown=originalCount,originalCooldown
''')
print(f"PASS: {runtime.globals().warriorItemChecks} Fury/Arms/Protection Healthstone5512 and potion checks, boolean/legacy flags, inventory, cooldowns, disabled/unknown inputs and failure recovery; no input sent.")
