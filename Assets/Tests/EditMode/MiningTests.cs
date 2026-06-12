using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 鉱山会社（採掘業・#2018・<see cref="MiningRules"/>）を固定する：鉱床と埋蔵量(MIN-1)、採掘と産出(MIN-2)、
    /// 品位低下とコスト上昇(MIN-3)、探鉱(MIN-4)、収益と枯渇(MIN-5)。
    /// </summary>
    public class MiningTests
    {
        // ===== MIN-1 鉱床と埋蔵量 =====
        [Test]
        public void Reserves_ExtractionAndDepletion()
        {
            Assert.AreEqual(200f, MiningRules.ExtractedOre(1000f, 200f), 1e-3f);
            Assert.AreEqual(150f, MiningRules.ExtractedOre(150f, 200f), 1e-3f);   // 埋蔵量を超えては掘れない
            Assert.AreEqual(800f, MiningRules.ReservesAfterExtraction(1000f, 200f), 1e-3f);
            Assert.AreEqual(0.3f, MiningRules.DepletionRatio(300f, 1000f), 1e-4f);
            Assert.IsTrue(MiningRules.IsDepleted(0f));   // 枯渇＝閉山
            Assert.IsFalse(MiningRules.IsDepleted(100f));
        }

        // ===== MIN-2 採掘と産出 =====
        [Test]
        public void Extraction_MetalAndCost()
        {
            Assert.AreEqual(10f, MiningRules.MetalOutput(200f, 0.05f), 1e-3f); // 鉱石200×品位5%
            Assert.AreEqual(400f, MiningRules.ExtractionCost(200f, 2f), 1e-3f);
        }

        // ===== MIN-3 品位低下とコスト上昇 =====
        [Test]
        public void Depletion_GradeFallsCostRises()
        {
            // 枯渇率50%で品位 0.05→0.035（良鉱から掘り尽くす）
            Assert.AreEqual(0.035f, MiningRules.GradeAfterDepletion(0.05f, 0.5f, 0.6f), 1e-4f);
            // 枯渇率50%でコスト 2→3（深部化）
            Assert.AreEqual(3f, MiningRules.CostAfterDepletion(2f, 0.5f, 1.0f), 1e-3f);
        }

        // ===== MIN-4 探鉱 =====
        [Test]
        public void Exploration_RiskInvestment()
        {
            Assert.IsTrue(MiningRules.ExplorationSuccess(0.3f, 0.5f));   // roll<確率＝成功
            Assert.IsFalse(MiningRules.ExplorationSuccess(0.7f, 0.5f));
            Assert.AreEqual(500f, MiningRules.DiscoveredReserves(1000f, 0.5f, true), 1e-3f);  // 成功＝新鉱脈
            Assert.AreEqual(0f, MiningRules.DiscoveredReserves(1000f, 0.5f, false), 1e-3f);   // 失敗＝空振り
        }

        // ===== MIN-5 収益と枯渇 =====
        [Test]
        public void Revenue_ProfitAndMineLife()
        {
            Assert.AreEqual(1000f, MiningRules.MiningRevenue(10f, 100f), 1e-3f);
            // 収益1000−採掘コスト400 = 600
            Assert.AreEqual(600f, MiningRules.MiningProfit(10f, 100f, 200f, 2f), 1e-3f);
            // 価格暴落で赤字
            Assert.AreEqual(-300f, MiningRules.MiningProfit(10f, 10f, 200f, 2f), 1e-3f); // 100−400
            // 鉱山寿命＝埋蔵量/年間採掘
            Assert.AreEqual(5f, MiningRules.MineLifeYears(1000f, 200f), 1e-3f);
        }
    }
}
