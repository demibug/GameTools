using System.Drawing;
using System.Drawing.Printing;

class BEAR
{
    #region Singleton
    private static BEAR _inst;
    private BEAR() { }

    public static BEAR Inst
    {
        get
        {
            if (_inst == null)
            {
                _inst = new BEAR();
            }
            return _inst;
        }
    }
    #endregion

    // ÐèÒªÌø¹ý¼ì²âµÄ¼üµÄÐéÄâ¼üÂë
    public int[] skipKeys = {
        //0xC0, // ` ¼ü
        //0x31, // 1
        0x32, // 2
        //0x33, // 3
        //0x34, // 4
        //0x35, // 5
        //0x36, // 6
        //0x51, // Q
        //0x45, // E
        0x52, // R
        0x54, // T
        0x46, // F
        0x47, // G
        //0x5A, // Z
        0x58, // X
        //0x43, // C
        0x56, // V
        0x42, // B
        // °üº¬´óÐ´×ÖÄ¸µÄ×éºÏ¼ü
        //0x31 + 0x20, 0x32 + 0x20, 0x33 + 0x20, 0x34 + 0x20, 0x35 + 0x20, 0x36 + 0x20, // Shift + 1 2 3 4 5 6
        //0x51 + 0x20, 0x45 + 0x20, 0x52 + 0x20, 0x54 + 0x20, 0x46 + 0x20, 0x47 + 0x20, // Shift + Q E R T F G
        //0x5A + 0x20, 0x58 + 0x20, 0x43 + 0x20, 0x56 + 0x20, 0x42 + 0x20 // Shift + Z X C V B
    };

    private int m_idxIsCombat = 1;
    private int m_idxIsAoe = 2;
    private int m_idxRange5 = 3;
    private int m_idxRange13 = 4;
    private int m_idxRangeFriend40 = 5;
    private int m_idxHp = 6;
    private int m_idxMp = 7;
    private int m_idxPlayerIsNotCasting = 8;
    private int m_idxHasDispelDebuff = 9;
    private int m_idxJunGuanMark = 10;
    //private int m_idxFoodMark = 11;
    //private int m_idxStablesMark = 12;
    private int m_idxIsTargetCasting = 12;
    private int m_idxHasIronfur = 13;
    private int m_idxHasRageGenerate = 14;
    private int m_idxHasUrsol = 15;
    private int m_idxIsPlayerStand = 16;

    private int m_idxPotionHealStone = 18;
    private int m_idxPotionHp = 19;

    private int m_idxGCD = 20;
    private int m_idxThrashCD = 21;
    private int m_idxMangleCD = 22;
    private int m_idxLunarBeamCD = 23;
    private int m_idxRageGenerateCD = 24;
    private int m_idxSurvivalInstinctsCD = 25;
    private int m_idxInterruptCD = 26;
    private int m_idxClearCD = 27;
    private int m_idxTyphoonCD = 28;
    private int m_idxAnfuCD = 29;

    private int m_idxRageGenerateUsable = 30;
    private int m_idxClearUsable = 31;
    private int m_idxIronfurUsable = 32;
    private int m_idxRavageUsable = 33;
    private int m_idxRazeUsable = 34;
    private int m_idxAnfuUsable = 35;

    private int m_idxMoonfireRecommend = 40;



    private int m_keyRageGenerate = 1;
    private int m_keyHealStone = 2;
    private int m_keyHpPotion = 3;
    private int m_keySurvivalInstincts = 4;
    private int m_keyInterrupt = 5;
    private int m_keyClear = 6;
    private int m_keyIronfur = 7;
    private int m_keyRavage = 8;
    private int m_keyThrash = 9;
    private int m_keyMangle = 10;
    private int m_keyMoonfire = 11;
    private int m_keySwipe = 12;
    private int m_keyLunarBeam = 13;
    private int m_keyTyphoon = 14;
    private int m_keyAnfu = 15;

    private int m_keyCancelJunGuanMark = 28;
    private int m_keyCancelFoodMark = 29;
    private int m_keyCancelStablesMark = 30;

    public void Process(Dictionary<int, Color> dictFrameColors, Dictionary<int, Color> dictBarColors, Dictionary<int, bool> dictStates)
    {
        bool isCombat = GetColorBoolean(m_idxIsCombat, dictFrameColors);
        bool isAoe = GetColorBoolean(m_idxIsAoe, dictFrameColors);
        bool isRange5 = GetColorBoolean(m_idxRange5, dictFrameColors);
        bool isRange13 = GetColorBoolean(m_idxRange13, dictFrameColors);
        bool isRangeFriend40 = GetColorBoolean(m_idxRangeFriend40, dictFrameColors);
        float hpPct = GetColorFloat(m_idxHp, dictFrameColors);
        float mpPct = GetColorFloat(m_idxMp, dictFrameColors);
        bool isPlayerNotCasting = GetColorBoolean(m_idxPlayerIsNotCasting, dictFrameColors);
        bool hasDispelDebuff = GetColorBoolean(m_idxHasDispelDebuff, dictFrameColors);
        bool isJunGuanMark = GetColorBoolean(m_idxJunGuanMark, dictFrameColors);
        //bool isFoodMark = GetColorBoolean(m_idxFoodMark, dictFrameColors);
        //bool isStablesMark = GetColorBoolean(m_idxStablesMark, dictFrameColors);
        bool isTargetCasting = GetColorBoolean(m_idxIsTargetCasting, dictFrameColors);
        bool hasIronfur = GetColorBoolean(m_idxHasIronfur, dictFrameColors);
        bool hasRageGenerate = GetColorBoolean(m_idxHasRageGenerate, dictFrameColors);
        bool hasUrsol = GetColorBoolean(m_idxHasUrsol, dictFrameColors);
        bool isPlayerStand = GetColorBoolean(m_idxIsPlayerStand, dictFrameColors);


        bool isHealStoneUsable = GetColorBoolean(m_idxPotionHealStone, dictFrameColors);
        bool isHpPotionUsable = GetColorBoolean(m_idxPotionHp, dictFrameColors);

        bool isGCD = GetColorBoolean(m_idxGCD, dictFrameColors);
        bool isThrashCD = GetColorBoolean(m_idxThrashCD, dictFrameColors);
        bool isMangleCD = GetColorBoolean(m_idxMangleCD, dictFrameColors);
        bool isLunarBeamCD = GetColorBoolean(m_idxLunarBeamCD, dictFrameColors);
        bool isRageGenerateCD = GetColorBoolean(m_idxRageGenerateCD, dictFrameColors);
        bool isSurvivalInstinctsCD = GetColorBoolean(m_idxSurvivalInstinctsCD, dictFrameColors);
        bool isInterruptCD = GetColorBoolean(m_idxInterruptCD, dictFrameColors);
        bool isClearCD = GetColorBoolean(m_idxClearCD, dictFrameColors);
        bool isTyphoonCD = GetColorBoolean(m_idxTyphoonCD, dictFrameColors);
        bool isAnfuCD = GetColorBoolean(m_idxAnfuCD, dictFrameColors);

        bool isRageGenerateUsable = GetColorBoolean(m_idxRageGenerateUsable, dictFrameColors);
        bool isClearUsable = GetColorBoolean(m_idxClearUsable, dictFrameColors);
        bool isIronfurUsable = GetColorBoolean(m_idxIronfurUsable, dictFrameColors);
        bool isRavageUsable = GetColorBoolean(m_idxRavageUsable, dictFrameColors);
        bool isRazeUsable = GetColorBoolean(m_idxRazeUsable, dictFrameColors);
        bool isAnfuUsable = GetColorBoolean(m_idxAnfuUsable, dictFrameColors);

        bool isMoonfireRecommend = GetColorBoolean(m_idxMoonfireRecommend, dictFrameColors);

        bool isProcessed = false;

        // ¿ñ±©»Ø¸´
        if (!isProcessed && isCombat && isRange13 && hpPct < 0.7f && isRageGenerateCD && isRageGenerateUsable && !hasRageGenerate)
        {
            isProcessed = true;
            dictStates[m_keyRageGenerate] = true;
        }

        // ÖÎÁÆÊ¯
        if (!isProcessed && isCombat && isRange13 && hpPct <= 0.3f && isHealStoneUsable)
        {
            isProcessed = true;
            dictStates[m_keyHealStone] = true;
        }

        // ÑªÆ¿
        if (!isProcessed && isCombat && isRange13 && hpPct <= 0.3f && isHpPotionUsable)
        {
            isProcessed = true;
            dictStates[m_keyHpPotion] = true;
        }

        // interrupt mark
        if (!isProcessed && isJunGuanMark && (!isCombat || !isTargetCasting || !isInterruptCD || !isRange13))
        {
            isProcessed = true;
            dictStates[m_keyCancelJunGuanMark] = true;
        }

        // ´ò¶Ï
        if (!isProcessed && isCombat && isRange13 && isTargetCasting && isInterruptCD && isJunGuanMark)
        {
            isProcessed = true;
            dictStates[m_keyInterrupt] = true;
        }

        // Éú´æ±¾ÄÜ
        if (!isProcessed && isCombat && isRange13 && hpPct < 0.3f && isSurvivalInstinctsCD)
        {
            isProcessed = true;
            dictStates[m_keySurvivalInstincts] = true;
        }

        // ÇýÉ¢×Ô¼º
        if (!isProcessed && isCombat && isRange13 && isGCD && isPlayerNotCasting && hasDispelDebuff && isClearCD && isClearUsable)
        {
            isProcessed = true;
            dictStates[m_keyClear] = true;
        }

        // ÇýÉ¢¶ÓÓÑ
        if (!isProcessed && isCombat && isRangeFriend40 && isGCD && isPlayerNotCasting && hasDispelDebuff && isClearCD && isClearUsable)
        {
            isProcessed = true;
            dictStates[m_keyClear] = true;
        }

        // Ìú××
        if (!isProcessed && isCombat && isRange13 && isPlayerNotCasting && isIronfurUsable && !hasIronfur && !isGCD)
        {
            isProcessed = true;
            dictStates[m_keyIronfur] = true;
        }

        // »ÙÃð
        if (!isProcessed && isCombat && isRange5 && isGCD && isPlayerNotCasting && isRavageUsable)
        {
            isProcessed = true;
            dictStates[m_keyRavage] = true;
        }

        // Ìú××
        if (!isProcessed && isCombat && isRange13 && isPlayerNotCasting && !isGCD && mpPct >= 0.95f)
        {
            isProcessed = true;
            dictStates[m_keyIronfur] = true;
        }

        // ´ÝÕÛ
        if (!isProcessed && isCombat && isRange5 && isGCD && isPlayerNotCasting && isRazeUsable && mpPct > 0.8f)
        {
            isProcessed = true;
            dictStates[m_keyRavage] = true;
        }

        // ÔÂ»ð
        if (!isProcessed && isCombat && isRange13 && isGCD && isPlayerNotCasting && isMoonfireRecommend)
        {
            isProcessed = true;
            dictStates[m_keyMoonfire] = true;
        }

        // ÎÚË÷¶û
        if (!isProcessed && isCombat && isRange13 && isGCD && isPlayerNotCasting && isTyphoonCD && hasUrsol)
        {
            isProcessed = true;
            dictStates[m_keyTyphoon] = true;
        }

        // Í´»÷
        if (!isProcessed && isCombat && isRange5 && isGCD && isPlayerNotCasting && isThrashCD)
        {
            isProcessed = true;
            dictStates[m_keyThrash] = true;
        }

        // ÁÑÉË
        if (!isProcessed && isCombat && isRange5 && isGCD && isPlayerNotCasting && isMangleCD && mpPct <= 0.83f)
        {
            isProcessed = true;
            dictStates[m_keyMangle] = true;
        }

        // ÔÂÒ«
        if (!isProcessed && isCombat && isRange13 && isGCD && isPlayerNotCasting && isLunarBeamCD && isPlayerStand)
        {
            isProcessed = true;
            dictStates[m_keyLunarBeam] = true;
        }

        // ºáÉ¨
        if (!isProcessed && isCombat && isRange5 && isGCD && isPlayerNotCasting)
        {
            isProcessed = true;
            dictStates[m_keySwipe] = true;
        }

        // ÔÂ»ð
        if (!isProcessed && isCombat && isRange13 && isGCD && isPlayerNotCasting)
        {
            isProcessed = true;
            dictStates[m_keyMoonfire] = true;
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