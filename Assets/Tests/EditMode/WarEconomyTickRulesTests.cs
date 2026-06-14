using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 戦時経済・銃後の薄いオーケストレータを固定する：動員で軍需生産が増え、過大動員/厭戦で銃後の支持が負へ、
    /// 封鎖強度と交易依存で経済打撃が増し、null は無いが境界（0・1・過大）が安全に bounded であること。
    /// 数式は委譲先（Mobilization/HomeFront/Blockade/ScorchedEarth）が担い、ここは合成の固定のみ。
    /// </summary>
    public class WarEconomyTickRulesTests
    {
        [Test]
        public void WarProductionFactor_RisesWithMobilization()
        {
            // 動員→軍需生産（MobilizedPool 委譲＝industryBase×動員率）。
            Assert.AreEqual(0f, WarEconomyTickRules.WarProductionFactor(0f, 100f), 1e-5f);   // 未動員＝0
            Assert.AreEqual(50f, WarEconomyTickRules.WarProductionFactor(0.5f, 100f), 1e-5f);
            float low = WarEconomyTickRules.WarProductionFactor(0.5f, 100f);
            float high = WarEconomyTickRules.WarProductionFactor(0.8f, 100f);
            Assert.Greater(high, low);                                                       // 動員が深いほど軍需↑
        }

        [Test]
        public void HomeFrontSupportDelta_FallsWithOvermobilizationAndWeariness()
        {
            // 平時（動員0・厭戦0）＝銃後の士気が支持を支える＝正。
            float calm = WarEconomyTickRules.HomeFrontSupportDelta(0f, 0f);
            Assert.Greater(calm, 0f);

            // 過大動員（持続可能率を超える）＝厭戦#113 で支持が削られる。
            float overMob = WarEconomyTickRules.HomeFrontSupportDelta(1f, 0f);
            Assert.Less(overMob, calm);

            // 厭戦が深い＝勝利を信じられず士気が落ちて支持は負へ。
            float weary = WarEconomyTickRules.HomeFrontSupportDelta(0f, 1f);
            Assert.Less(weary, 0f);
            Assert.Less(weary, calm);
        }

        [Test]
        public void BlockadeEconomicHit_RisesWithIntensityAndDependence()
        {
            // 封鎖なし＝打撃なし。
            Assert.AreEqual(0f, WarEconomyTickRules.BlockadeEconomicHit(0f, 1f), 1e-5f);
            // 交易に依存しない＝打撃なし（届かなくても困らない）。
            Assert.AreEqual(0f, WarEconomyTickRules.BlockadeEconomicHit(1f, 0f), 1e-5f);
            // 完全封鎖×全面依存＝最大打撃1.0。
            Assert.AreEqual(1f, WarEconomyTickRules.BlockadeEconomicHit(1f, 1f), 1e-5f);
            // 強度が上がるほど打撃が増す（単調）。
            float mid = WarEconomyTickRules.BlockadeEconomicHit(0.5f, 1f);
            Assert.Greater(mid, 0f);
            Assert.Less(mid, 1f);
        }

        [Test]
        public void ScorchedEarthDenial_DeniesAssetsToInvader()
        {
            // 焼却範囲0＝拒否なし。
            Assert.AreEqual(0f, WarEconomyTickRules.ScorchedEarthDenial(100f, 0f), 1e-5f);
            // 焼くほど拒否価値が増す（徹底度0.8＝範囲1で80）。
            float denied = WarEconomyTickRules.ScorchedEarthDenial(100f, 1f);
            Assert.Greater(denied, 0f);
            Assert.Greater(WarEconomyTickRules.ScorchedEarthDenial(100f, 1f), WarEconomyTickRules.ScorchedEarthDenial(100f, 0.5f));
        }

        [Test]
        public void Boundaries_AreSafeAndClamped()
        {
            // 範囲外/負の入力でも bounded（クランプ済み）。
            Assert.AreEqual(0f, WarEconomyTickRules.WarProductionFactor(-1f, 100f), 1e-5f);       // 負の動員→0
            Assert.AreEqual(0f, WarEconomyTickRules.WarProductionFactor(2f, -50f), 1e-5f);        // 負の基盤→0

            float hit = WarEconomyTickRules.BlockadeEconomicHit(2f, 2f);                          // 1超入力
            Assert.GreaterOrEqual(hit, 0f);
            Assert.LessOrEqual(hit, 1f);

            float sup = WarEconomyTickRules.HomeFrontSupportDelta(5f, -3f);                       // 範囲外
            Assert.GreaterOrEqual(sup, -1f);
            Assert.LessOrEqual(sup, 1f);

            Assert.AreEqual(0f, WarEconomyTickRules.ScorchedEarthDenial(-10f, 1f), 1e-5f);        // 負の資産→0
        }
    }
}
