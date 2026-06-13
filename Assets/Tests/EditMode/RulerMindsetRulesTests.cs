using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>元首の主観状態（HDRN-1 #1803・死の自覚/倦怠/遺産志向）の純ロジックを担保する。</summary>
    public class RulerMindsetRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>死の自覚＝発症年齢超過の加齢ぶん＋健康悪化。老いと病が重なるほど高い。</summary>
        [Test]
        public void MortalityAwareness_老いと病で高まる()
        {
            // 60歳・健康1.0：加齢(60-50)*0.02=0.2、病0 → 0.2
            Assert.AreEqual(0.2f, RulerMindsetRules.MortalityAwareness(60f, 1f), Eps);
            // 40歳（発症前）・健康1.0 → 0
            Assert.AreEqual(0f, RulerMindsetRules.MortalityAwareness(40f, 1f), Eps);
            // 50歳・健康0.5：加齢0、病(1-0.5)*0.6=0.3 → 0.3
            Assert.AreEqual(0.3f, RulerMindsetRules.MortalityAwareness(50f, 0.5f), Eps);
            // 70歳・健康0.5：加齢0.4＋病0.3 → 0.7
            Assert.AreEqual(0.7f, RulerMindsetRules.MortalityAwareness(70f, 0.5f), Eps);
        }

        /// <summary>統治への倦怠＝治世の長さ×0.03＋危機負荷×0.5。長く危機に追われるほど募る。</summary>
        [Test]
        public void GovernanceFatigue_長い治世と危機で募る()
        {
            Assert.AreEqual(0.3f, RulerMindsetRules.GovernanceFatigue(10f, 0f), Eps);
            Assert.AreEqual(0.5f, RulerMindsetRules.GovernanceFatigue(0f, 1f), Eps);
            Assert.AreEqual(0.8f, RulerMindsetRules.GovernanceFatigue(10f, 1f), Eps);
        }

        /// <summary>遺産志向＝死の自覚×未達成感。どちらか0なら駆動は生まれない。</summary>
        [Test]
        public void LegacyOrientation_終わりと未達成感の積()
        {
            Assert.AreEqual(0.4f, RulerMindsetRules.LegacyOrientation(0.8f, 0.5f), Eps);
            Assert.AreEqual(0f, RulerMindsetRules.LegacyOrientation(0.8f, 0f), Eps);
            Assert.AreEqual(0f, RulerMindsetRules.LegacyOrientation(0f, 0.5f), Eps);
        }

        /// <summary>後継への関心＝死の自覚×（後継ありで満額・不在は半分）。</summary>
        [Test]
        public void SuccessionFocus_後継の有無で増幅される()
        {
            Assert.AreEqual(0.8f, RulerMindsetRules.SuccessionFocus(0.8f, true), Eps);
            Assert.AreEqual(0.4f, RulerMindsetRules.SuccessionFocus(0.8f, false), Eps);
        }

        /// <summary>リスク選好シフト＝遺産志向の博打(+)と死の自覚の保身(-)の差し引き。</summary>
        [Test]
        public void RiskAppetiteShift_博打と保身の綱引き()
        {
            // 遺産1.0×0.6=0.6（攻め）− 死の自覚0.2×0.5=0.1 → +0.5
            Assert.AreEqual(0.5f, RulerMindsetRules.RiskAppetiteShift(0.2f, 1f), Eps);
            // 死の自覚1.0×0.5=0.5（守り）− 遺産0 → -0.5
            Assert.AreEqual(-0.5f, RulerMindsetRules.RiskAppetiteShift(1f, 0f), Eps);
            // 拮抗：0.5*0.6=0.3 − 0.5*0.5=0.25 → +0.05
            Assert.AreEqual(0.05f, RulerMindsetRules.RiskAppetiteShift(0.5f, 0.5f), Eps);
        }

        /// <summary>改革か現状維持か＝遺産志向(改革+)と倦怠(現状維持-)の綱引き。</summary>
        [Test]
        public void ReformVsConservatism_遺産は改革倦怠は維持()
        {
            Assert.AreEqual(0.5f, RulerMindsetRules.ReformVsConservatism(0.3f, 0.8f), Eps);
            Assert.AreEqual(-0.6f, RulerMindsetRules.ReformVsConservatism(0.8f, 0.2f), Eps);
        }

        /// <summary>倦怠下の判断の質＝1−倦怠×0.4の実効倍率（基準非破壊）。</summary>
        [Test]
        public void DecisionQualityUnderFatigue_倦怠で判断が雑になる()
        {
            Assert.AreEqual(0.8f, RulerMindsetRules.DecisionQualityUnderFatigue(0.5f), Eps);
            Assert.AreEqual(0.6f, RulerMindsetRules.DecisionQualityUnderFatigue(1f), Eps);
            Assert.AreEqual(1f, RulerMindsetRules.DecisionQualityUnderFatigue(0f), Eps);
        }

        /// <summary>引退の傾き＝死の自覚と倦怠の平均が押し、権力への執着が抗う。</summary>
        [Test]
        public void GracefulExitInclination_執着が引退を阻む()
        {
            // push=0.5*(0.8+0.6)=0.7、執着0 → 0.7
            Assert.AreEqual(0.7f, RulerMindsetRules.GracefulExitInclination(0.8f, 0.6f, 0f), Eps);
            // 執着1.0なら手放せない → 0
            Assert.AreEqual(0f, RulerMindsetRules.GracefulExitInclination(0.8f, 0.6f, 1f), Eps);
            // 執着0.5 → 0.35
            Assert.AreEqual(0.35f, RulerMindsetRules.GracefulExitInclination(0.8f, 0.6f, 0.5f), Eps);
        }

        /// <summary>遺産志向の判定＝既定閾値0.5以上で大事業に駆られた心境。</summary>
        [Test]
        public void IsLegacyDriven_閾値で判定()
        {
            Assert.IsTrue(RulerMindsetRules.IsLegacyDriven(0.6f));
            Assert.IsFalse(RulerMindsetRules.IsLegacyDriven(0.4f));
            Assert.IsTrue(RulerMindsetRules.IsLegacyDriven(0.5f)); // 境界は以上
        }

        /// <summary>RulerMindset はコンストラクタで全フィールドをクランプする。</summary>
        [Test]
        public void RulerMindset_コンストラクタでクランプ()
        {
            var m = new RulerMindset(1.5f, -0.2f, 2f, 0.7f);
            Assert.AreEqual(1f, m.mortalityAwareness, Eps);
            Assert.AreEqual(0f, m.governanceFatigue, Eps);
            Assert.AreEqual(1f, m.legacyOrientation, Eps);
            Assert.AreEqual(0.7f, m.successionFocus, Eps);
        }

        /// <summary>物語：老いて病んだ元首は死の自覚から遺産と後継を志向するが、権力への執着が引退を阻む。</summary>
        [Test]
        public void Story_老いた元首は遺産を志向するが執着が退場を阻む()
        {
            float mortality = RulerMindsetRules.MortalityAwareness(70f, 0.5f); // 0.7
            float legacy = RulerMindsetRules.LegacyOrientation(mortality, 0.8f); // 0.56
            float focus = RulerMindsetRules.SuccessionFocus(mortality, true); // 0.7
            // 死の自覚が遺産志向と後継への関心を生む
            Assert.IsTrue(RulerMindsetRules.IsLegacyDriven(legacy));
            Assert.Greater(focus, 0.5f);
            // 同じ心境でも、権力への執着が強いほど退場の傾きは小さい
            float fatigue = RulerMindsetRules.GovernanceFatigue(20f, 0.5f); // 0.85
            float exitClinging = RulerMindsetRules.GracefulExitInclination(mortality, fatigue, 0.9f);
            float exitDetached = RulerMindsetRules.GracefulExitInclination(mortality, fatigue, 0.1f);
            Assert.Less(exitClinging, exitDetached);
        }
    }
}
