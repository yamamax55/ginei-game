using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>連鎖の脆さ（#1112）の純ロジック検証＝上流グルット/下流欠品カスケード・単一障害点・頑健性。</summary>
    public class ChainFragilityRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>上流グルット＝遮断で行き場を失った生産が貯蔵不能ぶん廃棄され在庫として残る（既定 spoilRate=0.5）。</summary>
        [Test]
        public void UpstreamGlut_StoresOverflowMinusSpoilage()
        {
            // overflow = 10-2 = 8、stored = 8×(1-0.5) = 4、×dt1 = 4
            float glut = ChainFragilityRules.UpstreamGlut(blockedThroughput: 2f, upstreamProduction: 10f, dt: 1f);
            Assert.AreEqual(4f, glut, Eps);
            // 上流生産が通過能力以下なら溢れない
            Assert.AreEqual(0f, ChainFragilityRules.UpstreamGlut(5f, 3f, 1f), Eps);
        }

        /// <summary>下流欠品＝需要に届かないぶんが操業停止として積み上がる（連鎖の起点）。</summary>
        [Test]
        public void DownstreamShortage_AccumulatesUnmetDemand()
        {
            // shortfall = 10-2 = 8、×dt1 = 8
            Assert.AreEqual(8f, ChainFragilityRules.DownstreamShortage(blockedThroughput: 2f, downstreamDemand: 10f, dt: 1f), Eps);
            // 通過量が需要を満たせば欠品なし
            Assert.AreEqual(0f, ChainFragilityRules.DownstreamShortage(10f, 8f, 1f), Eps);
        }

        /// <summary>バッファが薄いほどカスケードが深く伝播する（薄=即連鎖・厚=吸収して浅い）。</summary>
        [Test]
        public void CascadeDepth_ThinBufferPropagatesDeeper()
        {
            // 薄バッファ（buffer=0）：absorb=0、reach=0.8/0.05=16、ceil=16
            int thin = ChainFragilityRules.CascadeDepth(shortageSeverity: 0.8f, bufferStock: 0f);
            Assert.AreEqual(16, thin);
            // 厚バッファ（buffer=1）：absorb=0.25、reach=0.8/0.30=2.66…、ceil=3
            int thick = ChainFragilityRules.CascadeDepth(0.8f, 1f);
            Assert.AreEqual(3, thick);
            Assert.Greater(thin, thick, "バッファが薄いほど深く伝播する");
            // 欠品なしなら伝播しない
            Assert.AreEqual(0, ChainFragilityRules.CascadeDepth(0f, 0f));
        }

        /// <summary>単一障害点リスク＝代替の無いノードほど・連鎖が長いほど脆い。</summary>
        [Test]
        public void SinglePointRisk_RisesWithCriticalityAndLength()
        {
            // crit=1.0, len=3：lengthFactor=1-1/4=0.75
            Assert.AreEqual(0.75f, ChainFragilityRules.SinglePointRisk(1f, 3), Eps);
            // crit=0.5, len=1：lengthFactor=0.5、=0.25
            Assert.AreEqual(0.25f, ChainFragilityRules.SinglePointRisk(0.5f, 1), Eps);
            // 連鎖が長いほど単調増加
            Assert.Greater(ChainFragilityRules.SinglePointRisk(1f, 5), ChainFragilityRules.SinglePointRisk(1f, 1));
        }

        /// <summary>復旧時間＝深いカスケードほど・再起動コストが重いほど長い（既定 recoveryBase=2.0）。</summary>
        [Test]
        public void RecoveryTime_GrowsWithDepthAndRestartCost()
        {
            // depth=3, restart=1.0：2×3×(1+1)=12
            Assert.AreEqual(12f, ChainFragilityRules.RecoveryTime(3, 1f), Eps);
            // restart=0：2×3×1=6
            Assert.AreEqual(6f, ChainFragilityRules.RecoveryTime(3, 0f), Eps);
            // 段数0なら復旧なし
            Assert.AreEqual(0f, ChainFragilityRules.RecoveryTime(0, 1f), Eps);
        }

        /// <summary>頑健性＝冗長性と在庫が脆さを緩める（冗長0.6/在庫0.4の重み合成・両0は破断）。</summary>
        [Test]
        public void Resilience_RedundancyAndBufferEaseFragility()
        {
            Assert.AreEqual(0.6f, ChainFragilityRules.Resilience(redundancy: 1f, bufferStock: 0f), Eps);
            Assert.AreEqual(0.4f, ChainFragilityRules.Resilience(0f, 1f), Eps);
            Assert.AreEqual(1f, ChainFragilityRules.Resilience(1f, 1f), Eps);
            // 冗長も在庫も無ければ頑健性ゼロ＝最弱ノードで即破断
            Assert.AreEqual(0f, ChainFragilityRules.Resilience(0f, 0f), Eps);
        }

        /// <summary>1点の遮断が上流グルットと下流欠品を同時に生む＝連鎖の核を一度に担保。</summary>
        [Test]
        public void SingleBlock_GeneratesGlutAndShortageSimultaneously()
        {
            // 同じ遮断（throughput=2）で上流は溜まり（>0）下流は枯れる（>0）
            float glut = ChainFragilityRules.UpstreamGlut(2f, 10f, 1f);
            float shortage = ChainFragilityRules.DownstreamShortage(2f, 10f, 1f);
            Assert.Greater(glut, 0f, "上流はグルット");
            Assert.Greater(shortage, 0f, "下流は欠品");
        }
    }
}
