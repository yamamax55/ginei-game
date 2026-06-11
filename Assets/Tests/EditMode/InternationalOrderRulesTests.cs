using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>多極経済秩序の相互支持と連鎖崩壊（四本柱カスケード・#1599）の純ロジック検証。</summary>
    public class InternationalOrderRulesTests
    {
        const float Eps = 0.0001f;

        /// <summary>秩序の安定度は最弱柱がそのまま効く＝鎖は最弱の輪で切れる。</summary>
        [Test]
        public void OrderStability_WeakestPillarRules()
        {
            float[] pillars = { 0.9f, 0.8f, 0.3f, 0.7f };
            Assert.AreEqual(0.3f, InternationalOrderRules.OrderStability(pillars), Eps);
        }

        /// <summary>最も脆い柱が崩壊の起点として返る。</summary>
        [Test]
        public void WeakestPillar_ReturnsLowestIndex()
        {
            float[] pillars = { 0.9f, 0.8f, 0.3f, 0.7f };
            Assert.AreEqual(2, InternationalOrderRules.WeakestPillar(pillars));
        }

        /// <summary>相互支持＝周囲の柱が健全なほど自柱が底上げされる（既定重み0.5）。</summary>
        [Test]
        public void MutualSupport_HealthyNeighborsLiftSelf()
        {
            // self=0.4*0.5=0.2, neighborAvg=1.0*0.5=0.5 → 0.7
            float v = InternationalOrderRules.MutualSupport(0.4f, new[] { 1.0f, 1.0f });
            Assert.AreEqual(0.7f, v, Eps);
        }

        /// <summary>周囲が無ければ相互支持なし＝素の健全度そのまま。</summary>
        [Test]
        public void MutualSupport_NoNeighborsReturnsSelf()
        {
            Assert.AreEqual(0.4f, InternationalOrderRules.MutualSupport(0.4f, null), Eps);
            Assert.AreEqual(0.4f, InternationalOrderRules.MutualSupport(0.4f, new float[0]), Eps);
        }

        /// <summary>連鎖の1ステップ＝倒れた柱は支えを失わず無傷、突出して健全な柱が周囲に引きずられて沈む。</summary>
        [Test]
        public void CascadeTick_StrongPillarsDragged_FailedPillarStays()
        {
            // total=3.0。柱0: self=1.0, othersAvg=(3-1)/3=0.6667, deficit=0.3333, drop=0.3333*0.2≈0.06667 → ≈0.93333
            // 柱3: self=0, othersAvg=(3-0)/3=1.0, deficit=0 → 0
            float[] next = InternationalOrderRules.CascadeTick(new[] { 1.0f, 1.0f, 1.0f, 0.0f }, 1f);
            Assert.AreEqual(0.93333f, next[0], 0.001f);
            Assert.AreEqual(0.93333f, next[1], 0.001f);
            Assert.AreEqual(0.93333f, next[2], 0.001f);
            Assert.AreEqual(0.0f, next[3], Eps);
        }

        /// <summary>崩壊した柱が依存度ぶん依存先を引き倒す伝染。依存度0なら無影響。</summary>
        [Test]
        public void CollapseContagion_DropsDependentByInterdependence()
        {
            // 全依存(1.0)で倒れた柱(0)が依存先(0.8)を全壊させる
            Assert.AreEqual(0.0f, InternationalOrderRules.CollapseContagion(0.0f, 0.8f, 1.0f), Eps);
            // 依存度0なら独立＝無影響
            Assert.AreEqual(0.8f, InternationalOrderRules.CollapseContagion(0.0f, 0.8f, 0.0f), Eps);
        }

        /// <summary>秩序崩壊判定＝最弱柱が閾値（既定0.25）を割れば崩壊。空配列は崩壊扱い。</summary>
        [Test]
        public void OrderCollapsed_WeakestBelowThreshold()
        {
            Assert.IsTrue(InternationalOrderRules.OrderCollapsed(new[] { 0.9f, 0.2f, 0.8f }));   // 0.2 < 0.25
            Assert.IsFalse(InternationalOrderRules.OrderCollapsed(new[] { 0.9f, 0.3f, 0.8f }));  // 0.3 ≥ 0.25
            Assert.IsTrue(InternationalOrderRules.OrderCollapsed(new float[0]));                 // 柱なし＝崩壊
        }

        /// <summary>再建難度＝倒れた柱が多いほど高い。耐連鎖性＝冗長性が連鎖に耐える。</summary>
        [Test]
        public void RestorationAndResilience()
        {
            Assert.AreEqual(0.5f, InternationalOrderRules.RestorationDifficulty(2, 4), Eps);
            Assert.AreEqual(1.0f, InternationalOrderRules.RestorationDifficulty(4, 4), Eps);
            // 冗長0なら安定度（最弱柱）そのまま
            Assert.AreEqual(0.5f, InternationalOrderRules.Resilience(new[] { 0.5f, 0.5f, 0.5f }, 0.0f), Eps);
            // 冗長1なら全柱崩壊からでも肩代わりして1へ
            Assert.AreEqual(1.0f, InternationalOrderRules.Resilience(new[] { 0.0f, 0.0f, 0.0f }, 1.0f), Eps);
        }
    }
}
