using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 継承法（PDX-1 #646）を固定する：相続割合（長子＝総取り/分割＝均等/指名・選挙＝指名者総取り）と
    /// 継承戦争リスク（相続人1人以下は0・分割が最大・正統性で軽減）、および境界・クランプの決定論。
    /// </summary>
    public class SuccessionLawRulesTests
    {
        private static readonly SuccessionLawRules.SuccessionParams P =
            SuccessionLawRules.SuccessionParams.Default; // base0.1 / partition0.4 / disputed0.2 / relief0.5 / perHeir0.05

        // --- HeirShare ---

        [Test]
        public void HeirShare_Primogeniture_FirstHeirTakesAll()
        {
            Assert.AreEqual(1f, SuccessionLawRules.HeirShare(SuccessionLaw.長子, 0, 3), 1e-5f);
            Assert.AreEqual(0f, SuccessionLawRules.HeirShare(SuccessionLaw.長子, 1, 3), 1e-5f);
            Assert.AreEqual(0f, SuccessionLawRules.HeirShare(SuccessionLaw.長子, 2, 3), 1e-5f);
        }

        [Test]
        public void HeirShare_Partition_SplitsEvenly()
        {
            Assert.AreEqual(0.25f, SuccessionLawRules.HeirShare(SuccessionLaw.分割, 0, 4), 1e-5f);
            Assert.AreEqual(0.25f, SuccessionLawRules.HeirShare(SuccessionLaw.分割, 3, 4), 1e-5f);
        }

        [Test]
        public void HeirShare_AppointmentAndElection_DesignatedTakesAll()
        {
            Assert.AreEqual(1f, SuccessionLawRules.HeirShare(SuccessionLaw.指名, 0, 5), 1e-5f);
            Assert.AreEqual(0f, SuccessionLawRules.HeirShare(SuccessionLaw.指名, 1, 5), 1e-5f);
            Assert.AreEqual(1f, SuccessionLawRules.HeirShare(SuccessionLaw.選挙, 0, 2), 1e-5f);
        }

        [Test]
        public void HeirShare_ZeroOrNegativeCount_ReturnsZero()
        {
            Assert.AreEqual(0f, SuccessionLawRules.HeirShare(SuccessionLaw.長子, 0, 0), 1e-5f);
            Assert.AreEqual(0f, SuccessionLawRules.HeirShare(SuccessionLaw.分割, 0, -1), 1e-5f);
        }

        [Test]
        public void HeirShare_IndexClampedIntoRange()
        {
            // index が範囲外でも先頭/末尾へクランプ（長子で過大 index は0扱い、分割は均等のまま）
            Assert.AreEqual(0f, SuccessionLawRules.HeirShare(SuccessionLaw.長子, 99, 3), 1e-5f);
            Assert.AreEqual(1f, SuccessionLawRules.HeirShare(SuccessionLaw.長子, -5, 3), 1e-5f); // -5→0＝第一相続人
            Assert.AreEqual(0.5f, SuccessionLawRules.HeirShare(SuccessionLaw.分割, 99, 2), 1e-5f);
        }

        // --- SuccessionCrisisRisk ---

        [Test]
        public void Risk_SingleOrNoHeir_IsZero()
        {
            Assert.AreEqual(0f, SuccessionLawRules.SuccessionCrisisRisk(SuccessionLaw.分割, 1, 0f, P), 1e-5f);
            Assert.AreEqual(0f, SuccessionLawRules.SuccessionCrisisRisk(SuccessionLaw.選挙, 0, 0f, P), 1e-5f);
        }

        [Test]
        public void Risk_Primogeniture_LowestForGivenHeirs()
        {
            // 2相続人・正統性0：base0.1 + 1*0.05 = 0.15（追加ペナルティ無し）
            float risk = SuccessionLawRules.SuccessionCrisisRisk(SuccessionLaw.長子, 2, 0f, P);
            Assert.AreEqual(0.15f, risk, 1e-5f);
        }

        [Test]
        public void Risk_Partition_HighestForGivenHeirs()
        {
            // 2相続人・正統性0：0.1 + 0.05 + partition0.4 = 0.55
            float risk = SuccessionLawRules.SuccessionCrisisRisk(SuccessionLaw.分割, 2, 0f, P);
            Assert.AreEqual(0.55f, risk, 1e-5f);
            // 分割 > 指名 > 長子 の序列
            float appoint = SuccessionLawRules.SuccessionCrisisRisk(SuccessionLaw.指名, 2, 0f, P);
            float primo = SuccessionLawRules.SuccessionCrisisRisk(SuccessionLaw.長子, 2, 0f, P);
            Assert.Greater(risk, appoint);
            Assert.Greater(appoint, primo);
        }

        [Test]
        public void Risk_LegitimacyReducesRisk()
        {
            // 指名・3相続人・正統性0：0.1 + 2*0.05 + disputed0.2 = 0.4
            float low = SuccessionLawRules.SuccessionCrisisRisk(SuccessionLaw.指名, 3, 0f, P);
            // 正統性1.0：- relief0.5 → 0.4 - 0.5 = -0.1 → クランプで0
            float high = SuccessionLawRules.SuccessionCrisisRisk(SuccessionLaw.指名, 3, 1f, P);
            Assert.AreEqual(0.4f, low, 1e-5f);
            Assert.AreEqual(0f, high, 1e-5f); // クランプ下限
        }

        [Test]
        public void Risk_ClampedToOne_ManyHeirsPartition()
        {
            // 大人数の分割は1.0でクランプ（オーバーフローしない）
            float risk = SuccessionLawRules.SuccessionCrisisRisk(SuccessionLaw.分割, 50, 0f, P);
            Assert.AreEqual(1f, risk, 1e-5f);
        }

        [Test]
        public void Risk_LegitimacyArgClamped()
        {
            // 正統性に範囲外を渡しても relief は0..1で効く（1.5→1.0扱い）
            float a = SuccessionLawRules.SuccessionCrisisRisk(SuccessionLaw.長子, 2, 1f, P);
            float b = SuccessionLawRules.SuccessionCrisisRisk(SuccessionLaw.長子, 2, 1.5f, P);
            Assert.AreEqual(a, b, 1e-5f);
        }

        [Test]
        public void Risk_DefaultOverload_MatchesExplicitParams()
        {
            float withDefault = SuccessionLawRules.SuccessionCrisisRisk(SuccessionLaw.分割, 3, 0.2f);
            float withParams = SuccessionLawRules.SuccessionCrisisRisk(SuccessionLaw.分割, 3, 0.2f, P);
            Assert.AreEqual(withParams, withDefault, 1e-5f);
        }
    }
}
