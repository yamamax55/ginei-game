using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>SIer・ソフト受託（#2025・<see cref="SystemIntegratorRules"/>）：受託収入(SI-1)・稼働率(SI-2)・工数超過(SI-3)・利益(SI-4)。</summary>
    public class SystemIntegratorTests
    {
        [Test]
        public void Revenue_AndUtilization()
        {
            Assert.AreEqual(100000000f, SystemIntegratorRules.ProjectRevenue(100f, 1000000f), 1e1f); // 100人月×単価
            Assert.AreEqual(0.8f, SystemIntegratorRules.Utilization(1600f, 2000f), 1e-4f);
        }

        [Test]
        public void Overrun_AndProfit()
        {
            Assert.AreEqual(24000000f, SystemIntegratorRules.OverrunLoss(100f, 130f, 800000f), 1e1f); // 30人月超過のデスマーチ
            Assert.AreEqual(0f, SystemIntegratorRules.OverrunLoss(100f, 90f, 800000f), 1e-1f);        // 前倒しは損失なし
            Assert.AreEqual(20000000f, SystemIntegratorRules.SiProfit(100000000f, 70000000f, 10000000f), 1e1f);
        }
    }
}
