using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 闇市＝統制経済の影を固定する：需要（統制×品不足）で湧き・黙認で複利に肥え・取り締まりで縮む、
    /// 骨抜き度は規模で飽和、取締コストは規模に比例、生存効用（命綱）、黙認の均衡点、根絶の安全条件。
    /// 「統制を強めるほど育つ」「根絶が無痛なのは配給が完璧な時だけ」を境界で担保。
    /// </summary>
    public class BlackMarketRulesTests
    {
        private static readonly BlackMarketParams P = BlackMarketParams.Default;
        // 湧き0.05/成長0.03/取締0.1/骨抜き上限0.5/半飽和規模10/取締コスト0.1/生存効用上限0.6/安全欠乏閾値0.1

        [Test]
        public void MarketSizeTick_SpawnsFromControlAndScarcity()
        {
            // 統制1×品不足1・取締0・dt1：湧き0.05（既存0なら肥えも削りも0）
            Assert.AreEqual(0.05f, BlackMarketRules.MarketSizeTick(0f, 1f, 1f, 0f, 1f, P), 1e-5f);
            // 統制が無ければ湧かない（自由市場が満たす）
            Assert.AreEqual(0f, BlackMarketRules.MarketSizeTick(0f, 0f, 1f, 0f, 1f, P), 1e-5f);
            // 品が足りていれば湧かない（闇市の出番が無い）
            Assert.AreEqual(0f, BlackMarketRules.MarketSizeTick(0f, 1f, 0f, 0f, 1f, P), 1e-5f);
        }

        [Test]
        public void MarketSizeTick_GrowsCompoundUnderTolerance()
        {
            // 既存10・統制1×欠乏1・黙認（取締0）：湧き0.05＋肥え0.03×10=0.3 → 10.35
            Assert.AreEqual(10.35f, BlackMarketRules.MarketSizeTick(10f, 1f, 1f, 0f, 1f, P), 1e-4f);
            // 鏡像＝統制を緩める（0.5）と需要が痩せて育ちが鈍る
            float relaxed = BlackMarketRules.MarketSizeTick(10f, 0.5f, 1f, 0f, 1f, P);
            Assert.AreEqual(10.175f, relaxed, 1e-4f);
            Assert.Less(relaxed, 10.35f);
        }

        [Test]
        public void MarketSizeTick_ShrinksUnderCrackdown()
        {
            // 既存10・全力取締（e=1）：肥え0（黙認分が無い）・削り0.1×10=1.0、湧き0.05 → 9.05
            Assert.AreEqual(9.05f, BlackMarketRules.MarketSizeTick(10f, 1f, 1f, 1f, 1f, P), 1e-4f);
            // 下限0（需要が消えた闇市を叩き続ければ根絶できる）
            Assert.AreEqual(0f, BlackMarketRules.MarketSizeTick(0.05f, 0f, 0f, 1f, 100f, P), 1e-5f);
        }

        [Test]
        public void ControlLeakage_SaturatesWithSize()
        {
            // 規模0＝漏れ無し
            Assert.AreEqual(0f, BlackMarketRules.ControlLeakage(0f, P), 1e-5f);
            // 半飽和規模10で上限の半分＝0.25
            Assert.AreEqual(0.25f, BlackMarketRules.ControlLeakage(10f, P), 1e-5f);
            // 巨大化しても上限0.5を超えない（統制は名ばかりになるが無効化はしない）
            float huge = BlackMarketRules.ControlLeakage(1000f, P);
            Assert.Greater(huge, 0.49f);
            Assert.Less(huge, 0.5f);
        }

        [Test]
        public void EnforcementCost_ScalesWithMarketSize()
        {
            // 黙認＝無料
            Assert.AreEqual(0f, BlackMarketRules.EnforcementCost(0f, 10f, P), 1e-5f);
            // 規模0でも全力取締には基礎コスト0.1
            Assert.AreEqual(0.1f, BlackMarketRules.EnforcementCost(1f, 0f, P), 1e-5f);
            // 大きな闇市ほど高くつく：規模10で 0.1×(1+10)=1.1
            Assert.AreEqual(1.1f, BlackMarketRules.EnforcementCost(1f, 10f, P), 1e-5f);
        }

        [Test]
        public void SurvivalValue_AndEradicationSafety()
        {
            // 欠乏1×規模10（半飽和）：0.6×0.5=0.3 ＝闇市が市民を養っている
            Assert.AreEqual(0.3f, BlackMarketRules.SurvivalValue(10f, 1f, P), 1e-5f);
            // つぶし切ると命綱が無い
            Assert.AreEqual(0f, BlackMarketRules.SurvivalValue(0f, 1f, P), 1e-5f);
            // 配給完璧なら効用0＝そのときだけ根絶は無痛
            Assert.AreEqual(0f, BlackMarketRules.SurvivalValue(10f, 0f, P), 1e-5f);
            Assert.IsTrue(BlackMarketRules.IsEradicationSafe(0f, P));
            Assert.IsTrue(BlackMarketRules.IsEradicationSafe(0.1f, P)); // 閾値ちょうどは安全
            Assert.IsFalse(BlackMarketRules.IsEradicationSafe(0.3f, P)); // 欠乏下の根絶は市民が死ぬ
        }

        [Test]
        public void ToleranceEquilibrium_RisesWithControl()
        {
            // 統制1×欠乏1：e*=0.03/(0.03+0.1)≈0.230769 ＝これ未満の取締は黙認＝育つ
            float full = BlackMarketRules.ToleranceEquilibrium(1f, 1f, P);
            Assert.AreEqual(0.03f / 0.13f, full, 1e-5f);
            // 統制半分なら均衡点も安くつく＝統制を強めるほど取り締まりも重くなる（鏡像）
            float half = BlackMarketRules.ToleranceEquilibrium(0.5f, 1f, P);
            Assert.AreEqual(0.015f / 0.115f, half, 1e-5f);
            Assert.Less(half, full);
            // 需要0＝取り締まる相手がいない
            Assert.AreEqual(0f, BlackMarketRules.ToleranceEquilibrium(0f, 1f, P), 1e-5f);

            // 均衡点ちょうどでは既存規模の肥えと削りが釣り合う（湧きぶんだけ増える）
            float next = BlackMarketRules.MarketSizeTick(10f, 1f, 1f, full, 1f, P);
            Assert.AreEqual(10f + 0.05f, next, 1e-4f);
        }
    }
}
