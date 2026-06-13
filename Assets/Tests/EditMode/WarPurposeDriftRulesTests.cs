using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>開戦理由の腐食（TYW-3 #1426・三十年戦争）の純ロジック検証。</summary>
    public class WarPurposeDriftRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>戦争が長引くほど当初の理想が薄いほど目的が権力政治へドリフトする。</summary>
        [Test]
        public void PurposeDrift_LongWarErodesIdeals()
        {
            // ideal=0.2, dur=1.0, dt=1: 0.1*(1-0.2)*(1.0*0.6) = 0.1*0.8*0.6 = 0.048
            float d = WarPurposeDriftRules.PurposeDrift(0.2f, 1f, 1f);
            Assert.AreEqual(0.048f, d, Eps);

            // 理想が強いほどドリフトは小さい（ideal=0.9 < ideal=0.2）。
            float strong = WarPurposeDriftRules.PurposeDrift(0.9f, 1f, 1f);
            Assert.Less(strong, d);

            // 戦争が短い（dur=0）ならドリフトしない。
            Assert.AreEqual(0f, WarPurposeDriftRules.PurposeDrift(0.2f, 0f, 1f), Eps);
        }

        /// <summary>権力政治の割合は腐食度に正比例（理想が腐食した分）。</summary>
        [Test]
        public void PowerPoliticsShare_EqualsDrift()
        {
            Assert.AreEqual(0.7f, WarPurposeDriftRules.PowerPoliticsShare(0.7f), Eps);
            Assert.AreEqual(1f, WarPurposeDriftRules.PowerPoliticsShare(2f), Eps); // クランプ
            Assert.AreEqual(0f, WarPurposeDriftRules.PowerPoliticsShare(-1f), Eps);
        }

        /// <summary>イデオロギー的に敵でも利害一致で同盟が逆転する（カトリック仏が新教側へ）。</summary>
        [Test]
        public void IdeologicalAllianceReversal_InterestTrumpsIdeology()
        {
            // align=0.0(敵対), interest=1.0: 1.0*(1-0.0)*0.8 = 0.8
            float r = WarPurposeDriftRules.IdeologicalAllianceReversal(0f, 1f);
            Assert.AreEqual(0.8f, r, Eps);

            // イデオロギーが一致(align=1)すれば逆転は起きない。
            Assert.AreEqual(0f, WarPurposeDriftRules.IdeologicalAllianceReversal(1f, 1f), Eps);

            // 利害がなければ逆転しない。
            Assert.AreEqual(0f, WarPurposeDriftRules.IdeologicalAllianceReversal(0f, 0f), Eps);
        }

        /// <summary>大義は冷笑が深く理想が薄いほど速く形骸化する（口実と化す）。</summary>
        [Test]
        public void CauseHollowing_CynicismHollowsCause()
        {
            // ideal=0.2, cyn=1.0, dt=1: 0.1*(1-0.2)*(1.0*0.7) = 0.1*0.8*0.7 = 0.056
            float h = WarPurposeDriftRules.CauseHollowing(0.2f, 1f, 1f);
            Assert.AreEqual(0.056f, h, Eps);

            // 冷笑がなければ形骸化しない。
            Assert.AreEqual(0f, WarPurposeDriftRules.CauseHollowing(0.2f, 0f, 1f), Eps);
        }

        /// <summary>大義が消えると戦争が利益（略奪・領土）の論理で動く。</summary>
        [Test]
        public void MercenaryWarLogic_ProfitDrivesWar()
        {
            // share=1.0, profit=1.0: 1.0*(1.0*0.6) = 0.6
            float m = WarPurposeDriftRules.MercenaryWarLogic(1f, 1f);
            Assert.AreEqual(0.6f, m, Eps);

            // 大義が健在（share=0）なら利益の論理は支配しない。
            Assert.AreEqual(0f, WarPurposeDriftRules.MercenaryWarLogic(0f, 1f), Eps);
        }

        /// <summary>当初の大義が強かった戦争ほど、そこからの逸脱が正統性を蝕む。</summary>
        [Test]
        public void LegitimacyErosionFromDrift_DeviationErodesLegitimacy()
        {
            // drift=1.0, orig=1.0: 1.0*1.0*0.5 = 0.5
            float e = WarPurposeDriftRules.LegitimacyErosionFromDrift(1f, 1f);
            Assert.AreEqual(0.5f, e, Eps);

            // 当初の正当化が弱ければ逸脱の落差も小さい。
            float weak = WarPurposeDriftRules.LegitimacyErosionFromDrift(1f, 0.2f);
            Assert.Less(weak, e);
        }

        /// <summary>大義が薄れると同盟が利害でころころ変わる（流動的な陣営）。</summary>
        [Test]
        public void RealignmentVolatility_InterestMakesAlliancesFluid()
        {
            // interest=1.0, align=0.0: 1.0*(1-0.0) = 1.0
            Assert.AreEqual(1f, WarPurposeDriftRules.RealignmentVolatility(1f, 0f), Eps);
            // イデオロギーの拘束が強い(align=1)なら陣営は固定。
            Assert.AreEqual(0f, WarPurposeDriftRules.RealignmentVolatility(1f, 1f), Eps);
        }

        /// <summary>権力政治割合が閾値（既定0.6）以上で純然たる権力闘争戦争と判定。</summary>
        [Test]
        public void IsPowerPoliticsWar_ThresholdGate()
        {
            Assert.IsTrue(WarPurposeDriftRules.IsPowerPoliticsWar(0.7f));   // 0.6 超え
            Assert.IsTrue(WarPurposeDriftRules.IsPowerPoliticsWar(0.6f));   // 境界＝以上
            Assert.IsFalse(WarPurposeDriftRules.IsPowerPoliticsWar(0.5f));  // 大義がまだ残る
        }

        /// <summary>既定 Params の具体値。</summary>
        [Test]
        public void DefaultParams_Values()
        {
            var p = WarPurposeDriftParams.Default;
            Assert.AreEqual(0.1f, p.driftRate, Eps);
            Assert.AreEqual(0.6f, p.durationWeight, Eps);
            Assert.AreEqual(0.1f, p.hollowingRate, Eps);
            Assert.AreEqual(0.7f, p.cynicismWeight, Eps);
            Assert.AreEqual(0.8f, p.realignmentWeight, Eps);
            Assert.AreEqual(0.5f, p.legitimacyErosionWeight, Eps);
            Assert.AreEqual(0.6f, p.profitWeight, Eps);
            Assert.AreEqual(0.6f, p.powerPoliticsThreshold, Eps);
        }
    }
}
