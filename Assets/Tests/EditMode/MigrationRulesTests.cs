using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 平時移民を固定する：経済×自由の格差が引力になり（負なら逆流）、国境の開き具合が流量を絞り、
    /// 引力が強いほど優秀層から先に出て（頭脳流出）、受け入れ側が質を得る。
    /// 国境を閉じれば流出は止まるが不満が中に溜まる。境界・クランプを担保。
    /// </summary>
    public class MigrationRulesTests
    {
        private static readonly MigrationParams P = MigrationParams.Default;
        // 経済重み0.6/自由重み0.4/最大流出率0.05/基礎優秀比0.2/頭脳偏り0.5/閉鎖不満0.5

        [Test]
        public void MigrationPull_CombinesProsperityAndFreedom()
        {
            Assert.AreEqual(1f, MigrationRules.MigrationPull(1f, 1f, P), 1e-5f);    // 双方最大＝引力1
            Assert.AreEqual(0.3f, MigrationRules.MigrationPull(0.5f, 0f, P), 1e-5f); // 経済だけ＝0.6×0.5
            Assert.AreEqual(0.4f, MigrationRules.MigrationPull(0f, 1f, P), 1e-5f);   // 自由だけ＝0.4
            Assert.AreEqual(-1f, MigrationRules.MigrationPull(-1f, -1f, P), 1e-5f);  // 自国が上＝逆流
            Assert.AreEqual(0f, MigrationRules.MigrationPull(0f, 0f, P), 1e-5f);     // 格差なし＝動機なし
        }

        [Test]
        public void MigrationPull_ClampsGapInputs()
        {
            // 入力 -1..1 を超えても引力は飽和する
            Assert.AreEqual(1f, MigrationRules.MigrationPull(10f, 10f, P), 1e-5f);
            Assert.AreEqual(-1f, MigrationRules.MigrationPull(-10f, -10f, P), 1e-5f);
        }

        [Test]
        public void FlowTick_ThrottledByBorderOpenness()
        {
            // 1000人×0.05×引力1×全開×dt1=50人が足で投票する
            Assert.AreEqual(50f, MigrationRules.FlowTick(1000f, 1f, 1f, 1f, P), 1e-4f);
            // 国境半開＝流量半分
            Assert.AreEqual(25f, MigrationRules.FlowTick(1000f, 1f, 0.5f, 1f, P), 1e-4f);
            // 国境閉鎖＝誰も動けない
            Assert.AreEqual(0f, MigrationRules.FlowTick(1000f, 1f, 0f, 1f, P), 1e-5f);
            // 引力が負＝流入（符号付き）
            Assert.AreEqual(-50f, MigrationRules.FlowTick(1000f, -1f, 1f, 1f, P), 1e-4f);
        }

        [Test]
        public void FlowTick_NeverExceedsPopulation()
        {
            // dt を巨大にしても人口以上は出ない
            Assert.AreEqual(100f, MigrationRules.FlowTick(100f, 1f, 1f, 1000f, P), 1e-4f);
            Assert.AreEqual(-100f, MigrationRules.FlowTick(100f, -1f, 1f, 1000f, P), 1e-4f);
        }

        [Test]
        public void BrainDrainRatio_TalentedLeaveFirst()
        {
            Assert.AreEqual(0.7f, MigrationRules.BrainDrainRatio(1f, P), 1e-5f);  // 引力最大＝0.2+0.5
            Assert.AreEqual(0.45f, MigrationRules.BrainDrainRatio(0.5f, P), 1e-5f);
            Assert.AreEqual(0.2f, MigrationRules.BrainDrainRatio(0f, P), 1e-5f);  // 引力なし＝基礎比率
            Assert.AreEqual(0.2f, MigrationRules.BrainDrainRatio(-1f, P), 1e-5f); // 流入側に偏りなし
        }

        [Test]
        public void TalentTransfer_ReceiverGainsQuality()
        {
            // 流入50人×優秀比0.7=35の質ボーナス
            Assert.AreEqual(35f, MigrationRules.TalentTransfer(50f, 0.7f), 1e-4f);
            // 流出側（負の flow）は受け取りゼロ
            Assert.AreEqual(0f, MigrationRules.TalentTransfer(-50f, 0.7f), 1e-5f);
        }

        [Test]
        public void ClosedBorderResentment_WallCompressesDiscontent()
        {
            // 出たい圧1×完全閉鎖×0.5=不満0.5（壁は不満を中に圧縮する）
            Assert.AreEqual(0.5f, MigrationRules.ClosedBorderResentment(1f, 0f, P), 1e-5f);
            // 半開なら不満半分
            Assert.AreEqual(0.25f, MigrationRules.ClosedBorderResentment(1f, 0.5f, P), 1e-5f);
            // 全開＝出たい者は出られる＝不満なし
            Assert.AreEqual(0f, MigrationRules.ClosedBorderResentment(1f, 1f, P), 1e-5f);
            // 出たい者がいない＝閉じても不満なし
            Assert.AreEqual(0f, MigrationRules.ClosedBorderResentment(-1f, 0f, P), 1e-5f);
        }
    }
}
