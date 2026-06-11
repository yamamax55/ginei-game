using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>政治の職業化＝召命型 vs 生業型（WEBR-3 #1531・ウェーバー『職業としての政治』）の純ロジック検証。</summary>
    public class PoliticalVocationRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>志向値＝生計依存−使命感：召命寄りは負、生業寄りは正、拮抗は0近傍。</summary>
        [Test]
        public void VocationOrientationValue_使命感は負へ生計依存は正へ振れる()
        {
            // 召命型（使命感0.9・生計依存0.1）→ 0.1−0.9 = −0.8
            Assert.AreEqual(-0.8f, PoliticalVocationRules.VocationOrientationValue(0.9f, 0.1f), Eps);
            // 生業型（使命感0.1・生計依存0.9）→ 0.9−0.1 = +0.8
            Assert.AreEqual(0.8f, PoliticalVocationRules.VocationOrientationValue(0.1f, 0.9f), Eps);
            // 拮抗 → 0
            Assert.AreEqual(0f, PoliticalVocationRules.VocationOrientationValue(0.5f, 0.5f), Eps);
        }

        /// <summary>志向値を召命型/生業型/均衡に弁別（既定閾値0.25）。</summary>
        [Test]
        public void TypeOf_閾値で三類型を弁別する()
        {
            Assert.AreEqual(VocationOrientation.召命型, PoliticalVocationRules.TypeOf(-0.8f));
            Assert.AreEqual(VocationOrientation.生業型, PoliticalVocationRules.TypeOf(0.8f));
            Assert.AreEqual(VocationOrientation.均衡, PoliticalVocationRules.TypeOf(0.1f));
            // ちょうど閾値は型側に入る
            Assert.AreEqual(VocationOrientation.召命型, PoliticalVocationRules.TypeOf(-0.25f));
            Assert.AreEqual(VocationOrientation.生業型, PoliticalVocationRules.TypeOf(0.25f));
        }

        /// <summary>理想への献身＝使命感×信条の強さ×scale。召命型ほど理想に殉じうる。</summary>
        [Test]
        public void IdealismDrivenService_使命感と信条で理想献身が増す()
        {
            // 既定 idealismScale=1.0、使命感0.8×信条0.5×1.0 = 0.4
            Assert.AreEqual(0.4f, PoliticalVocationRules.IdealismDrivenService(0.8f, 0.5f), Eps);
            // 使命感ゼロ（生業型）は理想に殉じない
            Assert.AreEqual(0f, PoliticalVocationRules.IdealismDrivenService(0f, 1f), Eps);
        }

        /// <summary>出世主義ドリフト＝生計依存×dt×rate。政治によって生きる者ほど保身・栄達へ傾く。</summary>
        [Test]
        public void CareerismDrift_生計依存が出世主義を進める()
        {
            // 既定 careerismRate=0.3、生計依存0.8×dt1×0.3 = 0.24
            Assert.AreEqual(0.24f, PoliticalVocationRules.CareerismDrift(0.8f, 1f), Eps);
            // 生計依存ゼロ（召命型）はドリフトしない
            Assert.AreEqual(0f, PoliticalVocationRules.CareerismDrift(0f, 1f), Eps);
        }

        /// <summary>党機械の官僚化＝生計依存×党機械の強さ×scale。党に飼われた職業政治家ほど硬直。</summary>
        [Test]
        public void PartyMachineBureaucratization_党機械依存で官僚化する()
        {
            // 既定 bureaucratizationScale=0.8、生計依存1×党機械1×0.8 = 0.8
            Assert.AreEqual(0.8f, PoliticalVocationRules.PartyMachineBureaucratization(1f, 1f), Eps);
            // 党機械が無ければ官僚化しない
            Assert.AreEqual(0f, PoliticalVocationRules.PartyMachineBureaucratization(1f, 0f), Eps);
        }

        /// <summary>腐敗傾性＝生計依存×(1−監督)×scale。生計を握られた者ほど・監督が緩いほど腐敗。</summary>
        [Test]
        public void CorruptionPropensity_生計依存と監督の緩さで腐敗が増す()
        {
            // 既定 corruptionScale=0.7、生計依存1×(1−0)×0.7 = 0.7
            Assert.AreEqual(0.7f, PoliticalVocationRules.CorruptionPropensity(1f, 0f), Eps);
            // 監督完璧なら無毒
            Assert.AreEqual(0f, PoliticalVocationRules.CorruptionPropensity(1f, 1f), Eps);
            // 召命型（生計依存ゼロ）は腐敗しにくい
            Assert.AreEqual(0f, PoliticalVocationRules.CorruptionPropensity(0f, 0f), Eps);
        }

        /// <summary>三要件＝情熱×判断力×責任感の積。一つ欠ければ総合は崩れる。</summary>
        [Test]
        public void PassionResponsibilityProportion_三要件の積で一つ欠けると崩れる()
        {
            // 0.8×0.5×1.0 = 0.4
            Assert.AreEqual(0.4f, PoliticalVocationRules.PassionResponsibilityProportion(0.8f, 0.5f, 1f), Eps);
            // 責任感ゼロ（煽動家）は総合ゼロ
            Assert.AreEqual(0f, PoliticalVocationRules.PassionResponsibilityProportion(1f, 1f, 0f), Eps);
        }

        /// <summary>自己犠牲＝使命感×(1−生計依存)×scale。召命型は地位を捨てられ、生業型は執着する。</summary>
        [Test]
        public void SacrificeWillingness_召命型は地位を捨て生業型は執着する()
        {
            // 既定 sacrificeScale=0.9、召命型（使命感1・生計依存0）→ 1×1×0.9 = 0.9
            Assert.AreEqual(0.9f, PoliticalVocationRules.SacrificeWillingness(1f, 0f), Eps);
            // 生業型（生計依存1）は地位に執着して犠牲を払えない
            Assert.AreEqual(0f, PoliticalVocationRules.SacrificeWillingness(1f, 1f), Eps);
        }

        /// <summary>職業政治家判定＝生業型に堕したか。生計のための von 偏重で true。</summary>
        [Test]
        public void IsCareerPolitician_生業型に堕したら職業政治家()
        {
            // 生計依存0.9・使命感0.1 → 志向+0.8 ≥ 0.25 → 職業政治家
            float vonVal = PoliticalVocationRules.VocationOrientationValue(0.1f, 0.9f);
            Assert.IsTrue(PoliticalVocationRules.IsCareerPolitician(vonVal));
            // 召命型は職業政治家でない
            float furVal = PoliticalVocationRules.VocationOrientationValue(0.9f, 0.1f);
            Assert.IsFalse(PoliticalVocationRules.IsCareerPolitician(furVal));
        }
    }
}
