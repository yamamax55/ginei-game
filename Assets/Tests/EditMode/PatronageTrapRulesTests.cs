using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 後見依存ジレンマ（CEND-2 #2358）を固定する：援助を受けるほど依存度が上がり短期ブーストは増す一方、
    /// 有機的成長の上限は下がる（依存の罠）。援助が止まれば依存は自然低下し自立を取り戻す。撤退ショックは
    /// 依存度に対し二乗で増える。発育不全は閾値以上で成立。境界クランプ・null/負安全を担保。
    /// </summary>
    public class PatronageTrapRulesTests
    {
        private static readonly PatronageTrapParams P = PatronageTrapParams.Default;
        // 低下0.05/秒・援助感度0.01・上限削り0.8・短期ブースト0.4・発育不全閾値0.7

        [Test]
        public void AccumulateDependency_MoreAidRaisesDependency()
        {
            var s0 = PatronageState.None;
            var s1 = PatronageTrapRules.AccumulateDependency(s0, 50f, P);
            var s2 = PatronageTrapRules.AccumulateDependency(s1, 50f, P);
            // 援助を積むほど依存度が上がる（飽和して1へ漸近）
            Assert.Greater(s1.dependencyRatio, 0f);
            Assert.Greater(s2.dependencyRatio, s1.dependencyRatio);
            Assert.AreEqual(100f, s2.aidAccumulator, 1e-4f);
            // 1 - exp(-0.01*100) = 1 - exp(-1) ≈ 0.6321
            Assert.AreEqual(1f - Mathf.Exp(-1f), s2.dependencyRatio, 1e-4f);
        }

        [Test]
        public void AccumulateDependency_NegativeAidIgnored()
        {
            var s0 = PatronageState.None;
            var s = PatronageTrapRules.AccumulateDependency(s0, -999f, P);
            // 負の援助は無視＝依存も累積も増えない（負債化しない）
            Assert.AreEqual(0f, s.dependencyRatio, 1e-6f);
            Assert.AreEqual(0f, s.aidAccumulator, 1e-6f);
        }

        [Test]
        public void OrganicGrowthCap_HigherDependencyLowersCeiling()
        {
            // 依存0で上限1.0（無制限＝従来動作）
            Assert.AreEqual(1f, PatronageTrapRules.OrganicGrowthCap(0f, P), 1e-6f);
            // 依存1で 1-capStrength = 0.2
            Assert.AreEqual(0.2f, PatronageTrapRules.OrganicGrowthCap(1f, P), 1e-6f);
            // 単調減少：依存が増えると上限は下がる
            Assert.Greater(PatronageTrapRules.OrganicGrowthCap(0.2f, P),
                           PatronageTrapRules.OrganicGrowthCap(0.8f, P));
        }

        [Test]
        public void ShortTermBoost_RisesWithDependencyButCapFalls()
        {
            // 同じ依存上昇で：短期ブーストは増え、有機成長上限は減る（罠の二面性）
            float lowDep = 0.2f, highDep = 0.8f;
            Assert.Greater(PatronageTrapRules.ShortTermBoost(highDep, P),
                           PatronageTrapRules.ShortTermBoost(lowDep, P));
            Assert.Less(PatronageTrapRules.OrganicGrowthCap(highDep, P),
                        PatronageTrapRules.OrganicGrowthCap(lowDep, P));
            // 依存0で 1.0、依存1で 1+0.4
            Assert.AreEqual(1f, PatronageTrapRules.ShortTermBoost(0f, P), 1e-6f);
            Assert.AreEqual(1.4f, PatronageTrapRules.ShortTermBoost(1f, P), 1e-6f);
        }

        [Test]
        public void DependencyDecay_RestoresAutonomyOverTime()
        {
            var s = new PatronageState(0.5f, 100f);
            var d = PatronageTrapRules.DependencyDecay(s, 2f, P);
            // 0.5 - 0.05*2 = 0.4
            Assert.AreEqual(0.4f, d.dependencyRatio, 1e-5f);
            // 累積も依存比率ぶん縮む（0.4/0.5 = 0.8 → 80）
            Assert.AreEqual(80f, d.aidAccumulator, 1e-4f);
            // 下限0でクランプ（大きな dt でも負にならない・累積もゼロへ）
            var floored = PatronageTrapRules.DependencyDecay(s, 100f, P);
            Assert.AreEqual(0f, floored.dependencyRatio, 1e-6f);
            Assert.AreEqual(0f, floored.aidAccumulator, 1e-6f);
        }

        [Test]
        public void WithdrawalShock_ScalesQuadraticallyWithDependency()
        {
            Assert.AreEqual(0f, PatronageTrapRules.WithdrawalShock(0f), 1e-6f);
            Assert.AreEqual(0.25f, PatronageTrapRules.WithdrawalShock(0.5f), 1e-6f);
            Assert.AreEqual(1f, PatronageTrapRules.WithdrawalShock(1f), 1e-6f);
            // 深い依存ほど打撃が加速度的に大きい（二乗）
            float shallow = PatronageTrapRules.WithdrawalShock(0.3f);
            float deep = PatronageTrapRules.WithdrawalShock(0.9f);
            Assert.Greater(deep, shallow * 3f);
        }

        [Test]
        public void IsStunted_TrueAtOrAboveThreshold()
        {
            Assert.IsFalse(PatronageTrapRules.IsStunted(0.69f, P));
            Assert.IsTrue(PatronageTrapRules.IsStunted(0.7f, P));
            Assert.IsTrue(PatronageTrapRules.IsStunted(0.95f, P));
        }

        [Test]
        public void Bounds_ClampedAcrossAllApis()
        {
            // 範囲外入力でもクランプ：上限/ブースト/ショックは破綻しない
            Assert.AreEqual(1f, PatronageTrapRules.OrganicGrowthCap(-5f, P), 1e-6f);
            Assert.AreEqual(0.2f, PatronageTrapRules.OrganicGrowthCap(5f, P), 1e-6f);
            Assert.AreEqual(1f, PatronageTrapRules.ShortTermBoost(-5f, P), 1e-6f);
            Assert.AreEqual(1.4f, PatronageTrapRules.ShortTermBoost(5f, P), 1e-6f);
            Assert.AreEqual(0f, PatronageTrapRules.WithdrawalShock(-5f), 1e-6f);
            Assert.AreEqual(1f, PatronageTrapRules.WithdrawalShock(5f), 1e-6f);
            // 負 dt は前進ゼロ（時間を巻き戻さない）
            var s = new PatronageState(0.5f, 100f);
            var same = PatronageTrapRules.DependencyDecay(s, -10f, P);
            Assert.AreEqual(0.5f, same.dependencyRatio, 1e-6f);
        }

        [Test]
        public void State_ConstructorClampsAndDefaultsAreSafe()
        {
            // 構造体コンストラクタも範囲外を吸収（null安全＝struct は非null・既定値安全）
            var clamped = new PatronageState(5f, -3f);
            Assert.AreEqual(1f, clamped.dependencyRatio, 1e-6f);
            Assert.AreEqual(0f, clamped.aidAccumulator, 1e-6f);
            // None は依存ゼロの初期状態（従来動作）
            Assert.AreEqual(0f, PatronageState.None.dependencyRatio, 1e-6f);
            Assert.AreEqual(0f, PatronageState.None.aidAccumulator, 1e-6f);
            // state 版オーバーロードは値版と一致
            var st = new PatronageState(0.6f, 0f);
            Assert.AreEqual(PatronageTrapRules.OrganicGrowthCap(0.6f, P),
                            PatronageTrapRules.OrganicGrowthCap(st, P), 1e-6f);
            Assert.AreEqual(PatronageTrapRules.ShortTermBoost(0.6f, P),
                            PatronageTrapRules.ShortTermBoost(st, P), 1e-6f);
            Assert.AreEqual(PatronageTrapRules.WithdrawalShock(0.6f),
                            PatronageTrapRules.WithdrawalShock(st), 1e-6f);
            Assert.AreEqual(PatronageTrapRules.IsStunted(0.6f, P),
                            PatronageTrapRules.IsStunted(st, P));
        }

        [Test]
        public void DefaultOverloads_MatchExplicitParams()
        {
            var s0 = PatronageState.None;
            Assert.AreEqual(PatronageTrapRules.AccumulateDependency(s0, 30f, P).dependencyRatio,
                            PatronageTrapRules.AccumulateDependency(s0, 30f).dependencyRatio, 1e-6f);
            Assert.AreEqual(PatronageTrapRules.OrganicGrowthCap(0.5f, P),
                            PatronageTrapRules.OrganicGrowthCap(0.5f), 1e-6f);
            Assert.AreEqual(PatronageTrapRules.ShortTermBoost(0.5f, P),
                            PatronageTrapRules.ShortTermBoost(0.5f), 1e-6f);
            Assert.AreEqual(PatronageTrapRules.IsStunted(0.8f, P),
                            PatronageTrapRules.IsStunted(0.8f));
        }
    }
}
