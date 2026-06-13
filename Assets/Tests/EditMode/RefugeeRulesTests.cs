using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 難民を固定する：戦火比例の流出、受け入れ負担は難民比×未統合分、溶け込みは時間で進み、
    /// 故郷が安全になれば未統合の難民だけが帰還する。境界・クランプを担保。
    /// </summary>
    public class RefugeeRulesTests
    {
        private static readonly RefugeeParams P = RefugeeParams.Default;
        // 最大流出0.3/負担0.5/溶け込み0.05/帰還閾値0.7/帰還率0.1

        [Test]
        public void Displaced_ScalesWithWarIntensity()
        {
            Assert.AreEqual(300f, RefugeeRules.Displaced(1000f, 1f, P), 1e-4f);   // 全面戦火＝30%流出
            Assert.AreEqual(150f, RefugeeRules.Displaced(1000f, 0.5f, P), 1e-4f);
            Assert.AreEqual(0f, RefugeeRules.Displaced(1000f, 0f, P), 1e-5f);     // 平和＝流出なし
        }

        [Test]
        public void RefugeeFraction_OfCombinedPopulation()
        {
            Assert.AreEqual(0.2f, RefugeeRules.RefugeeFraction(200f, 800f), 1e-5f);
            Assert.AreEqual(0f, RefugeeRules.RefugeeFraction(0f, 0f), 1e-5f); // 双方ゼロ＝0
            Assert.AreEqual(1f, RefugeeRules.RefugeeFraction(100f, 0f), 1e-5f);
        }

        [Test]
        public void HostBurden_EasedByIntegration()
        {
            // 難民比0.2×未統合1.0×0.5=0.1
            Assert.AreEqual(0.1f, RefugeeRules.HostBurden(200f, 800f, 0f, P), 1e-5f);
            // 半分溶け込めば負担半減
            Assert.AreEqual(0.05f, RefugeeRules.HostBurden(200f, 800f, 0.5f, P), 1e-5f);
            // 完全統合＝負担消滅
            Assert.AreEqual(0f, RefugeeRules.HostBurden(200f, 800f, 1f, P), 1e-5f);
        }

        [Test]
        public void IntegrationTick_AdvancesAndClamps()
        {
            Assert.AreEqual(0.5f, RefugeeRules.IntegrationTick(0f, 10f, P), 1e-5f); // 0.05×10
            Assert.AreEqual(1f, RefugeeRules.IntegrationTick(0.9f, 100f, P), 1e-5f); // 上限1
        }

        [Test]
        public void CanReturn_AtSafetyThreshold()
        {
            Assert.IsTrue(RefugeeRules.CanReturn(0.7f, P));  // 閾値ちょうど＝帰れる
            Assert.IsFalse(RefugeeRules.CanReturn(0.69f, P));
        }

        [Test]
        public void ReturnTick_OnlyUnintegratedReturn()
        {
            // 安全な故郷へ未統合の難民100×0.1×dt1=10 が帰る
            Assert.AreEqual(10f, RefugeeRules.ReturnTick(100f, 1f, 0f, 1f, P), 1e-4f);
            // 半分溶け込み＝帰還対象も半分
            Assert.AreEqual(5f, RefugeeRules.ReturnTick(100f, 1f, 0.5f, 1f, P), 1e-4f);
            // 故郷が危険＝誰も帰らない
            Assert.AreEqual(0f, RefugeeRules.ReturnTick(100f, 0.3f, 0f, 1f, P), 1e-5f);
            // 完全統合＝定住＝帰らない
            Assert.AreEqual(0f, RefugeeRules.ReturnTick(100f, 1f, 1f, 1f, P), 1e-5f);
        }
    }
}
