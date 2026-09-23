using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 会戦中の旗艦成長の調整値（#2757・LoL のレベリング数値を参考に会戦スケールへ適応）。
    /// LoL 参考：①必要XP＝レベル2で280・以降+100/レベル（線形増分）②能力成長＝
    /// base+g·(n-1)·(0.7025+0.0175·(n-1))＝後半ほど伸びる（back-loaded）③号令(ult)解禁＝
    /// LoL の 6/11/16 を最大レベル比で会戦尺へ（12レベル想定で 4/7/11）。
    /// </summary>
    public readonly struct FlagshipGrowthParams
    {
        public readonly int maxLevel;        // 会戦内の最大レベル（LoL=18／会戦尺へ適応＝12）
        public readonly float xpFirst;       // レベル1→2 の必要XP（LoL=280）
        public readonly float xpIncrement;   // レベルごとの必要XP増分（LoL=+100）
        public readonly float perPointGrowth;// 成長1単位あたりの実効戦闘倍率の増分
        public readonly float growthBase;    // LoL 成長式の定数 0.7025
        public readonly float growthAccel;   // LoL 成長式の加速 0.0175（後半ほど伸びる）
        public readonly float maxPowerBonus; // 実効戦闘倍率の上限（暴走の歯止め・賞金首LOL-3と併用）
        public readonly int baseSlots;       // レベル1での戦法スロット数
        public readonly int slotUnlockEvery; // 何レベルごとに戦法スロット+1
        public readonly int maxSlots;        // 戦法スロット上限（一斉砲撃/突撃/不退転＝3）
        public readonly int ultLevel1;       // 号令(ult)解禁レベル（LoL 6 相当）
        public readonly int ultLevel2;       // 号令 強化1（LoL 11 相当）
        public readonly int ultLevel3;       // 号令 強化2（LoL 16 相当）
        public readonly float xpPerKill;     // 旗艦撃破で得るXP（income・要調整）
        public readonly float xpPerObjective;// 中立目標確保(LOL-1)で得るXP（income・要調整）

        public FlagshipGrowthParams(int maxLevel, float xpFirst, float xpIncrement, float perPointGrowth,
            float growthBase, float growthAccel, float maxPowerBonus, int baseSlots, int slotUnlockEvery,
            int maxSlots, int ultLevel1, int ultLevel2, int ultLevel3, float xpPerKill, float xpPerObjective)
        {
            this.maxLevel = maxLevel; this.xpFirst = xpFirst; this.xpIncrement = xpIncrement;
            this.perPointGrowth = perPointGrowth; this.growthBase = growthBase; this.growthAccel = growthAccel;
            this.maxPowerBonus = maxPowerBonus; this.baseSlots = baseSlots; this.slotUnlockEvery = slotUnlockEvery;
            this.maxSlots = maxSlots; this.ultLevel1 = ultLevel1; this.ultLevel2 = ultLevel2; this.ultLevel3 = ultLevel3;
            this.xpPerKill = xpPerKill; this.xpPerObjective = xpPerObjective;
        }

        /// <summary>
        /// 既定（LoL参考・会戦スケール）：最大12Lv／必要XP 280・+100/Lv／成長式 0.7025+0.0175／
        /// 倍率+4%×成長単位（最大Lv12で約+39%・上限+45%）／戦法3スロット(L1/2/3)／号令解禁 4/7/11。
        /// </summary>
        public static FlagshipGrowthParams Default => new FlagshipGrowthParams(
            maxLevel: 12, xpFirst: 280f, xpIncrement: 100f, perPointGrowth: 0.04f,
            growthBase: 0.7025f, growthAccel: 0.0175f, maxPowerBonus: 0.45f,
            baseSlots: 1, slotUnlockEvery: 1, maxSlots: 3,
            ultLevel1: 4, ultLevel2: 7, ultLevel3: 11,
            xpPerKill: 50f, xpPerObjective: 120f);
    }

    /// <summary>
    /// 会戦中の旗艦成長の純ロジック（#2757・Core・test-first）。MOBA の「試合中レベリング」の
    /// 雪だるまカタルシスを会戦へ＝旗艦が会戦内XPでレベルアップし、レベルに応じて実効戦闘倍率と
    /// 能力（戦法スロット・号令ティア）が**解放**される。**会戦限定**＝終了で破棄（このルールは
    /// 累積XPに対する純関数＝state を持たない＝消費側が会戦開始で totalXp=0 に戻すだけ）。
    /// **人物（提督）能力には触れない**（永続成長は `VeterancyRules`＋覚醒 GIR-2 が担う＝二重計上回避）。
    /// 数値は LoL のレベリングを参考に会戦尺へ適応（`FlagshipGrowthParams` 参照）。実効値パターン。
    /// </summary>
    public static class FlagshipBattleGrowthRules
    {
        /// <summary>レベル level→level+1 に必要なXP（LoL：280・以降+100/Lv の線形増分）。</summary>
        public static float XpForNextLevel(int level, FlagshipGrowthParams p)
        {
            int lv = Mathf.Max(1, level);
            return Mathf.Max(1f, p.xpFirst + p.xpIncrement * (lv - 1));
        }

        /// <summary>レベル level に到達するための累積XP（level1=0）。</summary>
        public static float CumulativeXpForLevel(int level, FlagshipGrowthParams p)
        {
            int target = Mathf.Clamp(level, 1, Mathf.Max(1, p.maxLevel));
            float sum = 0f;
            for (int i = 1; i < target; i++) sum += XpForNextLevel(i, p);
            return sum;
        }

        /// <summary>累積XPから現在レベル（1..maxLevel・クランプ）。</summary>
        public static int LevelForXp(float totalXp, FlagshipGrowthParams p)
        {
            float xp = Mathf.Max(0f, totalXp);
            int max = Mathf.Max(1, p.maxLevel);
            int level = 1;
            for (int next = 2; next <= max; next++)
            {
                if (xp >= CumulativeXpForLevel(next, p)) level = next;
                else break;
            }
            return level;
        }

        /// <summary>LoL の成長係数 (n-1)·(0.7025+0.0175·(n-1))＝後半ほど伸びる（back-loaded）。</summary>
        public static float GrowthFactor(int level, FlagshipGrowthParams p)
        {
            int n = Mathf.Max(1, level);
            return (n - 1) * (p.growthBase + p.growthAccel * (n - 1));
        }

        /// <summary>レベルでの実効戦闘倍率（1.0起点・上限 1+maxPowerBonus・基準値非破壊）。</summary>
        public static float PowerBonusAtLevel(int level, FlagshipGrowthParams p)
        {
            float bonus = p.perPointGrowth * GrowthFactor(level, p);
            return Mathf.Clamp(1f + bonus, 1f, 1f + Mathf.Max(0f, p.maxPowerBonus));
        }

        /// <summary>解放済みの戦法スロット数（一斉砲撃/突撃/不退転＝最大3）。</summary>
        public static int WeaponSlotsAtLevel(int level, FlagshipGrowthParams p)
        {
            int lv = Mathf.Max(1, level);
            int every = Mathf.Max(1, p.slotUnlockEvery);
            int slots = p.baseSlots + (lv - 1) / every;
            return Mathf.Clamp(slots, p.baseSlots, Mathf.Max(p.baseSlots, p.maxSlots));
        }

        /// <summary>号令(ult)のティア（0=未解禁／1=解禁／2,3=強化。LoL 6/11/16 相当）。</summary>
        public static int CommandTierAtLevel(int level, FlagshipGrowthParams p)
        {
            int lv = Mathf.Max(1, level);
            if (lv >= p.ultLevel3) return 3;
            if (lv >= p.ultLevel2) return 2;
            if (lv >= p.ultLevel1) return 1;
            return 0;
        }

        /// <summary>HUD用：現在レベルと次レベルへの進捗割合(0..1)。最大レベルで割合=1。</summary>
        public static int Progress(float totalXp, FlagshipGrowthParams p, out float fractionToNext)
        {
            int level = LevelForXp(totalXp, p);
            if (level >= Mathf.Max(1, p.maxLevel)) { fractionToNext = 1f; return level; }
            float floor = CumulativeXpForLevel(level, p);
            float need = XpForNextLevel(level, p);
            fractionToNext = Mathf.Clamp01((Mathf.Max(0f, totalXp) - floor) / Mathf.Max(1f, need));
            return level;
        }
    }
}
