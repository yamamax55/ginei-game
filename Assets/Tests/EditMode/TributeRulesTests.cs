using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    public class TributeRulesTests
    {
        const float Eps = 1e-4f;

        [Test]
        public void TributeAmount_DiscountsByBargaining()
        {
            // 1000 * 0.5 * 0.6 / (1 + 0.25) = 300 / 1.25 = 240
            float amount = TributeRules.TributeAmount(1000f, 0.5f, 0.25f);
            Assert.AreEqual(240f, amount, Eps);
        }

        [Test]
        public void TributeAmount_HigherBargainingPaysLess()
        {
            float weak = TributeRules.TributeAmount(1000f, 0.5f, 0f);
            float strong = TributeRules.TributeAmount(1000f, 0.5f, 1f);
            Assert.Greater(weak, strong, "交渉力が高いほど貢納額は下がる");
        }

        [Test]
        public void ProtectionValue_RisesWithThreat()
        {
            // 0.8 * 0.5 * 0.7 = 0.28
            float v = TributeRules.ProtectionValue(0.8f, 0.5f);
            Assert.AreEqual(0.28f, v, Eps);
            // 脅威ゼロなら庇護の価値も無い
            Assert.AreEqual(0f, TributeRules.ProtectionValue(0.8f, 0f), Eps);
        }

        [Test]
        public void TributeBurden_ZeroWealthIsSafe()
        {
            // 240 / 1000 = 0.24
            Assert.AreEqual(0.24f, TributeRules.TributeBurden(240f, 1000f), Eps);
            // 富ゼロなら取るものが無く負担0
            Assert.AreEqual(0f, TributeRules.TributeBurden(240f, 0f), Eps);
        }

        [Test]
        public void VassalResentment_RisesWhenBurdenExceedsProtection()
        {
            // 見返りに見合う＝反発0（0.24*0.8 - 0.28 = -0.088 → clamp 0）
            Assert.AreEqual(0f, TributeRules.VassalResentment(0.24f, 0.28f), Eps);
            // 負担が重く庇護が薄い＝反発（0.5*0.8 - 0.1 = 0.3）
            Assert.AreEqual(0.3f, TributeRules.VassalResentment(0.5f, 0.1f), Eps);
        }

        [Test]
        public void OverlordPrestige_ScalesWithCountAndAmount()
        {
            // 4 * 240 * 0.5 = 480
            Assert.AreEqual(480f, TributeRules.OverlordPrestige(4, 240f), Eps);
            // 朝貢国ゼロなら威信なし
            Assert.AreEqual(0f, TributeRules.OverlordPrestige(0, 240f), Eps);
        }

        [Test]
        public void DefianceRisk_NeedsResentmentStrengthAndWeakness()
        {
            // 0.3 * 0.6 * 0.5 = 0.09
            Assert.AreEqual(0.09f, TributeRules.DefianceRisk(0.3f, 0.6f, 0.5f), Eps);
            // 宗主が強ければ（弱体化0）離反は起きない
            Assert.AreEqual(0f, TributeRules.DefianceRisk(0.3f, 0.6f, 0f), Eps);
        }

        [Test]
        public void TributeRelationStability_BalancesProtectionAgainstResentment()
        {
            // 0.28 - 0.3 - 0.09 = -0.11
            Assert.AreEqual(-0.11f, TributeRules.TributeRelationStability(0.28f, 0.3f, 0.09f), Eps);
            // 庇護だけで反発・離反が無ければ安定（正）
            float stable = TributeRules.TributeRelationStability(0.5f, 0f, 0f);
            Assert.AreEqual(0.5f, stable, Eps);
        }

        [Test]
        public void IsTributeSustained_DefaultThresholdIsZero()
        {
            Assert.IsTrue(TributeRules.IsTributeSustained(0.1f));
            Assert.IsTrue(TributeRules.IsTributeSustained(0f));
            Assert.IsFalse(TributeRules.IsTributeSustained(-0.05f));
        }

        [Test]
        public void OutputsClampToValidRanges()
        {
            // 極端入力でも範囲内
            float prot = TributeRules.ProtectionValue(99f, 99f);
            Assert.LessOrEqual(prot, 1f);
            Assert.GreaterOrEqual(prot, 0f);

            float resent = TributeRules.VassalResentment(99f, -99f);
            Assert.LessOrEqual(resent, 1f);
            Assert.GreaterOrEqual(resent, 0f);

            float stability = TributeRules.TributeRelationStability(-99f, 99f, 99f);
            Assert.GreaterOrEqual(stability, -1f);
            Assert.LessOrEqual(stability, 1f);
        }

        [Test]
        public void Narrative_WeakeningOverlordLosesItsTributary()
        {
            // 物語：強盛な宗主が脅威から弱小な属国を守る間は、属国は重い貢納にも甘んじ朝貢は続く。
            // やがて宗主が衰え属国が力を蓄えると、見合わぬ負担への不満が離反となり朝貢は破れる。

            float vassalWealth = 1000f;
            float tribute = TributeRules.TributeAmount(vassalWealth, 0.5f, 0.2f);
            float burden = TributeRules.TributeBurden(tribute, vassalWealth);

            // 全盛期：宗主強大・大きな外的脅威 → 厚い庇護
            float protectionPrime = TributeRules.ProtectionValue(0.95f, 0.8f);
            float resentPrime = TributeRules.VassalResentment(burden, protectionPrime);
            float defiancePrime = TributeRules.DefianceRisk(resentPrime, 0.3f, 0.1f);
            float stabilityPrime = TributeRules.TributeRelationStability(protectionPrime, resentPrime, defiancePrime);
            Assert.IsTrue(TributeRules.IsTributeSustained(stabilityPrime), "全盛期は朝貢が続く");

            // 衰退期：宗主弱体・脅威も去り庇護薄く、属国は力を蓄えた
            float protectionLate = TributeRules.ProtectionValue(0.3f, 0.2f);
            float resentLate = TributeRules.VassalResentment(burden, protectionLate);
            float defianceLate = TributeRules.DefianceRisk(resentLate, 0.9f, 0.85f);
            float stabilityLate = TributeRules.TributeRelationStability(protectionLate, resentLate, defianceLate);

            Assert.Less(stabilityLate, stabilityPrime, "衰退で関係は不安定化する");
            Assert.IsFalse(TributeRules.IsTributeSustained(stabilityLate), "衰退期に朝貢は破れる");
        }
    }
}
