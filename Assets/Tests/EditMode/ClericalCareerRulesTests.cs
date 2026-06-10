using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 聖職キャリアの純ロジック（#1096）のEditModeテスト。既定Paramsの具体値で期待値を固定し、
    /// 理想家と野心家が同じ階段を別の動機で登ること・世俗権力での腐敗リスクを担保する。
    /// </summary>
    public class ClericalCareerRulesTests
    {
        private static ClericalCareerParams P => ClericalCareerParams.Default;
        private const float Eps = 1e-4f;

        /// <summary>昇進力＝敬虔/野心/後ろ盾の加重和。理想家(敬虔のみ)も野心家(野心+後ろ盾)も上に行ける。</summary>
        [Test]
        public void PromotionScore_理想家も野心家も登る()
        {
            // 理想家：敬虔1のみ → 0.4
            float ideal = ClericalCareerRules.PromotionScore(1f, 0f, 0f, P);
            Assert.AreEqual(0.4f, ideal, Eps);
            // 野心家：野心1＋後ろ盾1 → 0.35+0.25=0.6（理想家を上回る＝出世主義は速い）
            float careerist = ClericalCareerRules.PromotionScore(0f, 1f, 1f, P);
            Assert.AreEqual(0.6f, careerist, Eps);
            Assert.Greater(careerist, ideal);
        }

        /// <summary>霊的権威＝高位×敬虔。大司教×敬虔1で最大、敬虔が落ちれば痩せる。</summary>
        [Test]
        public void SpiritualAuthority_高位かつ敬虔で最大()
        {
            Assert.AreEqual(1f, ClericalCareerRules.SpiritualAuthority(ClericalRank.大司教, 1f), Eps);
            // 司教(4/5=0.8)×敬虔0.5 → 0.4
            Assert.AreEqual(0.4f, ClericalCareerRules.SpiritualAuthority(ClericalRank.司教, 0.5f), Eps);
            // 高位でも敬虔0なら権威0
            Assert.AreEqual(0f, ClericalCareerRules.SpiritualAuthority(ClericalRank.大司教, 0f), Eps);
        }

        /// <summary>世俗権力＝高位ほど領地・政治力を持つ。修道士はほぼ0、野心家の司教は高い。</summary>
        [Test]
        public void TemporalPower_高位の野心家は諸侯化()
        {
            Assert.AreEqual(0f, ClericalCareerRules.TemporalPower(ClericalRank.修道士, 1f, P), Eps);
            // 司教 rf=0.8、blend=(0.8*0.6+1*0.4)/1.0=0.88 → 0.8*0.88=0.704
            Assert.AreEqual(0.704f, ClericalCareerRules.TemporalPower(ClericalRank.司教, 1f, P), Eps);
            // 同じ司教でも野心0なら世俗権力は低い
            Assert.Less(ClericalCareerRules.TemporalPower(ClericalRank.司教, 0f, P),
                        ClericalCareerRules.TemporalPower(ClericalRank.司教, 1f, P));
        }

        /// <summary>理想vs野心ドリフト：理想家は登っても正(理想が勝つ)、野心家は出世で負(堕落)。</summary>
        [Test]
        public void IdealVsAmbitionDrift_理想家は守り野心家は堕ちる()
        {
            // 理想家：敬虔1・野心0・到達1 → (1-0)*0.3=+0.3
            Assert.AreEqual(0.3f, ClericalCareerRules.IdealVsAmbitionDrift(1f, 0f, 1f, P), Eps);
            // 野心家：敬虔0・野心1・到達1 → (0-1)*0.3=-0.3（初心が世俗権力に侵食される）
            Assert.AreEqual(-0.3f, ClericalCareerRules.IdealVsAmbitionDrift(0f, 1f, 1f, P), Eps);
        }

        /// <summary>共同体の希望への寄与＝霊的権威に比例（理想駆動の聖職が希望を生む）。</summary>
        [Test]
        public void CommunityHopeContribution_霊的権威に比例()
        {
            float auth = ClericalCareerRules.SpiritualAuthority(ClericalRank.大司教, 0.7f); // =0.7
            Assert.AreEqual(0.7f, ClericalCareerRules.CommunityHopeContribution(auth), Eps);
        }

        /// <summary>腐敗リスク＝世俗権力×低敬虔。堕落した高位聖職は最大、清貧の高位は0。</summary>
        [Test]
        public void CorruptionRisk_世俗権力と低敬虔で堕落()
        {
            // 世俗権力1×敬虔0 → 1*1*0.8=0.8（最大＝地球教の影）
            Assert.AreEqual(0.8f, ClericalCareerRules.CorruptionRisk(1f, 0f, P), Eps);
            // 世俗権力1でも敬虔1なら腐敗0（清貧の高位は堕ちない）
            Assert.AreEqual(0f, ClericalCareerRules.CorruptionRisk(1f, 1f, P), Eps);
        }

        /// <summary>入力クランプ：範囲外でも0..1へ丸める（決定論・例外なし）。</summary>
        [Test]
        public void Clamp_範囲外入力を丸める()
        {
            Assert.AreEqual(1f, ClericalCareerRules.PromotionScore(5f, 5f, 5f, P), Eps); // 全要素飽和→重み和1.0
            Assert.AreEqual(0f, ClericalCareerRules.CorruptionRisk(-2f, 0.5f, P), Eps);
            Assert.AreEqual(0f, ClericalCareerRules.SpiritualAuthority(ClericalRank.修道士, -1f), Eps);
        }
    }
}
