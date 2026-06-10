using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 国家状態の合成（社会・政治シミュ層の統合）を固定する：収奪的統治は腐敗→合意の崩壊→末人で
    /// 崩れ、包摂的統治＋有徳は安定する。安定度の合成・崩壊判定。
    /// </summary>
    public class FactionStateRulesTests
    {
        [Test]
        public void ExtractiveRule_CollapsesOverTime()
        {
            // 収奪的（包摂度0＝抑圧最大）・無徳。
            var s = new FactionState(Faction.帝国, inclusiveness: 0f);
            s.regime.virtue = 0f;

            for (int i = 0; i < 10; i++) FactionStateRules.Tick(s, 1f);

            Assert.IsTrue(FactionStateRules.IsCollapsing(s));        // 天命喪失/統治不能/末人のいずれか
            Assert.Less(FactionStateRules.Stability(s), 0.5f);
        }

        [Test]
        public void InclusiveVirtuousRule_StaysStable()
        {
            // 包摂的（抑圧0）・有徳。
            var s = new FactionState(Faction.帝国, inclusiveness: 1f);
            s.regime.virtue = 0.9f;

            for (int i = 0; i < 10; i++) FactionStateRules.Tick(s, 1f);

            Assert.IsFalse(FactionStateRules.IsCollapsing(s));
            Assert.IsFalse(s.community.dissent);
            Assert.IsFalse(ConsentRules.IsUngovernable(s.polity));
            Assert.Greater(FactionStateRules.Stability(s), 0.6f);
        }

        [Test]
        public void Stability_IsAverageOfFourPillars()
        {
            var s = new FactionState(Faction.帝国);
            s.regime.legitimacy = 0.8f;
            s.polity.cooperation = 0.6f;
            s.organization.cohesion = 1.0f;
            s.community.hope = 0.4f;
            Assert.AreEqual((0.8f + 0.6f + 1.0f + 0.4f) / 4f, FactionStateRules.Stability(s), 1e-4f);
        }

        [Test]
        public void OrganizationFragmentation_CountsAsCollapsing()
        {
            var s = new FactionState(Faction.帝国, inclusiveness: 1f);
            s.regime.virtue = 0.9f;
            // 健全な国家でも、継承で組織が崩れれば崩壊扱い
            Assert.IsFalse(FactionStateRules.IsCollapsing(s));
            SuccessionRules.ResolveSuccession(s.organization, successorLegitimacy: 0.2f, successorCharisma: 0.2f);
            Assert.IsTrue(s.organization.fragmented);
            Assert.IsTrue(FactionStateRules.IsCollapsing(s));
        }

        // ---- 敵対的エッジケース（追記） ----

        /// <summary>null 安全：Tick は no-op、Stability=0、IsCollapsing=true（崩壊扱い）。</summary>
        [Test]
        public void NullState_IsHandledSafely()
        {
            Assert.DoesNotThrow(() => FactionStateRules.Tick(null, 1f));
            Assert.DoesNotThrow(() => FactionStateRules.Tick(null, 1f, FactionStateParams.Default));
            Assert.AreEqual(0f, FactionStateRules.Stability(null), 1e-5f);
            Assert.IsTrue(FactionStateRules.IsCollapsing(null));   // null=崩壊扱い（安全側）
        }

        /// <summary>dt<=0 は no-op（時間が進まないので状態は不変）。負の dt も同様。</summary>
        [Test]
        public void NonPositiveDt_DoesNotMutateState()
        {
            var s = new FactionState(Faction.帝国);
            float legit0 = s.regime.legitimacy;
            float coop0 = s.polity.cooperation;
            float hope0 = s.community.hope;
            float corr0 = s.regime.corruption;

            FactionStateRules.Tick(s, 0f);
            FactionStateRules.Tick(s, -5f);

            Assert.AreEqual(legit0, s.regime.legitimacy, 1e-5f);
            Assert.AreEqual(coop0, s.polity.cooperation, 1e-5f);
            Assert.AreEqual(hope0, s.community.hope, 1e-5f);
            Assert.AreEqual(corr0, s.regime.corruption, 1e-5f);
        }

        /// <summary>
        /// 既定状態からの 1 tick の厳密値を手計算で固定（実装が公式から逸れたら落ちる）。
        /// virtue=0.5: rise=0.1*(1-0.5)*1=0.05 → legitimacy 1→0.95, corruption 0→0.05。
        /// oppression=1-0.5=0.5。cooperation: delta=(0.1*0.95 - 0.2*0.5)*1=-0.005 → 0.995。
        /// hope target=0.5*0.995+0.5*0.95=0.97250 → hope=1+(0.97250-1)*0.3*1=0.991750。
        /// </summary>
        [Test]
        public void SingleTick_FromDefault_HasExactValues()
        {
            var s = new FactionState(Faction.帝国); // inclusiveness=0.5, virtue=0.5
            FactionStateRules.Tick(s, 1f);

            Assert.AreEqual(0.95f, s.regime.legitimacy, 1e-5f);
            Assert.AreEqual(0.05f, s.regime.corruption, 1e-5f);
            Assert.AreEqual(0.5f, s.polity.oppression, 1e-5f);
            Assert.AreEqual(0.95f, s.polity.legitimacy, 1e-5f);   // polity.legitimacy は regime に追従
            Assert.AreEqual(0.995f, s.polity.cooperation, 1e-5f);
            Assert.AreEqual(0.991750f, s.community.hope, 1e-5f);
            Assert.IsFalse(s.community.dissent);                   // hope>>0.25
        }

        /// <summary>
        /// inclusiveness を範囲外(>1)へ直接代入しても oppression は Clamp01 で 0 に張り付く
        /// （1 - inclusiveness の負値が漏れない＝クランプの下端を突く）。
        /// </summary>
        [Test]
        public void InclusivenessAboveOne_ClampsOppressionToZero()
        {
            var s = new FactionState(Faction.帝国);
            s.inclusiveness = 2f; // ctor を経ずに範囲外を注入
            FactionStateRules.Tick(s, 1f);
            Assert.AreEqual(0f, s.polity.oppression, 1e-5f);
        }

        /// <summary>
        /// inclusiveness を負へ直接代入すると oppression は Clamp01 の上端 1 に張り付く（最大抑圧）。
        /// </summary>
        [Test]
        public void NegativeInclusiveness_ClampsOppressionToOne()
        {
            var s = new FactionState(Faction.帝国);
            s.inclusiveness = -3f;
            FactionStateRules.Tick(s, 1f);
            Assert.AreEqual(1f, s.polity.oppression, 1e-5f);
        }

        /// <summary>
        /// dissent（末人）が立てば、他の柱が健全でも IsCollapsing=true（OR 分岐の独立性）。
        /// hope を閾値未満・repression 不足にして UpdateDissent を直接発火させる。
        /// </summary>
        [Test]
        public void DissentAlone_CountsAsCollapsing()
        {
            var s = new FactionState(Faction.帝国); // 正統性/合意/結束は満点
            s.community.hope = 0.1f;       // < 0.25
            s.community.repression = 0f;   // < 0.5 ＝鎮圧されない
            HopeRules.UpdateDissent(s.community);
            Assert.IsTrue(s.community.dissent);
            Assert.IsTrue(FactionStateRules.IsCollapsing(s));
        }

        /// <summary>
        /// 希望が尽きても抑圧が十分(>=0.5)なら dissent は立たず、他も健全なら IsCollapsing=false
        /// （秩序ルートの鎮圧分岐＝閾値の上端を突く）。
        /// </summary>
        [Test]
        public void HopelessButRepressed_DoesNotCollapseByDissent()
        {
            var s = new FactionState(Faction.帝国);
            s.community.hope = 0f;
            s.community.repression = 0.5f; // 鎮圧閾値ちょうど
            HopeRules.UpdateDissent(s.community);
            Assert.IsFalse(s.community.dissent);
            Assert.IsFalse(FactionStateRules.IsCollapsing(s));
        }

        /// <summary>
        /// Stability は4柱の単純平均：両端（全0/全1）で 0 と 1 になる（合計保存・境界）。
        /// </summary>
        [Test]
        public void Stability_Extremes_AreZeroAndOne()
        {
            var lo = new FactionState(Faction.帝国);
            lo.regime.legitimacy = 0f; lo.polity.cooperation = 0f;
            lo.organization.cohesion = 0f; lo.community.hope = 0f;
            Assert.AreEqual(0f, FactionStateRules.Stability(lo), 1e-5f);

            var hi = new FactionState(Faction.帝国);
            hi.regime.legitimacy = 1f; hi.polity.cooperation = 1f;
            hi.organization.cohesion = 1f; hi.community.hope = 1f;
            Assert.AreEqual(1f, FactionStateRules.Stability(hi), 1e-5f);
        }

        /// <summary>
        /// virtue=1（完全有徳）なら腐敗 rise=0.1*(1-1)*dt=0＝正統性も腐敗も一切動かない（単調性の退化点）。
        /// 抑圧も0（inclusiveness=1）＝合意は正統性で純増し、希望も上がる＝崩壊しない。
        /// </summary>
        [Test]
        public void PerfectVirtueAndInclusive_LegitimacyNeverDecays()
        {
            var s = new FactionState(Faction.帝国, inclusiveness: 1f);
            s.regime.virtue = 1f;
            for (int i = 0; i < 50; i++) FactionStateRules.Tick(s, 1f);

            Assert.AreEqual(1f, s.regime.legitimacy, 1e-5f);   // 一切減らない
            Assert.AreEqual(0f, s.regime.corruption, 1e-5f);
            Assert.IsFalse(FactionStateRules.IsCollapsing(s));
        }

        /// <summary>
        /// 多数 tick での単調性：収奪的・無徳では legitimacy が tick ごとに（クランプ前まで）単調非増加。
        /// 0 でクランプされるまで前 tick を下回り続けることを確認（途中で上振れしない）。
        /// </summary>
        [Test]
        public void ExtractiveRule_LegitimacyMonotonicallyNonIncreasing()
        {
            var s = new FactionState(Faction.帝国, inclusiveness: 0f);
            s.regime.virtue = 0f; // rise=0.1/tick

            float prev = s.regime.legitimacy;
            for (int i = 0; i < 20; i++)
            {
                FactionStateRules.Tick(s, 1f);
                Assert.LessOrEqual(s.regime.legitimacy, prev + 1e-6f);
                prev = s.regime.legitimacy;
            }
            Assert.AreEqual(0f, s.regime.legitimacy, 1e-5f); // 最終的に底打ち
        }
    }
}
