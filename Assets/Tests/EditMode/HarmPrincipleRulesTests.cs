using NUnit.Framework;

namespace Ginei.Tests
{
    /// <summary>危害原理（MILL-3 #1480・ミル『自由論』）の純ロジックの EditMode テスト。</summary>
    public class HarmPrincipleRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>規制の正当性＝他者への危害が大きいほど正当化され、危害がなければ低い。</summary>
        [Test]
        public void RegulationLegitimacy_RisesWithHarm()
        {
            float none = HarmPrincipleRules.RegulationLegitimacy(0f, 0.5f);
            float much = HarmPrincipleRules.RegulationLegitimacy(1f, 0.5f);
            Assert.AreEqual(0f, none, Eps); // 危害ゼロは正当性ゼロ
            Assert.Greater(much, none);
            // 危害1.0・範囲0.5・重み1.0＝justified=1.0 → 1.0*(1-0.5)+1.0*0.5*1.0 = 1.0（最大危害は規制を完全正当化）
            Assert.AreEqual(1.0f, much, Eps);
        }

        /// <summary>パターナリズム＝本人のための干渉は正当性を欠く（既定罰率0.8）。</summary>
        [Test]
        public void PaternalismPenalty_ScalesBySelfRegarding()
        {
            Assert.AreEqual(0f, HarmPrincipleRules.PaternalismPenalty(0f), Eps);
            Assert.AreEqual(0.8f, HarmPrincipleRules.PaternalismPenalty(1f), Eps);
            Assert.AreEqual(0.4f, HarmPrincipleRules.PaternalismPenalty(0.5f), Eps);
        }

        /// <summary>道徳的行き過ぎ＝道徳を理由にした規制は危害原理を超える（既定係数0.9）。</summary>
        [Test]
        public void MoralisticOverreach_ScalesByMorality()
        {
            Assert.AreEqual(0f, HarmPrincipleRules.MoralisticOverreach(0f), Eps);
            Assert.AreEqual(0.9f, HarmPrincipleRules.MoralisticOverreach(1f), Eps);
        }

        /// <summary>過剰抑圧のコスト＝危害を超えた抑圧が非線形（二乗）に増え、危害が範囲に追いつけば0。</summary>
        [Test]
        public void OverSuppressionCost_AcceleratesWithExcess()
        {
            // 範囲0.8・危害0.2＝超過0.6→0.6^2=0.36
            Assert.AreEqual(0.36f, HarmPrincipleRules.OverSuppressionCost(0.8f, 0.2f), Eps);
            // 危害が範囲に届けば過剰でない＝コスト0
            Assert.AreEqual(0f, HarmPrincipleRules.OverSuppressionCost(0.5f, 0.5f), Eps);
            Assert.AreEqual(0f, HarmPrincipleRules.OverSuppressionCost(0.3f, 0.9f), Eps);
            // 超過が大きいほど加速度的（0.6超過 0.36 > 0.3超過 0.09 の倍以上）
            float small = HarmPrincipleRules.OverSuppressionCost(0.4f, 0.1f); // 0.3^2=0.09
            Assert.Greater(0.36f, small * 2f);
        }

        /// <summary>正当性閾値＝危害が閾値（既定0.3）以上の規制のみ正当。</summary>
        [Test]
        public void LegitimacyThreshold_GatesOnHarm()
        {
            Assert.IsTrue(HarmPrincipleRules.LegitimacyThreshold(0.3f));
            Assert.IsTrue(HarmPrincipleRules.LegitimacyThreshold(0.5f));
            Assert.IsFalse(HarmPrincipleRules.LegitimacyThreshold(0.2f));
            Assert.IsFalse(HarmPrincipleRules.LegitimacyThreshold(0f));
        }

        /// <summary>自由の領域＝自己関与度がそのまま守られるべき自由（聖域）になる。</summary>
        [Test]
        public void LibertyZone_MirrorsSelfRegarding()
        {
            Assert.AreEqual(0f, HarmPrincipleRules.LibertyZone(0f), Eps);
            Assert.AreEqual(0.7f, HarmPrincipleRules.LibertyZone(0.7f), Eps);
            Assert.AreEqual(1f, HarmPrincipleRules.LibertyZone(1.5f), Eps); // クランプ
        }

        /// <summary>危害の勾配＝直接危害は満額、間接危害は重み（既定0.4）で割り引かれる。</summary>
        [Test]
        public void HarmGradient_WeightsDirectHeavier()
        {
            // 直接0.5・間接0.5＝0.5 + 0.5*0.4 = 0.7
            Assert.AreEqual(0.7f, HarmPrincipleRules.HarmGradient(0.5f, 0.5f), Eps);
            // 同量でも直接のみの方が重い
            float directOnly = HarmPrincipleRules.HarmGradient(0.5f, 0f);
            float indirectOnly = HarmPrincipleRules.HarmGradient(0f, 0.5f);
            Assert.Greater(directOnly, indirectOnly);
        }

        /// <summary>過剰介入国家の判定＝道徳的行き過ぎ＋パターナリズムの和が閾値（既定0.6）超で true。</summary>
        [Test]
        public void IsOverreachingState_FlagsExcessiveIntervention()
        {
            // 0.4 + 0.4 = 0.8 > 0.6
            Assert.IsTrue(HarmPrincipleRules.IsOverreachingState(0.4f, 0.4f));
            // 0.2 + 0.2 = 0.4 <= 0.6
            Assert.IsFalse(HarmPrincipleRules.IsOverreachingState(0.2f, 0.2f));
            // 完全に自由放任の国家は介入過剰でない
            Assert.IsFalse(HarmPrincipleRules.IsOverreachingState(0f, 0f));
        }
    }
}
