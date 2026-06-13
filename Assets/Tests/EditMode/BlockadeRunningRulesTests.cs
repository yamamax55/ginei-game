using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// 封鎖突破（ブロッケードランナー）純ロジックの EditMode テスト。既定 Params で期待値を固定する。
    /// </summary>
    public class BlockadeRunningRulesTests
    {
        const float Eps = 1e-4f;
        // Pow（sqrt）を含む箇所はわずかに緩める。
        const float PowEps = 1e-3f;

        [Test]
        public void RunnerSpeedEdge_FasterRunner_AboveNeutral()
        {
            // runner=10, blockader=6: rel=(10-6)/16=0.25, 0.5+0.5*0.25=0.625
            float edge = BlockadeRunningRules.RunnerSpeedEdge(10f, 6f);
            Assert.AreEqual(0.625f, edge, Eps);
        }

        [Test]
        public void RunnerSpeedEdge_EqualOrZero_Neutral()
        {
            Assert.AreEqual(0.5f, BlockadeRunningRules.RunnerSpeedEdge(8f, 8f), Eps);
            Assert.AreEqual(0.5f, BlockadeRunningRules.RunnerSpeedEdge(0f, 0f), Eps);
        }

        [Test]
        public void GapExploitation_ThinBlockadeStealthyRunner_Exploits()
        {
            // coverage=0.3 -> gap=0.7; stealth term=0.5+0.5*0.8=0.9; 0.7*0.9=0.63
            float gap = BlockadeRunningRules.GapExploitation(0.3f, 0.8f);
            Assert.AreEqual(0.63f, gap, Eps);
        }

        [Test]
        public void GapExploitation_FullCoverage_NoGap()
        {
            Assert.AreEqual(0f, BlockadeRunningRules.GapExploitation(1f, 1f), Eps);
        }

        [Test]
        public void InterceptionChance_ExploitedAndFast_Low()
        {
            // strength=1, gap=0.63, edge=0.625: 1*(1-0.63)*(1-0.625)=0.37*0.375=0.13875
            float ic = BlockadeRunningRules.InterceptionChance(1f, 0.63f, 0.625f);
            Assert.AreEqual(0.13875f, ic, Eps);
        }

        [Test]
        public void InterceptionChance_DenseNoEdge_Maxed()
        {
            // strength=1, gap=0, edge=0: 1*1*1=1
            float ic = BlockadeRunningRules.InterceptionChance(1f, 0f, 0f);
            Assert.AreEqual(1f, ic, Eps);
        }

        [Test]
        public void RunDamage_ScalesWithInterceptAndFirepower()
        {
            // 0.5 * 100 * damageScale(0.5) = 25
            Assert.AreEqual(25f, BlockadeRunningRules.RunDamage(0.5f, 100f), Eps);
            // 迎撃ゼロは無傷
            Assert.AreEqual(0f, BlockadeRunningRules.RunDamage(0f, 100f), Eps);
        }

        [Test]
        public void CargoDelivered_ProportionalToSuccess()
        {
            // 1000 * 0.63 = 630
            Assert.AreEqual(630f, BlockadeRunningRules.CargoDelivered(1000f, 0.63f), Eps);
            Assert.AreEqual(0f, BlockadeRunningRules.CargoDelivered(1000f, 0f), Eps);
        }

        [Test]
        public void BlockadeTightening_RepeatedRunsCloseTheGap()
        {
            // strength=0.5, attempts=3: 0.5 + 0.1*3 = 0.8
            Assert.AreEqual(0.8f, BlockadeRunningRules.BlockadeTightening(0.5f, 3), Eps);
            // 試行が増えれば 1 に張り付く
            Assert.AreEqual(1f, BlockadeRunningRules.BlockadeTightening(0.5f, 20), Eps);
        }

        [Test]
        public void Story_FastStealthyRunnerBreaksThroughThenBlockadeTightens()
        {
            // 速く隠密な突破船が、薄い封鎖の隙を突いて補給を届ける。
            float edge = BlockadeRunningRules.RunnerSpeedEdge(10f, 6f);      // 0.625
            float gap = BlockadeRunningRules.GapExploitation(0.3f, 0.8f);    // 0.63
            float ic = BlockadeRunningRules.InterceptionChance(1f, gap, edge); // 0.13875
            float success = BlockadeRunningRules.BreakoutSuccess(edge, gap, ic);

            // base=0.5*0.625+0.5*0.63=0.6275; sqrt=0.792149; *(1-0.13875)=0.682263
            Assert.AreEqual(0.682263f, success, PowEps);
            Assert.IsTrue(BlockadeRunningRules.IsBlockadeRun(success, 0.5f), "薄い封鎖は突破できる");

            float cargo = BlockadeRunningRules.CargoDelivered(1000f, success);
            Assert.AreEqual(682.263f, cargo, 1f);

            // だが突破を繰り返すと封鎖が締まる（網羅率↑）。
            float tightened = BlockadeRunningRules.BlockadeTightening(0.6f, 4); // 0.6+0.4=1.0
            Assert.AreEqual(1f, tightened, Eps);

            // 締まった封鎖（隙なし・速度優位だけ）では迎撃が跳ね上がり、突破は失敗側へ。
            float gap2 = BlockadeRunningRules.GapExploitation(tightened, 0.8f); // coverage=1 -> 0
            float ic2 = BlockadeRunningRules.InterceptionChance(1f, gap2, edge); // 1*(1-0)*(1-0.625)=0.375
            float success2 = BlockadeRunningRules.BreakoutSuccess(edge, gap2, ic2);
            // base=0.5*0.625+0=0.3125; sqrt=0.559017; *(1-0.375)=0.349386
            Assert.AreEqual(0.349386f, success2, PowEps);
            Assert.IsFalse(BlockadeRunningRules.IsBlockadeRun(success2, 0.5f), "締まった封鎖は突破できない");
            Assert.Less(success2, success, "封鎖が締まれば突破は難しくなる");
        }
    }
}
