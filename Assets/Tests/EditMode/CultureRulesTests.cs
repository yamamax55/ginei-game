using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 文化・民族・ナショナリズム（#194・宗教 #172 の姉妹）を固定する：占領直後の少数民族は同化圧力が高く、
    /// 多数派支配下で時間とともに同化が進む。同化が低く安定が割れると分離独立リスクが立ち、ナショナリズムが
    /// 結束/士気を底上げし、抑圧下では決定論的に亡命しうる。各分岐＋境界＋クランプを担保。
    /// </summary>
    public class CultureRulesTests
    {
        private static readonly CultureParams P = CultureParams.Default;
        // base 0.7 / warPenalty 0.4 / speed 0.04 / sepThreshold 40 / nationalismMaxBonus 0.3 / exileMaxChance 0.6

        // --- AssimilationPressure ---
        [Test]
        public void AssimilationPressure_OccupiedMinority_IsHigh()
        {
            var c = new Culture("辺境民", population: 100f, assimilation: 0.2f, isMinority: true);
            // 0.7 × (1-0.2) = 0.56
            Assert.AreEqual(0.56f, CultureRules.AssimilationPressure(c, "帝国民", false, P), 1e-4f);
        }

        [Test]
        public void AssimilationPressure_AtWar_DropsByPenalty()
        {
            var c = new Culture("辺境民", assimilation: 0.2f, isMinority: true);
            // 0.56 - 0.4 = 0.16（戦時は分断で圧力低下）
            Assert.AreEqual(0.16f, CultureRules.AssimilationPressure(c, "帝国民", true, P), 1e-4f);
        }

        [Test]
        public void AssimilationPressure_MajorityOrSameCulture_IsZero()
        {
            var majority = new Culture("帝国民", assimilation: 0f, isMinority: false);
            Assert.AreEqual(0f, CultureRules.AssimilationPressure(majority, "帝国民", false, P), 1e-4f);

            var same = new Culture("帝国民", assimilation: 0.1f, isMinority: true);
            // 少数民族でも名前が多数派と一致＝同化の余地なし
            Assert.AreEqual(0f, CultureRules.AssimilationPressure(same, "帝国民", false, P), 1e-4f);
        }

        // --- Tick ---
        [Test]
        public void Tick_UnderDominant_AdvancesAssimilation()
        {
            var c = new Culture("辺境民", assimilation: 0.2f, isMinority: true);
            CultureRules.Tick(c, dominantCultureMatch: true, atWar: false, deltaTime: 5f, P);
            // 0.2 + 0.04×5 = 0.4
            Assert.AreEqual(0.4f, c.assimilation, 1e-4f);
        }

        [Test]
        public void Tick_AtWar_IsSlower_And_NotMatch_DoesNothing()
        {
            var war = new Culture("辺境民", assimilation: 0.2f, isMinority: true);
            // 戦時は速度 0.04×(1-0.4)=0.024 → 0.2 + 0.024×5 = 0.32
            CultureRules.Tick(war, dominantCultureMatch: true, atWar: true, deltaTime: 5f, P);
            Assert.AreEqual(0.32f, war.assimilation, 1e-4f);

            // 多数派支配下でない＝同化は進まない（基準非破壊）
            var free = new Culture("辺境民", assimilation: 0.2f, isMinority: true);
            CultureRules.Tick(free, dominantCultureMatch: false, atWar: false, deltaTime: 5f, P);
            Assert.AreEqual(0.2f, free.assimilation, 1e-4f);
        }

        [Test]
        public void Tick_Clamps_To_One()
        {
            var c = new Culture("辺境民", assimilation: 0.95f, isMinority: true);
            CultureRules.Tick(c, true, false, 100f, P); // 大きな dt でも上限 1
            Assert.AreEqual(1f, c.assimilation, 1e-4f);
        }

        // --- SeparatismRisk ---
        [Test]
        public void SeparatismRisk_LowAssimilation_LowStability_IsHigh()
        {
            var c = new Culture("辺境民", assimilation: 0.2f, isMinority: true);
            // instability=(40-10)/40=0.75, disaffection=0.8 → 0.6
            Assert.AreEqual(0.6f, CultureRules.SeparatismRisk(c, 10f, P), 1e-4f);
        }

        [Test]
        public void SeparatismRisk_StableOrNonMinority_IsZero()
        {
            var c = new Culture("辺境民", assimilation: 0.2f, isMinority: true);
            // しきい値(40)以上の安定では分離独立しない
            Assert.AreEqual(0f, CultureRules.SeparatismRisk(c, 40f, P), 1e-4f);

            var majority = new Culture("帝国民", assimilation: 0f, isMinority: false);
            Assert.AreEqual(0f, CultureRules.SeparatismRisk(majority, 0f, P), 1e-4f);
        }

        // --- NationalismFactor ---
        [Test]
        public void NationalismFactor_LowAssimilation_BoostsCohesion()
        {
            var c = new Culture("辺境民", assimilation: 0.2f, isMinority: true);
            // 1 + 0.3×(1-0.2) = 1.24
            Assert.AreEqual(1.24f, CultureRules.NationalismFactor(c, P), 1e-4f);
        }

        [Test]
        public void NationalismFactor_AssimilatedOrMajority_IsNeutral()
        {
            var assimilated = new Culture("辺境民", assimilation: 1f, isMinority: true);
            Assert.AreEqual(1f, CultureRules.NationalismFactor(assimilated, P), 1e-4f);

            var majority = new Culture("帝国民", assimilation: 0f, isMinority: false);
            Assert.AreEqual(1f, CultureRules.NationalismFactor(majority, P), 1e-4f);
        }

        // --- ExileLikelihood ---
        [Test]
        public void ExileLikelihood_Deterministic_AcrossThreshold()
        {
            var c = new Culture("辺境民", assimilation: 0.2f, isMinority: true);
            // chance = 0.6 × 1.0(oppression) × (1-0.2) = 0.48
            Assert.IsTrue(CultureRules.ExileLikelihood(c, 1f, 0.4f, P));  // 0.4 < 0.48 → 亡命
            Assert.IsFalse(CultureRules.ExileLikelihood(c, 1f, 0.5f, P)); // 0.5 ≥ 0.48 → 留まる
        }

        [Test]
        public void ExileLikelihood_MajorityNeverExiles_And_NoOppressionNoExile()
        {
            var majority = new Culture("帝国民", assimilation: 0f, isMinority: false);
            Assert.IsFalse(CultureRules.ExileLikelihood(majority, 1f, 0f, P)); // 多数派は亡命せず（chance=0）

            var minority = new Culture("辺境民", assimilation: 0.2f, isMinority: true);
            Assert.IsFalse(CultureRules.ExileLikelihood(minority, 0f, 0f, P)); // 抑圧0＝chance0、roll 0 でも亡命せず
        }

        // --- null 安全 ---
        [Test]
        public void NullCulture_IsSafe()
        {
            Assert.AreEqual(0f, CultureRules.AssimilationPressure(null, "帝国民", false, P), 1e-4f);
            Assert.AreEqual(0f, CultureRules.SeparatismRisk(null, 0f, P), 1e-4f);
            Assert.AreEqual(1f, CultureRules.NationalismFactor(null, P), 1e-4f);
            Assert.IsFalse(CultureRules.ExileLikelihood(null, 1f, 0f, P));
            CultureRules.Tick(null, true, false, 5f, P); // 例外を投げない
        }

        // --- 既定オーバーロード（CultureParams 省略）が .Default と一致 ---
        [Test]
        public void DefaultOverloads_MatchExplicitParams()
        {
            var c = new Culture("辺境民", assimilation: 0.2f, isMinority: true);
            Assert.AreEqual(CultureRules.AssimilationPressure(c, "帝国民", false, P),
                            CultureRules.AssimilationPressure(c, "帝国民", false), 1e-4f);
            Assert.AreEqual(CultureRules.SeparatismRisk(c, 10f, P),
                            CultureRules.SeparatismRisk(c, 10f), 1e-4f);
            Assert.AreEqual(CultureRules.NationalismFactor(c, P),
                            CultureRules.NationalismFactor(c), 1e-4f);
            Assert.AreEqual(CultureRules.ExileLikelihood(c, 1f, 0.4f, P),
                            CultureRules.ExileLikelihood(c, 1f, 0.4f));
        }
    }
}
