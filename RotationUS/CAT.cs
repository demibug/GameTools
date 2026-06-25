using System.Drawing;
using System.Drawing.Printing;

class CAT
{
    #region Singleton
    private static CAT _inst;
    private CAT() { }

    public static CAT Inst
    {
        get
        {
            if (_inst == null)
            {
                _inst = new CAT();
            }
            return _inst;
        }
    }
    #endregion

    // 需要跳过检测的键的虚拟键码
    public int[] skipKeys = {
        //0xC0, // ` 键
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
        // 包含大写字母的组合键
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
    private int m_idxIsPlayerNotCasting = 8;
    private int m_idxHasDispelDebuff = 9;
    private int m_idxJunGuanMark = 10;
    private int m_idxFoodMark = 11;
    private int m_idxIsTargetCasting = 13;
    private int m_idxHasUrsol = 14;
    private int m_idxIsPlayerStand = 15;
    private int m_idxHasTigersFury = 16;
    private int m_idxHasLueshizhe = 17;

    private int m_idxPotionHealStone = 18;
    private int m_idxPotionHp = 19;

    private int m_idxGCD = 20;
    private int m_idxBerserkCD = 21;
    private int m_idxConvokeSpiritsCD = 22;
    private int m_idxTigersFuryCD = 23;
    private int m_idxFeralFrenzyCD = 24;
    private int m_idxChompCD = 25;
    private int m_idxSurvivalInstinctsCD = 26;
    private int m_idxInterruptCD = 27;
    private int m_idxClearCD = 28;
    private int m_idxTyphoonCD = 29;
    private int m_idxMangleCD = 30;

    private int m_idxClearUsable = 32;
    private int m_idxRipUsable = 33;
    private int m_idxRavageUsable = 34;
    private int m_idxFBiteUsable = 35;
    private int m_idxFeralFrenzyUsable = 36;
    private int m_idxShredUsable = 37;
    private int m_idxRakeUsable = 38;
    private int m_idxChompUsable = 39;
    private int m_idxYuheUsable = 40;

    private int m_idxTigersFuryRecommend = 41;
    private int m_idxRipRecommend = 42;
    private int m_idxFBiteRecommend = 43;
    private int m_idxShredRecommend = 44;
    private int m_idxRakeRecommend = 45;
    private int m_idxConvokeSpiritsRecommend = 46;
    private int m_idxChompRecommend = 47;
    private int m_idxSwipeRecommend = 48;
    private int m_idxFeralFrenzyRecommend = 49;

    private int m_idxComboPoint1 = 1;
    private int m_idxComboPoint2 = 2;
    private int m_idxComboPoint3 = 3;
    private int m_idxComboPoint4 = 4;
    private int m_idxComboPoint5 = 5;



    private int m_keyRageGenerate = 1;
    private int m_keyHealStone = 2;
    private int m_keyHpPotion = 3;
    private int m_keySurvivalInstincts = 4;
    private int m_keyInterrupt = 5;
    private int m_keyClear = 6;
    private int m_keyRake = 7;
    private int m_keyShred = 8;
    private int m_keyRip = 9;
    private int m_keyRavage = 10;
    private int m_keyChomp = 11;
    private int m_keyFeralFrenzy = 12;
    private int m_keyTigersFury = 13;
    private int m_keyConvokeSpirits = 14;
    private int m_keyBerserk = 15;
    private int m_keySwipe = 16;
    private int m_keyTyphoon = 17;
    private int m_keyYuhe = 18;

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
        float energy = mpPct * 140;
        bool isPlayerNotCasting = GetColorBoolean(m_idxIsPlayerNotCasting, dictFrameColors);
        bool hasDispelDebuff = GetColorBoolean(m_idxHasDispelDebuff, dictFrameColors);
        bool isJunGuanMark = GetColorBoolean(m_idxJunGuanMark, dictFrameColors);
        bool isFoodMark = GetColorBoolean(m_idxFoodMark, dictFrameColors);
        bool isTargetCasting = GetColorBoolean(m_idxIsTargetCasting, dictFrameColors);
        bool hasUrsol = GetColorBoolean(m_idxHasUrsol, dictFrameColors);
        bool isPlayerStand = GetColorBoolean(m_idxIsPlayerStand, dictFrameColors);
        bool hasTigersFury = GetColorBoolean(m_idxHasTigersFury, dictFrameColors);
        bool hasLueshizhe = GetColorBoolean(m_idxHasLueshizhe, dictFrameColors);


        bool isHealStoneUsable = GetColorBoolean(m_idxPotionHealStone, dictFrameColors);
        bool isHpPotionUsable = GetColorBoolean(m_idxPotionHp, dictFrameColors);

        bool isGCD = GetColorBoolean(m_idxGCD, dictFrameColors);
        bool isBerserkCD = GetColorBoolean(m_idxBerserkCD, dictFrameColors);
        bool isConvokeSpiritsCD = GetColorBoolean(m_idxConvokeSpiritsCD, dictFrameColors);
        bool isTigersFuryCD = GetColorBoolean(m_idxTigersFuryCD, dictFrameColors);
        bool isFeralFrenzyCD = GetColorBoolean(m_idxFeralFrenzyCD, dictFrameColors);
        bool isChompCD = GetColorBoolean(m_idxChompCD, dictFrameColors);
        bool isSurvivalInstinctsCD = GetColorBoolean(m_idxSurvivalInstinctsCD, dictFrameColors);
        bool isInterruptCD = GetColorBoolean(m_idxInterruptCD, dictFrameColors);
        bool isClearCD = GetColorBoolean(m_idxClearCD, dictFrameColors);
        bool isTyphoonCD = GetColorBoolean(m_idxTyphoonCD, dictFrameColors);
        bool isMangleCD = GetColorBoolean(m_idxMangleCD, dictFrameColors);

        bool isClearUsable = GetColorBoolean(m_idxClearUsable, dictFrameColors);
        bool isRipUsable = GetColorBoolean(m_idxRipUsable, dictFrameColors);
        bool isRavageUsable = GetColorBoolean(m_idxRavageUsable, dictFrameColors);
        bool isFBiteUsable = GetColorBoolean(m_idxFBiteUsable, dictFrameColors);
        bool isFeralFrenzyUsable = GetColorBoolean(m_idxFeralFrenzyUsable, dictFrameColors);
        bool isShredUsable = GetColorBoolean(m_idxShredUsable, dictFrameColors);
        bool isRakeUsable = GetColorBoolean(m_idxRakeUsable, dictFrameColors);
        bool isChompUsable = GetColorBoolean(m_idxChompUsable, dictFrameColors);
        bool isYuheUsable = GetColorBoolean(m_idxYuheUsable, dictFrameColors);

        bool isTigersFuryRecommend = GetColorBoolean(m_idxTigersFuryRecommend, dictFrameColors);
        bool isRipRecommend = GetColorBoolean(m_idxRipRecommend, dictFrameColors);
        bool isFBiteRecommend = GetColorBoolean(m_idxFBiteRecommend, dictFrameColors);
        bool isShredRecommend = GetColorBoolean(m_idxShredRecommend, dictFrameColors);
        bool isRakeRecommend = GetColorBoolean(m_idxRakeRecommend, dictFrameColors);
        bool isConvokeSpiritsRecommend = GetColorBoolean(m_idxConvokeSpiritsRecommend, dictFrameColors);
        bool isChompRecommend = GetColorBoolean(m_idxChompRecommend, dictFrameColors);
        bool isSwipeRecommend = GetColorBoolean(m_idxSwipeRecommend, dictFrameColors);
        bool isFeralFrenzyRecommend = GetColorBoolean(m_idxFeralFrenzyRecommend, dictFrameColors);

        bool isComboPoint1Active = GetColorBoolean(m_idxComboPoint1, dictBarColors);
        bool isComboPoint2Active = GetColorBoolean(m_idxComboPoint2, dictBarColors);
        bool isComboPoint3Active = GetColorBoolean(m_idxComboPoint3, dictBarColors);
        bool isComboPoint4Active = GetColorBoolean(m_idxComboPoint4, dictBarColors);
        bool isComboPoint5Active = GetColorBoolean(m_idxComboPoint5, dictBarColors);


        bool isProcessed = false;

        // 掠食者
        if (!isProcessed && isCombat && isRange13 && hpPct < 0.9f && hasLueshizhe && isYuheUsable)
        {
            isProcessed = true;
            dictStates[m_keyYuhe] = true;
        }

        // 治疗石
        if (!isProcessed && isCombat && isRange13 && hpPct <= 0.3f && isHealStoneUsable)
        {
            isProcessed = true;
            dictStates[m_keyHealStone] = true;
        }

        // 血瓶
        if (!isProcessed && isCombat && isRange13 && hpPct <= 0.3f && isHpPotionUsable)
        {
            isProcessed = true;
            dictStates[m_keyHpPotion] = true;
        }

        // berserk mark
        if (!isProcessed && isFoodMark && (!isCombat || !isBerserkCD))
        {
            isProcessed = true;
            dictStates[m_keyCancelFoodMark] = true;
        }

        // interrupt mark
        if (!isProcessed && isJunGuanMark && (!isCombat || !isTargetCasting || !isInterruptCD || !isRange13))
        {
            isProcessed = true;
            dictStates[m_keyCancelJunGuanMark] = true;
        }

        // 打断
        if (!isProcessed && isCombat && isRange13 && isTargetCasting && isInterruptCD && isJunGuanMark)
        {
            isProcessed = true;
            dictStates[m_keyInterrupt] = true;
        }

        // 生存本能
        if (!isProcessed && isCombat && isRange13 && hpPct < 0.3f && isSurvivalInstinctsCD)
        {
            isProcessed = true;
            dictStates[m_keySurvivalInstincts] = true;
        }

        // 驱散自己
        if (!isProcessed && isCombat && isRange13 && isGCD && isPlayerNotCasting && hasDispelDebuff && isClearCD && isClearUsable)
        {
            isProcessed = true;
            dictStates[m_keyClear] = true;
        }

        // 驱散队友
        if (!isProcessed && isCombat && isRangeFriend40 && isGCD && isPlayerNotCasting && hasDispelDebuff && isClearCD && isClearUsable)
        {
            isProcessed = true;
            dictStates[m_keyClear] = true;
        }

        // 乌索尔
        if (!isProcessed && isCombat && isRange13 && isGCD && isPlayerNotCasting && isTyphoonCD && hasUrsol)
        {
            isProcessed = true;
            dictStates[m_keyTyphoon] = true;
        }

        // 狂暴
        if (!isProcessed && isCombat && isRange5 && isGCD && isPlayerNotCasting && isFoodMark && isBerserkCD && hasTigersFury)
        {
            isProcessed = true;
            dictStates[m_keyBerserk] = true;
        }


        // 建议割裂
        if (!isProcessed && isCombat && isRange5 && isGCD && isPlayerNotCasting && isRipRecommend)
        {
            isProcessed = true;
            dictStates[m_keyRip] = true;
        }

        // 建议咬
        if (!isProcessed && isCombat && isRange5 && isGCD && isPlayerNotCasting && isFBiteRecommend)
        {
            isProcessed = true;
            dictStates[m_keyRavage] = true;
        }

        // 建议万灵
        if (!isProcessed && isCombat && isRange5 && isGCD && isPlayerNotCasting && isConvokeSpiritsRecommend && isConvokeSpiritsCD)
        {
            isProcessed = true;
            dictStates[m_keyConvokeSpirits] = true;
        }

        // 建议斜掠
        if (!isProcessed && isCombat && isRange5 && isGCD && isPlayerNotCasting && isRakeUsable && isRakeRecommend)
        {
            isProcessed = true;
            dictStates[m_keyRake] = true;
        }

        // 建议撕碎
        if (!isProcessed && isCombat && isRange5 && isGCD && isPlayerNotCasting && isShredUsable && isShredRecommend)
        {
            isProcessed = true;
            dictStates[m_keyShred] = true;
        }

        // 建议啃噬
        if (!isProcessed && isCombat && isRange5 && isGCD && isPlayerNotCasting && isChompUsable && isChompRecommend)
        {
            isProcessed = true;
            dictStates[m_keyChomp] = true;
        }

        // 建议狂乱
        if (!isProcessed && isCombat && isRange5 && isGCD && isPlayerNotCasting && isFeralFrenzyRecommend)
        {
            isProcessed = true;
            dictStates[m_keyFeralFrenzy] = true;
        }

        // 建议猛虎
        if (!isProcessed && isCombat && isRange5 && isGCD && isPlayerNotCasting && isTigersFuryCD && isTigersFuryRecommend)
        {
            isProcessed = true;
            dictStates[m_keyTigersFury] = true;
        }

        // 横扫
        if (!isProcessed && isCombat && isRange5 && isGCD && isPlayerNotCasting && isSwipeRecommend)
        {
            isProcessed = true;
            dictStates[m_keySwipe] = true;
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