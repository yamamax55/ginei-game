using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 戦後分割（分配の力学）を固定する：取り分の正当ラインは戦争貢献度、不足分が勝者間の不満、
    /// 住民を無視して机上で引いた線ほど恣意的、恣意的な線×抑圧された民族意識が将来紛争の火種、
    /// 取りすぎは敵対住民の占領負担、不満の集計が勝者連合の分裂の種。クランプを担保。
    /// </summary>
    public class PartitionRulesTests
    {
        private static readonly PartitionParams P = PartitionParams.Default;
        // 不満係数1.5/机上重み0.6/紛争リスク上限1.0/占領コスト係数1.2/分裂種係数0.5

        [Test]
        public void FairShare_TracksContribution()
        {
            Assert.AreEqual(0.6f, PartitionRules.FairShare(0.6f), 1e-5f); // 貢献に応じた正当ライン
            Assert.AreEqual(1f, PartitionRules.FairShare(2f), 1e-5f);     // クランプ上限
            Assert.AreEqual(0f, PartitionRules.FairShare(-1f), 1e-5f);    // クランプ下限
        }

        [Test]
        public void ShareGrievance_ShortfallFromFair()
        {
            // 貢献0.6に取り分0.4＝不足0.2×1.5＝0.3 の恨み
            Assert.AreEqual(0.3f, PartitionRules.ShareGrievance(0.4f, 0.6f, P), 1e-5f);
            // 貢献に見合う取り分＝不満ゼロ
            Assert.AreEqual(0f, PartitionRules.ShareGrievance(0.6f, 0.6f, P), 1e-5f);
            // もらいすぎは本人は不満を言わない（負の不足はゼロ）
            Assert.AreEqual(0f, PartitionRules.ShareGrievance(0.9f, 0.6f, P), 1e-5f);
            // 不足が大きいほど不満が増す（クランプ上限1）
            Assert.AreEqual(1f, PartitionRules.ShareGrievance(0f, 1f, P), 1e-5f);
        }

        [Test]
        public void PartitionLineArbitrariness_MapNotPeople()
        {
            // まとまった住民を机上で真っ二つ＝1×0.6 +(1−0.2)×0.4＝0.92 の高い恣意性
            Assert.AreEqual(0.92f, PartitionRules.PartitionLineArbitrariness(0.2f, 1f, P), 1e-5f);
            // 住民の輪郭に沿って引いた線＝恣意性ゼロ（民族整合1・机上度0）
            Assert.AreEqual(0f, PartitionRules.PartitionLineArbitrariness(1f, 0f, P), 1e-5f);
            // 机上度が高いほど恣意的（単調増）
            Assert.Greater(PartitionRules.PartitionLineArbitrariness(0.5f, 1f, P),
                           PartitionRules.PartitionLineArbitrariness(0.5f, 0f, P));
        }

        [Test]
        public void FutureConflictRisk_NeedsBothFactors()
        {
            // 恣意的な線0.92×抑圧された民族意識0.5＝0.46 の火種
            Assert.AreEqual(0.46f, PartitionRules.FutureConflictRisk(0.92f, 0.5f, P), 1e-5f);
            // 線が住民に沿っていれば（恣意性0）民族意識が高くても燃えない
            Assert.AreEqual(0f, PartitionRules.FutureConflictRisk(0f, 1f, P), 1e-5f);
            // 民族意識が眠っていれば（0）恣意的な線でもすぐには燃えない
            Assert.AreEqual(0f, PartitionRules.FutureConflictRisk(1f, 0f, P), 1e-5f);
            // 両方そろって初めて次の戦争の地図になる
            Assert.AreEqual(1f, PartitionRules.FutureConflictRisk(1f, 1f, P), 1e-5f);
        }

        [Test]
        public void OccupationCost_BigShareWithHostilesIsHeavy()
        {
            // 取り分0.5×敵対住民0.5×1.2＝0.3 の統治負担
            Assert.AreEqual(0.3f, PartitionRules.OccupationCost(0.5f, 0.5f, P), 1e-5f);
            // 取りすぎは消化不良＝取り分が大きいほど負担が重い（単調増）
            Assert.Greater(PartitionRules.OccupationCost(0.9f, 0.5f, P),
                           PartitionRules.OccupationCost(0.3f, 0.5f, P));
            // 従順な住民なら大きく取っても軽い
            Assert.AreEqual(0f, PartitionRules.OccupationCost(1f, 0f, P), 1e-5f);
        }

        [Test]
        public void VictorRivalrySeed_GrievancesFuelSplit()
        {
            // 最大の不満0.3 が主軸＋残りの不満(0.1)×0.5＝0.35 の分裂の種
            Assert.AreEqual(0.35f, PartitionRules.VictorRivalrySeed(new[] { 0.3f, 0.1f, 0f }, P), 1e-5f);
            // 全員満足なら連合は割れない
            Assert.AreEqual(0f, PartitionRules.VictorRivalrySeed(new[] { 0f, 0f, 0f }, P), 1e-5f);
            // 不満を持つ勝者が増えるほど割れやすい
            Assert.Greater(PartitionRules.VictorRivalrySeed(new[] { 0.3f, 0.3f, 0.3f }, P),
                           PartitionRules.VictorRivalrySeed(new[] { 0.3f, 0f, 0f }, P));
            // 空・null は種ゼロ
            Assert.AreEqual(0f, PartitionRules.VictorRivalrySeed(new float[0], P), 1e-5f);
            Assert.AreEqual(0f, PartitionRules.VictorRivalrySeed(null, P), 1e-5f);
        }

        [Test]
        public void Params_CtorClamps()
        {
            var p = new PartitionParams(-1f, 2f, 2f, -1f, -1f);
            Assert.AreEqual(0f, p.grievanceScale, 1e-5f);     // 非負
            Assert.AreEqual(1f, p.mapDrawnWeight, 1e-5f);     // 0..1
            Assert.AreEqual(1f, p.maxConflictRisk, 1e-5f);    // 0..1
            Assert.AreEqual(0f, p.occupationCostScale, 1e-5f); // 非負
            Assert.AreEqual(0f, p.rivalrySeedScale, 1e-5f);   // 非負
        }

        [Test]
        public void PartitionStory_DrawsNextWarMap()
        {
            // 住民を無視した分割（机上度1・民族非整合・抑圧された民族意識）は将来紛争を生み、
            // 住民に沿った公正な分割は火種を残さない＝「分割は次の戦争の地図を引く」
            float arbitrary = PartitionRules.PartitionLineArbitrariness(0.1f, 1f, P); // ≈0.96
            float coherent = PartitionRules.PartitionLineArbitrariness(1f, 0f, P);   // =0 完全に住民に沿う
            Assert.Greater(PartitionRules.FutureConflictRisk(arbitrary, 0.8f, P),
                           PartitionRules.FutureConflictRisk(coherent, 0.8f, P));
            // 完全に住民に沿った分割は民族意識が高くても火種ゼロ
            Assert.AreEqual(0f, PartitionRules.FutureConflictRisk(coherent, 0.8f, P), 1e-5f);
        }
    }
}
