using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 官僚制肥大（パーキンソンの法則）を固定する：定員は仕事量と無関係に自己増殖、管理コストは人数の二乗、
    /// 実効産出は適正規模を境に下がる山なりカーブ、肥大ほど・行革が遠いほど抵抗、行革は削減と混乱の交換。境界を担保。
    /// </summary>
    public class BureaucracyBloatRulesTests
    {
        private static readonly BureaucracyBloatRules.BureaucracyBloatParams P
            = BureaucracyBloatRules.BureaucracyBloatParams.Default;
        // 増殖0.06/年・調整1/3・1人産出1・最大削減0.5・混乱0.6・抵抗半値100・硬直20年

        [Test]
        public void HeadcountTick_GrowsRegardlessOfWorkload()
        {
            // 仕事量が何であれ同率で増える＝パーキンソン第一法則
            Assert.AreEqual(106f, BureaucracyBloatRules.HeadcountTick(100f, 100f, 1f, P), 1e-4f);
            Assert.AreEqual(106f, BureaucracyBloatRules.HeadcountTick(100f, 0f, 1f, P), 1e-4f);
            Assert.AreEqual(106f, BureaucracyBloatRules.HeadcountTick(100f, 9999f, 1f, P), 1e-4f);
        }

        [Test]
        public void AdminOverheadRatio_QuadraticInHeadcount()
        {
            Assert.AreEqual(1f / 3f, BureaucracyBloatRules.AdminOverheadRatio(100f, 100f, P), 1e-5f);
            // 定員倍増＝調整コスト4倍（二乗）→クランプ1.0
            Assert.AreEqual(1f, BureaucracyBloatRules.AdminOverheadRatio(200f, 100f, P), 1e-5f);
            // 仕事ゼロ＝全部オーバーヘッド
            Assert.AreEqual(1f, BureaucracyBloatRules.AdminOverheadRatio(50f, 0f, P), 1e-5f);
            Assert.AreEqual(0f, BureaucracyBloatRules.AdminOverheadRatio(0f, 100f, P), 1e-5f);
        }

        [Test]
        public void EffectiveOutput_HumpCurve()
        {
            float small = BureaucracyBloatRules.EffectiveOutput(80f, 100f, P);
            float peak = BureaucracyBloatRules.EffectiveOutput(100f, 100f, P);  // 適正規模
            float bloated = BureaucracyBloatRules.EffectiveOutput(200f, 100f, P);
            Assert.AreEqual(200f / 3f, peak, 1e-3f);   // 100×(1−1/3)=66.67
            Assert.AreEqual(0f, bloated, 1e-4f);        // 肥大の極み＝全員が会議＝産出0
            Assert.Greater(peak, small);                // 増員が効く領域
            Assert.Greater(peak, bloated);              // ピークを過ぎると下がる＝山なり
        }

        [Test]
        public void OptimalHeadcount_EqualsWorkloadByDefault()
        {
            Assert.AreEqual(100f, BureaucracyBloatRules.OptimalHeadcount(100f, P), 1e-4f); // 1人1仕事
            Assert.AreEqual(0f, BureaucracyBloatRules.OptimalHeadcount(0f, P), 1e-5f);
        }

        [Test]
        public void ReformResistance_HarderWhenBloatedAndOssified()
        {
            // 肥大(100)×硬直化満了(20年)＝0.5
            Assert.AreEqual(0.5f, BureaucracyBloatRules.ReformResistance(100f, 20f, P), 1e-5f);
            // 行革直後＝抵抗ゼロ（定期的な行革が安い）
            Assert.AreEqual(0f, BureaucracyBloatRules.ReformResistance(100f, 0f, P), 1e-5f);
            Assert.AreEqual(0f, BureaucracyBloatRules.ReformResistance(0f, 20f, P), 1e-5f);
        }

        [Test]
        public void ReformEffect_CutVsDisruption()
        {
            var hard = BureaucracyBloatRules.ReformEffect(100f, 1f, P);
            Assert.AreEqual(50f, hard.newHeadcount, 1e-4f);  // 最大削減0.5＝半減
            Assert.AreEqual(0.6f, hard.disruption, 1e-5f);   // 強く切るほど現場が止まる
            var none = BureaucracyBloatRules.ReformEffect(100f, 0f, P);
            Assert.AreEqual(100f, none.newHeadcount, 1e-4f);
            Assert.AreEqual(0f, none.disruption, 1e-5f);
        }
    }
}
