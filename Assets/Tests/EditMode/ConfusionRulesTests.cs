using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>混乱（#突撃）の純ロジックテスト。統率による軽減（被ダメ増/与ダメ減/機動低下/持続）を担保する。</summary>
    public class ConfusionRulesTests
    {
        [Test]
        public void Effect_統率50は基準値()
        {
            var e = ConfusionRules.Effect(50f);
            Assert.AreEqual(1f - ConfusionRules.BaseAttackPenalty, e.attackFactor, 1e-4f);   // 0.65
            Assert.AreEqual(1f + ConfusionRules.BaseDamageTakenBonus, e.damageTakenFactor, 1e-4f); // 1.40
            Assert.AreEqual(1f - ConfusionRules.BaseMobilityPenalty, e.mobilityFactor, 1e-4f); // 0.60
            Assert.AreEqual(ConfusionRules.BaseDuration, e.duration, 1e-4f);
        }

        [Test]
        public void Effect_高統率ほど影響が小さい()
        {
            var low = ConfusionRules.Effect(0f);
            var mid = ConfusionRules.Effect(50f);
            var high = ConfusionRules.Effect(100f);

            // 高統率ほど与ダメ低下が小さい（attackFactor が1に近い）
            Assert.Greater(high.attackFactor, mid.attackFactor);
            Assert.Greater(mid.attackFactor, low.attackFactor);
            // 高統率ほど被ダメ増が小さい
            Assert.Less(high.damageTakenFactor, mid.damageTakenFactor);
            Assert.Less(mid.damageTakenFactor, low.damageTakenFactor);
            // 高統率ほど機動低下が小さい
            Assert.Greater(high.mobilityFactor, mid.mobilityFactor);
            // 高統率ほど早く立て直す（持続が短い）
            Assert.Less(high.duration, low.duration);
        }

        [Test]
        public void Effect_倍率は安全側にクランプ()
        {
            var e = ConfusionRules.Effect(0f); // 最も強い混乱
            Assert.GreaterOrEqual(e.attackFactor, 0.1f);
            Assert.GreaterOrEqual(e.mobilityFactor, 0.1f);
            Assert.Greater(e.damageTakenFactor, 1f);
        }
    }
}
