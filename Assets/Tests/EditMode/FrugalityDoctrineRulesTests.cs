using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>節用（倹約）ドクトリンの財政効率と貴族不満のトレードオフ（MOZI-5 #1567）のテスト。</summary>
    public class FrugalityDoctrineRulesTests
    {
        const float Eps = 0.0001f;

        /// <summary>財政効率＝倹約×無駄削減で 1.0 を超えて伸び、倹約0なら満額1.0のまま。</summary>
        [Test]
        public void FiscalEfficiency_RisesWithFrugalityAndWasteCut()
        {
            // 既定 efficiencyScale=0.3。倹約1.0×削減1.0 → 1+1*1*0.3 = 1.3
            Assert.AreEqual(1.3f, FrugalityDoctrineRules.FiscalEfficiency(1f, 1f), Eps);
            // 倹約0.5×削減0.5 → 1+0.25*0.3 = 1.075
            Assert.AreEqual(1.075f, FrugalityDoctrineRules.FiscalEfficiency(0.5f, 0.5f), Eps);
            // 倹約0 → 浪費を省かないので満額1.0
            Assert.AreEqual(1.0f, FrugalityDoctrineRules.FiscalEfficiency(0f, 1f), Eps);
        }

        /// <summary>産出＝財政効率の余剰×再投資で増え、再投資0なら浮いても産出は伸びない。</summary>
        [Test]
        public void OutputGain_RisesWithReinvestmentOfSurplus()
        {
            // 効率1.3（余剰0.3）×再投資1.0、outputScale=0.2 → 1+0.3*1*0.2 = 1.06
            Assert.AreEqual(1.06f, FrugalityDoctrineRules.OutputGain(1.3f, 1f), Eps);
            // 再投資0 → 浮いても産出は伸びない＝1.0
            Assert.AreEqual(1.0f, FrugalityDoctrineRules.OutputGain(1.3f, 0f), Eps);
            // 余剰なし（効率1.0以下） → 1.0
            Assert.AreEqual(1.0f, FrugalityDoctrineRules.OutputGain(1.0f, 1f), Eps);
        }

        /// <summary>貴族合意＝倹約×貴族の力で下がり、貴族が居なければ不満0。</summary>
        [Test]
        public void NobleConsent_FallsWithFrugalityAndAristocraticPower()
        {
            // 既定 nobleConsentScale=0.4。倹約1.0×貴族1.0 → 0.4
            Assert.AreEqual(0.4f, FrugalityDoctrineRules.NobleConsent(1f, 1f), Eps);
            // 倹約0.5×貴族0.5 → 0.25*0.4 = 0.1
            Assert.AreEqual(0.1f, FrugalityDoctrineRules.NobleConsent(0.5f, 0.5f), Eps);
            // 貴族の力0 → 奪うものが無く不満0
            Assert.AreEqual(0f, FrugalityDoctrineRules.NobleConsent(1f, 0f), Eps);
        }

        /// <summary>民の益＝倹約に比例して増える。</summary>
        [Test]
        public void CommonerBenefit_ScalesWithFrugality()
        {
            // 既定 commonerBenefitScale=0.3。倹約1.0 → 0.3
            Assert.AreEqual(0.3f, FrugalityDoctrineRules.CommonerBenefit(1f), Eps);
            Assert.AreEqual(0.15f, FrugalityDoctrineRules.CommonerBenefit(0.5f), Eps);
            Assert.AreEqual(0f, FrugalityDoctrineRules.CommonerBenefit(0f), Eps);
        }

        /// <summary>倹約疲れ＝倹約×継続期間で蓄積し、期間0なら疲れ0。</summary>
        [Test]
        public void AusterityFatigue_RisesWithDuration()
        {
            // 既定 fatigueScale=0.5。倹約1.0×期間1.0 → 0.5
            Assert.AreEqual(0.5f, FrugalityDoctrineRules.AusterityFatigue(1f, 1f), Eps);
            // 期間0 → 疲れ0
            Assert.AreEqual(0f, FrugalityDoctrineRules.AusterityFatigue(1f, 0f), Eps);
        }

        /// <summary>総合効果＝民の益−貴族の不満−倹約疲れのトレードオフが一式に出る。</summary>
        [Test]
        public void NetGovernanceEffect_TradesBenefitAgainstNobleAndFatigue()
        {
            // 民益0.3 − 貴族不満0.4 − 疲れ0.1 = -0.2（貴族の強い国では倹約が逆効果になりうる）
            Assert.AreEqual(-0.2f, FrugalityDoctrineRules.NetGovernanceEffect(0.3f, 0.4f, 0.1f), Eps);
            // 貴族不満も疲れも0なら民益が丸ごとプラス
            Assert.AreEqual(0.3f, FrugalityDoctrineRules.NetGovernanceEffect(0.3f, 0f, 0f), Eps);
        }

        /// <summary>奢侈禁止令の貫徹＝貴族の抵抗が強いほど削られ、抵抗0なら倹約意志のまま貫ける。</summary>
        [Test]
        public void SumptuaryEnforcement_WeakenedByEliteResistance()
        {
            // 倹約1.0×(1-抵抗0.5) = 0.5
            Assert.AreEqual(0.5f, FrugalityDoctrineRules.SumptuaryEnforcement(1f, 0.5f), Eps);
            // 抵抗0 → 倹約意志のまま貫ける
            Assert.AreEqual(0.8f, FrugalityDoctrineRules.SumptuaryEnforcement(0.8f, 0f), Eps);
            // 抵抗1.0 → 貫けない
            Assert.AreEqual(0f, FrugalityDoctrineRules.SumptuaryEnforcement(1f, 1f), Eps);
        }

        /// <summary>過度な倹約判定＝既定閾値0.8を超えると窮屈すぎ。</summary>
        [Test]
        public void IsExcessiveAusterity_AboveThreshold()
        {
            // 既定閾値0.8
            Assert.IsTrue(FrugalityDoctrineRules.IsExcessiveAusterity(0.9f));
            Assert.IsFalse(FrugalityDoctrineRules.IsExcessiveAusterity(0.7f));
            Assert.IsFalse(FrugalityDoctrineRules.IsExcessiveAusterity(0.8f)); // 境界は超過でない
        }
    }
}
