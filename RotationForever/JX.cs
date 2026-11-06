using System.Drawing;

class JX
{
    #region Singleton
    private static JX _inst;
    private JX() { }

    public static JX Inst
    {
        get
        {
            if (_inst == null)
            {
                _inst = new JX();
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
        0x33, // 3
        //0x34, // 4
        0x35, // 5
        //0x36, // 6
        //0x51, // Q
        //0x45, // E
        //0x52, // R
        //0x54, // T
        0x46, // F
        //0x47, // G
        //0x5A, // Z
        0x58, // X
        //0x43, // C
        //0x56, // V
        0x42, // B
        // °üº¬´óÐ´×ÖÄ¸µÄ×éºÏ¼ü
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
    private int m_idxStaggerPct = 12;
    private int m_idxHasCombo = 13;

    private int m_idxPotionHealStone = 15;
    private int m_idxPotionHp = 16;

    private int m_idxGCD = 20;
    private int m_idxExploKegCD = 21;
    private int m_idxBlackoutKickCD = 22;
    private int m_idxKegSmashCD = 23;
    private int m_idxFireBreathCD = 24;
    private int m_idxYihuajiemuCD = 25;
    private int m_idxTianshenJiuCD = 26;

    private int m_idxYihuajiemuUsable = 30;
    private int m_idxDeathTouchUsable = 31;
    private int m_idxKegSmashUsable = 32;
    private int m_idxTianshenJiuUsable = 33;
    private int m_idxBreathOfFireUsable = 34;
    private int m_idxTigerPalmUsable = 35;
    private int m_idxCraneKickUsable = 36;

    private int m_idxBlackoutKickRecommend = 40;
    private int m_idxKegSmashRecommend = 41;
    private int m_idxDeathTouchRecommend = 42;
    private int m_idxTigerPalmRecommend = 43;
    private int m_idxCraneKickRecommend = 44;
    private int m_idxFireBreathRecommend = 45;
    private int m_idxExploKegRecommend = 46;

    private int m_idxPurifyBrewCharge1 = 1;
    private int m_idxPurifyBrewCharge2 = 2;
    private int m_idxKegSmashCharge1 = 3;
    private int m_idxKegSmashCharge2 = 4;

    private int m_keyYihuajiemu = 1;
    private int m_keyExploKeg = 2;
    private int m_keyPurifyBrew = 3;
    private int m_keyTianshenJiu = 4;
    private int m_keyHealStone = 5;
    private int m_keyHpPotion = 6;
    private int m_keyDeathTouch = 7;
    private int m_blackoutKick = 8;
    private int m_keyKegSmash = 9;
    private int m_keyFireBreath = 10;
    private int m_keyCraneKick = 11;
    private int m_keyTigerPalm = 12;
    private int m_keyCancelJunGuan = 30;

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
        float staggerPct = GetColorFloat(m_idxStaggerPct, dictFrameColors);
        bool hasCombo = GetColorBoolean(m_idxHasCombo, dictFrameColors);
        bool isHealStoneUsable = GetColorBoolean(m_idxPotionHealStone, dictFrameColors);
        bool isHpPotionUsable = GetColorBoolean(m_idxPotionHp, dictFrameColors);

        bool isGCD = GetColorBoolean(m_idxGCD, dictFrameColors);
        bool isExploKegCD = GetColorBoolean(m_idxExploKegCD, dictFrameColors);
        bool isBlackoutKickCD = GetColorBoolean(m_idxBlackoutKickCD, dictFrameColors);
        float kegSmashRemainsCD = GetColorFloat(m_idxKegSmashCD, dictFrameColors);
        bool isKegSmashCD = GetColorBoolean(m_idxKegSmashCD, dictFrameColors);
        bool isFireBreathCD = GetColorBoolean(m_idxFireBreathCD, dictFrameColors);
        bool isYihuajiemuCD = GetColorBoolean(m_idxYihuajiemuCD, dictFrameColors);
        bool isTianshenJiuCD = GetColorBoolean(m_idxTianshenJiuCD, dictFrameColors);


        bool isYihuajiemuUsable = GetColorBoolean(m_idxYihuajiemuUsable, dictFrameColors);
        bool isDeathTouchUsable = GetColorBoolean(m_idxDeathTouchUsable, dictFrameColors);
        bool isKegSmashUsable = GetColorBoolean(m_idxKegSmashUsable, dictFrameColors);
        bool isTianshenJiuUsable = GetColorBoolean(m_idxTianshenJiuUsable, dictFrameColors);
        bool isBreathOfFireUsable = GetColorBoolean(m_idxBreathOfFireUsable, dictFrameColors);
        bool isTigerPalmUsable = GetColorBoolean(m_idxTigerPalmUsable, dictFrameColors);
        bool isCraneKickUsable = GetColorBoolean(m_idxCraneKickUsable, dictFrameColors);


        bool isBlackoutKickRecommend = GetColorBoolean(m_idxBlackoutKickRecommend, dictFrameColors);
        bool isKegSmashRecommend = GetColorBoolean(m_idxKegSmashRecommend, dictFrameColors);
        bool isDeathTouchRecommend = GetColorBoolean(m_idxDeathTouchRecommend, dictFrameColors);
        bool isTigerPalmRecommend = GetColorBoolean(m_idxTigerPalmRecommend, dictFrameColors);
        bool isCraneKickRecommend = GetColorBoolean(m_idxCraneKickRecommend, dictFrameColors);
        bool isFireBreathRecommend = GetColorBoolean(m_idxFireBreathRecommend, dictFrameColors);
        bool isExploKegRecommend = GetColorBoolean(m_idxExploKegRecommend, dictFrameColors);

        bool isPurifyBrewCharge1 = GetColorBoolean(m_idxPurifyBrewCharge1, dictBarColors);
        bool isPurifyBrewCharge2 = GetColorBoolean(m_idxPurifyBrewCharge2, dictBarColors);
        bool isKegSmashCharge1 = GetColorBoolean(m_idxKegSmashCharge1, dictBarColors);
        bool isKegSmashCharge2 = GetColorBoolean(m_idxKegSmashCharge2, dictBarColors);

        float stagger = staggerPct * 6;

        bool isProcessed = false;

        // ÒÆ»¨½ÓÄ¾
        if (!isProcessed && isCombat &&  isRange10 && hpPct <= 0.4f && isYihuajiemuUsable && isYihuajiemuCD)
        {
            isProcessed = true;
            dictStates[m_keyYihuajiemu] = true;
        }

        // »îÑª¾Æ 1²ã
        if (!isProcessed && isCombat && isRange10 && stagger >= 3.0f && isPurifyBrewCharge1)
        {
            isProcessed = true;
            dictStates[m_keyPurifyBrew] = true;
        }


        // ÌìÉñ
        if (!isProcessed && isCombat && isJunGuanMark && isRange10 && isTianshenJiuUsable && isTianshenJiuCD)
        {
            isProcessed = true;
            dictStates[m_keyTianshenJiu] = true;
        }

        // È¡Ïû¾ü¹Ùmark
        if (!isProcessed && isJunGuanMark && (!isTianshenJiuUsable || !isCombat))
        {
            isProcessed = true;
            dictStates[m_keyCancelJunGuan] = true;
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

        // ÂÖ»Ø
        if (!isProcessed && isCombat && isRange10 && isNotCasting && isGCD && isDeathTouchUsable)
        {
            isProcessed = true;
            dictStates[m_keyDeathTouch] = true;
        }

        // »ÃÃðÌß
        if (!isProcessed && isCombat && isRange5 && isNotCasting && isGCD && isBlackoutKickCD)
        {
            isProcessed = true;
            dictStates[m_blackoutKick] = true;
        }


        // »ðÑæÖ®Ï¢
        if (!isProcessed && isCombat && isRange5 && isNotCasting && isGCD && isBreathOfFireUsable && isFireBreathCD)
        {
            isProcessed = true;
            dictStates[m_keyFireBreath] = true; ;
        }

        // ×íÄðÍ¶
        if (!isProcessed && isCombat && isRange20 && isNotCasting && isGCD && isKegSmashUsable && isKegSmashCD && hasCombo && isAoe)
        {
            isProcessed = true;
            dictStates[m_keyKegSmash] = true;
        }

        // »¢ÕÆ
        if (!isProcessed && isCombat && isRange5 && isNotCasting && isGCD && hasCombo && isTigerPalmUsable)
        {
            isProcessed = true;
            dictStates[m_keyTigerPalm] = true;
        }

        // ×íÄðÍ¶
        if (!isProcessed && isCombat && isRange20 && isNotCasting && isGCD && isKegSmashUsable && isKegSmashCD)
        {
            isProcessed = true;
            dictStates[m_keyKegSmash] = true;
        }

        //Console.WriteLine(" isRange5 " + isRange5 + " stagger " + stagger + " isGcd " + isGCD + " isPurifyBrewCharge2 " + isPurifyBrewCharge2);
        // »îÑª¾Æ 2²ã
        if (!isProcessed && isCombat && isRange5 && stagger >= 0.1f && isPurifyBrewCharge2)
        {
            isProcessed = true;
            dictStates[m_keyPurifyBrew] = true;
        }


        // ±¬Õ¨¾ÆÍ°
        if (!isProcessed && isCombat && isRange5 && isExploKegRecommend)
        {
            isProcessed = true;
            dictStates[m_keyExploKeg] = true;
        }

        // º×Ìß
        if (!isProcessed && isCombat && isRange5 && isNotCasting && isGCD && isTigerPalmUsable && isAoe)
        {
            isProcessed = true;
            dictStates[m_keyCraneKick] = true;
        }

        // »¢ÕÆ
        if (!isProcessed && isCombat && isRange5 && isNotCasting && isGCD && isTigerPalmUsable && (kegSmashRemainsCD < 0.6f || mpPct > 0.65f) && !isAoe)
        {
            isProcessed = true;
            dictStates[m_keyTigerPalm] = true;
        }

        // Ò»¼ü¸¨Öú
        //// ÂÖ»Ø
        //if (!isProcessed && isCombat && isRange10 && isNotCasting && isDeathTouchRecommend)
        //{
        //    isProcessed = true;
        //    dictStates[m_keyDeathTouch] = true;
        //}

        ////Console.WriteLine(" isRange5 " + isRange5 + " isNotCasting " + isNotCasting + " isGcd " + isGCD + " isBlackoutKickCD " + isBlackoutKickCD);
        //// »ÃÃðÌß
        //if (!isProcessed && isCombat && isRange5 && isNotCasting && isBlackoutKickRecommend)
        //{
        //    isProcessed = true;
        //    dictStates[m_blackoutKick] = true;
        //}


        //// »ðÑæÖ®Ï¢
        //if (!isProcessed && isCombat && isRange5 && isNotCasting && isFireBreathRecommend)
        //{
        //    isProcessed = true;
        //    dictStates[m_keyFireBreath] = true; ;
        //}

        //// »¢ÕÆ
        //if (!isProcessed && isCombat && isRange5 && isNotCasting && isTigerPalmRecommend && mpPct > 0.6f)
        //{
        //    isProcessed = true;
        //    dictStates[m_keyTigerPalm] = true;
        //}

        //// ×íÄðÍ¶
        //if (!isProcessed && isCombat && isRange20 && isNotCasting && isKegSmashRecommend)
        //{
        //    isProcessed = true;
        //    dictStates[m_keyKegSmash] = true;
        //}

        //// »îÑª¾Æ 2²ã
        //if (!isProcessed && isCombat && isRange5 && stagger >= 0.2f && isPurifyBrewCharge2)
        //{
        //    isProcessed = true;
        //    dictStates[m_keyPurifyBrew] = true;
        //}

        //// º×Ìß
        //if (!isProcessed && isCombat && isRange5 && isNotCasting && isCraneKickRecommend)
        //{
        //    isProcessed = true;
        //    dictStates[m_keyCraneKick] = true;
        //}
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