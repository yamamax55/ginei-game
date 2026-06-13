using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>商業誠実性の信頼基盤（#1590・反復交易が育てる信頼の蓄積・崩壊・回復・opinion修正）の純ロジック検証。</summary>
    public class CommercialIntegrityRulesTests
    {
        const float Eps = 0.0001f;

        /// <summary>約束を守った取引は信頼を積み、規模が大きいほど多く積む（守らなければ増えない）。</summary>
        [Test]
        public void TrustAccumulation_HonoredDealBuildsTrust_ScaledBySize()
        {
            // 既定 buildRate0.1・dealSize1.0・dt1.0 → +0.1
            float full = CommercialIntegrityRules.TrustAccumulation(0.3f, true, 1f, 1f);
            Assert.AreEqual(0.4f, full, Eps);
            // 規模半分は積みも半分
            float half = CommercialIntegrityRules.TrustAccumulation(0.3f, true, 0.5f, 1f);
            Assert.AreEqual(0.35f, half, Eps);
            // 守らなければ変化なし（崩壊は TrustBreach の役目）
            float unhonored = CommercialIntegrityRules.TrustAccumulation(0.3f, false, 1f, 1f);
            Assert.AreEqual(0.3f, unhonored, Eps);
        }

        /// <summary>裏切りは蓄積を一気に崩す＝積む(0.1/tick)より速い非対称。</summary>
        [Test]
        public void TrustBreach_CollapsesFasterThanBuild()
        {
            // severity1.0×breachScale0.6 → 0.8 から 0.6 削れて0.2
            float breached = CommercialIntegrityRules.TrustBreach(0.8f, 1f);
            Assert.AreEqual(0.2f, breached, Eps);
            // 一度の裏切り(-0.6)は一度の honored 取引(+0.1)の6倍の落差＝崩すは一気
            float built = CommercialIntegrityRules.TrustAccumulation(0.8f, true, 1f, 1f);
            float buildDelta = built - 0.8f;          // +0.1
            float breachDelta = 0.8f - breached;       // -0.6
            Assert.Greater(breachDelta, buildDelta * 5f);
        }

        /// <summary>opinion修正は信頼0.5を中立に高信頼で正・低信頼で負（±opinionSwing）。</summary>
        [Test]
        public void OpinionModifier_CentersAtHalf_SwingsBothWays()
        {
            Assert.AreEqual(0.4f, CommercialIntegrityRules.OpinionModifier(1f), Eps);   // 最高信頼→+swing
            Assert.AreEqual(-0.4f, CommercialIntegrityRules.OpinionModifier(0f), Eps);  // 最低信頼→-swing
            Assert.AreEqual(0f, CommercialIntegrityRules.OpinionModifier(0.5f), Eps);   // 中立
        }

        /// <summary>誠実評判は信頼と取引実績の積＝両方揃って高まる。</summary>
        [Test]
        public void IntegrityReputation_RequiresBothTrustAndDeals()
        {
            Assert.AreEqual(0.48f, CommercialIntegrityRules.IntegrityReputation(0.8f, 0.6f), Eps);
            // 実績が浅ければ高信頼でも評判は低い
            Assert.AreEqual(0.08f, CommercialIntegrityRules.IntegrityReputation(0.8f, 0.1f), Eps);
            // 実績ゼロは評判ゼロ
            Assert.AreEqual(0f, CommercialIntegrityRules.IntegrityReputation(1f, 0f), Eps);
        }

        /// <summary>裏切りの誘惑は目先利得で生まれ、蓄積信頼が大きいほど抑止される。</summary>
        [Test]
        public void DefaultTemptation_DeterredByAccumulatedTrust()
        {
            // 信頼0で誘惑は素の利得そのまま（trustDeterrence1.0・利得0.8→0.8）
            float noTrust = CommercialIntegrityRules.DefaultTemptation(0.8f, 0f);
            Assert.AreEqual(0.8f, noTrust, Eps);
            // 信頼0.5なら 0.8×(1-0.5)=0.4 へ減衰
            float someTrust = CommercialIntegrityRules.DefaultTemptation(0.8f, 0.5f);
            Assert.AreEqual(0.4f, someTrust, Eps);
            // 信頼が大きいほど裏切りにくい
            Assert.Less(someTrust, noTrust);
            // 利得が無ければ誘惑も無い
            Assert.AreEqual(0f, CommercialIntegrityRules.DefaultTemptation(0f, 0.2f), Eps);
        }

        /// <summary>反復取引は取引コストを下げる配当を生む（実績に線形比例）。</summary>
        [Test]
        public void RepeatDealingBonus_ScalesWithDealCount()
        {
            Assert.AreEqual(0.3f, CommercialIntegrityRules.RepeatDealingBonus(1f), Eps);   // 既定 repeatDiscount0.3
            Assert.AreEqual(0.15f, CommercialIntegrityRules.RepeatDealingBonus(0.5f), Eps);
            Assert.AreEqual(0f, CommercialIntegrityRules.RepeatDealingBonus(0f), Eps);
        }

        /// <summary>裏切り後の回復は遅い＝積む(0.1/tick)より遥かに遅い(0.02/tick)非対称。</summary>
        [Test]
        public void RecoveryAsymmetry_RecoversSlowlyComparedToBuild()
        {
            // 既定 recoveryRate0.02・dt1.0
            float recovered = CommercialIntegrityRules.RecoveryAsymmetry(0.2f, 1f);
            Assert.AreEqual(0.22f, recovered, Eps);
            // 同条件の honored 取引(+0.1)の方がずっと速い＝回復は徐々
            float built = CommercialIntegrityRules.TrustAccumulation(0.2f, true, 1f, 1f);
            Assert.Greater(built - 0.2f, (recovered - 0.2f) * 4f);
        }

        /// <summary>信頼が閾値以上なら信頼できる取引相手。</summary>
        [Test]
        public void IsTrustedPartner_AboveThreshold()
        {
            Assert.IsTrue(CommercialIntegrityRules.IsTrustedPartner(0.7f, 0.6f));
            Assert.IsFalse(CommercialIntegrityRules.IsTrustedPartner(0.5f, 0.6f));
            // 既定 Params 閾値0.6
            Assert.IsTrue(CommercialIntegrityRules.IsTrustedPartner(0.6f, CommercialIntegrityParams.Default));
            Assert.IsFalse(CommercialIntegrityRules.IsTrustedPartner(0.59f, CommercialIntegrityParams.Default));
        }
    }
}
