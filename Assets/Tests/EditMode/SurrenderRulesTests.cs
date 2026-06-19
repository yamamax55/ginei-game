using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>降伏判断（#降伏）の純ロジック検証。既定 Params で期待値を固定。</summary>
    public class SurrenderRulesTests
    {
        const float Eps = 1e-4f;
        const float PowEps = 1e-3f; // Sqrt 箇所のみ緩め

        [Test]
        public void Hopelessness_WeightedAverage_OfThreeFactors()
        {
            // inferiority = 1 - min(1,0.2) = 0.8
            // hope = 0.8*0.5 + 0.9*0.3 + 0.8*0.2 = 0.4 + 0.27 + 0.16 = 0.83
            float hope = SurrenderRules.HopelessnessLevel(0.2f, 0.9f, 0.8f);
            Assert.AreEqual(0.83f, hope, Eps);
        }

        [Test]
        public void Hopelessness_EvenStrength_NoInferiorityTerm()
        {
            // 兵力比1.0以上で劣勢項=0、包囲0・補給0なら絶望0
            Assert.AreEqual(0f, SurrenderRules.HopelessnessLevel(1.0f, 0f, 0f), Eps);
            Assert.AreEqual(0f, SurrenderRules.HopelessnessLevel(2.0f, 0f, 0f), Eps);
        }

        [Test]
        public void SurrenderInclination_HighMorale_HalvesTendency()
        {
            // hope=0.8, morale=1.0 → 0.8*(1-0.5*1)=0.4
            Assert.AreEqual(0.4f, SurrenderRules.SurrenderInclination(0.8f, 100f), Eps);
            // morale=0 → 素通し
            Assert.AreEqual(0.8f, SurrenderRules.SurrenderInclination(0.8f, 0f), Eps);
        }

        [Test]
        public void HonorResistance_MajorPlusDampenedMinor()
        {
            // pride=0.2, cause=0.1 → major=0.2, minor=0.1 → 0.2 + 0.1*0.8*0.5 = 0.24
            Assert.AreEqual(0.24f, SurrenderRules.HonorResistance(20f, 10f), Eps);
            // 片方最大なら1.0、両方0なら0
            Assert.AreEqual(1.0f, SurrenderRules.HonorResistance(100f, 0f), Eps);
            Assert.AreEqual(0f, SurrenderRules.HonorResistance(0f, 0f), Eps);
        }

        [Test]
        public void QuarterOffered_GeometricMean_RequiresBoth()
        {
            // sqrt(0.8*0.7)=sqrt(0.56)=0.748331
            Assert.AreEqual(0.748331f, SurrenderRules.QuarterOffered(80f, 70f), PowEps);
            // 片方が0なら誘因も0
            Assert.AreEqual(0f, SurrenderRules.QuarterOffered(0f, 100f), Eps);
        }

        [Test]
        public void NetSurrender_HonorCutsThenQuarterLifts()
        {
            // incl=0.747, honor=0.24 → base=0.747*0.76=0.56772
            // quarter=0.748331 → net = 0.56772 + (1-0.56772)*0.748331
            //   = 0.56772 + 0.43228*0.748331 = 0.56772 + 0.323502 = 0.891222
            float net = SurrenderRules.NetSurrenderProbability(0.747f, 0.24f, 0.748331f);
            Assert.AreEqual(0.891222f, net, PowEps);
        }

        [Test]
        public void NetSurrender_FullHonor_NoSurrenderWithoutQuarter()
        {
            // honor=1 → base=0、quarter=0 → net=0
            Assert.AreEqual(0f, SurrenderRules.NetSurrenderProbability(0.9f, 1.0f, 0f), Eps);
        }

        [Test]
        public void LastStand_GeometricMean_OfHonorAndHopelessness()
        {
            // sqrt(0.81*0.83)... use clean: honor=0.5, hope=0.5 → sqrt(0.25)=0.5
            Assert.AreEqual(0.5f, SurrenderRules.LastStandResolve(0.5f, 0.5f), Eps);
            // 片方0なら死兵にならない
            Assert.AreEqual(0f, SurrenderRules.LastStandResolve(1.0f, 0f), Eps);
        }

        [Test]
        public void PrisonersTaken_StrengthTimesControl_Clamped()
        {
            Assert.AreEqual(800, SurrenderRules.PrisonersTaken(1000, 0.8f)); // 緩い制圧で取り逃がし
            Assert.AreEqual(1000, SurrenderRules.PrisonersTaken(1000, 1.0f)); // 完全制圧で全数
            Assert.AreEqual(0, SurrenderRules.PrisonersTaken(-50, 1.0f)); // 負値は0
        }

        [Test]
        public void DoesUnitSurrender_ThresholdGate()
        {
            Assert.IsTrue(SurrenderRules.DoesUnitSurrender(0.5f));   // 既定しきい値0.5
            Assert.IsTrue(SurrenderRules.DoesUnitSurrender(0.9f));
            Assert.IsFalse(SurrenderRules.DoesUnitSurrender(0.49f));
        }

        // ───────── 物語テスト ─────────

        [Test]
        public void Story_SurroundedStarvingUnit_OfferedQuarter_LaysDownArms()
        {
            // 包囲され補給も尽き、士気低く、指揮官の意地も大義も薄い部隊。
            // 敵将は信義に厚く寛大な助命を提示する。捕虜となって生き延びる道を選ぶ。
            var p = SurrenderParams.Default;
            float hope = SurrenderRules.HopelessnessLevel(0.2f, 0.9f, 0.8f, p);      // 0.83
            float incl = SurrenderRules.SurrenderInclination(hope, 20f);            // 0.83*0.9=0.747
            float honor = SurrenderRules.HonorResistance(20f, 10f);                 // 0.24
            float quarter = SurrenderRules.QuarterOffered(80f, 70f);                // 0.748331
            float net = SurrenderRules.NetSurrenderProbability(incl, honor, quarter);

            Assert.Greater(net, 0.8f, "助命を得た絶望部隊は高い確率で降伏する");
            Assert.IsTrue(SurrenderRules.DoesUnitSurrender(net), "部隊は降伏する");

            int prisoners = SurrenderRules.PrisonersTaken(5000, 0.9f);
            Assert.AreEqual(4500, prisoners, "制圧度0.9で5000中4500を捕虜化");

            // 同条件でも名誉の高い不屈の指揮官なら死兵となり降らない（対照）。
            float diehardHonor = SurrenderRules.HonorResistance(95f, 90f);
            float diehardNet = SurrenderRules.NetSurrenderProbability(incl, diehardHonor, quarter);
            float lastStand = SurrenderRules.LastStandResolve(diehardHonor, hope);
            Assert.Less(diehardNet, net, "不屈の指揮官は降伏傾きが低い");
            Assert.Greater(lastStand, 0.5f, "絶望下でも背水＝死兵となる");
        }
    }
}
