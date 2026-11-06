using System.Drawing;

class TF
{
    #region Singleton
    private static TF _inst;
    private TF() { }

    public static TF Inst
    {
        get
        {
            if (_inst == null)
            {
                _inst = new TF();
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
        //0x33, // 3
        //0x34, // 4
        0x35, // 5
        //0x36, // 6
        //0x51, // Q
        //0x45, // E
        0x52, // R
        0x54, // T
        0x46, // F
        //0x47, // G
        //0x5A, // Z
        0x58, // X
        0x43, // C
        //0x56, // V
        0x42, // B
        // 包含大写字母的组合键
        //0x31 + 0x20, 0x32 + 0x20, 0x33 + 0x20, 0x34 + 0x20, 0x35 + 0x20, 0x36 + 0x20, // Shift + 1 2 3 4 5 6
        //0x51 + 0x20, 0x45 + 0x20, 0x52 + 0x20, 0x54 + 0x20, 0x46 + 0x20, 0x47 + 0x20, // Shift + Q E R T F G
        //0x5A + 0x20, 0x58 + 0x20, 0x43 + 0x20, 0x56 + 0x20, 0x42 + 0x20 // Shift + Z X C V B
    };

    private int m_idxIsInCombat = 1;
    private int m_idxIsAoe = 2;
    private int m_idxRange5 = 3;
    private int m_idxRange10 = 4;
    private int m_idxRange15 = 5;
    private int m_idxRange20 = 6;
    private int m_idxHp = 7;
    private int m_idxMp = 8;
    private int m_idxTargetHp = 9;
    private int m_idxIsNotCasting = 10;
    private int m_idxJunGuanMark = 11;
    private int m_idxCastingSlicingWinds = 12;
    private int m_idxHasHuoxueBuff = 13;

    private int m_idxPotionHealStone = 15;
    private int m_idxPotionHp = 16;

    private int m_idxGCD = 20;
    private int m_idxDeathTouchCD = 21;
    private int m_idxWindlordStrikeCD = 22;
    private int m_idxFirstsFuryCD = 23;
    private int m_idxRisingSunKickCD = 24;
    private int m_idxSlicingWindsCD = 25;
    private int m_idxBlackoutKickCD = 26;
    private int m_idxYihuajiemuCD = 27;

    private int m_idxDeathTouchUsable = 30;
    private int m_idxWindlordStrikeUsable = 31;
    private int m_idxFirstsFuryUsable = 32;
    private int m_idxRisingSunKickUsable = 33;
    private int m_idxSlicingWindsUsable = 34;
    private int m_idxBlackoutKickUsable = 35;
    private int m_idxTigerPalmUsable = 36;
    private int m_idxYihuajiemuUsable = 37;
    private int m_idxHuoxueshuUsable = 38;

    private int m_idxLastBlackoutKick = 40;


    private int m_keyYihuajiemu = 1;
    private int m_keyHuoxueshu = 2;
    private int m_keyHealStone = 5;
    private int m_keyHpPotion = 6;
    private int m_keyDeathTouch = 7;
    private int m_keyWindlordStrike = 8;
    private int m_keyFirstsFury = 9;
    private int m_keyRisingSunKick = 10;
    private int m_keySlicingWinds = 11;
    private int m_keyBlackoutKick = 12;
    private int m_keyTigerPalm = 13;

    public void Process(Dictionary<int, Color> dictFrameColors, Dictionary<int, Color> dictBarColors, Dictionary<int, bool> dictStates)
    {
        bool isCombat = GetColorBoolean(m_idxIsInCombat, dictFrameColors);
        bool isAoe = GetColorBoolean(m_idxIsAoe, dictFrameColors);
        bool isRange5 = GetColorBoolean(m_idxRange5, dictFrameColors);
        bool isRange10 = GetColorBoolean(m_idxRange10, dictFrameColors);
        bool isRange15 = GetColorBoolean(m_idxRange15, dictFrameColors);
        bool isRange20 = GetColorBoolean(m_idxRange20, dictFrameColors);
        float hpPct = GetColorFloat(m_idxHp, dictFrameColors);
        float mpPct = GetColorFloat(m_idxMp, dictFrameColors);
        float targetHpPct = GetColorFloat(m_idxTargetHp, dictFrameColors);
        bool isNotCasting = GetColorBoolean(m_idxIsNotCasting, dictFrameColors);
        bool isJunGuanMark = GetColorBoolean(m_idxJunGuanMark, dictFrameColors);
        bool isCastingSlicingWinds = GetColorBoolean(m_idxCastingSlicingWinds, dictFrameColors);
        bool isHasHuoxueBuff = GetColorBoolean(m_idxHasHuoxueBuff, dictFrameColors);
        bool isHealStoneUsable = GetColorBoolean(m_idxPotionHealStone, dictFrameColors);
        bool isHpPotionUsable = GetColorBoolean(m_idxPotionHp, dictFrameColors);

        bool isGCD = GetColorBoolean(m_idxGCD, dictFrameColors);
        bool isDeathTouchCD = GetColorBoolean(m_idxDeathTouchCD, dictFrameColors);
        bool iswindlordStrikeCD = GetColorBoolean(m_idxWindlordStrikeCD, dictFrameColors);
        bool isFirstsFuryCD = GetColorBoolean(m_idxFirstsFuryCD, dictFrameColors);
        bool isRisingSunKickCD = GetColorBoolean(m_idxRisingSunKickCD, dictFrameColors);
        bool isSlicingWindsCD = GetColorBoolean(m_idxSlicingWindsCD, dictFrameColors);
        bool isBlackoutKickCD = GetColorBoolean(m_idxBlackoutKickCD, dictFrameColors);
        bool isYihuajiemuCD = GetColorBoolean(m_idxYihuajiemuCD, dictFrameColors);

        bool isDeathTouchUsable = GetColorBoolean(m_idxDeathTouchUsable, dictFrameColors);
        bool isWindlordStrikeUsable = GetColorBoolean(m_idxWindlordStrikeUsable, dictFrameColors);
        bool isFirstsFuryUsable = GetColorBoolean(m_idxFirstsFuryUsable, dictFrameColors);
        bool isRisingSunKickUsable = GetColorBoolean(m_idxRisingSunKickUsable, dictFrameColors);
        bool isSlicingWindsUsable = GetColorBoolean(m_idxSlicingWindsUsable, dictFrameColors);
        bool isBlackoutKickUsable = GetColorBoolean(m_idxBlackoutKickUsable, dictFrameColors);
        bool isTigerPalmUsable = GetColorBoolean(m_idxTigerPalmUsable, dictFrameColors);
        bool isYihuajiemuUsable = GetColorBoolean(m_idxYihuajiemuUsable, dictFrameColors);
        bool isHuoxueshuUsable = GetColorBoolean(m_idxHuoxueshuUsable, dictFrameColors);

        bool isLastBlackoutKick = GetColorBoolean(m_idxLastBlackoutKick, dictFrameColors);



        bool isProcessed = false;

        // 移花接木
        if (!isProcessed && isCombat &&  isRange10 && hpPct <= 0.8f && isYihuajiemuUsable && isYihuajiemuCD)
        {
            isProcessed = true;
            dictStates[m_keyYihuajiemu] = true;
        }

        // 活血
        if (!isProcessed && isCombat && isRange10 && hpPct <= 0.8f && isHuoxueshuUsable && isHasHuoxueBuff)
        {
            isProcessed = true;
            dictStates[m_keyHuoxueshu] = true;
        }

        // 治疗石
        if (!isProcessed && isCombat && isRange10 && hpPct <= 0.4f && isHealStoneUsable)
        {
            isProcessed = true;
            dictStates[m_keyHealStone] = true;
        }

        // 血瓶
        if (!isProcessed && isCombat && isRange10 && hpPct <= 0.4f && isHpPotionUsable)
        {
            isProcessed = true;
            dictStates[m_keyHpPotion] = true;
        }

        // 轮回
        if (!isProcessed && isCombat && isNotCasting && isRange5 && isDeathTouchCD && isDeathTouchUsable)
        {
            isProcessed = true;
            dictStates[m_keyDeathTouch] = true;
        }

        // 风领主之击
        if (!isProcessed && isCombat && isNotCasting && isRange5 && iswindlordStrikeCD && isWindlordStrikeUsable)
        {
            isProcessed = true;
            dictStates[m_keyWindlordStrike] = true;
        }

        // 切削之风 aoe
        if (!isProcessed && isCombat && isNotCasting && isRange5 && isSlicingWindsCD && isSlicingWindsUsable && isAoe)
        {
            isProcessed = true;
            dictStates[m_keySlicingWinds] = true;
        }
        if (!isProcessed && isCombat && isCastingSlicingWinds && isRange5 && isSlicingWindsCD && isSlicingWindsUsable)
        {
            isProcessed = true;
            dictStates[m_keySlicingWinds] = true;
        }

        // 怒雷破
        if (!isProcessed && isCombat && isNotCasting && isRange5 && isFirstsFuryCD && isFirstsFuryUsable && !iswindlordStrikeCD)
        {
            isProcessed = true;
            dictStates[m_keyFirstsFury] = true;
        }

        // 旭日东升踢
        if (!isProcessed && isCombat && isNotCasting && isRange5 && isRisingSunKickCD && isRisingSunKickUsable && !iswindlordStrikeCD && !isFirstsFuryCD)
        {
            isProcessed = true;
            dictStates[m_keyRisingSunKick] = true;
        }

        // 切削之风
        if (!isProcessed && isCombat && isNotCasting && isRange5 && isSlicingWindsCD && isSlicingWindsUsable && !isAoe)
        {
            isProcessed = true;
            dictStates[m_keySlicingWinds] = true;
        }
        if (!isProcessed && isCombat && isCastingSlicingWinds && isRange5 && isSlicingWindsCD && isSlicingWindsUsable)
        {
            isProcessed = true;
            dictStates[m_keySlicingWinds] = true;
        }

        // 幻灭踢
        if (!isProcessed && isCombat && isNotCasting && isRange5 && isBlackoutKickCD && isBlackoutKickUsable && !isLastBlackoutKick && !iswindlordStrikeCD && !isFirstsFuryCD && !isRisingSunKickCD)
        {
            isProcessed = true;
            dictStates[m_keyBlackoutKick] = true;
        }

        // 猛虎掌
        if (!isProcessed && isCombat && isNotCasting && isRange5 && isTigerPalmUsable)
        {
            isProcessed = true;
            dictStates[m_keyTigerPalm] = true;
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