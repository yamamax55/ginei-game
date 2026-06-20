using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>兵力節用（二次正面は最小限で保持し浮いた戦力を主攻へ回す）純ロジックの EditMode テスト（既定 Params 期待値固定）。</summary>
    public class EconomyOfForceRulesTests
    {
        const float Eps = 1e-4f;

        [Test]
        public void 最小所要は脅威に比例し係数06倍()
        {
            // 100*0.6=60。脅威0は0、負入力もクランプで0。
            Assert.AreEqual(60f, EconomyOfForceRules.MinimumViableForce(100f), Eps);
            Assert.AreEqual(0f, EconomyOfForceRules.MinimumViableForce(0f), Eps);
            Assert.AreEqual(0f, EconomyOfForceRules.MinimumViableForce(-50f), Eps);
        }

        [Test]
        public void 最小を超えた分が主攻へ回せる浮き_最小未満は浮きゼロ()
        {
            Assert.AreEqual(40f, EconomyOfForceRules.EconomizedSurplus(100f, 60f), Eps);
            Assert.AreEqual(0f, EconomyOfForceRules.EconomizedSurplus(50f, 60f), Eps);
            Assert.AreEqual(0f, EconomyOfForceRules.EconomizedSurplus(60f, 60f), Eps);
        }

        [Test]
        public void 節約しすぎると二次正面崩れリスク_最小以上はゼロ()
        {
            // (60-30)/60=0.5 *1.0 = 0.5。
            Assert.AreEqual(0.5f, EconomyOfForceRules.SecondarySectorRisk(30f, 60f), Eps);
            // 割当が最小以上なら不足なし→0。
            Assert.AreEqual(0f, EconomyOfForceRules.SecondarySectorRisk(60f, 60f), Eps);
            Assert.AreEqual(0f, EconomyOfForceRules.SecondarySectorRisk(80f, 60f), Eps);
        }

        [Test]
        public void 浮いた戦力は効率08で主攻を充足_所要ゼロは満額()
        {
            // clamp01(40*0.8/50)=clamp01(0.64)=0.64。
            Assert.AreEqual(0.64f, EconomyOfForceRules.MainEffortReinforcement(40f, 50f), Eps);
            // 主攻所要が無ければ満額1.0。
            Assert.AreEqual(1f, EconomyOfForceRules.MainEffortReinforcement(40f, 0f), Eps);
            // 浮きが大きければ上限1.0。
            Assert.AreEqual(1f, EconomyOfForceRules.MainEffortReinforcement(200f, 50f), Eps);
        }

        [Test]
        public void 牽制は少数で敵を釘付け_圧倒で上限()
        {
            // clamp01(30*1.0/100)=0.3。
            Assert.AreEqual(0.3f, EconomyOfForceRules.HoldingActionEffectiveness(30f, 100f), Eps);
            // 牽制戦力が敵圧力を上回れば上限1.0。
            Assert.AreEqual(1f, EconomyOfForceRules.HoldingActionEffectiveness(200f, 100f), Eps);
        }

        [Test]
        public void 節約しすぎは二次正面が崩壊_適度なら持ちこたえる()
        {
            // 20 < 60*0.5=30 → 崩壊。
            Assert.IsTrue(EconomyOfForceRules.OvereconomyCollapse(20f, 60f));
            // 40 >= 30 → 崩壊しない。
            Assert.IsFalse(EconomyOfForceRules.OvereconomyCollapse(40f, 60f));
            // 最小所要が無い正面は崩壊しない。
            Assert.IsFalse(EconomyOfForceRules.OvereconomyCollapse(0f, 0f));
        }

        [Test]
        public void 配分バランスは主攻強化マイナス二次リスク_過小は負()
        {
            // 0.64-0.5=0.14（適正寄り）。
            Assert.AreEqual(0.14f, EconomyOfForceRules.ForceAllocationBalance(0.5f, 0.64f), Eps);
            // 節約しすぎ（リスク高・主攻弱）は負。
            Assert.AreEqual(-0.6f, EconomyOfForceRules.ForceAllocationBalance(0.8f, 0.2f), Eps);
        }

        [Test]
        public void うまく節用できているかは浮きとリスクで判定()
        {
            // 浮きあり＆リスク低→true。
            Assert.IsTrue(EconomyOfForceRules.IsEconomicallyAllocated(40f, 0.2f));
            // 浮きが無ければ主攻へ回せず false。
            Assert.IsFalse(EconomyOfForceRules.IsEconomicallyAllocated(0f, 0.2f));
            // リスクが既定上限0.3を超えると false。
            Assert.IsFalse(EconomyOfForceRules.IsEconomicallyAllocated(40f, 0.5f));
        }

        [Test]
        public void 物語_二次正面を最小限で保持し浮きを主攻へ回すが節約しすぎると崩れる()
        {
            // 脅威100の二次正面：最小所要60。
            float min = EconomyOfForceRules.MinimumViableForce(100f);
            Assert.AreEqual(60f, min, Eps);

            // 100割く→浮き40を主攻（所要50）へ回せば0.64充足・二次リスク0・うまく節用。
            float surplus = EconomyOfForceRules.EconomizedSurplus(100f, min);
            Assert.AreEqual(40f, surplus, Eps);
            float reinforce = EconomyOfForceRules.MainEffortReinforcement(surplus, 50f);
            Assert.AreEqual(0.64f, reinforce, Eps);
            float riskOk = EconomyOfForceRules.SecondarySectorRisk(100f, min);
            Assert.AreEqual(0f, riskOk, Eps);
            Assert.IsTrue(EconomyOfForceRules.IsEconomicallyAllocated(surplus, riskOk));
            Assert.IsFalse(EconomyOfForceRules.OvereconomyCollapse(100f, min));

            // だが20まで削る（節約しすぎ）と二次正面が崩れる：浮き0・リスク高・崩壊。
            float surplusThin = EconomyOfForceRules.EconomizedSurplus(20f, min);
            Assert.AreEqual(0f, surplusThin, Eps);
            float riskHigh = EconomyOfForceRules.SecondarySectorRisk(20f, min); // (60-20)/60≈0.6667
            Assert.AreEqual(0.6667f, riskHigh, 1e-3f);
            Assert.IsTrue(EconomyOfForceRules.OvereconomyCollapse(20f, min));
            Assert.IsFalse(EconomyOfForceRules.IsEconomicallyAllocated(surplusThin, riskHigh));
            // 節約過小で主攻も弱るバランスは負側。
            Assert.Less(EconomyOfForceRules.ForceAllocationBalance(riskHigh, 0f), 0f);
        }
    }
}
