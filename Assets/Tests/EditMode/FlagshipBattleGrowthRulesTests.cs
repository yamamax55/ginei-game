using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>会戦中の旗艦成長（#2757・LoL参考の数値）。必要XP/レベル/倍率/解放を既定値で固定。</summary>
    public class FlagshipBattleGrowthRulesTests
    {
        private static FlagshipGrowthParams P => FlagshipGrowthParams.Default;

        [Test]
        public void XpForNextLevel_LoL_280_Plus100()
        {
            Assert.AreEqual(280f, FlagshipBattleGrowthRules.XpForNextLevel(1, P), 1e-3f); // L1→2=280
            Assert.AreEqual(380f, FlagshipBattleGrowthRules.XpForNextLevel(2, P), 1e-3f); // +100
            Assert.AreEqual(680f, FlagshipBattleGrowthRules.XpForNextLevel(5, P), 1e-3f); // 280+100*4
            // level<1 はクランプ
            Assert.AreEqual(280f, FlagshipBattleGrowthRules.XpForNextLevel(0, P), 1e-3f);
        }

        [Test]
        public void CumulativeXp_SumsLinearIncrement()
        {
            Assert.AreEqual(0f, FlagshipBattleGrowthRules.CumulativeXpForLevel(1, P), 1e-3f);
            Assert.AreEqual(280f, FlagshipBattleGrowthRules.CumulativeXpForLevel(2, P), 1e-3f);   // 280
            Assert.AreEqual(660f, FlagshipBattleGrowthRules.CumulativeXpForLevel(3, P), 1e-3f);   // 280+380
            // 最大レベル12到達の総XP = 11*280 + 100*(0+..+10) = 3080+5500 = 8580
            Assert.AreEqual(8580f, FlagshipBattleGrowthRules.CumulativeXpForLevel(12, P), 1e-2f);
        }

        [Test]
        public void LevelForXp_Boundaries_And_Cap()
        {
            Assert.AreEqual(1, FlagshipBattleGrowthRules.LevelForXp(0f, P));
            Assert.AreEqual(1, FlagshipBattleGrowthRules.LevelForXp(279f, P));
            Assert.AreEqual(2, FlagshipBattleGrowthRules.LevelForXp(280f, P));   // ちょうど
            Assert.AreEqual(2, FlagshipBattleGrowthRules.LevelForXp(659f, P));
            Assert.AreEqual(3, FlagshipBattleGrowthRules.LevelForXp(660f, P));
            Assert.AreEqual(12, FlagshipBattleGrowthRules.LevelForXp(8580f, P)); // 最大
            Assert.AreEqual(12, FlagshipBattleGrowthRules.LevelForXp(999999f, P)); // クランプ
            Assert.AreEqual(1, FlagshipBattleGrowthRules.LevelForXp(-50f, P));  // 負はクランプ
        }

        [Test]
        public void PowerBonus_BackLoaded_LoLShape()
        {
            // Lv1=成長0＝1.0倍
            Assert.AreEqual(1.0f, FlagshipBattleGrowthRules.PowerBonusAtLevel(1, P), 1e-4f);
            // Lv2: 1+0.04*(1*(0.7025+0.0175))=1+0.04*0.72=1.0288
            Assert.AreEqual(1.0288f, FlagshipBattleGrowthRules.PowerBonusAtLevel(2, P), 1e-4f);
            // Lv12: 1+0.04*(11*(0.7025+0.0175*11))=1+0.04*9.845=1.3938（上限0.45未満）
            Assert.AreEqual(1.3938f, FlagshipBattleGrowthRules.PowerBonusAtLevel(12, P), 1e-3f);
            // 単調増加かつ後半ほど増分が大きい（back-loaded＝LoL成長式）
            float d21 = FlagshipBattleGrowthRules.PowerBonusAtLevel(2, P) - FlagshipBattleGrowthRules.PowerBonusAtLevel(1, P);
            float d32 = FlagshipBattleGrowthRules.PowerBonusAtLevel(3, P) - FlagshipBattleGrowthRules.PowerBonusAtLevel(2, P);
            Assert.Greater(d32, d21);
        }

        [Test]
        public void PowerBonus_ClampedToMax()
        {
            // 上限が小さい params だと頭打ち
            var p = new FlagshipGrowthParams(12, 280f, 100f, 0.04f, 0.7025f, 0.0175f,
                0.10f, 1, 1, 3, 4, 7, 11, 50f, 120f); // maxPowerBonus=0.10
            Assert.AreEqual(1.10f, FlagshipBattleGrowthRules.PowerBonusAtLevel(12, p), 1e-4f);
        }

        [Test]
        public void WeaponSlots_OpenL1L2L3_CapAt3()
        {
            Assert.AreEqual(1, FlagshipBattleGrowthRules.WeaponSlotsAtLevel(1, P));
            Assert.AreEqual(2, FlagshipBattleGrowthRules.WeaponSlotsAtLevel(2, P));
            Assert.AreEqual(3, FlagshipBattleGrowthRules.WeaponSlotsAtLevel(3, P));
            Assert.AreEqual(3, FlagshipBattleGrowthRules.WeaponSlotsAtLevel(12, P)); // 上限
        }

        [Test]
        public void CommandTier_LoL_6_11_16_ScaledTo_4_7_11()
        {
            Assert.AreEqual(0, FlagshipBattleGrowthRules.CommandTierAtLevel(3, P)); // 未解禁
            Assert.AreEqual(1, FlagshipBattleGrowthRules.CommandTierAtLevel(4, P)); // 解禁
            Assert.AreEqual(1, FlagshipBattleGrowthRules.CommandTierAtLevel(6, P));
            Assert.AreEqual(2, FlagshipBattleGrowthRules.CommandTierAtLevel(7, P)); // 強化1
            Assert.AreEqual(3, FlagshipBattleGrowthRules.CommandTierAtLevel(11, P)); // 強化2
            Assert.AreEqual(3, FlagshipBattleGrowthRules.CommandTierAtLevel(12, P));
        }

        [Test]
        public void Progress_FractionToNext()
        {
            int lv = FlagshipBattleGrowthRules.Progress(0f, P, out float f0);
            Assert.AreEqual(1, lv); Assert.AreEqual(0f, f0, 1e-4f);
            // レベル1の半分（280の半分=140）
            lv = FlagshipBattleGrowthRules.Progress(140f, P, out float fHalf);
            Assert.AreEqual(1, lv); Assert.AreEqual(0.5f, fHalf, 1e-3f);
            // ちょうどレベル2
            lv = FlagshipBattleGrowthRules.Progress(280f, P, out float f2);
            Assert.AreEqual(2, lv); Assert.AreEqual(0f, f2, 1e-3f);
            // 最大レベルで割合=1
            lv = FlagshipBattleGrowthRules.Progress(8580f, P, out float fMax);
            Assert.AreEqual(12, lv); Assert.AreEqual(1f, fMax, 1e-4f);
        }
    }
}
