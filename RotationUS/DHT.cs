using System.Drawing;
using System.Drawing.Printing;

class DHT
{
    #region Singleton
    private static DHT _inst;
    private DHT() { }

    public static DHT Inst
    {
        get
        {
            if (_inst == null)
            {
                _inst = new DHT();
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
        0x5A, // Z
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
    private int m_idxRange15 = 5;
    private int m_idxHp = 6;
    private int m_idxMp = 7;
    private int m_idxPlayerIsNotCasting = 8;
    private int m_idxCanDispel = 9;
    private int m_idxJunGuanMark = 10;
    private int m_idxIsTargetCasting = 11;
    private int m_idxIsPlayerStand = 12;
    private int m_idxHasJianci = 13;
    private int m_idxFoodMark = 14;

    private int m_idxPotionHealStone = 18;
    private int m_idxPotionHp = 19;

    private int m_idxGCD = 20;
    private int m_idxInterruptCD = 21;
    private int m_idxJianciCD = 22;
    private int m_idxInfernalStrikeCD = 23;
    private int m_idxPolieCD = 24;
    private int m_idxBombCD = 25;
    private int m_idxFireBrandCD = 26;
    private int m_idxFireSigilCD = 27;
    private int m_idxImmolationAuraCD = 28;
    private int m_idxSigilSpiteCD = 29;
    private int m_idxFelDevasCD = 30;
    private int m_idxFelbladeCD = 31;
    private int m_idxThrowGlaiveCD = 32;
    private int m_idxDispelCD = 33;

    private int m_idxBombUsable = 34;
    private int m_idxSoulCleaveUsable = 35;
    private int m_idxFelDevasUsable = 36;

    private int m_idxFireSigilRecommend = 40;

    private int m_idxSoulShape = 51;

    private int m_idxPolieCharge1 = 1;
    private int m_idxPolieCharge2 = 2;
    private int m_idxInfernalStrikeCharge1 = 3;
    private int m_idxInfernalStrikeCharge2 = 4;



    private int m_keyHealStone = 1;
    private int m_keyHpPotion = 2;
    private int m_keyInterrupt = 3;
    private int m_keyDispel = 4;
    private int m_keyJianci = 5;
    private int m_keyInfernalStrike = 6;
    private int m_keyPolie = 7;
    private int m_keyBomb = 8;
    private int m_keyFireBrand = 9;
    private int m_keyFireSigil = 10;
    private int m_keyImmolationAura = 11;
    private int m_keySigilSpite = 12;
    private int m_keySoulCleave = 13;
    private int m_keyFelDevas = 14;
    private int m_keyFelblade = 15;
    private int m_keyThrowGlaive = 16;
    private int m_keyCancelFoodMark = 29;
    private int m_keyCancelJunGuan = 30;

    public void Process(Dictionary<int, Color> dictFrameColors, Dictionary<int, Color> dictBarColors, Dictionary<int, bool> dictStates)
    {
        bool isCombat = GetColorBoolean(m_idxIsCombat, dictFrameColors);
        bool isAoe = GetColorBoolean(m_idxIsAoe, dictFrameColors);
        bool isRange5 = GetColorBoolean(m_idxRange5, dictFrameColors);
        bool isRange10 = GetColorBoolean(m_idxRange10, dictFrameColors);
        bool isRange15 = GetColorBoolean(m_idxRange15, dictFrameColors);
        float hpPct = GetColorFloat(m_idxHp, dictFrameColors);
        float mpPct = GetColorFloat(m_idxMp, dictFrameColors);
        bool playerIsNotCasting = GetColorBoolean(m_idxPlayerIsNotCasting, dictFrameColors);
        bool canDispel = GetColorBoolean(m_idxCanDispel, dictFrameColors);
        bool isJunGuanMark = GetColorBoolean(m_idxJunGuanMark, dictFrameColors);
        bool isTargetCasting = GetColorBoolean(m_idxIsTargetCasting, dictFrameColors);
        bool isPlayerStand = GetColorBoolean(m_idxIsPlayerStand, dictFrameColors);
        bool hasJianci = GetColorBoolean(m_idxHasJianci, dictFrameColors);
        bool isFoodMark = GetColorBoolean(m_idxFoodMark, dictFrameColors);


        bool isHealStoneUsable = GetColorBoolean(m_idxPotionHealStone, dictFrameColors);
        bool isHpPotionUsable = GetColorBoolean(m_idxPotionHp, dictFrameColors);

        bool isGCD = GetColorBoolean(m_idxGCD, dictFrameColors);
        bool isInterruptCD = GetColorBoolean(m_idxInterruptCD, dictFrameColors);
        bool isJianciCD = GetColorBoolean(m_idxJianciCD, dictFrameColors);
        bool isInfernalStrikeCD = GetColorBoolean(m_idxInfernalStrikeCD, dictFrameColors);
        bool isPolieCD = GetColorBoolean(m_idxPolieCD, dictFrameColors);
        bool isBombCD = GetColorBoolean(m_idxBombCD, dictFrameColors);
        bool isFireBrandCD = GetColorBoolean(m_idxFireBrandCD, dictFrameColors);
        bool isFireSigilCD = GetColorBoolean(m_idxFireSigilCD, dictFrameColors);
        bool isImmolationAuraCD = GetColorBoolean(m_idxImmolationAuraCD, dictFrameColors);
        bool isSigilSpiteCD = GetColorBoolean(m_idxSigilSpiteCD, dictFrameColors);
        bool isFelDevasCD = GetColorBoolean(m_idxFelDevasCD, dictFrameColors);
        bool isFelbladeCD = GetColorBoolean(m_idxFelbladeCD, dictFrameColors);
        bool isThrowGlaiveCD = GetColorBoolean(m_idxThrowGlaiveCD, dictFrameColors);
        bool isDispelCD = GetColorBoolean(m_idxDispelCD, dictFrameColors);

        bool isBombUsable = GetColorBoolean(m_idxBombUsable, dictFrameColors);
        bool isSoulCleaveUsable = GetColorBoolean(m_idxSoulCleaveUsable, dictFrameColors);
        bool isFelDevasUsable = GetColorBoolean(m_idxFelDevasUsable, dictFrameColors);

        bool isFireSigilRecommend = GetColorBoolean(m_idxFireSigilRecommend, dictFrameColors);
        
        bool hasSoulShape = GetColorSpecial(m_idxSoulShape, dictFrameColors);

        bool isPolieCharge1 = GetColorBoolean(m_idxPolieCharge1, dictBarColors);
        bool isPolieCharge2 = GetColorBoolean(m_idxPolieCharge2, dictBarColors);
        bool isInfernalStrikeCharge1 = GetColorBoolean(m_idxInfernalStrikeCharge1, dictBarColors);
        bool isInfernalStrikeCharge2 = GetColorBoolean(m_idxInfernalStrikeCharge2, dictBarColors);

        bool isProcessed = false;

        float currentMp = mpPct * 120;

        // ÖÎÁÆÊ¯
        if (!isProcessed && isCombat && isRange15 && hpPct <= 0.3f && isHealStoneUsable)
        {
            isProcessed = true;
            dictStates[m_keyHealStone] = true;
        }

        // ÑªÆ¿
        if (!isProcessed && isCombat && isRange15 && hpPct <= 0.3f && isHpPotionUsable)
        {
            isProcessed = true;
            dictStates[m_keyHpPotion] = true;
        }

        // mark
        if (!isProcessed && isJunGuanMark && (!isCombat || (!isFireBrandCD && isGCD)))
        {
            isProcessed = true;
            dictStates[m_keyCancelJunGuan] = true;
        }

        // interrupt mark
        if (!isProcessed && isFoodMark && (!isCombat || !isTargetCasting || !isInterruptCD || !isRange10))
        {
            isProcessed = true;
            dictStates[m_keyCancelFoodMark] = true;
        }

        // ´ò¶Ï
        if (!isProcessed && isCombat && isRange10 && isTargetCasting && isInterruptCD && isJunGuanMark)
        {
            isProcessed = true;
            dictStates[m_keyInterrupt] = true;
        }

        // ¼â´Ì
        if (!isProcessed && isCombat && isRange15 && playerIsNotCasting && !hasJianci && isJianciCD)
        {
            isProcessed = true;
            dictStates[m_keyJianci] = true;
        }

        // ÇýÉ¢
        if (!isProcessed && isCombat && isRange15 && isGCD && playerIsNotCasting && canDispel && isDispelCD)
        {
            isProcessed = true;
            dictStates[m_keyDispel] = true;
        }

        // µØÓü»ð×²»÷
        if (!isProcessed && isCombat && isRange5 && playerIsNotCasting && isGCD && isPlayerStand && isInfernalStrikeCD && isInfernalStrikeCharge2)
        {
            isProcessed = true;
            dictStates[m_keyInfernalStrike] = true;
        }

        // ÆÆÁÑ
        if (!isProcessed && isCombat && isRange5 && playerIsNotCasting && isGCD && isPolieCD && isPolieCharge2)
        {
            isProcessed = true;
            dictStates[m_keyPolie] = true;
        }

        // Õ¨µ¯
        if (!isProcessed && isCombat && isRange5 && playerIsNotCasting && isGCD && isBombUsable && isBombCD && hasSoulShape)
        {
            isProcessed = true;
            dictStates[m_keyBomb] = true;
        }

        // ÁÒ»ðÀÓÓ¡
        if (!isProcessed && isCombat && isRange5 && playerIsNotCasting && isGCD && isFireBrandCD && isJunGuanMark)
        {
            isProcessed = true;
            dictStates[m_keyFireBrand] = true;
        }

        // Ï×¼À¹â»·
        if (!isProcessed && isCombat && isRange5 && playerIsNotCasting && isGCD && isImmolationAuraCD)
        {
            isProcessed = true;
            dictStates[m_keyImmolationAura] = true;
        }

        // ÁÒÑæÖä·û
        if (!isProcessed && isCombat && isRange5 && playerIsNotCasting && isGCD && isFireSigilCD && isFireSigilRecommend)
        {
            isProcessed = true;
            dictStates[m_keyFireSigil] = true;
        }

        // Ô¹ÄîÖä·û
        if (!isProcessed && isCombat && isRange5 && playerIsNotCasting && isGCD && isSigilSpiteCD && !hasSoulShape)
        {
            isProcessed = true;
            dictStates[m_keySigilSpite] = true;
        }

        // Ð°ÄÜÖ®ÈÐ
        if (!isProcessed && isCombat && isRange5 && playerIsNotCasting && isGCD && isFelbladeCD && hasSoulShape && currentMp < 40f)
        {
            isProcessed = true;
            dictStates[m_keyFelblade] = true;
        }


        // Ð°ÄÜ»ÙÃð
        if (!isProcessed && isCombat && isRange5 && playerIsNotCasting && isGCD && isFelDevasUsable && isFelDevasCD && isPlayerStand)
        {
            isProcessed = true;
            dictStates[m_keyFelDevas] = true;
        }

        // Áé»êÁÑÅü
        if (!isProcessed && isCombat && isRange5 && playerIsNotCasting && isGCD && isSoulCleaveUsable && !isBombCD && (!isFelDevasCD || currentMp > 50))
        {
            isProcessed = true;
            dictStates[m_keySoulCleave] = true;
        }

        // ÆÆÁÑ
        if (!isProcessed && isCombat && isRange5 && playerIsNotCasting && isGCD && isPolieCD && isPolieCharge1)
        {
            isProcessed = true;
            dictStates[m_keyPolie] = true;
        }

        // Ð°ÄÜÖ®ÈÐ
        if (!isProcessed && isCombat && isRange5 && playerIsNotCasting && isGCD && isFelbladeCD && currentMp < 100f)
        {
            isProcessed = true;
            dictStates[m_keyFelblade] = true;
        }

        // Í¶ÖÀ
        if (!isProcessed && isCombat && isRange15 && playerIsNotCasting && isGCD && isThrowGlaiveCD)
        {
            isProcessed = true;
            dictStates[m_keyThrowGlaive] = true;
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

    private bool GetColorSpecial(int colorIdx, Dictionary<int, Color> dictColors)
    {
        Color color = dictColors[colorIdx];
        if (color.R != 0 || color.G != 0 || color.B != 0)
        {
            return true;
        }
        else
        {
            return false;
        }
    }

}