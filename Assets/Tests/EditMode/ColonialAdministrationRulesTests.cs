using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>植民地統治の純ロジックの EditMode テスト（既定Params期待値固定）。</summary>
    public class ColonialAdministrationRulesTests
    {
        const float Eps = 1e-4f;
        const float SatEps = 1e-3f; // 飽和（v/(v+1)）箇所

        [Test]
        public void GovernorAutonomy_DistanceAndDelayIncreaseAutonomy()
        {
            // v = 0.8*1 + 0.6*1 = 1.4 ; 1.4/2.4 = 0.583333
            float a = ColonialAdministrationRules.GovernorAutonomy(0.8f, 0.6f);
            Assert.AreEqual(0.583333f, a, SatEps);
            // 距離も遅延もゼロ→裁量0
            Assert.AreEqual(0f, ColonialAdministrationRules.GovernorAutonomy(0f, 0f), Eps);
        }

        [Test]
        public void CentralControl_OversightOverAutonomy()
        {
            // 0.7/(0.7+0.3) = 0.7
            Assert.AreEqual(0.7f, ColonialAdministrationRules.CentralControl(0.7f, 0.3f), Eps);
            // 裁量0→完全な中央集権
            Assert.AreEqual(1f, ColonialAdministrationRules.CentralControl(0.5f, 0f), Eps);
            // 監督も裁量もゼロ→統制0
            Assert.AreEqual(0f, ColonialAdministrationRules.CentralControl(0f, 0f), Eps);
        }

        [Test]
        public void LocalAssimilation_SaturatesWithTime()
        {
            // timeFactor = 1 - 1/(1+0.001*1000) = 1 - 1/2 = 0.5 ; *1.0*1 = 0.5
            Assert.AreEqual(0.5f, ColonialAdministrationRules.LocalAssimilation(1f, 1000f), Eps);
            // 文化政策ゼロ→同化進まず
            Assert.AreEqual(0f, ColonialAdministrationRules.LocalAssimilation(0f, 1000f), Eps);
        }

        [Test]
        public void ColonialResentment_TaxForceAndLowAutonomy()
        {
            // 0.8*0.5*(1-0.25)*1 = 0.3
            Assert.AreEqual(0.3f, ColonialAdministrationRules.ColonialResentment(0.8f, 0.5f, 0.25f), Eps);
            // 完全自治→反発0
            Assert.AreEqual(0f, ColonialAdministrationRules.ColonialResentment(1f, 1f, 1f), Eps);
        }

        [Test]
        public void ColonialRevenue_EconomyTaxAndResentment()
        {
            // 1000*0.2*(1-0.4) = 120
            Assert.AreEqual(120f, ColonialAdministrationRules.ColonialRevenue(1000f, 0.2f, 0.4f), Eps);
            // 反発1→徴税不能
            Assert.AreEqual(0f, ColonialAdministrationRules.ColonialRevenue(1000f, 0.2f, 1f), Eps);
        }

        [Test]
        public void EliteCooption_SaturatesWithEliteAndOpportunity()
        {
            // v=3*1=3 ; 3/4 = 0.75
            Assert.AreEqual(0.75f, ColonialAdministrationRules.EliteCooption(3f, 1f), SatEps);
            // 門戸ゼロ→取り込み0
            Assert.AreEqual(0f, ColonialAdministrationRules.EliteCooption(3f, 0f), Eps);
        }

        [Test]
        public void AdministrativeEffectiveness_ControlPlusCooptionMinusResentment()
        {
            // 0.7 + 0.75*0.5 - 0.3*1 = 0.775
            Assert.AreEqual(0.775f, ColonialAdministrationRules.AdministrativeEffectiveness(0.7f, 0.75f, 0.3f), Eps);
            // 反発が圧倒→負（空洞化）
            float low = ColonialAdministrationRules.AdministrativeEffectiveness(0f, 0f, 1f);
            Assert.AreEqual(-1f, low, Eps);
        }

        [Test]
        public void IsColonyStable_ThresholdAndResentmentCap()
        {
            // 既定閾値0.3：実効0.775≥0.3 かつ 反発0.3<0.7 →安定
            Assert.IsTrue(ColonialAdministrationRules.IsColonyStable(0.775f, 0.3f));
            // 反発0.8≥0.7 →不安定
            Assert.IsFalse(ColonialAdministrationRules.IsColonyStable(0.775f, 0.8f));
            // 明示閾値0.5：実効0.4<0.5 →不安定
            Assert.IsFalse(ColonialAdministrationRules.IsColonyStable(0.4f, 0.2f, 0.5f));
        }

        [Test]
        public void ClampsInputs_NoOutOfRangeOutput()
        {
            // 範囲外入力でも 0..1 / -1..1 に収まる
            float a = ColonialAdministrationRules.GovernorAutonomy(5f, 5f);
            Assert.AreEqual(0.666667f, a, SatEps); // v=2 → 2/3
            float res = ColonialAdministrationRules.ColonialResentment(2f, 2f, -1f);
            Assert.AreEqual(1f, res, Eps); // clamp後 1*1*1*1 = 1
            float rev = ColonialAdministrationRules.ColonialRevenue(-100f, 2f, -1f);
            Assert.AreEqual(0f, rev, Eps); // 経済<0→0
            float eff = ColonialAdministrationRules.AdministrativeEffectiveness(2f, 2f, -2f);
            Assert.AreEqual(1f, eff, Eps); // 上方クランプ
        }

        [Test]
        public void Narrative_RemoteHarshColonyDestabilizesElseLoyalAssimilates()
        {
            // 物語：遠方の重税・強制同化・無自治の植民地は反発が燃え行政が空洞化し不安定。
            float autonomyDistant = ColonialAdministrationRules.GovernorAutonomy(0.95f, 0.9f);
            float controlDistant = ColonialAdministrationRules.CentralControl(0.2f, autonomyDistant);
            float resentHarsh = ColonialAdministrationRules.ColonialResentment(0.95f, 0.95f, 0.05f);
            float cooptNone = ColonialAdministrationRules.EliteCooption(0f, 0f);
            float effHarsh = ColonialAdministrationRules.AdministrativeEffectiveness(controlDistant, cooptNone, resentHarsh);
            Assert.IsFalse(ColonialAdministrationRules.IsColonyStable(effHarsh, resentHarsh),
                "遠方・重税・強制同化・無自治の植民地は不安定なはず");

            // 対比：近場で監督厚く、軽税・自治尊重・現地名士を登用すれば反発が低く行政が回り安定。
            float autonomyNear = ColonialAdministrationRules.GovernorAutonomy(0.1f, 0.1f);
            float controlNear = ColonialAdministrationRules.CentralControl(0.8f, autonomyNear);
            float resentMild = ColonialAdministrationRules.ColonialResentment(0.2f, 0.1f, 0.8f);
            float cooptMany = ColonialAdministrationRules.EliteCooption(5f, 0.9f);
            float effGood = ColonialAdministrationRules.AdministrativeEffectiveness(controlNear, cooptMany, resentMild);
            Assert.IsTrue(ColonialAdministrationRules.IsColonyStable(effGood, resentMild),
                "近場・軽税・自治尊重・現地登用の植民地は安定なはず");

            // 厳しい統治のほうが反発は高く、行政実効性は低い。
            Assert.Greater(resentHarsh, resentMild);
            Assert.Greater(effGood, effHarsh);
        }
    }
}
