using System.Drawing;
using System.Drawing.Printing;

class FQ
{
    #region Singleton
    private static FQ _inst;
    private FQ() { }

    public static FQ Inst
    {
        get
        {
            if (_inst == null)
            {
                _inst = new FQ();
            }
            return _inst;
        }
    }
    #endregion

    // ÐèÒªÌø¹ý¼ì²âµÄ¼üµÄÐéÄâ¼üÂë
    public int[] skipKeys = {
        0xC0, // ` ¼ü
        //0x31, // 1
        //0x32, // 2
        0x33, // 3
        //0x34, // 4
        //0x35, // 5
        //0x36, // 6
        0x51, // Q
        //0x45, // E
        0x52, // R
        //0x54, // T
        //0x46, // F
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
    private int m_idxRange10 = 4;
    private int m_idxRangeFriend40 = 5;
    private int m_idxHp = 6;
    private int m_idxMp = 7;
    private int m_idxIsNotCasting = 8;
    private int m_idxHasDispelDebuff = 9;
    private int m_idxJunGuanMark = 10;
    private int m_idxIsTargetChanneling = 11;
    private int m_idxIsTargetCasting = 12;
    private int m_idxHasShiningLight = 13;
    private int m_idxIsPlayerStand = 14;

    private int m_idxPotionHealStone = 18;
    private int m_idxPotionHp = 19;

    private int m_idxGCD = 20;
    private int m_idxDivineTollCD = 21;
    private int m_idxConsecrationCD = 22;
    private int m_idxJudgmentCD = 23;
    private int m_idxHammerOfWarthCD = 24;
    private int m_idxAvengersShieldCD = 25;
    private int m_idxArdentDefenderCD = 26;
    private int m_idxInterruptCD = 27;
    private int m_idxDispelCD = 28;
    private int m_idxBlessedHammerCD = 29;

    private int m_idxDivineTollUsable = 30;
    private int m_idxJudgmentUsable = 31;
    private int m_idxHammerOfWarthUsable = 32;
    private int m_idxBlessedHammerUsable = 33;
    private int m_idxWordOfGloryUsable = 34;
    private int m_idxDispelUsable = 35;
    private int m_idxHammerOfLightUsable = 36;

    private int m_idxConsecrationRecommend = 40;

    private int m_idxHolyPower1 = 1;
    private int m_idxHolyPower2 = 2;
    private int m_idxHolyPower3 = 3;
    private int m_idxHolyPower4 = 4;
    private int m_idxHolyPower5 = 5;



    private int m_keyWordOfGlory = 1;
    private int m_keyArdentDefender = 2;
    private int m_keyHealStone = 3;
    private int m_keyHpPotion = 4;
    private int m_keyInterrupt = 5;
    private int m_keyDispel = 6;
    private int m_keyDivineToll = 7;
    private int m_keyShieldOfRighteous = 8;
    private int m_keyJudgment = 9;
    private int m_keyBlessedHammer = 10;
    private int m_keyAvengersShield = 11;
    private int m_keyConsecration = 12;
    private int m_keyCancelJunGuan = 13;

    public void Process(Dictionary<int, Color> dictFrameColors, Dictionary<int, Color> dictBarColors, Dictionary<int, bool> dictStates)
    {
        bool isCombat = GetColorBoolean(m_idxIsCombat, dictFrameColors);
        bool isAoe = GetColorBoolean(m_idxIsAoe, dictFrameColors);
        bool isRange5 = GetColorBoolean(m_idxRange5, dictFrameColors);
        bool isRange10 = GetColorBoolean(m_idxRange10, dictFrameColors);
        bool isRangeFriend40 = GetColorBoolean(m_idxRangeFriend40, dictFrameColors);
        float hpPct = GetColorFloat(m_idxHp, dictFrameColors);
        float mpPct = GetColorFloat(m_idxMp, dictFrameColors);
        bool isNotCasting = GetColorBoolean(m_idxIsNotCasting, dictFrameColors);
        bool hasDispelDebuff = GetColorBoolean(m_idxHasDispelDebuff, dictFrameColors);
        bool isJunGuanMark = GetColorBoolean(m_idxJunGuanMark, dictFrameColors);
        bool isTargetChanneling = GetColorBoolean(m_idxIsTargetChanneling, dictFrameColors);
        bool isTargetCasting = GetColorBoolean(m_idxIsTargetCasting, dictFrameColors);
        bool hasShiningLight = GetColorBoolean(m_idxHasShiningLight, dictFrameColors);
        bool isPlayerStand = GetColorBoolean(m_idxIsPlayerStand, dictFrameColors);


        bool isHealStoneUsable = GetColorBoolean(m_idxPotionHealStone, dictFrameColors);
        bool isHpPotionUsable = GetColorBoolean(m_idxPotionHp, dictFrameColors);

        bool isGCD = GetColorBoolean(m_idxGCD, dictFrameColors);
        bool isDivineTollCD = GetColorBoolean(m_idxDivineTollCD, dictFrameColors);
        bool isConsecrationCD = GetColorBoolean(m_idxConsecrationCD, dictFrameColors);
        bool isJudgmentCD = GetColorBoolean(m_idxJudgmentCD, dictFrameColors);
        bool isHammerOfWarthCD = GetColorBoolean(m_idxHammerOfWarthCD, dictFrameColors);
        bool isAvengersShieldCD = GetColorBoolean(m_idxAvengersShieldCD, dictFrameColors);
        bool isArdentDefenderCD = GetColorBoolean(m_idxArdentDefenderCD, dictFrameColors);
        bool isInterruptCD = GetColorBoolean(m_idxInterruptCD, dictFrameColors);
        bool isDispelCD = GetColorBoolean(m_idxDispelCD, dictFrameColors);
        bool isBlessedHammerCD = GetColorBoolean(m_idxBlessedHammerCD, dictFrameColors);

        bool isDivineTollUsable = GetColorBoolean(m_idxDivineTollUsable, dictFrameColors);
        bool isJudgmentUsable = GetColorBoolean(m_idxJudgmentUsable, dictFrameColors);
        bool isHammerOfWarthUsable = GetColorBoolean(m_idxHammerOfWarthUsable, dictFrameColors);
        bool isWordOfGloryUsable = GetColorBoolean(m_idxWordOfGloryUsable, dictFrameColors);
        bool isDispelUsable = GetColorBoolean(m_idxDispelUsable, dictFrameColors);
        bool isBlessedHammerUsable = GetColorBoolean(m_idxBlessedHammerUsable, dictFrameColors);
        bool isHammerOfLightUsable = GetColorBoolean(m_idxHammerOfLightUsable, dictFrameColors);

        bool isConsecrationRecommend = GetColorBoolean(m_idxConsecrationRecommend, dictFrameColors);

        bool isHolyPower1Active = GetColorBoolean(m_idxHolyPower1, dictBarColors);
        bool isHolyPower2Active = GetColorBoolean(m_idxHolyPower2, dictBarColors);
        bool isHolyPower3Active = GetColorBoolean(m_idxHolyPower3, dictBarColors);
        bool isHolyPower4Active = GetColorBoolean(m_idxHolyPower4, dictBarColors);
        bool isHolyPower5Active = GetColorBoolean(m_idxHolyPower5, dictBarColors);

        bool isProcessed = false;

        // ¹â
        if (!isProcessed && isCombat && isRange10 && hpPct < 0.5f && hasShiningLight && isWordOfGloryUsable)
        {
            isProcessed = true;
            dictStates[m_keyWordOfGlory] = true;
        }

        if (!isProcessed && isCombat && isRange10 && hpPct < 0.3f && isHolyPower3Active && isWordOfGloryUsable)
        {
            isProcessed = true;
            dictStates[m_keyWordOfGlory] = true;
        }


        // ¶Ü»÷
        if (!isProcessed && isCombat && isRange5 && isHolyPower4Active)
        {
            isProcessed = true;
            dictStates[m_keyShieldOfRighteous] = true;
        }

        // ÈÈÇÐ·ÀÓùÕß
        if (!isProcessed && isCombat && isRange10 && hpPct <= 0.2f && isArdentDefenderCD)
        {
            isProcessed = true;
            dictStates[m_keyArdentDefender] = true;
        }

        // ÖÎÁÆÊ¯
        if (!isProcessed && isCombat && isRange10 && hpPct <= 0.3f && isHealStoneUsable)
        {
            isProcessed = true;
            dictStates[m_keyHealStone] = true;
        }

        // ÑªÆ¿
        if (!isProcessed && isCombat && isRange10 && hpPct <= 0.3f && isHpPotionUsable)
        {
            isProcessed = true;
            dictStates[m_keyHpPotion] = true;
        }

        // mark
        if (!isProcessed && isJunGuanMark && !isCombat)
        {
            isProcessed = true;
            dictStates[m_keyCancelJunGuan] = true;
        }

        // ·É¶Ü´ò¶Ï
        if (!isProcessed && isCombat && isRange10 && (isTargetChanneling || isTargetCasting) && isAvengersShieldCD)
        {
            isProcessed = true;
            dictStates[m_keyAvengersShield] = true;
        }

        // ´ò¶Ï
        if (!isProcessed && isCombat && isRange5 && isTargetChanneling && isInterruptCD)
        {
            isProcessed = true;
            dictStates[m_keyInterrupt] = true;
        }

        // ÇýÉ¢×Ô¼º
        if (!isProcessed && isCombat && isRange10 && isGCD && isNotCasting && hasDispelDebuff && isDispelCD && isDispelUsable)
        {
            isProcessed = true;
            dictStates[m_keyDispel] = true;
        }

        // ÇýÉ¢¶ÓÓÑ
        if (!isProcessed && isCombat && isRangeFriend40 && isGCD && isNotCasting && hasDispelDebuff && isDispelCD && isDispelUsable)
        {
            isProcessed = true;
            dictStates[m_keyDispel] = true;
        }

        // ·É¶ÜAOE
        if (!isProcessed && isCombat && isRange10 && isGCD && isNotCasting && isAvengersShieldCD && isAoe)
        {
            isProcessed = true;
            dictStates[m_keyAvengersShield] = true;
        }

        // ÇÃÖÓ
        if (!isProcessed && isCombat && isRange10 && isGCD && isNotCasting && isDivineTollCD && isPlayerStand && !isJunGuanMark)
        {
            isProcessed = true;
            dictStates[m_keyDivineToll] = true;
        }

        // ´ó´¸
        if (!isProcessed && isCombat && isRange10 && isGCD && isNotCasting && isHammerOfLightUsable)
        {
            isProcessed = true;
            dictStates[m_keyDivineToll] = true;
        }

        // ½¨Òé·îÏ×
        if (!isProcessed && isCombat && isRange5 && isGCD && isNotCasting && isPlayerStand && isConsecrationRecommend && isConsecrationCD)
        {
            isProcessed = true;
            dictStates[m_keyConsecration] = true;
        }

        // Õ¶É±´¸
        if (!isProcessed && isCombat && isRange10 && isGCD && isNotCasting && isHammerOfWarthCD && isHammerOfWarthUsable)
        {
            isProcessed = true;
            dictStates[m_keyJudgment] = true;
        }

        // ÉóÅÐ
        if (!isProcessed && isCombat && isRange10 && isGCD && isNotCasting && isJudgmentCD && isJudgmentUsable)
        {
            isProcessed = true;
            dictStates[m_keyJudgment] = true;
        }

        // ÕýÒåÖ®´¸
        if (!isProcessed && isCombat && isRange10 && isGCD && isNotCasting && isBlessedHammerCD && isBlessedHammerUsable)
        {
            ;
            isProcessed = true;
            dictStates[m_keyBlessedHammer] = true;
        }

        // ·É¶Ü
        if (!isProcessed && isCombat && isRange10 && isGCD && isNotCasting && isAvengersShieldCD && !isAoe)
        {
            isProcessed = true;
            dictStates[m_keyAvengersShield] = true;
        }

        // ·îÏ×
        if (!isProcessed && isCombat && isRange5 && isGCD && isNotCasting && isConsecrationCD)
        {
            isProcessed = true;
            dictStates[m_keyConsecration] = true;
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