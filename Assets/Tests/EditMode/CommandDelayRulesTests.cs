using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 指揮伝達遅延：規模・階層で伝達遅延が増え、妨害で膨らみ、指揮系統の質・分権で緩和、好機を逃す。
    /// 既定パラメータで期待値固定（線形/多項式ゆえ厳密一致）。
    /// </summary>
    public class CommandDelayRulesTests
    {
        const float Tol = 1e-4f;

        [Test]
        public void TransmissionDelay_GrowsWithSizeAndDepth()
        {
            // base0.5 + 10000*0.001(=10) + 3*0.5(=1.5) = 12.0
            Assert.AreEqual(12.0f, CommandDelayRules.TransmissionDelay(10000f, 3), Tol);
            // 規模0・階層0でも基礎遅延はかかる。
            Assert.AreEqual(0.5f, CommandDelayRules.TransmissionDelay(0f, 0), Tol);
        }

        [Test]
        public void TransmissionDelay_ClampedToMax()
        {
            // base0.5 + 100000*0.001(=100) → 上限60でクランプ。
            Assert.AreEqual(60f, CommandDelayRules.TransmissionDelay(100000f, 0), Tol);
        }

        [Test]
        public void JammingPenalty_IncreasesDelay()
        {
            // 12 * (1 + 0.5) = 18.0
            Assert.AreEqual(18.0f, CommandDelayRules.JammingPenalty(12f, 0.5f), Tol);
            // 妨害なしは不変。
            Assert.AreEqual(12.0f, CommandDelayRules.JammingPenalty(12f, 0f), Tol);
        }

        [Test]
        public void EffectiveDelay_ReducedByCommandQuality()
        {
            // gross=max(12,18)=18 ; relief=1-0.5*0.8=0.6 → 10.8
            Assert.AreEqual(10.8f, CommandDelayRules.EffectiveDelay(12f, 18f, 0.8f), Tol);
            // 質0なら短縮なし＝gross そのまま。
            Assert.AreEqual(18.0f, CommandDelayRules.EffectiveDelay(12f, 18f, 0f), Tol);
        }

        [Test]
        public void DecentralizationRelief_SoftensDelay()
        {
            // 10.8 * (1 - 0.6*1.0) = 4.32
            Assert.AreEqual(4.32f, CommandDelayRules.DecentralizationRelief(10.8f, 1.0f), Tol);
            // 集権（裁量0）は緩和なし。
            Assert.AreEqual(10.8f, CommandDelayRules.DecentralizationRelief(10.8f, 0f), Tol);
        }

        [Test]
        public void MissedOpportunity_FractionOfWindowLost()
        {
            // 4.32 / 10 = 0.432
            Assert.AreEqual(0.432f, CommandDelayRules.MissedOpportunity(4.32f, 10f), Tol);
            // 窓を超える遅れは全逃し（クランプ）。
            Assert.AreEqual(1.0f, CommandDelayRules.MissedOpportunity(15f, 10f), Tol);
            // 瞬間的好機（窓0）に遅れがあれば全逃し。
            Assert.AreEqual(1.0f, CommandDelayRules.MissedOpportunity(4.32f, 0f), Tol);
        }

        [Test]
        public void OrderObsolescence_AndParalysisThreshold()
        {
            // 4.32 * 0.1 = 0.432（届く頃には状況が変わっている度合い）。
            Assert.AreEqual(0.432f, CommandDelayRules.OrderObsolescence(4.32f, 0.1f), Tol);
            // 実効遅延が閾値（既定20）以上で麻痺。
            Assert.IsFalse(CommandDelayRules.IsCommandParalyzed(10.8f));
            Assert.IsTrue(CommandDelayRules.IsCommandParalyzed(25f));
        }

        [Test]
        public void Story_LargeFleetJammedMissesOpportunityButDecentralizationSaves()
        {
            // 大艦隊（規模20000・階層4）＝伝達遅延が膨らむ。
            float t = CommandDelayRules.TransmissionDelay(20000f, 4); // 0.5+20+2 = 22.5
            Assert.AreEqual(22.5f, t, Tol);

            // 強い通信妨害（0.8）でさらに膨らむ。
            float jammed = CommandDelayRules.JammingPenalty(t, 0.8f); // 22.5*1.8 = 40.5
            Assert.AreEqual(40.5f, jammed, Tol);

            // 凡庸な指揮系統（質0）＝実効遅延は妨害込みのまま。
            float eff = CommandDelayRules.EffectiveDelay(t, jammed, 0f); // 40.5
            Assert.AreEqual(40.5f, eff, Tol);

            // 反応が大きく遅れ、好機（窓10）を取りこぼし、指揮は麻痺する。
            float lag = CommandDelayRules.ReactionLag(eff); // 40.5
            Assert.AreEqual(1.0f, CommandDelayRules.MissedOpportunity(lag, 10f), Tol); // 完全に逃す
            Assert.IsTrue(CommandDelayRules.IsCommandParalyzed(eff)); // 麻痺

            // 分権（下級の裁量1.0＝任務戦術）で実効遅延を緩和すると麻痺を脱する。
            float relieved = CommandDelayRules.DecentralizationRelief(eff, 1.0f); // 40.5*0.4 = 16.2
            Assert.AreEqual(16.2f, relieved, Tol);
            Assert.IsFalse(CommandDelayRules.IsCommandParalyzed(relieved)); // 16.2 < 20 ＝麻痺せず
            // 広めの好機窓(20)で比べると、緩和後は取りこぼしが減る（未緩和は飽和して1.0のまま）。
            Assert.Less(CommandDelayRules.MissedOpportunity(relieved, 20f),
                        CommandDelayRules.MissedOpportunity(lag, 20f)); // 取りこぼしも減る
        }
    }
}
