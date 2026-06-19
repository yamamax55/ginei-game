using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// BlockadeEconomyRules（封鎖の経済的影響）の EditMode テスト。既定 Params で期待値固定。
    /// </summary>
    public class BlockadeEconomyRulesTests
    {
        const float Eps = 1e-4f;

        [Test]
        public void TradeStrangulation_HighDependenceStranglesMore()
        {
            // b=clamp01(0.8×1.0)=0.8, dep=0.7 → 0.56 ×(1-0.1)=0.504
            Assert.AreEqual(0.504f, BlockadeEconomyRules.TradeStrangulation(0.8f, 0.7f), Eps);
            // 貿易依存ゼロなら締め上げ効かない
            Assert.AreEqual(0f, BlockadeEconomyRules.TradeStrangulation(0.8f, 0f), Eps);
        }

        [Test]
        public void ResourceShortage_DomesticProductionEases()
        {
            // 0.5 ×(1-0.4)=0.3
            Assert.AreEqual(0.3f, BlockadeEconomyRules.ResourceShortage(0.5f, 0.4f), Eps);
            // 完全自給なら不足しない
            Assert.AreEqual(0f, BlockadeEconomyRules.ResourceShortage(0.5f, 1f), Eps);
        }

        [Test]
        public void EconomicContraction_DeepensWithDuration()
        {
            // durationFactor=1+0.05×10=1.5 → 0.5×0.6×1.5=0.45
            Assert.AreEqual(0.45f, BlockadeEconomyRules.EconomicContraction(0.5f, 10f), Eps);
            // 開戦直後（duration 0）は係数 1.0 → 0.5×0.6=0.3
            Assert.AreEqual(0.3f, BlockadeEconomyRules.EconomicContraction(0.5f, 0f), Eps);
        }

        [Test]
        public void WarEffortDegradation_ProportionalToShortage()
        {
            // 0.5 ×0.8 = 0.4
            Assert.AreEqual(0.4f, BlockadeEconomyRules.WarEffortDegradation(0.5f), Eps);
        }

        [Test]
        public void BlackMarketLeakage_NetworksVsBlockade()
        {
            // s=50×1=50 → 50/(100+50)=0.33333...
            Assert.AreEqual(50f / 150f, BlockadeEconomyRules.BlackMarketLeakage(100f, 50f), Eps);
            // 密輸網なしなら抜け穴なし
            Assert.AreEqual(0f, BlockadeEconomyRules.BlackMarketLeakage(100f, 0f), Eps);
        }

        [Test]
        public void AutarkyAdaptation_ProgressesWithTime()
        {
            // progress=clamp01(0.1×5)=0.5 → 0.8×0.5=0.4
            Assert.AreEqual(0.4f, BlockadeEconomyRules.AutarkyAdaptation(0.8f, 5f), Eps);
            // 開戦直後は代替まだ進まない
            Assert.AreEqual(0f, BlockadeEconomyRules.AutarkyAdaptation(0.8f, 0f), Eps);
        }

        [Test]
        public void CivilianSuffering_RationingRelieves()
        {
            // relief=clamp01(0.5×0.5)=0.25 → 0.6×(1-0.25)=0.45
            Assert.AreEqual(0.45f, BlockadeEconomyRules.CivilianSuffering(0.6f, 0.5f), Eps);
            // 配給なしなら不足がそのまま苦しみに
            Assert.AreEqual(0.6f, BlockadeEconomyRules.CivilianSuffering(0.6f, 0f), Eps);
        }

        [Test]
        public void IsEconomyStrangled_AtThreshold()
        {
            Assert.IsTrue(BlockadeEconomyRules.IsEconomyStrangled(0.45f, 0.4f));
            Assert.IsTrue(BlockadeEconomyRules.IsEconomyStrangled(0.4f, 0.4f));
            Assert.IsFalse(BlockadeEconomyRules.IsEconomyStrangled(0.3f, 0.4f));
        }

        [Test]
        public void Narrative_LongBlockadeStranglesButAutarkyAndSmugglingResist()
        {
            var p = BlockadeEconomyRules.BlockadeEconomyParams.Default;

            // 強い封鎖（強度0.9）で、貿易依存の高い敵（0.8）を締め上げる。
            float strangle = BlockadeEconomyRules.TradeStrangulation(0.9f, 0.8f, p);
            // b=0.9, dep=0.8 → 0.72 ×0.9 = 0.648
            Assert.AreEqual(0.648f, strangle, Eps);

            // 国内生産が乏しければ資源は大きく不足する。
            float shortage = BlockadeEconomyRules.ResourceShortage(strangle, 0.2f);
            Assert.Greater(shortage, 0.4f);

            // 封鎖が長引くほど経済縮小は深まる。
            float early = BlockadeEconomyRules.EconomicContraction(shortage, 2f, p);
            float late = BlockadeEconomyRules.EconomicContraction(shortage, 30f, p);
            Assert.Greater(late, early);

            // 戦争遂行能力も削がれる。
            float warHit = BlockadeEconomyRules.WarEffortDegradation(shortage, p);
            Assert.Greater(warHit, 0f);

            // しかし敵は抗う：闇貿易と自給自足が時間で進み、緩和される。
            float leak = BlockadeEconomyRules.BlackMarketLeakage(0.9f, 0.6f, p);
            Assert.Greater(leak, 0f);
            float autarkyEarly = BlockadeEconomyRules.AutarkyAdaptation(0.7f, 3f, p);
            float autarkyLate = BlockadeEconomyRules.AutarkyAdaptation(0.7f, 8f, p);
            Assert.Greater(autarkyLate, autarkyEarly);

            // 配給が巧ければ民生の困窮は和らぐ。
            float withRation = BlockadeEconomyRules.CivilianSuffering(shortage, 0.8f, p);
            float noRation = BlockadeEconomyRules.CivilianSuffering(shortage, 0f, p);
            Assert.Less(withRation, noRation);

            // 長期封鎖で経済は締め上げられたと判定される。
            Assert.IsTrue(BlockadeEconomyRules.IsEconomyStrangled(late, 0.3f));
        }
    }
}
