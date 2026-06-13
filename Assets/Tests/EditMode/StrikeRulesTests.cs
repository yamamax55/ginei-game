using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 労働運動（賃金・待遇闘争）を固定する：組織率×人手不足の交渉力、勝ち目で振れる決行圧力、
    /// 長引くほど双方が痩せる生産損害、基金が買う持久力、同情される労働者への弾圧の政治的代償、
    /// そして「先に限界が来た方が折れる」我慢比べの妥結水準。境界を担保。
    /// </summary>
    public class StrikeRulesTests
    {
        private static readonly StrikeParams P = StrikeParams.Default;
        // 不況時交渉力0.3・弱腰決行0.25・決行閾値0.5・損害速度0.2・基金枯渇0.25・弾圧効率0.6・同情逆風0.5

        [Test]
        public void BargainingPower_UnionTimesScarcity()
        {
            // 完全組織×人手不足の極み＝交渉力1（替えが効かない）
            Assert.AreEqual(1f, StrikeRules.BargainingPower(1f, 1f, P), 1e-5f);
            // 完全組織でも不況（人手余り）＝0.3 まで目減り（替えが効く）
            Assert.AreEqual(0.3f, StrikeRules.BargainingPower(1f, 0f, P), 1e-5f);
            // 半分組織×好景気＝0.5
            Assert.AreEqual(0.5f, StrikeRules.BargainingPower(0.5f, 1f, P), 1e-5f);
            // 無組織なら景気が良くても交渉力なし（個人は替えられる）
            Assert.AreEqual(0f, StrikeRules.BargainingPower(0f, 1f, P), 1e-5f);
            // 入力は0..1にクランプ
            Assert.AreEqual(1f, StrikeRules.BargainingPower(5f, 5f, P), 1e-5f);
        }

        [Test]
        public void StrikeThreshold_WeakHandHesitates_StrongHandStrikes()
        {
            // 不満最大×交渉力最大＝決行圧力1
            Assert.AreEqual(1f, StrikeRules.StrikeThreshold(1f, 1f, P), 1e-5f);
            // 不満最大でも勝ち目なし＝0.25 まで減衰（窮して立つ分だけ残る）
            Assert.AreEqual(0.25f, StrikeRules.StrikeThreshold(1f, 0f, P), 1e-5f);
            // 不満0.8×交渉力0.5＝0.8×(0.25+0.75×0.5)=0.5＝ちょうど閾値で決行
            Assert.AreEqual(0.5f, StrikeRules.StrikeThreshold(0.8f, 0.5f, P), 1e-5f);
            Assert.IsTrue(StrikeRules.StrikeBreaksOut(0.8f, 0.5f, P));
            // 同じ不満でも交渉力が無ければ決行しない（負け筋）
            Assert.IsFalse(StrikeRules.StrikeBreaksOut(0.8f, 0f, P));
            // 不満が無ければ決行しない
            Assert.IsFalse(StrikeRules.StrikeBreaksOut(0f, 1f, P));
        }

        [Test]
        public void ProductionLoss_LongerStrikeStarvesBoth()
        {
            // 半数参加×2期間＝0.5×0.2×2=0.2 の損害
            Assert.AreEqual(0.2f, StrikeRules.ProductionLoss(0.5f, 2f, P), 1e-5f);
            // 全員参加が長引けば損害は上限1（双方が痩せる消耗戦）
            Assert.AreEqual(1f, StrikeRules.ProductionLoss(1f, 10f, P), 1e-5f);
            // 参加者がいなければ損害なし
            Assert.AreEqual(0f, StrikeRules.ProductionLoss(0f, 10f, P), 1e-5f);
            // 負の期間は0扱い
            Assert.AreEqual(0f, StrikeRules.ProductionLoss(1f, -1f, P), 1e-5f);
        }

        [Test]
        public void StrikeFundDepletion_FundBuysEndurance()
        {
            // 基金なしなら4期間で飢える＝4×0.25=1
            Assert.AreEqual(1f, StrikeRules.StrikeFundDepletion(4f, 0f, P), 1e-5f);
            // 基金0.5は持久力を半分継ぎ足す＝4×0.25−0.5=0.5
            Assert.AreEqual(0.5f, StrikeRules.StrikeFundDepletion(4f, 0.5f, P), 1e-5f);
            // 満額の基金でも8期間で尽きる＝8×0.25−1=1
            Assert.AreEqual(1f, StrikeRules.StrikeFundDepletion(8f, 1f, P), 1e-5f);
            // 短期戦なら満額基金は無傷＝下限0
            Assert.AreEqual(0f, StrikeRules.StrikeFundDepletion(2f, 1f, P), 1e-5f);
        }

        [Test]
        public void RepressionOutcome_SympathyBackfires()
        {
            // 全力弾圧×同情なし＝鎮圧0.6・支持ペナルティなし（誰も見ていない弾圧は効くだけ）
            StrikeRepression cold = StrikeRules.RepressionOutcome(1f, 0f, P);
            Assert.AreEqual(0.6f, cold.suppression, 1e-5f);
            Assert.AreEqual(0f, cold.supportPenalty, 1e-5f);
            // 全力弾圧×満場の同情＝鎮圧は同じ0.6だが支持を0.5削る（NonviolenceRules と同型＝見られて高くつく）
            StrikeRepression hot = StrikeRules.RepressionOutcome(1f, 1f, P);
            Assert.AreEqual(0.6f, hot.suppression, 1e-5f);
            Assert.AreEqual(0.5f, hot.supportPenalty, 1e-5f);
            // 半力×同情0.8＝鎮圧0.3・ペナルティ0.2
            StrikeRepression half = StrikeRules.RepressionOutcome(0.5f, 0.8f, P);
            Assert.AreEqual(0.3f, half.suppression, 1e-5f);
            Assert.AreEqual(0.2f, half.supportPenalty, 1e-5f);
            // 弾圧しなければ代償もなし
            Assert.AreEqual(0f, StrikeRules.RepressionOutcome(0f, 1f, P).supportPenalty, 1e-5f);
        }

        [Test]
        public void SettlementWage_MeetsInTheMiddle_StarvedSideFolds()
        {
            // 労働側全強×企業体力ゼロ＝満額妥結1
            Assert.AreEqual(1f, StrikeRules.SettlementWage(1f, 0f, P), 1e-5f);
            // 労働側無力×企業体力満タン＝ゼロ回答0
            Assert.AreEqual(0f, StrikeRules.SettlementWage(0f, 1f, P), 1e-5f);
            // 互角＝中間0.5
            Assert.AreEqual(0.5f, StrikeRules.SettlementWage(0.5f, 0.5f, P), 1e-5f);
            // 我慢比べ：基金が枯渇した労働側は手札を失い折れる＝(1×0+1)/2=0.5 まで落ちる
            Assert.AreEqual(0.5f, StrikeRules.SettlementWage(1f, 0f, 1f, P), 1e-5f);
            // 交渉力0.6×企業体力0.2＝(0.6+0.8)/2=0.7
            Assert.AreEqual(0.7f, StrikeRules.SettlementWage(0.6f, 0.2f, 0f, P), 1e-5f);
        }

        [Test]
        public void DefaultParams_CtorClamps()
        {
            // 負値・範囲外はctorでクランプされる
            var p = new StrikeParams(-1f, 2f, 2f, -1f, -1f, 2f, -1f);
            Assert.AreEqual(0f, p.scarcitySlack, 1e-6f);
            Assert.AreEqual(1f, p.weakHandFactor, 1e-6f);
            Assert.AreEqual(1f, p.strikeTrigger, 1e-6f);
            Assert.AreEqual(0f, p.lossRate, 1e-6f);
            Assert.AreEqual(1f, p.repressionEffect, 1e-6f);
            Assert.AreEqual(0f, p.sympathyBackfire, 1e-6f);
            // 既定値の固定
            Assert.AreEqual(0.3f, P.scarcitySlack, 1e-6f);
            Assert.AreEqual(0.25f, P.weakHandFactor, 1e-6f);
            Assert.AreEqual(0.5f, P.strikeTrigger, 1e-6f);
            Assert.AreEqual(0.2f, P.lossRate, 1e-6f);
            Assert.AreEqual(0.25f, P.fundDrainRate, 1e-6f);
            Assert.AreEqual(0.6f, P.repressionEffect, 1e-6f);
            Assert.AreEqual(0.5f, P.sympathyBackfire, 1e-6f);
        }
    }
}
