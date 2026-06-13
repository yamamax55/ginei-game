using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 大事業が都市を育てる純ロジックのテスト（#1094）。既定 Params の具体値で期待値を固定し、
    /// 事業活発での人口流入と中断での衰退（職人が去る）を担保する。
    /// </summary>
    public class CityGrowthRulesTests
    {
        /// <summary>事業が活発(activity=1)なら雇用が人を呼び人口が流入する＝大事業は街を生む。</summary>
        [Test]
        public void 事業活発で人口が流入する()
        {
            // 流入=5×1×1×1=5、流出=0 → 105
            float pop = CityGrowthRules.PopulationInflowTick(100f, 1f, 1f, 1f);
            Assert.AreEqual(105f, pop, 1e-4f);
        }

        /// <summary>事業が止まる(activity=0)と流入が消え、止まったぶん人口が流出する＝職人が去る。</summary>
        [Test]
        public void 事業停止で人口が流出する()
        {
            // 流入=0、流出=100×0.02×1×1=2 → 98
            float pop = CityGrowthRules.PopulationInflowTick(100f, 0f, 1f, 1f);
            Assert.AreEqual(98f, pop, 1e-4f);
        }

        /// <summary>人口規模で集落→村→町→都市→大都市へ段階が上がる。</summary>
        [Test]
        public void 人口規模で集落段階が決まる()
        {
            Assert.AreEqual(SettlementTier.集落, CityGrowthRules.TierOf(50f));
            Assert.AreEqual(SettlementTier.村, CityGrowthRules.TierOf(100f));
            Assert.AreEqual(SettlementTier.町, CityGrowthRules.TierOf(500f));
            Assert.AreEqual(SettlementTier.都市, CityGrowthRules.TierOf(2000f));
            Assert.AreEqual(SettlementTier.大都市, CityGrowthRules.TierOf(10000f));
        }

        /// <summary>人口集積が市場を生む＝閾値超で交易が立ち、閾値以下では市場ゼロ。</summary>
        [Test]
        public void 人口集積で市場が成立する()
        {
            // 閾値200以下は市場ゼロ
            Assert.AreEqual(0f, CityGrowthRules.MarketEmergence(150f, 1f), 1e-4f);
            // 600人：InverseLerp(200,1000,600)=0.5、tradeAccess=1 → 0.5
            Assert.AreEqual(0.5f, CityGrowthRules.MarketEmergence(600f, 1f), 1e-4f);
        }

        /// <summary>大きい都市ほど集積ボーナスが高い＝都市化の利益。</summary>
        [Test]
        public void 集積ボーナスは都市規模で増える()
        {
            Assert.AreEqual(1f, CityGrowthRules.AgglomerationBonus(SettlementTier.集落), 1e-4f);
            Assert.AreEqual(1.15f, CityGrowthRules.AgglomerationBonus(SettlementTier.町), 1e-4f);
            Assert.Greater(CityGrowthRules.AgglomerationBonus(SettlementTier.大都市),
                           CityGrowthRules.AgglomerationBonus(SettlementTier.都市));
        }

        /// <summary>事業中断が長引くほど衰退が深まる＝建てかけの街は空洞化する。中断なしは衰退しない。</summary>
        [Test]
        public void 事業中断が長引くほど衰退する()
        {
            // halt=60（=rampで最大）：severity=1、loss=1000×0.02×1×1=20 → 980
            Assert.AreEqual(980f, CityGrowthRules.DeclineOnProjectHalt(1000f, 60f, 1f), 1e-4f);
            // halt=30：severity=0.5、loss=10 → 990
            Assert.AreEqual(990f, CityGrowthRules.DeclineOnProjectHalt(1000f, 30f, 1f), 1e-4f);
            // halt=0（中断なし）：衰退なし
            Assert.AreEqual(1000f, CityGrowthRules.DeclineOnProjectHalt(1000f, 0f, 1f), 1e-4f);
        }

        /// <summary>Province成長寄与は集積と市場の両輪で増える＝都市が地方を育てる。</summary>
        [Test]
        public void Province成長寄与は集積と市場で増える()
        {
            // 都市：aggNorm=(1.3-1)/(1.5-1)=0.6、market=0 → 0.6×0.6+0=0.36
            Assert.AreEqual(0.36f, CityGrowthRules.ProvinceGrowthContribution(SettlementTier.都市, 0f), 1e-4f);
            // 市場が立つと寄与が増える
            Assert.Greater(CityGrowthRules.ProvinceGrowthContribution(SettlementTier.都市, 1f),
                           CityGrowthRules.ProvinceGrowthContribution(SettlementTier.都市, 0f));
            // 集落かつ無市場は寄与ゼロ
            Assert.AreEqual(0f, CityGrowthRules.ProvinceGrowthContribution(SettlementTier.集落, 0f), 1e-4f);
        }
    }
}
