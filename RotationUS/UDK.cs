using System.Drawing;
using System.Drawing.Printing;

class UDK
{
    #region Singleton
    private static UDK _inst;
    private UDK() { }

    public static UDK Inst
    {
        get
        {
            if (_inst == null)
            {
                _inst = new UDK();
            }
            return _inst;
        }
    }
    #endregion

    // 需要跳过检测的键的虚拟键码
    public int[] skipKeys = {
        0xC0, // ` 键
        //0x31, // 1
        //0x32, // 2
        0x33, // 3
        //0x34, // 4
        0x35, // 5
        //0x36, // 6
        //0x51, // Q
        //0x45, // E
        //0x52, // R
        0x54, // T
        0x46, // F
        //0x47, // G
        0x5A, // Z
        //0x58, // X
        //0x43, // C
        //0x56, // V
        0x42, // B
        // 包含大写字母的组合键
        //0x31 + 0x20, 0x32 + 0x20, 0x33 + 0x20, 0x34 + 0x20, 0x35 + 0x20, 0x36 + 0x20, // Shift + 1 2 3 4 5 6
        //0x51 + 0x20, 0x45 + 0x20, 0x52 + 0x20, 0x54 + 0x20, 0x46 + 0x20, 0x47 + 0x20, // Shift + Q E R T F G
        //0x5A + 0x20, 0x58 + 0x20, 0x43 + 0x20, 0x56 + 0x20, 0x42 + 0x20 // Shift + Z X C V B
    };

    private int m_idxIsCombat = 1;
    private int m_idxIsAoe = 2;
    private int m_idxRange5 = 3;
    private int m_idxRange15 = 4;
    private int m_idxRange20 = 5;
    private int m_idxHp = 6;
    private int m_idxMp = 7;
    private int m_idxJunGuanMark = 8;
    private int m_idxIsTargetChanneling = 9;
    private int m_idxIsTargetCasting = 10;
    private int m_idxPotionHealStone = 11;
    private int m_idxPotionHp = 12;

    private int m_idxBingfengCD = 13;
    private int m_idxDeathStrikeUsable = 14;
    private int m_idxSichanUsable = 15;


    private int m_idxBaofaRecommend = 21;
    private int m_idxDajunRecommend = 22;
    private int m_idxDarkTransformRecommend = 23;
    private int m_idxFuhuaRecommend = 24;
    private int m_idxSoulReaperRecommend = 25;
    private int m_idxNongchuangRecommend = 26;
    private int m_idxSichanRecommend = 27;
    private int m_idxTianzaiRecommend = 28;
    private int m_idxKuosanRecommend = 29;
    private int m_idxFushengRecommend = 30;

    private int m_idxRune1 = 1;
    private int m_idxRune2 = 2;
    private int m_idxRune3 = 3;
    private int m_idxRune4 = 4;
    private int m_idxRune5 = 5;
    private int m_idxRune6 = 6;



    private int m_keyDeathStrike = 1;
    private int m_keyBingfeng = 2;
    private int m_keyAvatar = 3;
    private int m_keyCancelJunGuan = 4;
    private int m_keyHealStone = 5;
    private int m_keyHpPotion = 6;
    private int m_keyBaofa = 7;
    private int m_keyDajun = 8;
    private int m_keyTubian = 9;
    private int m_keyFuhua = 10;
    private int m_keySoulReaper = 11;
    private int m_keyNongchuang = 12;
    private int m_keySichan= 13;
    private int m_keyTianzai = 14;
    private int m_keyKuosan = 15;
    private int m_keyFusheng = 16;

    public void Process(Dictionary<int, Color> dictFrameColors, Dictionary<int, Color> dictBarColors, Dictionary<int, bool> dictStates)
    {
        bool isCombat = GetColorBoolean(m_idxIsCombat, dictFrameColors);
        bool isAoe = GetColorBoolean(m_idxIsAoe, dictFrameColors);
        bool isRange5 = GetColorBoolean(m_idxRange5, dictFrameColors);
        bool isRange15 = GetColorBoolean(m_idxRange15, dictFrameColors);
        bool isRange20 = GetColorBoolean(m_idxRange20, dictFrameColors);
        float hpPct = GetColorFloat(m_idxHp, dictFrameColors);
        float mpPct = GetColorFloat(m_idxMp, dictFrameColors);
        bool isJunGuanMark = GetColorBoolean(m_idxJunGuanMark, dictFrameColors);
        bool isTargetChanneling = GetColorBoolean(m_idxIsTargetChanneling, dictFrameColors);
        bool isTargetCasting = GetColorBoolean(m_idxIsTargetCasting, dictFrameColors);

        bool isHealStoneUsable = GetColorBoolean(m_idxPotionHealStone, dictFrameColors);
        bool isHpPotionUsable = GetColorBoolean(m_idxPotionHp, dictFrameColors);
        bool isBingfengCd = GetColorBoolean(m_idxBingfengCD, dictFrameColors);
        bool isDeathStrikeUsable = GetColorBoolean(m_idxDeathStrikeUsable, dictFrameColors);
        bool isSichanUsable = GetColorBoolean(m_idxSichanUsable, dictFrameColors);

        bool isBaofaRecommend = GetColorBoolean(m_idxBaofaRecommend, dictFrameColors);
        bool isDajunRecommend = GetColorBoolean(m_idxDajunRecommend, dictFrameColors);
        bool isDarkTransformRecommend = GetColorBoolean(m_idxDarkTransformRecommend, dictFrameColors);
        bool isFuhuaRecommend = GetColorBoolean(m_idxFuhuaRecommend, dictFrameColors);
        bool isSoulReaperRecommend = GetColorBoolean(m_idxSoulReaperRecommend, dictFrameColors);
        bool isNongchuangRecommend = GetColorBoolean(m_idxNongchuangRecommend, dictFrameColors);
        bool isSichanRecommend = GetColorBoolean(m_idxSichanRecommend, dictFrameColors);
        bool isTianzaiRecommend = GetColorBoolean(m_idxTianzaiRecommend, dictFrameColors);
        bool isKuosanRecommend = GetColorBoolean(m_idxKuosanRecommend, dictFrameColors);
        bool isFushengRecommend = GetColorBoolean(m_idxFushengRecommend, dictFrameColors);

        bool isRune1Active = GetColorBoolean(m_idxRune1, dictBarColors);
        bool isRune2Active = GetColorBoolean(m_idxRune2, dictBarColors);
        bool isRune3Active = GetColorBoolean(m_idxRune3, dictBarColors);
        bool isRune4Active = GetColorBoolean(m_idxRune4, dictBarColors);
        bool isRune5Active = GetColorBoolean(m_idxRune5, dictBarColors);
        bool isRune6Active = GetColorBoolean(m_idxRune6, dictBarColors);
        //Console.WriteLine("isShieldBlockCharge2 " + isShieldBlockCharge2 + " isShieldBlockCharge1 " + isShieldBlockCharge1 + " isShieldBlockCharge0 " + isShieldBlockCharge0);

        bool isProcessed = false;

        // 死打
        if (!isProcessed && isRange5 && hpPct <= 0.5f && isDeathStrikeUsable)
        {
            isProcessed = true;
            dictStates[m_keyDeathStrike] = true;
        }

        // 冰封
        if (!isProcessed && isRange15 && hpPct <= 0.3f && isBingfengCd)
        {
            isProcessed = true;
            dictStates[m_keyBingfeng] = true;
        }

        // 血瓶
        if (!isProcessed && isRange15 && hpPct <= 0.4f && isHpPotionUsable)
        {
            isProcessed = true;
            dictStates[m_keyHpPotion] = true;
        }

        // 爆发
        if (!isProcessed && isRange15 && isBaofaRecommend && isRune1Active)
        {
            isProcessed = true;
            dictStates[m_keyBaofa] = true;
        }

        // 大军
        if (!isProcessed && isRange15 && isDajunRecommend && isRune1Active)
        {
            isProcessed = true;
            dictStates[m_keyDajun] = true;
        }

        // 突变
        if (!isProcessed && isRange15 && isDarkTransformRecommend && isRune1Active)
        {
            isProcessed = true;
            dictStates[m_keyTubian] = true;
        }

        // 腐化
        if (!isProcessed && isRange20 && isFuhuaRecommend && isRune1Active)
        {
            isProcessed = true;
            dictStates[m_keyFuhua] = true;
        }

        // 收割
        if (!isProcessed && isRange5 && isSoulReaperRecommend && isRune1Active)
        {
            isProcessed = true;
            dictStates[m_keySoulReaper] = true;
        }

        // 脓疮
        if (!isProcessed && isRange15 && isNongchuangRecommend && isRune2Active)
        {
            isProcessed = true;
            dictStates[m_keyNongchuang] = true;
        }

        // 死缠或者死打
        if (!isProcessed && mpPct >= 0.8f) // 能量快满了
        {
            if (hpPct <= 0.8f && isRange15)
            {
                isProcessed = true;
                dictStates[m_keyDeathStrike] = true;
            }
            else if (isRange20 && isSichanUsable)
            {
                isProcessed = true;
                dictStates[m_keySichan] = true;
            }
        }

        // 没符文并且符量够
        if (!isProcessed && !isRune1Active) 
        {
            if (hpPct <= 0.8f && isRange15 && isDeathStrikeUsable)
            {
                isProcessed = true;
                dictStates[m_keyDeathStrike] = true;
            }
            else if (isRange20 && isSichanUsable)
            {
                isProcessed = true;
                dictStates[m_keySichan] = true;
            }
        }

        // 天灾
        if (!isProcessed && isRange20 && isTianzaiRecommend && isRune1Active)
        {
            isProcessed = true;
            dictStates[m_keyTianzai] = true;
        }

        // 扩散
        if (!isProcessed && isRange20 && isKuosanRecommend)
        {
            isProcessed = true;
            dictStates[m_keyKuosan] = true;
        }

        // 复生
        if (!isProcessed && isRange20 && isFushengRecommend)
        {
            isProcessed = true;
            dictStates[m_keyFusheng] = true;
        }

        // 死缠
        if (!isProcessed && isSichanRecommend) // 建议死缠了
        {
            if (isRange15 && hpPct <= 0.8f && isDeathStrikeUsable)
            {
                isProcessed = true;
                dictStates[m_keyDeathStrike] = true;
            }
            else if (isRange20 && isSichanUsable)
            {
                isProcessed = true;
                dictStates[m_keySichan] = true;
            }
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