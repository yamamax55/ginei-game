using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>PsyOpsRules（対敵心理戦）の純ロジック検証。既定 Params で期待値を固定。</summary>
    public class PsyOpsRulesTests
    {
        const float Eps = 1e-4f;

        [Test]
        public void PropagandaReach_IsolatedEnemyAmplifies()
        {
            // isolationGain=0.75; 0.8*1.0*0.75=0.6
            Assert.AreEqual(0.6f, PsyOpsRules.PropagandaReach(0.8f, 0.5f), Eps);
            // 完全孤立・満出力で満額
            Assert.AreEqual(1.0f, PsyOpsRules.PropagandaReach(1.0f, 1.0f), Eps);
            // 非孤立はゲイン半分
            Assert.AreEqual(0.5f, PsyOpsRules.PropagandaReach(1.0f, 0.0f), Eps);
        }

        [Test]
        public void MoraleErosion_ConvictionResists()
        {
            // resist=0.5; 0.6*0.8*0.5=0.24
            Assert.AreEqual(0.24f, PsyOpsRules.MoraleErosion(0.6f, 0.5f), Eps);
            // 信念が固ければ完全に抗う
            Assert.AreEqual(0f, PsyOpsRules.MoraleErosion(0.6f, 1.0f), Eps);
        }

        [Test]
        public void SurrenderInducement_HopelessnessDrives()
        {
            // 0.24*0.8*0.7=0.1344
            Assert.AreEqual(0.1344f, PsyOpsRules.SurrenderInducement(0.24f, 0.8f), Eps);
            // 絶望感ゼロなら投降を促さない
            Assert.AreEqual(0f, PsyOpsRules.SurrenderInducement(0.24f, 0f), Eps);
        }

        [Test]
        public void DefectionRate_LoyaltySuppresses()
        {
            // disloyal=0.8; 0.24*0.8*0.5=0.096
            Assert.AreEqual(0.096f, PsyOpsRules.DefectionRate(0.24f, 0.2f), Eps);
            // 忠誠が完全なら離反しない
            Assert.AreEqual(0f, PsyOpsRules.DefectionRate(0.24f, 1.0f), Eps);
        }

        [Test]
        public void Counterpropaganda_CohesionBlunts()
        {
            // 0.5*(1-0.7)=0.15
            Assert.AreEqual(0.15f, PsyOpsRules.Counterpropaganda(0.5f, 0.7f), Eps);
            // 鉄壁の結束は完全に打ち消す
            Assert.AreEqual(0f, PsyOpsRules.Counterpropaganda(0.5f, 1.0f), Eps);
        }

        [Test]
        public void BackfireFromHarshness_OnlyAboveThreshold()
        {
            // 穏当な勧告は逆効果なし
            Assert.AreEqual(0f, PsyOpsRules.BackfireFromHarshness(0.5f), Eps);
            // excess=(0.8-0.6)/0.4=0.5; 0.5*0.5=0.25
            Assert.AreEqual(0.25f, PsyOpsRules.BackfireFromHarshness(0.8f), Eps);
        }

        [Test]
        public void WillToFightReduction_BlendsErosionAndSurrender()
        {
            // 0.24*0.6 + 0.1344*0.4 = 0.19776
            Assert.AreEqual(0.19776f, PsyOpsRules.WillToFightReduction(0.24f, 0.1344f), Eps);
        }

        [Test]
        public void IsEnemyDemoralized_ThresholdGate()
        {
            Assert.IsTrue(PsyOpsRules.IsEnemyDemoralized(0.24f, 0.2f));
            Assert.IsFalse(PsyOpsRules.IsEnemyDemoralized(0.1f, 0.2f));
        }

        [Test]
        public void Params_ClampsOutOfRangeInputs()
        {
            var p = new PsyOpsParams(99f, -1f, 99f, -1f, 9f, 9f, 9f);
            Assert.AreEqual(2f, p.reachScale, Eps);
            Assert.AreEqual(0f, p.erosionScale, Eps);
            Assert.AreEqual(2f, p.surrenderScale, Eps);
            Assert.AreEqual(0f, p.defectionScale, Eps);
            Assert.AreEqual(1f, p.willWeight, Eps);
            Assert.AreEqual(1f, p.backfireScale, Eps);
            Assert.AreEqual(1f, p.coercionThreshold, Eps);
        }

        [Test]
        public void Story_IsolatedDispiritedGarrisonIsInducedToSurrender()
        {
            // 包囲され孤立した敵守備隊へ降伏勧告。出力1.0・孤立0.9で宣伝が深く届く。
            float reach = PsyOpsRules.PropagandaReach(1.0f, 0.9f);
            // 大義への信念は薄い（0.2）→士気がよく削れる。
            float erosion = PsyOpsRules.MoraleErosion(reach, 0.2f);
            // 包囲で絶望感が強い（0.9）→投降を強く促す。
            float surrender = PsyOpsRules.SurrenderInducement(erosion, 0.9f);
            float will = PsyOpsRules.WillToFightReduction(erosion, surrender);

            Assert.IsTrue(PsyOpsRules.IsEnemyDemoralized(erosion, 0.3f), "孤立・低信念なら戦意を挫けるはず");
            Assert.Greater(surrender, 0f, "絶望した守備隊は投降へ傾くはず");
            Assert.Greater(will, 0f, "敵全体の戦意が下がるはず");

            // 対照：高圧的すぎる強要(0.95)は逆に敵の結束を固める（背水の陣）。
            Assert.Greater(PsyOpsRules.BackfireFromHarshness(0.95f), 0f, "過度の強要は逆効果を生むはず");
        }
    }
}
