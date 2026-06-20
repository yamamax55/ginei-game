using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>要人暗殺工作の純ロジック（盤面非依存）EditMode テスト。既定 Params で期待値を固定。</summary>
    public class AssassinationOperationRulesTests
    {
        const float Eps = 1e-4f;

        [Test]
        public void AttemptSuccess_SkillVsGuard_AndEdges()
        {
            // 80*1 / (80+40) = 2/3
            Assert.AreEqual(2f / 3f, AssassinationOperationRules.AttemptSuccess(80f, 40f, 1f), Eps);
            // 互角 50 vs 50 → 0.5
            Assert.AreEqual(0.5f, AssassinationOperationRules.AttemptSuccess(50f, 50f, 1f), Eps);
            Assert.AreEqual(1f, AssassinationOperationRules.AttemptSuccess(80f, 0f, 1f), Eps);   // 護衛皆無＝必中
            Assert.AreEqual(0f, AssassinationOperationRules.AttemptSuccess(0f, 40f, 1f), Eps);   // 技量なし
            Assert.AreEqual(0f, AssassinationOperationRules.AttemptSuccess(80f, 40f, 0f), Eps);  // 好機なし
        }

        [Test]
        public void TargetSurvival_RobustTargetSurvivesMore()
        {
            // 1 - 0.6/(1+1*1) = 1 - 0.3 = 0.7
            Assert.AreEqual(0.7f, AssassinationOperationRules.TargetSurvival(0.6f, 1f), Eps);
            // 頑健さゼロ・完全成功 → 死(0)
            Assert.AreEqual(0f, AssassinationOperationRules.TargetSurvival(1f, 0f), Eps);
            // 成功度ゼロ → 完全生存
            Assert.AreEqual(1f, AssassinationOperationRules.TargetSurvival(0f, 1f), Eps);
        }

        [Test]
        public void AssassinCapture_EscapeReducesResponseIncreases()
        {
            // base = 0.5+0.5*0.6 = 0.8、警備0・脱出0 → 0.8
            Assert.AreEqual(0.8f, AssassinationOperationRules.AssassinCapture(0.6f, 0f, 0f), Eps);
            // base=1.0、警備満点で *2 → clamp 1.0
            Assert.AreEqual(1f, AssassinationOperationRules.AssassinCapture(1f, 0f, 1f), Eps);
            // base=0.5、脱出計画満点 → 0.5*(1-0.5)=0.25
            Assert.AreEqual(0.25f, AssassinationOperationRules.AssassinCapture(0f, 1f, 0f), Eps);
        }

        [Test]
        public void MastermindExposure_PerfectSecurityHidesMastermind()
        {
            Assert.AreEqual(0.8f, AssassinationOperationRules.MastermindExposure(0.8f, 0f), Eps);  // 秘匿ゼロ＝露見
            Assert.AreEqual(0f, AssassinationOperationRules.MastermindExposure(0.8f, 1f), Eps);    // 秘匿完璧＝割れない
            Assert.AreEqual(0f, AssassinationOperationRules.MastermindExposure(0f, 0f), Eps);      // 捕縛なし
        }

        [Test]
        public void PoliticalChaos_VacantSuccessionAmplifies()
        {
            // 後継不在(0)：0.8*(1+1)/2 = 0.8
            Assert.AreEqual(0.8f, AssassinationOperationRules.PoliticalChaos(0.8f, 0f), Eps);
            // 後継明確(1)：0.8*1/2 = 0.4
            Assert.AreEqual(0.4f, AssassinationOperationRules.PoliticalChaos(0.8f, 1f), Eps);
            Assert.AreEqual(0f, AssassinationOperationRules.PoliticalChaos(0f, 0f), Eps);          // 重要度ゼロ
        }

        [Test]
        public void MartyrEffect_PopularTargetUnitesEnemies()
        {
            Assert.AreEqual(0.7f, AssassinationOperationRules.MartyrEffect(0.7f), Eps);
            Assert.AreEqual(0f, AssassinationOperationRules.MartyrEffect(0f), Eps);                // 無名＝殉教なし
        }

        [Test]
        public void NetStrategicGain_ChaosMinusMartyrAndExposure()
        {
            // 0.8 - 0.7 - 0 = 0.1
            Assert.AreEqual(0.1f, AssassinationOperationRules.NetStrategicGain(0.8f, 0.7f, 0f), Eps);
            // 0.8 - 0.2 - 0.1 = 0.5
            Assert.AreEqual(0.5f, AssassinationOperationRules.NetStrategicGain(0.8f, 0.2f, 0.1f), Eps);
            // 露見・殉教が混乱を食い潰す → 負
            Assert.AreEqual(-0.5f, AssassinationOperationRules.NetStrategicGain(0.3f, 0.5f, 0.3f), Eps);
        }

        [Test]
        public void IsAssassinationViable_NeedsSuccessAndGain()
        {
            // 既定閾値0.5：成功見込み十分＋価値0.5 → 実行
            Assert.IsTrue(AssassinationOperationRules.IsAssassinationViable(0.6f, 0.5f));
            // 価値が閾値未満 → 見送り
            Assert.IsFalse(AssassinationOperationRules.IsAssassinationViable(0.6f, 0.4f));
            // 成功見込みが下限(0.3)未満 → 価値が高くても論外
            Assert.IsFalse(AssassinationOperationRules.IsAssassinationViable(0.2f, 0.9f));
            // 明示閾値0.1で緩めれば通る
            Assert.IsTrue(AssassinationOperationRules.IsAssassinationViable(0.6f, 0.2f, 0.1f));
        }

        [Test]
        public void Story_StrikeWeaklyGuardedHeirlessFoe_ButPopularTargetBackfires()
        {
            // 好機をついて手薄な護衛の重鎮を狙う：技量80・護衛20・好機1.0
            float success = AssassinationOperationRules.AttemptSuccess(80f, 20f, 1f);     // 80/100 = 0.8
            Assert.AreEqual(0.8f, success, Eps);

            // 頑健さの低い標的はほぼ生き延びられない
            float survival = AssassinationOperationRules.TargetSurvival(success, 0f);     // 1 - 0.8 = 0.2
            Assert.AreEqual(0.2f, survival, Eps);

            // 脱出計画が甘く警備が固いと実行犯は捕まる
            float capture = AssassinationOperationRules.AssassinCapture(success, 0f, 1f); // (0.5+0.4)*(1+1)=1.8 → 1.0
            Assert.AreEqual(1f, capture, Eps);

            // 秘匿が甘ければ首謀者まで露見する
            float exposure = AssassinationOperationRules.MastermindExposure(capture, 0.2f); // 1*1*0.8 = 0.8
            Assert.AreEqual(0.8f, exposure, Eps);

            // 後継不在の重鎮を討てば混乱は大きい
            float chaos = AssassinationOperationRules.PoliticalChaos(1f, 0f);             // 1*(2)/2 = 1.0
            Assert.AreEqual(1f, chaos, Eps);

            // しかし人望厚い標的は殉教効果で敵を結束させる
            float martyr = AssassinationOperationRules.MartyrEffect(0.9f);                // 0.9
            Assert.AreEqual(0.9f, martyr, Eps);

            // 混乱(1.0)から殉教(0.9)・露見(0.8)を引くと割に合わない
            float gain = AssassinationOperationRules.NetStrategicGain(chaos, martyr, exposure); // 1-0.9-0.8 → -0.7
            Assert.AreEqual(-0.7f, gain, Eps);
            Assert.IsFalse(AssassinationOperationRules.IsAssassinationViable(success, gain)); // 見送り

            // 同じ標的でも秘匿万全・無名なら割に合う：露見0・殉教0で混乱まるごと利益
            float cleanExposure = AssassinationOperationRules.MastermindExposure(capture, 1f); // 0
            float noMartyr = AssassinationOperationRules.MartyrEffect(0f);                      // 0
            float cleanGain = AssassinationOperationRules.NetStrategicGain(chaos, noMartyr, cleanExposure); // 1.0
            Assert.AreEqual(1f, cleanGain, Eps);
            Assert.IsTrue(AssassinationOperationRules.IsAssassinationViable(success, cleanGain));
        }
    }
}
