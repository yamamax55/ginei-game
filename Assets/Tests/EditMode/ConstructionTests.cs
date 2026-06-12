using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>建設会社（#2024・<see cref="ConstructionRules"/>）：工事進行(BLD-1)・計上売上(BLD-2)・採算/原価超過(BLD-3)・受注残(BLD-4)。</summary>
    public class ConstructionTests
    {
        [Test]
        public void PercentageOfCompletion_AndRevenue()
        {
            Assert.AreEqual(0.6f, ConstructionRules.PercentageOfCompletion(600f, 1000f), 1e-4f); // 原価6割消化
            Assert.AreEqual(720f, ConstructionRules.RecognizedRevenue(1200f, 0.6f), 1e-3f);       // 契約1200×進捗0.6
        }

        [Test]
        public void Profit_OverrunAndBacklog()
        {
            Assert.AreEqual(200f, ConstructionRules.ProjectProfit(1200f, 1000f), 1e-3f);
            Assert.AreEqual(100f, ConstructionRules.CostOverrun(1000f, 1100f), 1e-3f); // 原価超過＝採算悪化
            Assert.AreEqual(600f, ConstructionRules.BacklogAfterOrders(500f, 300f, 200f), 1e-3f);
        }
    }
}
