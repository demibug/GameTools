using System.Drawing;
using System.Drawing.Printing;

class WQZ
{
    #region Singleton
    private static WQZ _inst;
    private WQZ() { }

    public static WQZ Inst
    {
        get
        {
            if (_inst == null)
            {
                _inst = new WQZ();
            }
            return _inst;
        }
    }
    #endregion

    // 需要跳过检测的键的虚拟键码
    public int[] skipKeys = {
        //0xC0, // ` 键
        //0x31, // 1
        //0x32, // 2
        0x33, // 3
        //0x34, // 4
        0x35, // 5
        0x36, // 6
        //0x51, // Q
        //0x45, // E
        //0x52, // R
        0x54, // T
        0x46, // F
        //0x47, // G
        //0x5A, // Z
        0x58, // X
        //0x43, // C
        //0x56, // V
        //0x42, // B
        // 包含大写字母的组合键
        //0x31 + 0x20, 0x32 + 0x20, 0x33 + 0x20, 0x34 + 0x20, 0x35 + 0x20, 0x36 + 0x20, // Shift + 1 2 3 4 5 6
        //0x51 + 0x20, 0x45 + 0x20, 0x52 + 0x20, 0x54 + 0x20, 0x46 + 0x20, 0x47 + 0x20, // Shift + Q E R T F G
        //0x5A + 0x20, 0x58 + 0x20, 0x43 + 0x20, 0x56 + 0x20, 0x42 + 0x20 // Shift + Z X C V B
    };

    private int m_colorIdxIsCombat = 1;
    private int m_colorIdxIsAoe = 2;
    private int m_colorIdxRange5 = 3;
    private int m_colorIdxRange10 = 4;
    private int m_colorIdxRange15 = 5;
    private int m_colorIdxHp = 6;
    private int m_colorIdxMp = 7;
    private int m_colorIdxTargetHp = 8;
    private int m_colorIdxJunGuanMark = 9;
    private int m_colorIdxHasAbsorb = 10;
    private int m_colorIdxPotionHealStone = 11;
    private int m_colorIdxPotionHp = 12;
    private int m_colorIdxAvatarCD = 13;
    private int m_colorIdxColossusSmashCD = 14;
    private int m_colorIdxSweepingStrikeCD = 15;
    private int m_colorIdxBladestormCD = 16;
    private int m_colorIdxMortalStrikeCD = 17;
    private int m_colorIdxWreckThrowCD = 18;
    private int m_colorIdxRendRecommend = 20;
    private int m_colorIdxAvatarRecommend = 21;
    private int m_colorIdxThrowRecommend = 27;

    private int m_colorIdxRendUsable = 22;
    private int m_colorIdxMortalStrikeUsable = 23;
    private int m_colorIdxExecuteUsable = 24;
    private int m_colorIdxSlamUsable = 25;
    private int m_colorIdxVictoryRushUsable = 26;

    private int m_colorIdxOverpowerCharge1 = 1;
    private int m_colorIdxOverpowerCharge2 = 2;


    private int m_colorIdxIp = 31;
    private Color m_colorIp = Color.FromArgb(255, 144, 92, 18);

    private int m_keyVirtoryRush = 1;
    private int m_keyJianRen = 2;
    private int m_keyAvatar = 3;
    private int m_keyCancelJunGuan = 4;
    private int m_keyHealStone = 5;
    private int m_keyHpPotion = 6;
    private int m_keyRend = 7;
    private int m_keyColossusSmash = 8;
    private int m_keySweepStrike = 9;
    private int m_keyMortalStrike = 10;
    private int m_keyBladestrom = 11;
    private int m_keyExecute = 12;
    private int m_keyOverpower = 13;
    private int m_keyWreckThrow = 14;
    private int m_keySlam = 15;
    private int m_keyThrow = 16;
    public void Process(Dictionary<int, Color> dictFrameColors, Dictionary<int, Color> dictBarColors, Dictionary<int, bool> dictStates)
    {
        bool isCombat = GetColorBoolean(m_colorIdxIsCombat, dictFrameColors);
        bool isAoe = GetColorBoolean(m_colorIdxIsAoe, dictFrameColors);
        bool isRange15 = GetColorBoolean(m_colorIdxRange15, dictFrameColors);
        bool isRange10 = GetColorBoolean(m_colorIdxRange10, dictFrameColors);
        bool isRange5 = GetColorBoolean(m_colorIdxRange5, dictFrameColors);
        float hpPct = GetColorFloat(m_colorIdxHp, dictFrameColors);
        float mpPct = GetColorFloat(m_colorIdxMp, dictFrameColors);
        float targetHpPct = GetColorFloat(m_colorIdxTargetHp, dictFrameColors);
        bool isJunGuanMark = GetColorBoolean(m_colorIdxJunGuanMark, dictFrameColors);
        bool hasAbsorb = GetColorBoolean(m_colorIdxHasAbsorb, dictFrameColors);

        bool isHealStoneUsable = GetColorBoolean(m_colorIdxPotionHealStone, dictFrameColors);
        bool isHpPotionUsable = GetColorBoolean(m_colorIdxPotionHp, dictFrameColors);
        bool isAvatarCd = GetColorBoolean(m_colorIdxAvatarCD, dictFrameColors);
        bool isColossusSmashCd = GetColorBoolean(m_colorIdxColossusSmashCD, dictFrameColors);
        bool isSweepingStrikeCd = GetColorBoolean(m_colorIdxSweepingStrikeCD, dictFrameColors);
        bool isBladestormCd = GetColorBoolean(m_colorIdxBladestormCD, dictFrameColors);
        bool isMortalStrikeCd = GetColorBoolean(m_colorIdxMortalStrikeCD, dictFrameColors);
        bool isWreckThrowCd = GetColorBoolean(m_colorIdxWreckThrowCD, dictFrameColors);

        bool isRendRecommend = GetColorBoolean(m_colorIdxRendRecommend, dictFrameColors);
        bool isAvatarRecommend = GetColorBoolean(m_colorIdxAvatarRecommend, dictFrameColors);
        bool isThrowRecommend = GetColorBoolean(m_colorIdxThrowRecommend, dictFrameColors);

        bool isRendUsable = GetColorBoolean(m_colorIdxRendUsable, dictFrameColors);
        bool isMortalStrikeUsable = GetColorBoolean(m_colorIdxMortalStrikeUsable, dictFrameColors);
        bool isExecuteUsable = GetColorBoolean(m_colorIdxExecuteUsable, dictFrameColors);
        bool isSlamUsable = GetColorBoolean(m_colorIdxSlamUsable, dictFrameColors);
        bool isVictoryRushUsable = GetColorBoolean(m_colorIdxVictoryRushUsable, dictFrameColors);

        bool isOverpowerCharge2 = GetColorBoolean(m_colorIdxOverpowerCharge2, dictBarColors);
        bool isOverpowerCharge1 = GetColorBoolean(m_colorIdxOverpowerCharge1, dictBarColors);
        //Console.WriteLine("isShieldBlockCharge2 " + isShieldBlockCharge2 + " isShieldBlockCharge1 " + isShieldBlockCharge1 + " isShieldBlockCharge0 " + isShieldBlockCharge0);

        bool isProcessed = false;

        // 胜利在望
        if (!isProcessed && isRange10 && hpPct <= 0.7f && isVictoryRushUsable)
        {
            isProcessed = true;
            dictStates[m_keyVirtoryRush] = true;
        }

        // 治疗石
        if (!isProcessed && isRange10 && hpPct <= 0.4f && isHealStoneUsable)
        {
            isProcessed = true;
            dictStates[m_keyHealStone] = true;
        }

        // 血瓶
        if (!isProcessed && isRange10 && hpPct <= 0.4f && isHpPotionUsable)
        {
            isProcessed = true;
            dictStates[m_keyHpPotion] = true;
        }

        // wreck throw
        if (!isProcessed && isRange15 && isWreckThrowCd && hasAbsorb)
        {
            isProcessed = true;
            dictStates[m_keyWreckThrow] = true;
        }

        // rend
        if (!isProcessed && isRange10 && isRendRecommend && isRendUsable)
        {
            isProcessed = true;
            dictStates[m_keyRend] = true;
        }

        // avatar
        if (!isProcessed && isRange10 && isAvatarRecommend && isColossusSmashCd)
        {
            isProcessed = true;
            dictStates[m_keyAvatar] = true;
        }

        // colossus smash
        if (!isProcessed && isRange10 && isColossusSmashCd)
        {
            isProcessed = true;
            dictStates[m_keyColossusSmash] = true;
        }

        // sweeping strike
        if (!isProcessed && isRange10 && isAoe && isSweepingStrikeCd)
        {
            isProcessed = true;
            dictStates[m_keySweepStrike] = true;
        }

        // overpower 2 charges 
        if (!isProcessed && isRange10 && isOverpowerCharge2)
        {
            isProcessed = true;
            dictStates[m_keyOverpower] = true;
        }

        // mortal strike
        if (!isProcessed && isRange10 && isMortalStrikeUsable && isMortalStrikeCd)
        {
            isProcessed = true;
            dictStates[m_keyMortalStrike] = true;
        }

        // bladestorm
        if (!isProcessed && isRange5 && isBladestormCd)
        {
            isProcessed = true;
            dictStates[m_keyBladestrom] = true;
        }

        // execute
        if (!isProcessed && isRange10 && isExecuteUsable && (!isAoe || targetHpPct >= 0.35f))
        {
            isProcessed = true;
            dictStates[m_keyExecute] = true;
        }

        // overpower
        if (!isProcessed && isRange10 && isOverpowerCharge1)
        {
            isProcessed = true;
            dictStates[m_keyOverpower] = true;
        }

        // slam
        if (!isProcessed && isRange10 && isSlamUsable && !isAoe && (!isMortalStrikeCd || mpPct >= 0.5f))
        {
            isProcessed = true;
            dictStates[m_keySlam] = true;
        }

        // aoe execute
        if (!isProcessed && isRange10 && isExecuteUsable && isAoe && targetHpPct <= 0.35f)
        {
            isProcessed = true;
            dictStates[m_keyExecute] = true;
        }

        // wreck throw
        if (!isProcessed && isRange10 && isWreckThrowCd)
        {
            isProcessed = true;
            dictStates[m_keyWreckThrow] = true;
        }

        if (!isProcessed && isRange15 && isThrowRecommend)
        {
            isProcessed = true;
            dictStates[m_keyThrow] = true;
        }
    }

    private bool GetColorBoolean(int colorIdx, Dictionary<int, Color> dictColors)
    {
        Color color = dictColors[colorIdx];
        if (color.R == 255)
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    private float GetColorFloat(int colorIdx, Dictionary<int, Color> dictColors)
    {
        Color color = dictColors[colorIdx];
        return color.R / 255.0f;
    }

    private bool GetColorSpecial(int colorIdx, Dictionary<int, Color> dictColors, Color targetColor)
    {
        Color color = dictColors[colorIdx];
        if (color.R == targetColor.R && color.G == targetColor.G && color.B == targetColor.B)
        {
            return true;
        }
        else
        {
            return false;
        }
    }

}