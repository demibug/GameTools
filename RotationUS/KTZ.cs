using System.Drawing;
using System.Drawing.Printing;

class KTZ
{
    #region Singleton
    private static KTZ _inst;
    private KTZ() { }

    public static KTZ Inst
    {
        get
        {
            if (_inst == null)
            {
                _inst = new KTZ();
            }
            return _inst;
        }
    }
    #endregion

    // ÐèÒªÌø¹ý¼ì²âµÄ¼üµÄÐéÄâ¼üÂë
    public int[] skipKeys = {
        //0xC0, // ` ¼ü
        0x31, // 1
        0x32, // 2
        //0x33, // 3
        //0x34, // 4
        0x35, // 5
        //0x36, // 6
        //0x51, // Q
        //0x45, // E
        //0x52, // R
        0x54, // T
        //0x46, // F
        //0x47, // G
        //0x5A, // Z
        //0x58, // X
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
    private int m_idxRange20 = 4;
    private int m_idxHp = 5;
    private int m_idxMp = 6;
    private int m_idxIsNotCasting = 7;
    private int m_idxJunGuanMark = 8;
    private int m_idxIsTargetChanneling = 9;
    private int m_idxNeedKeepRolling = 10;

    private int m_idxPotionHealStone = 15;
    private int m_idxPotionHp = 16;

    private int m_idxGCD = 20;
    private int m_idxAdRushCD = 21;
    private int m_idxJiahuoCD = 22;
    private int m_idxInterruptCD = 23;
    private int m_idxCrimsonVialCD = 24;
    private int m_idxKeepRollingCD = 25;

    private int m_idxPistalShotRecommend = 30;
    private int m_idxSinisterStrikeRecommend = 31;
    private int m_idxDispatchRecommend = 32;
    private int m_idxBetweenEyesRecommend = 33;
    private int m_idxBladeFlurryRecommend = 34;
    private int m_idxKillingSpreeRecommend = 35;
    private int m_idxBladeRushRecommend = 36;
    private int m_idxRollTheBonesRecommend = 37;

    private int m_idxComboPoint1 = 1;
    private int m_idxComboPoint2 = 2;
    private int m_idxComboPoint3 = 3;
    private int m_idxComboPoint4 = 4;
    private int m_idxComboPoint5 = 5;
    private int m_idxComboPoint6 = 6;
    private int m_idxComboPoint7 = 7;



    private int m_keyCrimsonVial = 1;
    private int m_keyHealStone = 2;
    private int m_keyHpPotion = 3;
    private int m_keyCancelJunGuan = 4;
    private int m_keyKick = 6;
    private int m_keyPistalShot = 7;
    private int m_keySinisterStrike = 8;
    private int m_keyDispatch = 9;
    private int m_keyBetweenEyes = 10;
    private int m_keyBladeFlurry = 11;
    private int m_keyKillingSpree = 12;
    private int m_keyBladeRush = 13;
    private int m_keyAdRush = 14;
    private int m_keyRollTheBones = 15;
    private int m_keyKeepRolling = 16;

    public void Process(Dictionary<int, Color> dictFrameColors, Dictionary<int, Color> dictBarColors, Dictionary<int, bool> dictStates)
    {
        bool isCombat = GetColorBoolean(m_idxIsCombat, dictFrameColors);
        bool isAoe = GetColorBoolean(m_idxIsAoe, dictFrameColors);
        bool isRange5 = GetColorBoolean(m_idxRange5, dictFrameColors);
        bool isRange20 = GetColorBoolean(m_idxRange20, dictFrameColors);
        float hpPct = GetColorFloat(m_idxHp, dictFrameColors);
        float mpPct = GetColorFloat(m_idxMp, dictFrameColors);
        bool isNotCasting = GetColorBoolean(m_idxIsNotCasting, dictFrameColors);
        bool isJunGuanMark = GetColorBoolean(m_idxJunGuanMark, dictFrameColors);
        bool isTargetChanneling = GetColorBoolean(m_idxIsTargetChanneling, dictFrameColors);
        bool isNeedKeepRolling = GetColorBoolean(m_idxNeedKeepRolling, dictFrameColors);

        bool isHealStoneUsable = GetColorBoolean(m_idxPotionHealStone, dictFrameColors);
        bool isHpPotionUsable = GetColorBoolean(m_idxPotionHp, dictFrameColors);

        bool isGcd = GetColorBoolean(m_idxGCD, dictFrameColors);
        bool isAdRushCd = GetColorBoolean(m_idxAdRushCD, dictFrameColors);
        bool isJiahuoCd = GetColorBoolean(m_idxJiahuoCD, dictFrameColors);
        bool isKickCd = GetColorBoolean(m_idxInterruptCD, dictFrameColors);
        bool isCrimsonVialCd = GetColorBoolean(m_idxCrimsonVialCD, dictFrameColors);
        bool isKeepRollingCd = GetColorBoolean(m_idxKeepRollingCD, dictFrameColors);

        bool isPistalShotRecommend = GetColorBoolean(m_idxPistalShotRecommend, dictFrameColors);
        bool isSinisterStrikeRecommend = GetColorBoolean(m_idxSinisterStrikeRecommend, dictFrameColors);
        bool isDispatchRecommend = GetColorBoolean(m_idxDispatchRecommend, dictFrameColors);
        bool isBetweenEyesRecommend = GetColorBoolean(m_idxBetweenEyesRecommend, dictFrameColors);
        bool isBladeFlurryRecommend = GetColorBoolean(m_idxBladeFlurryRecommend, dictFrameColors);
        bool isKillingSpreeRecommend = GetColorBoolean(m_idxKillingSpreeRecommend, dictFrameColors);
        bool isBladeRushRecommend = GetColorBoolean(m_idxBladeRushRecommend, dictFrameColors);
        bool isRollTheBonesRecommend = GetColorBoolean(m_idxRollTheBonesRecommend, dictFrameColors);

        bool isComboPoint1 = GetColorBoolean(m_idxComboPoint1, dictBarColors);
        bool isComboPoint2 = GetColorBoolean(m_idxComboPoint2, dictBarColors);
        bool isComboPoint3 = GetColorBoolean(m_idxComboPoint3, dictBarColors);
        bool isComboPoint4 = GetColorBoolean(m_idxComboPoint4, dictBarColors);
        bool isComboPoint5 = GetColorBoolean(m_idxComboPoint5, dictBarColors);
        bool isComboPoint6 = GetColorBoolean(m_idxComboPoint6, dictBarColors);
        bool isComboPoint7 = GetColorBoolean(m_idxComboPoint7, dictBarColors);

        bool isProcessed = false;

        // ÐÉºì
        if (!isProcessed && isCombat && isRange20 && hpPct <= 0.6f && isCrimsonVialCd)
        {
            isProcessed = true;
            dictStates[m_keyCrimsonVial] = true;
        }

        // ÖÎÁÆÊ¯
        if (!isProcessed && isCombat && isRange20 && hpPct <= 0.3f && isHealStoneUsable)
        {
            isProcessed = true;
            dictStates[m_keyHealStone] = true;
        }

        // ÑªÆ¿
        if (!isProcessed && isCombat && isRange20 && hpPct <= 0.3f && isHpPotionUsable)
        {
            isProcessed = true;
            dictStates[m_keyHpPotion] = true;
        }

        if (!isProcessed && isJunGuanMark && !isCombat)
        {
            isProcessed = true;
            dictStates[m_keyCancelJunGuan] = true;
        }

        // ´ò¶Ï
        if (!isProcessed && isCombat && isRange5 && isTargetChanneling && isKickCd)
        {
            isProcessed = true;
            dictStates[m_keyKick] = true;
        }

        // ½£ÈÐÂÒÎè
        if (!isProcessed && isCombat && isRange5 && isNotCasting && isGcd && isBladeFlurryRecommend)
        {
            isProcessed = true;
            dictStates[m_keyBladeFlurry] = true;
        }

        // Ò¡÷»
        if (!isProcessed && isCombat && isRange20 && isNotCasting && isGcd && isRollTheBonesRecommend)
        {
            isProcessed = true;
            dictStates[m_keyRollTheBones] = true;
        }

        // ±£³ÖÒ¡÷»
        if (!isProcessed && isCombat && isRange20 && isNotCasting && isGcd && isNeedKeepRolling && isKeepRollingCd)
        {
            isProcessed = true;
            dictStates[m_keyKeepRolling] = true;
        }

        // ³å¶¯
        if (!isProcessed && isCombat && isRange5 && isGcd && !isComboPoint3 && mpPct <= 0.6f && isAdRushCd)
        {
            isProcessed = true;
            dictStates[m_keyAdRush] = true;
        }

        // ´óÇ¹
        if (!isProcessed && isCombat && isRange20 && isNotCasting && isGcd && isBetweenEyesRecommend)
        {
            isProcessed = true;
            dictStates[m_keyBetweenEyes] = true;
        }

        // É±Â¾ÃüÁî
        if (!isProcessed && isCombat && isRange5 && isNotCasting && isGcd && isKillingSpreeRecommend)
        {
            isProcessed = true;
            dictStates[m_keyKillingSpree] = true;
        }

        // µ¶·æ³å´Ì
        if (!isProcessed && isCombat && isRange5 && isNotCasting && isGcd && isBladeRushRecommend)
        {
            isProcessed = true;
            dictStates[m_keyBladeRush] = true;
        }

        // ÊÖÇ¹
        if (!isProcessed && isCombat && isRange20 && isNotCasting && isGcd && isPistalShotRecommend)
        {
            isProcessed = true;
            dictStates[m_keyPistalShot] = true;
        }

        // Õ¶»÷
        if (!isProcessed && isCombat && isRange5 && isNotCasting && isGcd && isDispatchRecommend)
        {
            isProcessed = true;
            dictStates[m_keyDispatch] = true;
        }

        // Ð°¶ñ
        if (!isProcessed && isCombat && isRange5 && isNotCasting && isGcd && isSinisterStrikeRecommend)
        {
            isProcessed = true;
            dictStates[m_keySinisterStrike] = true;
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