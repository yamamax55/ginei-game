using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>艦隊演習（NavalDrill）の純ロジック・EditModeテスト。既定Params期待値を手計算で固定。</summary>
    public class NavalDrillRulesTests
    {
        const float Eps = 1e-4f;

        [Test]
        public void DrillIntensity_DefaultParams_WeightedAverage()
        {
            // wsum=1.0 → (0.5*0.8 + 0.5*0.6)/1.0 = 0.7
            Assert.AreEqual(0.7f, NavalDrillRules.DrillIntensity(0.8f, 0.6f), Eps);
        }

        [Test]
        public void DrillIntensity_ClampsInputs()
        {
            // 規模/頻度ともにフル → 1.0、負入力は0扱い
            Assert.AreEqual(1f, NavalDrillRules.DrillIntensity(5f, 2f), Eps);
            Assert.AreEqual(0f, NavalDrillRules.DrillIntensity(-1f, -1f), Eps);
        }

        [Test]
        public void RealismFactor_DefaultParams_WeightedAverage()
        {
            // (0.5*0.6 + 0.5*0.4)/1.0 = 0.5
            Assert.AreEqual(0.5f, NavalDrillRules.RealismFactor(0.6f, 0.4f), Eps);
        }

        [Test]
        public void SkillGain_HighProficiency_Diminishes()
        {
            // 強度0.7×実戦0.5×(1-0.2)×0.5 = 0.14
            Assert.AreEqual(0.14f, NavalDrillRules.SkillGain(0.7f, 0.5f, 0.2f), Eps);
            // 同条件でも現練度が高いほど伸びは鈍化する
            float low = NavalDrillRules.SkillGain(0.7f, 0.5f, 0.2f);
            float high = NavalDrillRules.SkillGain(0.7f, 0.5f, 0.9f);
            Assert.Less(high, low);
            // 練度満タンなら向上ゼロ
            Assert.AreEqual(0f, NavalDrillRules.SkillGain(1f, 1f, 1f), Eps);
        }

        [Test]
        public void FleetCoordination_LargerFleet_HarderToMaster()
        {
            // 強度0.8/(1+0.25*4)=0.8/2.0=0.4
            Assert.AreEqual(0.4f, NavalDrillRules.FleetCoordination(0.8f, 4), Eps);
            // 部隊数0なら分母1＝強度そのまま
            Assert.AreEqual(0.8f, NavalDrillRules.FleetCoordination(0.8f, 0), Eps);
            // 大編成ほど習熟は鈍る（単調減少）
            Assert.Less(NavalDrillRules.FleetCoordination(0.8f, 8),
                        NavalDrillRules.FleetCoordination(0.8f, 2));
        }

        [Test]
        public void DrillAttrition_ProportionalToIntensity()
        {
            // 0.6*0.4 = 0.24
            Assert.AreEqual(0.24f, NavalDrillRules.DrillAttrition(0.6f), Eps);
            Assert.AreEqual(0f, NavalDrillRules.DrillAttrition(0f), Eps);
        }

        [Test]
        public void WeaknessExposure_RealisticDrillsRevealMore()
        {
            // 0.5*0.6 = 0.3
            Assert.AreEqual(0.3f, NavalDrillRules.WeaknessExposure(0.5f), Eps);
            // 実戦度が高いほど露呈は増える
            Assert.Greater(NavalDrillRules.WeaknessExposure(0.9f),
                           NavalDrillRules.WeaknessExposure(0.3f));
        }

        [Test]
        public void OvertrainingFatigue_RestRelieves()
        {
            // 0.8*(1-0.25)*0.5 = 0.3
            Assert.AreEqual(0.3f, NavalDrillRules.OvertrainingFatigue(0.8f, 0.25f), Eps);
            // 十分な休養なら疲弊ゼロ
            Assert.AreEqual(0f, NavalDrillRules.OvertrainingFatigue(0.8f, 1f), Eps);
            // 休養が少ないほど疲弊は増える
            Assert.Greater(NavalDrillRules.OvertrainingFatigue(0.8f, 0f),
                           NavalDrillRules.OvertrainingFatigue(0.8f, 0.5f));
        }

        [Test]
        public void NetTrainingValue_BalancesGainAndCost()
        {
            // 0.14 - 0.24 - 0.3 = -0.4
            Assert.AreEqual(-0.4f, NavalDrillRules.NetTrainingValue(0.14f, 0.24f, 0.3f), Eps);
            // 消耗・疲弊ゼロなら向上ぶんがそのまま正の価値
            Assert.AreEqual(0.5f, NavalDrillRules.NetTrainingValue(0.5f, 0f, 0f), Eps);
            // -1..1 にクランプ（過大な消耗で下限）
            Assert.AreEqual(-1f, NavalDrillRules.NetTrainingValue(0f, 1f, 1f), Eps);
        }

        // ---- 物語テスト：精鋭艦隊の錬成と過訓練の逆効果 ----
        [Test]
        public void Narrative_QualityDrillsPayOff_OverdrillingBackfires()
        {
            // 小編成（2部隊）に対し、複雑な想定＋強力な対抗部隊で実戦的に、十分な休養を取りながら錬成する精鋭。
            // まだ伸びしろのある（低練度）艦隊に、ほどほどの強度で質の高い演習を施す＝消耗・疲弊を抑えつつ練度を伸ばす。
            float intensityElite = NavalDrillRules.DrillIntensity(0.5f, 0.4f);     // 規模・頻度は控えめ＝消耗を抑える
            float realismElite = NavalDrillRules.RealismFactor(1f, 1f);            // 最大の実戦度
            float gainElite = NavalDrillRules.SkillGain(intensityElite, realismElite, 0.1f); // 伸びしろ大
            float attritionElite = NavalDrillRules.DrillAttrition(intensityElite);
            float fatigueElite = NavalDrillRules.OvertrainingFatigue(0.4f, 1f);    // 十分な休養＝疲弊ゼロ
            float netElite = NavalDrillRules.NetTrainingValue(gainElite, attritionElite, fatigueElite);

            // 同じ艦隊が休みなく規模・頻度を最大で回し続ける（実戦度は形骸化して低い）過訓練。
            float intensityGrind = NavalDrillRules.DrillIntensity(1f, 1f);         // フル稼働
            float realismGrind = NavalDrillRules.RealismFactor(0.2f, 0.2f);        // 形だけの演習
            float gainGrind = NavalDrillRules.SkillGain(intensityGrind, realismGrind, 0.3f);
            float attritionGrind = NavalDrillRules.DrillAttrition(intensityGrind);
            float fatigueGrind = NavalDrillRules.OvertrainingFatigue(1f, 0f);      // 休養なし
            float netGrind = NavalDrillRules.NetTrainingValue(gainGrind, attritionGrind, fatigueGrind);

            // 質の高い演習は正味プラス、過訓練（消耗・疲弊過多）は正味マイナスで逆効果。
            Assert.Greater(netElite, 0f);
            Assert.Less(netGrind, 0f);
            Assert.Greater(netElite, netGrind);

            // 実戦的な精鋭演習は弱点もよく露呈する（改善の機会）。
            Assert.Greater(NavalDrillRules.WeaknessExposure(realismElite),
                           NavalDrillRules.WeaknessExposure(realismGrind));

            // 小編成のほうが連携習熟が高い（大編成ほど難しい）。
            float coordSmall = NavalDrillRules.FleetCoordination(intensityElite, 2);
            float coordLarge = NavalDrillRules.FleetCoordination(intensityElite, 12);
            Assert.Greater(coordSmall, coordLarge);
        }
    }
}
