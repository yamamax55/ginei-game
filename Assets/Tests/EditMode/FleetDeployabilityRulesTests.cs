using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 艦隊即応性（FleetDeployabilityRules・MILDEP-1）の純ロジックテスト。
    /// 単調性・境界・委譲整合・null/極値安全・決定論を担保する。
    /// </summary>
    public class FleetDeployabilityRulesTests
    {
        const float Eps = 1e-4f;

        // ---- 委譲整合（delegation correctness） ----

        [Test]
        public void SupplyFactor_補給はOverallReadinessへ委譲()
        {
            float expected = MilitaryReadinessRules.OverallReadiness(0.7f, 0.4f, 0.9f);
            Assert.AreEqual(expected, FleetDeployabilityRules.SupplyFactor(0.7f, 0.4f, 0.9f), Eps);
        }

        [Test]
        public void SupplyFactor_最小律で最も欠けた物資に律速される()
        {
            // 燃料0.4 が最小＝OverallReadiness=0.4。
            Assert.AreEqual(0.4f, FleetDeployabilityRules.SupplyFactor(0.9f, 0.4f, 1.0f), Eps);
        }

        [Test]
        public void CrewFactor_疲労はCombatEffectivenessDecayへ委譲()
        {
            float expected = CombatFatigueRules.CombatEffectivenessDecay(0.5f);
            Assert.AreEqual(expected, FleetDeployabilityRules.CrewFactor(0.5f), Eps);
        }

        [Test]
        public void ConditionFactor_整備はReactivatedEffectivenessへ委譲()
        {
            float expected = MothballRules.ReactivatedEffectiveness(0.6f);
            Assert.AreEqual(expected, FleetDeployabilityRules.ConditionFactor(0.6f), Eps);
        }

        // ---- 内訳の単調性（monotonicity） ----

        [Test]
        public void CrewFactor_疲労が増えるほど即応性は下がる()
        {
            float low = FleetDeployabilityRules.CrewFactor(0.1f);
            float high = FleetDeployabilityRules.CrewFactor(0.8f);
            Assert.Greater(low, high);
        }

        [Test]
        public void ConditionFactor_整備が良いほど即応性は上がる()
        {
            float poor = FleetDeployabilityRules.ConditionFactor(0.2f);
            float good = FleetDeployabilityRules.ConditionFactor(0.9f);
            Assert.Greater(good, poor);
        }

        [Test]
        public void Compose_補給が増えれば総合即応性は単調増加()
        {
            float lo = FleetDeployabilityRules.Compose(0.5f, 0.8f, 0.8f);
            float hi = FleetDeployabilityRules.Compose(0.9f, 0.8f, 0.8f);
            Assert.Greater(hi, lo);
        }

        // ---- 境界（bounds） ----

        [Test]
        public void Compose_全要素満点で1に近い()
        {
            float d = FleetDeployabilityRules.Compose(1f, 1f, 1f);
            Assert.AreEqual(1f, d, Eps);
        }

        [Test]
        public void Compose_結果は0以上1以下に収まる()
        {
            for (float s = 0f; s <= 1f; s += 0.25f)
                for (float c = 0f; c <= 1f; c += 0.25f)
                    for (float k = 0f; k <= 1f; k += 0.25f)
                    {
                        float d = FleetDeployabilityRules.Compose(s, c, k);
                        Assert.GreaterOrEqual(d, 0f);
                        Assert.LessOrEqual(d, 1f);
                    }
        }

        [Test]
        public void Compose_いずれかがゼロなら総合はゼロ_律速()
        {
            // 補給ゼロは他が満点でも全体を律速する（相乗）。
            Assert.AreEqual(0f, FleetDeployabilityRules.Compose(0f, 1f, 1f), Eps);
            Assert.AreEqual(0f, FleetDeployabilityRules.Compose(1f, 0f, 1f), Eps);
            Assert.AreEqual(0f, FleetDeployabilityRules.Compose(1f, 1f, 0f), Eps);
        }

        [Test]
        public void Compose_危機床未満なら0へ切り落とす()
        {
            var p = new FleetDeployabilityParams(1f, 1f, 1f, 0.4f, 0.3f);
            // 三要素とも 0.2 → 相乗 0.2（床 0.3 未満）→ 0。
            Assert.AreEqual(0f, FleetDeployabilityRules.Compose(0.2f, 0.2f, 0.2f, p), Eps);
        }

        [Test]
        public void Compose_重み合計ゼロなら0()
        {
            var p = new FleetDeployabilityParams(0f, 0f, 0f, 0.4f, 0.1f);
            Assert.AreEqual(0f, FleetDeployabilityRules.Compose(1f, 1f, 1f, p), Eps);
        }

        [Test]
        public void Compose_重みゼロの要素はその要素を無視する()
        {
            // 整備の重みを 0 にすれば整備が悪くても総合は下がらない。
            var p = new FleetDeployabilityParams(0.5f, 0.5f, 0f, 0.1f, 0.05f);
            float withBadCondition = FleetDeployabilityRules.Compose(0.8f, 0.8f, 0.1f, p);
            float withGoodCondition = FleetDeployabilityRules.Compose(0.8f, 0.8f, 1.0f, p);
            Assert.AreEqual(withGoodCondition, withBadCondition, Eps);
        }

        // ---- Evaluate（一括評価）・出撃可否 ----

        [Test]
        public void Evaluate_内訳が各委譲値と一致する()
        {
            var r = FleetDeployabilityRules.Evaluate(0.8f, 0.7f, 0.9f, 0.3f, 0.6f);
            Assert.AreEqual(MilitaryReadinessRules.OverallReadiness(0.8f, 0.7f, 0.9f), r.supplyFactor, Eps);
            Assert.AreEqual(CombatFatigueRules.CombatEffectivenessDecay(0.3f), r.crewFactor, Eps);
            Assert.AreEqual(MothballRules.ReactivatedEffectiveness(0.6f), r.conditionFactor, Eps);
        }

        [Test]
        public void Evaluate_高即応の艦隊は出撃可能()
        {
            var r = FleetDeployabilityRules.Evaluate(1f, 1f, 1f, 0f, 1f);
            Assert.IsTrue(r.isDeployable);
            Assert.GreaterOrEqual(r.deployability, 0.4f);
        }

        [Test]
        public void Evaluate_補給枯渇の艦隊は出撃不能()
        {
            // 弾薬ゼロ＝補給0＝総合0＝出撃不能。
            var r = FleetDeployabilityRules.Evaluate(0f, 1f, 1f, 0f, 1f);
            Assert.IsFalse(r.isDeployable);
            Assert.AreEqual(0f, r.deployability, Eps);
        }

        [Test]
        public void IsDeployable_閾値ちょうどは出撃可能()
        {
            var p = new FleetDeployabilityParams(1f, 1f, 1f, 0.5f, 0.1f);
            Assert.IsTrue(FleetDeployabilityRules.IsDeployable(0.5f, p));
            Assert.IsFalse(FleetDeployabilityRules.IsDeployable(0.49f, p));
        }

        // ---- 実効戦力 ----

        [Test]
        public void EffectiveStrength_即応性で線形に割り引かれる()
        {
            Assert.AreEqual(5000f, FleetDeployabilityRules.EffectiveStrength(10000f, 0.5f), Eps);
            Assert.AreEqual(0f, FleetDeployabilityRules.EffectiveStrength(10000f, 0f), Eps);
            Assert.AreEqual(10000f, FleetDeployabilityRules.EffectiveStrength(10000f, 1f), Eps);
        }

        [Test]
        public void EffectiveStrength_負の戦力や即応性はクランプされる()
        {
            Assert.AreEqual(0f, FleetDeployabilityRules.EffectiveStrength(-100f, 1f), Eps);
            Assert.AreEqual(0f, FleetDeployabilityRules.EffectiveStrength(100f, -1f), Eps);
        }

        // ---- 極値・null安全（入力クランプ） ----

        [Test]
        public void Compose_範囲外入力はクランプされ例外を投げない()
        {
            float d = FleetDeployabilityRules.Compose(5f, -3f, 100f);
            Assert.GreaterOrEqual(d, 0f);
            Assert.LessOrEqual(d, 1f);
        }

        [Test]
        public void Params_負の重みや範囲外閾値はクランプされる()
        {
            var p = new FleetDeployabilityParams(-1f, 2f, -0.5f, 5f, -2f);
            Assert.GreaterOrEqual(p.supplyWeight, 0f);
            Assert.GreaterOrEqual(p.fatigueWeight, 0f);
            Assert.GreaterOrEqual(p.conditionWeight, 0f);
            Assert.AreEqual(1f, p.deployableThreshold, Eps); // 5→1 へクランプ
            Assert.AreEqual(0f, p.criticalFloor, Eps);       // -2→0 へクランプ
        }

        // ---- 決定論（determinism） ----

        [Test]
        public void Evaluate_同入力は常に同結果_決定論()
        {
            var a = FleetDeployabilityRules.Evaluate(0.6f, 0.7f, 0.5f, 0.4f, 0.55f);
            var b = FleetDeployabilityRules.Evaluate(0.6f, 0.7f, 0.5f, 0.4f, 0.55f);
            Assert.AreEqual(a.supplyFactor, b.supplyFactor, Eps);
            Assert.AreEqual(a.crewFactor, b.crewFactor, Eps);
            Assert.AreEqual(a.conditionFactor, b.conditionFactor, Eps);
            Assert.AreEqual(a.deployability, b.deployability, Eps);
            Assert.AreEqual(a.isDeployable, b.isDeployable);
        }
    }
}
