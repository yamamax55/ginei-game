using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 競争的民主主義と経済置換（シュンペーター・SCHU-6 #1598）の純ロジック検証。
    /// 既定 Params（セーフティ吸収0.6・隙係数0.7・不信増幅1.0・品質浸食0.05）で期待値を固定する。
    /// </summary>
    public class CompetitiveDemocracyRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>選挙競争＝候補の質×開放度の積（どちらか欠ければ機能しない）。</summary>
        [Test]
        public void ElectoralCompetition_質と開放度の積()
        {
            Assert.AreEqual(0.8f * 0.5f, CompetitiveDemocracyRules.ElectoralCompetition(0.8f, 0.5f), Eps);
            // 開放度0（無投票・談合）＝競争は機能しない
            Assert.AreEqual(0f, CompetitiveDemocracyRules.ElectoralCompetition(0.9f, 0f), Eps);
        }

        /// <summary>置換ショック＝社会的セーフティで吸収しきれずに残るショック。</summary>
        [Test]
        public void DisplacementShock_セーフティで吸収した残り()
        {
            // raw=0.8, absorbed=0.8*0.5*0.6=0.24, 残り=0.56
            Assert.AreEqual(0.56f, CompetitiveDemocracyRules.DisplacementShock(0.8f, 0.5f), Eps);
            // セーフティ0＝丸ごと残る
            Assert.AreEqual(0.8f, CompetitiveDemocracyRules.DisplacementShock(0.8f, 0f), Eps);
        }

        /// <summary>扇動の隙＝置換ショックが大きく制度不信が深いほど広がる。</summary>
        [Test]
        public void DemagogueOpening_ショックと不信で広がる()
        {
            // shock=0.5, trust=0.4→distrust=0.6, 0.5*0.7*(1+0.6*1.0)=0.5*0.7*1.6=0.56
            Assert.AreEqual(0.56f, CompetitiveDemocracyRules.DemagogueOpening(0.5f, 0.4f), Eps);
            // 制度完全信頼（trust=1）＝不信ぶんの増幅なし
            Assert.AreEqual(0.5f * 0.7f, CompetitiveDemocracyRules.DemagogueOpening(0.5f, 1f), Eps);
        }

        /// <summary>民主的品質＝競争の健全さを扇動の隙が蝕んだ残り。</summary>
        [Test]
        public void DemocraticQuality_競争を隙が蝕む()
        {
            // 0.6*(1-0.25)=0.45
            Assert.AreEqual(0.45f, CompetitiveDemocracyRules.DemocraticQuality(0.6f, 0.25f), Eps);
            // 隙が無ければ競争の健全さがそのまま品質
            Assert.AreEqual(0.6f, CompetitiveDemocracyRules.DemocraticQuality(0.6f, 0f), Eps);
        }

        /// <summary>経済の置換が政治の品質を下げる：高い創造的破壊・薄いセーフティ・深い不信は品質を強く蝕む。</summary>
        [Test]
        public void 経済置換が民主的品質を下げる経路()
        {
            float comp = CompetitiveDemocracyRules.ElectoralCompetition(0.9f, 0.9f); // 0.81 健全な競争
            // 創造的破壊が激しくセーフティ薄く制度不信が深いケース
            float shockBad = CompetitiveDemocracyRules.DisplacementShock(0.9f, 0.1f);
            float openBad = CompetitiveDemocracyRules.DemagogueOpening(shockBad, 0.2f);
            float qualBad = CompetitiveDemocracyRules.DemocraticQuality(comp, openBad);
            // セーフティ厚くショックが小さいケース
            float shockGood = CompetitiveDemocracyRules.DisplacementShock(0.9f, 0.9f);
            float openGood = CompetitiveDemocracyRules.DemagogueOpening(shockGood, 0.2f);
            float qualGood = CompetitiveDemocracyRules.DemocraticQuality(comp, openGood);
            Assert.Less(qualBad, qualGood, "置換ショックが大きいほど民主的品質は下がる");
        }

        /// <summary>品質劣化＝扇動圧が品質を時間で削る。</summary>
        [Test]
        public void QualityErosionTick_扇動圧で時間劣化()
        {
            // erosion=0.8*0.05*2=0.08, 0.7-0.08=0.62
            Assert.AreEqual(0.62f, CompetitiveDemocracyRules.QualityErosionTick(0.7f, 0.8f, 2f), Eps);
            // 扇動圧0＝劣化なし
            Assert.AreEqual(0.7f, CompetitiveDemocracyRules.QualityErosionTick(0.7f, 0f, 5f), Eps);
        }

        /// <summary>説明責任＝品質×透明性。後退判定＝品質が閾値割れ。</summary>
        [Test]
        public void Accountabilityと後退判定()
        {
            Assert.AreEqual(0.5f * 0.8f, CompetitiveDemocracyRules.AccountabilityStrength(0.5f, 0.8f), Eps);
            Assert.IsTrue(CompetitiveDemocracyRules.IsDemocraticBacksliding(0.3f, 0.5f));
            Assert.IsFalse(CompetitiveDemocracyRules.IsDemocraticBacksliding(0.6f, 0.5f));
        }

        /// <summary>制度の頑健性＝制度信頼×競争（どちらか欠ければ扇動に脆い）。</summary>
        [Test]
        public void ResilienceFromInstitutions_信頼と競争の積()
        {
            Assert.AreEqual(0.7f * 0.6f, CompetitiveDemocracyRules.ResilienceFromInstitutions(0.7f, 0.6f), Eps);
            // 競争が死んでいれば信頼が高くても頑健でない
            Assert.AreEqual(0f, CompetitiveDemocracyRules.ResilienceFromInstitutions(0.9f, 0f), Eps);
        }

        /// <summary>全入力クランプ＝範囲外でも0..1に収まる。</summary>
        [Test]
        public void 入力クランプ()
        {
            Assert.AreEqual(1f, CompetitiveDemocracyRules.ElectoralCompetition(2f, 2f), Eps);
            Assert.AreEqual(0f, CompetitiveDemocracyRules.DisplacementShock(-1f, 0.5f), Eps);
            float q = CompetitiveDemocracyRules.QualityErosionTick(0.1f, 5f, 100f);
            Assert.GreaterOrEqual(q, 0f);
        }
    }
}
