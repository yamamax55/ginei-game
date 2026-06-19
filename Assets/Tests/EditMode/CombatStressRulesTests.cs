using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    public class CombatStressRulesTests
    {
        const float Eps = 1e-4f;
        // Pow を含む箇所はわずかに緩める。
        const float PowEps = 1e-3f;

        [Test]
        public void 戦闘でストレスが溜まる()
        {
            // 0 から交戦強度1.0で10秒 → 3*1*10 = 30。
            float s = CombatStressRules.StressAccumulation(0f, 1f, 10f);
            Assert.AreEqual(30f, s, Eps);
        }

        [Test]
        public void 蓄積は上限でクランプされる()
        {
            // 大きな dt でも maxStress(100)を超えない。
            float s = CombatStressRules.StressAccumulation(90f, 1f, 1000f);
            Assert.AreEqual(100f, s, Eps);
        }

        [Test]
        public void 休息で回復する()
        {
            // 30 から休息品質1.0で4秒 → 30 - 5*1*4 = 10。
            float s = CombatStressRules.StressRecovery(30f, 1f, 4f);
            Assert.AreEqual(10f, s, Eps);
            // 過回復は0でクランプ。
            Assert.AreEqual(0f, CombatStressRules.StressRecovery(5f, 1f, 100f), Eps);
        }

        [Test]
        public void ストレスで能力が落ちる倍率()
        {
            Assert.AreEqual(1f, CombatStressRules.PerformanceDegradation(0f), Eps);
            Assert.AreEqual(0.5f, CombatStressRules.PerformanceDegradation(100f), Eps);
            // ratio=0.5, curve=0.5^1.5=0.353553, factor=1-0.5*0.353553=0.823223。
            Assert.AreEqual(0.823223f, CombatStressRules.PerformanceDegradation(50f), PowEps);
        }

        [Test]
        public void 高ストレスで判断ミスが増える()
        {
            Assert.AreEqual(0f, CombatStressRules.JudgmentImpairment(0f), Eps);
            Assert.AreEqual(0.6f, CombatStressRules.JudgmentImpairment(100f), Eps);
            // 単調増加：ストレスが高いほど判断ミス確率が高い。
            Assert.Greater(
                CombatStressRules.JudgmentImpairment(80f),
                CombatStressRules.JudgmentImpairment(40f));
        }

        [Test]
        public void 累積ストレスが士気を削る()
        {
            Assert.AreEqual(0.2f, CombatStressRules.MoralePenaltyFromStress(50f), Eps);
            Assert.AreEqual(0.4f, CombatStressRules.MoralePenaltyFromStress(100f), Eps);
            Assert.AreEqual(0f, CombatStressRules.MoralePenaltyFromStress(0f), Eps);
        }

        [Test]
        public void 歴戦兵はストレス耐性が高い()
        {
            Assert.AreEqual(0.5f, CombatStressRules.VeterancyResilience(0f), Eps);
            Assert.AreEqual(0.75f, CombatStressRules.VeterancyResilience(0.5f), Eps);
            Assert.AreEqual(1f, CombatStressRules.VeterancyResilience(1f), Eps);
        }

        [Test]
        public void 限界と戦闘疲労の判定()
        {
            // 新兵(耐性0.5→しきい値55)は95で限界、歴戦兵(耐性1.0→しきい値100)は95でまだ耐える。
            Assert.IsTrue(CombatStressRules.BreakingPoint(95f, CombatStressRules.VeterancyResilience(0f)));
            Assert.IsFalse(CombatStressRules.BreakingPoint(95f, CombatStressRules.VeterancyResilience(1f)));
            // 戦闘疲労（割合しきい値0.75=75）。
            Assert.IsTrue(CombatStressRules.IsCombatExhausted(80f, 0.75f));
            Assert.IsFalse(CombatStressRules.IsCombatExhausted(70f, 0.75f));
        }

        [Test]
        public void 物語_連戦でストレスが溜まり限界に至るが練度で耐える()
        {
            var p = CombatStressParams.Default;

            // 新兵と歴戦兵が同じ激戦（交戦強度1.0）を戦い続ける。
            float rookieStress = 0f;
            float veteranStress = 0f;
            float rookieRes = CombatStressRules.VeterancyResilience(0.0f); // 0.5
            float veteranRes = CombatStressRules.VeterancyResilience(1.0f); // 1.0

            // 連戦＝30秒ぶん戦闘（蓄積3/s → 90まで上昇）。
            for (int i = 0; i < 30; i++)
            {
                rookieStress = CombatStressRules.StressAccumulation(rookieStress, 1f, 1f, p);
                veteranStress = CombatStressRules.StressAccumulation(veteranStress, 1f, 1f, p);
            }
            Assert.AreEqual(90f, rookieStress, Eps);
            Assert.AreEqual(90f, veteranStress, Eps);

            // 能力が落ちている（無傷1.0より低い）。
            Assert.Less(CombatStressRules.PerformanceDegradation(rookieStress, p), 1f);
            // 判断ミス確率が積み上がっている。
            Assert.Greater(CombatStressRules.JudgmentImpairment(rookieStress, p), 0f);

            // この時点で新兵は限界（55しきい値超え）、歴戦兵はまだ戦える（100しきい値）。
            Assert.IsTrue(CombatStressRules.BreakingPoint(rookieStress, rookieRes, p));
            Assert.IsFalse(CombatStressRules.BreakingPoint(veteranStress, veteranRes, p));

            // 後退して休息（品質1.0で20秒）→ 90 - 5*20 = 0 まで回復。
            float rested = veteranStress;
            for (int i = 0; i < 20; i++)
                rested = CombatStressRules.StressRecovery(rested, 1f, 1f, p);
            Assert.AreEqual(0f, rested, Eps);

            // 休息後は能力が完全回復し、もう疲労状態でない。
            Assert.AreEqual(1f, CombatStressRules.PerformanceDegradation(rested, p), Eps);
            Assert.IsFalse(CombatStressRules.IsCombatExhausted(rested, 0.75f, p));
        }
    }
}
