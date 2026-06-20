using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 銃後の士気（国民の戦争支持・長期戦の世論）を固定する：戦勝報道は支持を押し上げ、損害は
    /// 感受性ぶん支持を削り、長期化で厭戦が積み上がり、宣伝・報道統制が支持を維持し、支持低下が
    /// 反戦運動へ転化し、支持が体制安定を支え、閾値割れで銃後が崩壊する。クランプを担保。
    /// </summary>
    public class HomeFrontMoraleRulesTests
    {
        private static readonly HomeFrontMoraleParams P = HomeFrontMoraleParams.Default;
        // 基準0.5/戦勝0.5/損害0.4/生活苦0.3/厭戦0.02/宣伝0.5/反戦0.8/安定1/崩壊閾値0.25

        [Test]
        public void VictoryBoost_LiftsSupport_AndClamps()
        {
            // 戦勝規模0.5＝0.5×0.5=0.25 の上昇
            Assert.AreEqual(0.25f, HomeFrontMoraleRules.VictoryBoost(0.5f, P), 1e-4f);
            // 規模なしなら上昇なし
            Assert.AreEqual(0f, HomeFrontMoraleRules.VictoryBoost(0f, P), 1e-4f);
            // 入力はクランプ（規模2でも 0.5×1=0.5）
            Assert.AreEqual(0.5f, HomeFrontMoraleRules.VictoryBoost(2f, P), 1e-4f);
        }

        [Test]
        public void CasualtyShock_ScalesWithSensitivity()
        {
            // 損害0.5×感受性1＝0.4×0.5×1=0.2
            Assert.AreEqual(0.2f, HomeFrontMoraleRules.CasualtyShock(0.5f, 1f, P), 1e-4f);
            // 同じ損害でも感受性が半分なら半分
            Assert.AreEqual(0.1f, HomeFrontMoraleRules.CasualtyShock(0.5f, 0.5f, P), 1e-4f);
            // 損害最大×感受性最大＝0.4
            Assert.AreEqual(0.4f, HomeFrontMoraleRules.CasualtyShock(1f, 1f, P), 1e-4f);
        }

        [Test]
        public void WarWearinessGrowth_AccumulatesAndClamps()
        {
            // 継続10＝0.02×10=0.2
            Assert.AreEqual(0.2f, HomeFrontMoraleRules.WarWearinessGrowth(10f, P), 1e-4f);
            // 長すぎれば1で頭打ち（0.02×100=2→1）
            Assert.AreEqual(1f, HomeFrontMoraleRules.WarWearinessGrowth(100f, P), 1e-4f);
            // 負の時間は0
            Assert.AreEqual(0f, HomeFrontMoraleRules.WarWearinessGrowth(-5f, P), 1e-4f);
        }

        [Test]
        public void PropagandaEffect_NeedsBothIntensityAndMediaControl()
        {
            // 宣伝最大×統制最大＝0.5
            Assert.AreEqual(0.5f, HomeFrontMoraleRules.PropagandaEffect(1f, 1f, P), 1e-4f);
            // 統制が半分なら効果も半分
            Assert.AreEqual(0.25f, HomeFrontMoraleRules.PropagandaEffect(1f, 0.5f, P), 1e-4f);
            // 報道統制ゼロなら宣伝は効かない（積）
            Assert.AreEqual(0f, HomeFrontMoraleRules.PropagandaEffect(1f, 0f, P), 1e-4f);
        }

        [Test]
        public void WarSupport_BalancesVictoryAgainstLossAndHardship()
        {
            // 0.5 + 0.5×0.4 − 0.4×0.3 − 0.3×0.2 = 0.5+0.2−0.12−0.06 = 0.52
            Assert.AreEqual(0.52f, HomeFrontMoraleRules.WarSupport(0.4f, 0.3f, 0.2f, P), 1e-4f);
            // 連戦連勝・無傷・好景気なら支持は最大（クランプ）
            Assert.AreEqual(1f, HomeFrontMoraleRules.WarSupport(1f, 0f, 0f, P), 1e-4f);
            // 大損害＋生活苦で支持は底（0未満はクランプ）
            Assert.AreEqual(0f, HomeFrontMoraleRules.WarSupport(0f, 1f, 1f, P), 1e-4f);
        }

        [Test]
        public void AntiwarMovement_RisesAsSupportFalls()
        {
            // 支持0.3・不満0.5＝0.8×0.7×0.5=0.28
            Assert.AreEqual(0.28f, HomeFrontMoraleRules.AntiwarMovement(0.3f, 0.5f, P), 1e-4f);
            // 支持満点なら反戦は起きない
            Assert.AreEqual(0f, HomeFrontMoraleRules.AntiwarMovement(1f, 1f, P), 1e-4f);
            // 支持ゼロ×不満最大＝0.8
            Assert.AreEqual(0.8f, HomeFrontMoraleRules.AntiwarMovement(0f, 1f, P), 1e-4f);
        }

        [Test]
        public void RegimeStabilityFromSupport_TracksSupport()
        {
            Assert.AreEqual(0.6f, HomeFrontMoraleRules.RegimeStabilityFromSupport(0.6f, P), 1e-4f);
            Assert.AreEqual(1f, HomeFrontMoraleRules.RegimeStabilityFromSupport(1f, P), 1e-4f);
            Assert.AreEqual(0f, HomeFrontMoraleRules.RegimeStabilityFromSupport(0f, P), 1e-4f);
        }

        [Test]
        public void IsHomeFrontCollapsing_BelowThreshold()
        {
            // 支持0.2は閾値0.25を下回る＝崩壊
            Assert.IsTrue(HomeFrontMoraleRules.IsHomeFrontCollapsing(0.2f, 0.25f));
            // 0.3は持ちこたえる
            Assert.IsFalse(HomeFrontMoraleRules.IsHomeFrontCollapsing(0.3f, 0.25f));
            // 閾値ちょうどは崩壊とみなさない（厳密に未満）
            Assert.IsFalse(HomeFrontMoraleRules.IsHomeFrontCollapsing(0.25f, 0.25f));
        }

        [Test]
        public void Narrative_LongBloodyWar_ErodesSupportIntoCollapse()
        {
            // わずかな戦果(0.1)・甚大な損害(0.8)・生活苦(0.6)の泥沼
            // 0.5 + 0.5×0.1 − 0.4×0.8 − 0.3×0.6 = 0.5+0.05−0.32−0.18 = 0.05
            float support = HomeFrontMoraleRules.WarSupport(0.1f, 0.8f, 0.6f, P);
            Assert.AreEqual(0.05f, support, 1e-4f);
            // 銃後は崩壊しつつある
            Assert.IsTrue(HomeFrontMoraleRules.IsHomeFrontCollapsing(support, P.collapseThreshold));
            // 不満0.7の社会では反戦運動が燃え上がる＝0.8×0.95×0.7=0.532
            Assert.AreEqual(0.532f, HomeFrontMoraleRules.AntiwarMovement(support, 0.7f, P), 1e-4f);
            // 支持が崩れれば体制の支えも崩れる
            Assert.AreEqual(0.05f, HomeFrontMoraleRules.RegimeStabilityFromSupport(support, P), 1e-4f);
        }
    }
}
