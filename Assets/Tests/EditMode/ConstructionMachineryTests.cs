using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 建機メーカー（建設機械・#2022・<see cref="ConstructionMachineryRules"/>）を固定する：加速度原理(CON-1)、新車と
    /// アフターサービス(CON-2)、レンタル/中古/残価(CON-3)、グローバル需要(CON-4)、景気循環と利益(CON-5)。
    /// </summary>
    public class ConstructionMachineryTests
    {
        // ===== CON-1 資本財需要と加速度原理 =====
        [Test]
        public void AcceleratedDemand_AmplifiesActivityChange()
        {
            // 建設活動+10%×増幅3 = 需要+30%（投資財は景気に超敏感）
            Assert.AreEqual(130f, ConstructionMachineryRules.AcceleratedDemand(100f, 0.1f, 3f), 1e-3f);
            // 不況も増幅：−10%×3 = 需要−30%
            Assert.AreEqual(70f, ConstructionMachineryRules.AcceleratedDemand(100f, -0.1f, 3f), 1e-3f);
            // 急落でも0でクランプ
            Assert.AreEqual(0f, ConstructionMachineryRules.AcceleratedDemand(100f, -0.5f, 3f), 1e-3f);
        }

        // ===== CON-2 新車とアフターサービス =====
        [Test]
        public void NewSales_AndAfterServices()
        {
            Assert.AreEqual(10000f, ConstructionMachineryRules.NewSalesRevenue(50f, 200f), 1e-3f);
            // 稼働台数1000×部品単価5 = 5000（新車が落ちても安定）
            Assert.AreEqual(5000f, ConstructionMachineryRules.AfterSalesRevenue(1000f, 5f), 1e-3f);
            Assert.AreEqual(0.3333f, ConstructionMachineryRules.AfterSalesShare(5000f, 15000f), 1e-3f);
        }

        // ===== CON-3 レンタル・中古・残価 =====
        [Test]
        public void Rental_UsedAndResidual()
        {
            Assert.AreEqual(200f, ConstructionMachineryRules.RentalIncome(20f, 10f), 1e-3f);
            // 経年3年×減価10% → 残価7割
            Assert.AreEqual(140f, ConstructionMachineryRules.ResidualValue(200f, 3f, 0.1f, 0.3f), 1e-3f);
            // 古くても下限率3割で下げ止まり
            Assert.AreEqual(60f, ConstructionMachineryRules.ResidualValue(200f, 8f, 0.1f, 0.3f), 1e-3f);
            Assert.AreEqual(1400f, ConstructionMachineryRules.UsedSaleProceeds(200f, 0.7f, 10f), 1e-3f);
        }

        // ===== CON-4 グローバル需要 =====
        [Test]
        public void GlobalDemand_AndDiversification()
        {
            Assert.AreEqual(1000f, ConstructionMachineryRules.GlobalDemand(new List<float> { 300f, 200f, 500f }), 1e-3f);
            Assert.AreEqual(0.72f, ConstructionMachineryRules.GeographicDiversification(new List<float> { 300f, 300f, 300f, 100f }), 1e-3f);
            Assert.AreEqual(0f, ConstructionMachineryRules.GeographicDiversification(new List<float> { 1000f }), 1e-3f); // 一国依存
            Assert.AreEqual(0f, ConstructionMachineryRules.GeographicDiversification(null), 1e-4f);
        }

        // ===== CON-5 景気循環と利益 =====
        [Test]
        public void Cyclical_AfterSalesCushions()
        {
            // 不況（景気0.5）：新車1000×0.5＋アフター500 = 1000
            Assert.AreEqual(1000f, ConstructionMachineryRules.CyclicalProfit(1000f, 500f, 0.5f), 1e-3f);
            // 好況（1.5）：1500＋500 = 2000
            Assert.AreEqual(2000f, ConstructionMachineryRules.CyclicalProfit(1000f, 500f, 1.5f), 1e-3f);
            Assert.AreEqual(0.5f, ConstructionMachineryRules.DownturnResilience(500f, 1000f), 1e-3f);
        }
    }
}
