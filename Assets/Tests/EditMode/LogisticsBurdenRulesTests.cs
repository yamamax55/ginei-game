using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// 超線形兵站消費＝尾高比スケーリング（CRV-2 #1365）のテスト。既定Params具体値で期待値固定。
    /// Pow を含む式は近似許容（Assert に許容誤差を付ける）。
    /// </summary>
    public class LogisticsBurdenRulesTests
    {
        const float Tol = 0.005f;

        /// <summary>兵站負担＝規模^1.3×距離^0.7の超線形。規模も距離も最大なら負担1.0、片方0なら0。</summary>
        [Test]
        public void SupplyBurden_IsSuperlinearInSizeAndDistance()
        {
            // 1^1.3 × 1^0.7 = 1.0
            Assert.AreEqual(1f, LogisticsBurdenRules.SupplyBurden(1f, 1f), Tol);
            // 規模0／距離0なら負担なし
            Assert.AreEqual(0f, LogisticsBurdenRules.SupplyBurden(0f, 1f), Tol);
            Assert.AreEqual(0f, LogisticsBurdenRules.SupplyBurden(1f, 0f), Tol);
            // 0.5^1.3 × 0.5^0.7 = 0.5^2.0 = 0.25
            Assert.AreEqual(0.25f, LogisticsBurdenRules.SupplyBurden(0.5f, 0.5f), Tol);
            // 規模を倍にすると負担は2^1.3=2.46倍に膨らむ＝超線形（比例の2倍を超える）
            float small = LogisticsBurdenRules.SupplyBurden(0.4f, 0.6f);
            float big = LogisticsBurdenRules.SupplyBurden(0.8f, 0.6f);
            Assert.Greater(big / small, 2.3f);
        }

        /// <summary>尾高比＝戦闘部隊に対する後方兵站の比。兵站負担が重く戦闘部隊が少ないほど tail が膨らむ。</summary>
        [Test]
        public void TailToToothRatio_GrowsWithBurdenAndShrinksWithCombatForce()
        {
            // baseTailRatio 3 × burden 0.5 / tooth 0.5 = 3.0
            Assert.AreEqual(3f, LogisticsBurdenRules.TailToToothRatio(0.5f, 0.5f), Tol);
            // 戦闘部隊が半分になると尾高比は倍に膨らむ
            float wide = LogisticsBurdenRules.TailToToothRatio(0.5f, 0.25f);
            Assert.AreEqual(6f, wide, Tol);
            // 戦闘部隊0なら無限（支えるだけの軍）
            Assert.IsTrue(float.IsPositiveInfinity(LogisticsBurdenRules.TailToToothRatio(0.5f, 0f)));
        }

        /// <summary>超線形ペナルティ＝線形予測（規模×距離）を超えた分。規模を効かせると過小見積もりになる。</summary>
        [Test]
        public void SuperlinearPenalty_ExceedsLinearEstimate()
        {
            // SupplyBurden(0.8,0.4)=0.8^1.3×0.4^0.7≈0.748×0.527≈0.394、線形 0.32、超過≈0.074
            float penalty = LogisticsBurdenRules.SuperlinearPenalty(0.8f, 0.4f);
            Assert.AreEqual(0.074f, penalty, 0.01f);
            Assert.Greater(penalty, 0f); // 線形で見積もると足りない
            // 規模・距離が0なら超過もなし
            Assert.AreEqual(0f, LogisticsBurdenRules.SuperlinearPenalty(0f, 0.5f), Tol);
        }

        /// <summary>持続可能な最大規模＝距離が遠いほど小さくなる（同じ補給能力でも遠征では大軍を養えない）。</summary>
        [Test]
        public void SustainableForceAtDistance_ShrinksWithDistance()
        {
            float near = LogisticsBurdenRules.SustainableForceAtDistance(0.3f, 0.5f);
            float far = LogisticsBurdenRules.SustainableForceAtDistance(0.9f, 0.5f);
            Assert.Greater(near, far); // 遠いほど養える規模が小さい
            // 距離0なら制約なし＝1.0
            Assert.AreEqual(1f, LogisticsBurdenRules.SustainableForceAtDistance(0f, 0.5f), Tol);
            // 供給能力0なら何も養えない
            Assert.AreEqual(0f, LogisticsBurdenRules.SustainableForceAtDistance(0.5f, 0f), Tol);
            // 逆算の整合：求めた規模での負担≈供給能力
            float size = LogisticsBurdenRules.SustainableForceAtDistance(0.6f, 0.4f);
            float burden = LogisticsBurdenRules.SupplyBurden(size, 0.6f);
            Assert.AreEqual(0.4f, burden, 0.02f);
        }

        /// <summary>集中vs分散＝集中の超線形兵站負担をデポ支援で緩和。デポが充実するほど集中が軽くなる。</summary>
        [Test]
        public void ConcentrationVsDispersion_RelievedByDepotSupport()
        {
            float noDepot = LogisticsBurdenRules.ConcentrationVsDispersion(0.8f, 0.6f, 0f);
            float withDepot = LogisticsBurdenRules.ConcentrationVsDispersion(0.8f, 0.6f, 0.5f);
            Assert.Greater(noDepot, withDepot); // デポ支援で集中兵站が緩和
            // デポ0なら集中兵站負担そのもの
            float burden = LogisticsBurdenRules.SupplyBurden(0.8f, 0.6f);
            Assert.AreEqual(Mathf.Clamp01(burden), noDepot, Tol);
            // フルデポなら兵站負担0
            Assert.AreEqual(0f, LogisticsBurdenRules.ConcentrationVsDispersion(0.8f, 0.6f, 1f), Tol);
        }

        /// <summary>補給の距離減衰＝遠いほど補給効率が落ちる（運ぶ途中で消費される）。距離0で満杯、下限で止まる。</summary>
        [Test]
        public void DistanceDecayOfSupply_FallsWithDistance()
        {
            // 距離0なら満杯
            Assert.AreEqual(1f, LogisticsBurdenRules.DistanceDecayOfSupply(0f, 0.5f), Tol);
            float near = LogisticsBurdenRules.DistanceDecayOfSupply(0.3f, 0.5f);
            float far = LogisticsBurdenRules.DistanceDecayOfSupply(0.9f, 0.5f);
            Assert.Greater(near, far); // 遠いほど効率が落ちる
            // 補給能力が高いほど遠くまで効率を保つ
            float lowEff = LogisticsBurdenRules.DistanceDecayOfSupply(0.7f, 0.2f);
            float highEff = LogisticsBurdenRules.DistanceDecayOfSupply(0.7f, 0.8f);
            Assert.Greater(highEff, lowEff);
            // 下限 0.1 を下回らない
            Assert.GreaterOrEqual(LogisticsBurdenRules.DistanceDecayOfSupply(1f, 0f), 0.1f - Tol);
        }

        /// <summary>過伸張閾値＝規模×距離の超線形兵站が閾値を超えると兵站破綻。大軍遠征で成立。</summary>
        [Test]
        public void OverstretchThreshold_TripsOnLargeForceAtDistance()
        {
            // 大軍を遠くへ＝負担が既定閾値0.5を超える（SupplyBurden(0.9,0.9)≈0.81>0.5）
            Assert.IsTrue(LogisticsBurdenRules.OverstretchThreshold(0.9f, 0.9f));
            // 小規模・近距離なら破綻しない（SupplyBurden(0.3,0.3)≈0.09<0.5）
            Assert.IsFalse(LogisticsBurdenRules.OverstretchThreshold(0.3f, 0.3f));
        }

        /// <summary>持続不能判定＝兵站負担が供給能力に対し閾値超で持続できない。供給能力0なら負担あれば必ず持続不能。</summary>
        [Test]
        public void IsLogisticallyUnsustainable_WhenBurdenExceedsCapacity()
        {
            // 負担0.8／能力0.5 = 1.6 > 既定閾値1.0 ＝持続不能
            Assert.IsTrue(LogisticsBurdenRules.IsLogisticallyUnsustainable(0.8f, 0.5f));
            // 負担0.3／能力0.8 = 0.375 < 1.0 ＝持続可能
            Assert.IsFalse(LogisticsBurdenRules.IsLogisticallyUnsustainable(0.3f, 0.8f));
            // 供給能力0で負担ありなら持続不能
            Assert.IsTrue(LogisticsBurdenRules.IsLogisticallyUnsustainable(0.2f, 0f));
            // 供給能力0でも負担0なら持続可能
            Assert.IsFalse(LogisticsBurdenRules.IsLogisticallyUnsustainable(0f, 0f));
        }
    }
}
