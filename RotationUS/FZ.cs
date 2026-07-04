using System.Drawing;
using System.Drawing.Printing;

class FZ
{
    #region Singleton
    private static FZ _inst;
    private FZ() { }

    public static FZ Inst
    {
        get
        {
            if (_inst == null)
            {
                _inst = new FZ();
            }
            return _inst;
        }
    }
    #endregion

    // 需要跳过检测的按键虚拟键码
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
    private int m_colorIdxJunGuanMark = 8;
    private int m_colorIdxIsInTeam = 9;
    private int m_colorIdxHasAbsorb = 10;
    private int m_colorIdxPotionHealStone = 11;
    private int m_colorIdxPotionHp = 12;
    private int m_colorIdxVirtoryRushCD = 13;
    private int m_colorIdxShieldWallCD = 14;
    private int m_colorIdxShieldChargeCD = 15;
    private int m_colorIdxCuoZhiCD = 16;
    private int m_colorIdxThunderClapCD = 17;
    private int m_colorIdxAvatarCD = 18;
    private int m_colorIdSuilieThrowCD = 19;
    private int m_colorIdxInterruptCD = 20;
    private int m_colorIdxShieldSlamRecommend = 21;
    private int m_colorIdxThunderClapRecommend = 22;
    private int m_colorIdxRevengeRecommend = 23;
    private int m_colorIdxExecuteRecommend = 24;
    private int m_colorIdxThrowRecommend = 25;
    private int m_colorIdxVirtoryRushIsUsable = 26;
    private int m_colorIdxFoxBagCD = 27;
    private int m_colorIdxFoodMark = 28;
    private int m_colorIdxIsTargetCasting = 29;

    private int m_colorIdxShieldBlockCharge1 = 1;
    private int m_colorIdxShieldBlockCharge2 = 2;


    private int m_colorIdxIp = 51;
    private Color m_colorIp = Color.FromArgb(255, 144, 92, 18);

    private int m_keyVirtoryRush = 1;
    private int m_keyShieldWall = 2;
    private int m_keyAvatar = 3;
    private int m_keyInterrupt = 4;
    private int m_keyHealStone = 5;
    private int m_keyHpPotion = 6;
    private int m_keyShieldBlock = 7;
    private int m_keyIp = 8;
    private int m_keyShieldCharge = 9;
    private int m_keyCuoZhi = 10;
    private int m_keyShieldSlam = 11;
    private int m_keyThunderClap = 12;
    private int m_keyRevenge = 13;
    private int m_keyExecute = 14;
    private int m_keySuilieThrow = 15;
    private int m_keyThrow = 16;
    private int m_keyFoxBag = 18;

    private int m_keyCancelJunGuan = 28;
    private int m_keyCancelFoodMark = 29;
    public void Process(Dictionary<int, Color> dictFrameColors, Dictionary<int, Color> dictBarColors, Dictionary<int, bool> dictStates)
    {
        bool isCombat = GetColorBoolean(m_colorIdxIsCombat, dictFrameColors);
        bool isAoe = GetColorBoolean(m_colorIdxIsAoe, dictFrameColors);
        bool isRange5 = GetColorBoolean(m_colorIdxRange5, dictFrameColors);
        bool isRange10 = GetColorBoolean(m_colorIdxRange10, dictFrameColors);
        bool isRange15 = GetColorBoolean(m_colorIdxRange15, dictFrameColors);
        float hpPct = GetColorFloat(m_colorIdxHp, dictFrameColors);
        float mpPct = GetColorFloat(m_colorIdxMp, dictFrameColors);
        bool isJunGuanMark = GetColorBoolean(m_colorIdxJunGuanMark, dictFrameColors);
        bool isInTeam = GetColorBoolean(m_colorIdxIsInTeam, dictFrameColors);
        bool hasAbsorb = GetColorBoolean(m_colorIdxHasAbsorb, dictFrameColors);
        bool isFoodMark = GetColorBoolean(m_colorIdxFoodMark, dictFrameColors);
        bool isTargetCasting = GetColorBoolean(m_colorIdxIsTargetCasting, dictFrameColors);

        bool isHealStoneUsable = GetColorBoolean(m_colorIdxPotionHealStone, dictFrameColors);
        bool isHpPotionUsable = GetColorBoolean(m_colorIdxPotionHp, dictFrameColors);
        bool isVictoryRushCd = GetColorBoolean(m_colorIdxVirtoryRushCD, dictFrameColors);
        bool isShieldWallCd = GetColorBoolean(m_colorIdxShieldWallCD, dictFrameColors);
        bool isShieldChargeCd = GetColorBoolean(m_colorIdxShieldChargeCD, dictFrameColors);
        bool isCuoZhiCd = GetColorBoolean(m_colorIdxCuoZhiCD, dictFrameColors);
        bool isThunderClapCd = GetColorBoolean(m_colorIdxThunderClapCD, dictFrameColors);
        bool isAvatarCd = GetColorBoolean(m_colorIdxAvatarCD, dictFrameColors);
        bool isSuilieThrowCd = GetColorBoolean(m_colorIdSuilieThrowCD, dictFrameColors);
        bool isInterruptCD = GetColorBoolean(m_colorIdxInterruptCD, dictFrameColors);
        bool isFoxBagCd = GetColorBoolean(m_colorIdxFoxBagCD, dictFrameColors);

        bool isShieldSlamRecommend = GetColorBoolean(m_colorIdxShieldSlamRecommend, dictFrameColors);
        bool isThunderClapRecommend = GetColorBoolean(m_colorIdxThunderClapRecommend, dictFrameColors);
        bool isRevengeRecommend = GetColorBoolean(m_colorIdxRevengeRecommend, dictFrameColors);
        bool isExecuteRecommend = GetColorBoolean(m_colorIdxExecuteRecommend, dictFrameColors);
        bool isThrowRecommend = GetColorBoolean(m_colorIdxThrowRecommend, dictFrameColors);
        bool isVictoryRusnUsable = GetColorBoolean(m_colorIdxVirtoryRushIsUsable, dictFrameColors);
        bool isNeedIp = GetColorSpecial(m_colorIdxIp, dictFrameColors, m_colorIp);

        bool isShieldBlockCharge2 = GetColorBoolean(m_colorIdxShieldBlockCharge2, dictBarColors);
        bool isShieldBlockCharge1 = GetColorBoolean(m_colorIdxShieldBlockCharge1, dictBarColors);
        bool isShieldBlockCharge0 = !isShieldBlockCharge1 && !isShieldBlockCharge2;
        //Console.WriteLine("isShieldBlockCharge2 " + isShieldBlockCharge2 + " isShieldBlockCharge1 " + isShieldBlockCharge1 + " isShieldBlockCharge0 " + isShieldBlockCharge0);

        bool isProcessed = false;

        // 胜利在望
        if (!isProcessed && isRange5 && hpPct <= 0.7f && isVictoryRushCd && isVictoryRusnUsable)
        {
            isProcessed = true;
            dictStates[m_keyVirtoryRush] = true;
        }

        // 盾墙
        //if (!isProcessed && isRange10 && hpPct <= 0.35f && isShieldWallCd)
        //{
        //    isProcessed = true;
        //    dictStates[m_keyShieldWall] = true;
        //}

        // 袋里乾坤
        if (!isProcessed && isCombat && isRange15 && hpPct <= 0.6f && isFoxBagCd)
        {
            isProcessed = true;
            dictStates[m_keyFoxBag] = true;
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

        // interrupt mark
        if (!isProcessed && isFoodMark && (!isCombat || !isTargetCasting || !isInterruptCD || !isRange5))
        {
            isProcessed = true;
            dictStates[m_keyCancelFoodMark] = true;
        }

        // 打断
        if (!isProcessed && isCombat && isRange5 && isTargetCasting && isInterruptCD && isFoodMark)
        {
            isProcessed = true;
            dictStates[m_keyInterrupt] = true;
        }

        // 盾牌格挡
        if (!isProcessed && isRange10 && isShieldBlockCharge2 && mpPct >= 0.33f)
        {
            isProcessed = true;
            dictStates[m_keyShieldBlock] = true;
        }

        // 无视痛苦
        if (!isProcessed && isRange10 && isNeedIp && mpPct >= 0.36f)
        {
            isProcessed = true;
            dictStates[m_keyIp] = true;
        }

        // 无视痛苦(怒气太多)
        if (!isProcessed && isRange10 && isInTeam && mpPct >= 0.8f)
        {
            isProcessed = true;
            dictStates[m_keyIp] = true;
        }

        // 天神下凡
        if (!isProcessed && isJunGuanMark && isRange5 && isAvatarCd && !isThunderClapCd)
        {
            isProcessed = true;
            dictStates[m_keyAvatar] = true;
        }

        // 取消军官标记
        if (!isProcessed && isJunGuanMark && (!isAvatarCd || !isCombat))
        {
            isProcessed = true;
            dictStates[m_keyCancelJunGuan] = true;
        }

        // 碎裂投掷
        //if (!isProcessed && isRange15 && hasAbsorb && isSuilieThrowCd)
        //{
        //    isProcessed = true;
        //    dictStates[m_keySuilieThrow] = true;
        //}

        // 盾牌冲锋
        //if (!isProcessed && isRange10 && isShieldChargeCd)
        //{
        //    isProcessed = true;
        //    dictStates[m_keyShieldCharge] = true;
        //}

        // 挫志怒吼
        if (!isProcessed && isRange5 && isCuoZhiCd)
        {
            isProcessed = true;
            dictStates[m_keyCuoZhi] = true;
        }

        // 盾牌猛击
        if (!isProcessed && isRange5 && isShieldSlamRecommend)
        {
            isProcessed = true;
            dictStates[m_keyShieldSlam] = true;
        }

        // 雷霆一击
        if (!isProcessed && isRange5 && isThunderClapRecommend)
        {
            isProcessed = true;
            dictStates[m_keyThunderClap] = true;
        }

        // 复仇
        if (!isProcessed && isRange5 && isRevengeRecommend)
        {
            isProcessed = true;
            dictStates[m_keyRevenge] = true;
        }

        // 斩杀
        if (!isProcessed && isRange5 && isExecuteRecommend)
        {
            isProcessed = true;
            dictStates[m_keyExecute] = true;
        }

        // 英勇投掷
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
        if (color.R == 0 && color.G == 0 && color.B == 0)
        {
            return true;
        }
        else
        {
            return false;
        }
    }

}