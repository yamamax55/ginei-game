using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 回廊封鎖を固定する：戦力比で通過率が線形に締まり、需要を割れば備蓄が枯渇する。封鎖判定・突破成功率・
    /// 護衛ゼロの完全封鎖・封鎖側ゼロの素通りなどの境界を担保。
    /// </summary>
    public class BlockadeRulesTests
    {
        private static readonly BlockadeParams P = BlockadeParams.Default; // 開始比1.0/完全比3.0/突破基礎0.3

        [Test]
        public void Ratio_HandlesZeros()
        {
            Assert.AreEqual(0f, BlockadeRules.Ratio(0f, 100f), 1e-5f);          // 封鎖側なし＝0
            Assert.IsTrue(float.IsPositiveInfinity(BlockadeRules.Ratio(100f, 0f))); // 護衛なし＝無限大
            Assert.AreEqual(2f, BlockadeRules.Ratio(200f, 100f), 1e-5f);
        }

        [Test]
        public void Throughput_LinearChoke()
        {
            // 比1.0以下＝素通り
            Assert.AreEqual(1f, BlockadeRules.Throughput(100f, 100f, P), 1e-5f);
            // 比2.0＝閾値1と完全3の中間＝1−0.5=0.5
            Assert.AreEqual(0.5f, BlockadeRules.Throughput(200f, 100f, P), 1e-5f);
            // 比3.0以上＝完全封鎖
            Assert.AreEqual(0f, BlockadeRules.Throughput(300f, 100f, P), 1e-5f);
            Assert.AreEqual(0f, BlockadeRules.Throughput(500f, 100f, P), 1e-5f);
            // 護衛ゼロ＝完全封鎖
            Assert.AreEqual(0f, BlockadeRules.Throughput(50f, 0f, P), 1e-5f);
        }

        [Test]
        public void IsBlockaded_AboveThreshold()
        {
            Assert.IsFalse(BlockadeRules.IsBlockaded(100f, 100f, P)); // 比1.0＝閾値ちょうどは未成立
            Assert.IsTrue(BlockadeRules.IsBlockaded(150f, 100f, P));
            Assert.IsFalse(BlockadeRules.IsBlockaded(0f, 100f, P));   // 封鎖側なし
        }

        [Test]
        public void StockpileDelta_DepletesWhenStarved()
        {
            // 通過率0.5×供給100=50 配達、需要80＝−30/dt
            Assert.AreEqual(-30f, BlockadeRules.StockpileDelta(0.5f, 100f, 80f, 1f), 1e-4f);
            // 余剰なら増える
            Assert.AreEqual(20f, BlockadeRules.StockpileDelta(1f, 100f, 80f, 1f), 1e-4f);
        }

        [Test]
        public void RunnerSuccessChance_HarderWhenTightlyBlockaded()
        {
            float tight = BlockadeRules.RunnerSuccessChance(0f, 0f, P);   // 完全封鎖・低回避
            float loose = BlockadeRules.RunnerSuccessChance(1f, 0f, P);   // 緩い封鎖
            Assert.Less(tight, loose);
            // 回避が高いほど通りやすい
            float evasive = BlockadeRules.RunnerSuccessChance(0f, 1f, P);
            Assert.Greater(evasive, tight);
            // 0..1 クランプ
            Assert.GreaterOrEqual(tight, 0f);
            Assert.LessOrEqual(BlockadeRules.RunnerSuccessChance(1f, 1f, P), 1f);
        }
    }
}
