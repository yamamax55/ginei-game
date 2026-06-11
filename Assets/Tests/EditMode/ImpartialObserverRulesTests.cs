using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// 公平な観察者フィルター（TMS-2 #1582）の純ロジックテスト。
    /// 既定 ImpartialObserverParams（私利0.6/激情0.4/良心0.6/露出0.4/最小ブレーキ0.4/涵養0.1/衰え0.1）。
    /// </summary>
    public class ImpartialObserverRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>私利と激情が高いほど自己欺瞞バイアスが大きく、ゼロなら欺瞞もゼロ。</summary>
        [Test]
        public void SelfDeceptionBias_私利と激情で増える()
        {
            // 私利1・激情1 → 全重み発火＝1.0
            Assert.AreEqual(1f, ImpartialObserverRules.SelfDeceptionBias(1f, 1f), Eps);
            // 私利1・激情0 → 0.6/(0.6+0.4)=0.6
            Assert.AreEqual(0.6f, ImpartialObserverRules.SelfDeceptionBias(1f, 0f), Eps);
            // 無欲・冷静 → 0
            Assert.AreEqual(0f, ImpartialObserverRules.SelfDeceptionBias(0f, 0f), Eps);
        }

        /// <summary>良心と社会の目で観察者の強さが育つ（見られる意識が良心を育てる）。</summary>
        [Test]
        public void ObserverStrength_良心と社会の目で強まる()
        {
            // 良心1・露出1 → 1.0
            Assert.AreEqual(1f, ImpartialObserverRules.ObserverStrength(1f, 1f), Eps);
            // 良心1・露出0 → 0.6/(0.6+0.4)=0.6
            Assert.AreEqual(0.6f, ImpartialObserverRules.ObserverStrength(1f, 0f), Eps);
            // 良心0・露出1 → 0.4
            Assert.AreEqual(0.4f, ImpartialObserverRules.ObserverStrength(0f, 1f), Eps);
        }

        /// <summary>観察者が強いほど甘い自己評価を公平へ引き戻す（偏りが減る）。</summary>
        [Test]
        public void CorrectedJudgment_観察者が公平へ戻す()
        {
            // 自己評価0.8・欺瞞0.5・観察者0 → 補正なし＝0.8
            Assert.AreEqual(0.8f, ImpartialObserverRules.CorrectedJudgment(0.8f, 0.5f, 0f), Eps);
            // 観察者1.0 → 嵩上げ分(0.8*0.5=0.4)を全削り＝0.4
            Assert.AreEqual(0.4f, ImpartialObserverRules.CorrectedJudgment(0.8f, 0.5f, 1f), Eps);
            // 観察者0.5 → 0.8 - 0.4*0.5 = 0.6（部分補正）
            Assert.AreEqual(0.6f, ImpartialObserverRules.CorrectedJudgment(0.8f, 0.5f, 0.5f), Eps);
        }

        /// <summary>観察者が強いほど腐敗ブレーキが効く（1.0→minBrake へ・1.0以下）。</summary>
        [Test]
        public void CorruptionBrake_観察者が腐敗を遅らせる()
        {
            // 観察者0 → 減速なし＝1.0
            Assert.AreEqual(1f, ImpartialObserverRules.CorruptionBrake(0f), Eps);
            // 観察者1 → 最小ブレーキ0.4
            Assert.AreEqual(0.4f, ImpartialObserverRules.CorruptionBrake(1f), Eps);
            // 観察者0.5 → Lerp(1,0.4,0.5)=0.7
            Assert.AreEqual(0.7f, ImpartialObserverRules.CorruptionBrake(0.5f), Eps);
            // 常に1.0以下
            Assert.LessOrEqual(ImpartialObserverRules.CorruptionBrake(0.2f), 1f);
        }

        /// <summary>観察者が弱いと私利が自己合理化される（私利×(1-観察者)）。</summary>
        [Test]
        public void MoralRationalization_弱い観察者は私利を正当化()
        {
            // 私利1・観察者0 → 1.0（全面的に正当化）
            Assert.AreEqual(1f, ImpartialObserverRules.MoralRationalization(1f, 0f), Eps);
            // 私利1・観察者1 → 0（観察者が止める）
            Assert.AreEqual(0f, ImpartialObserverRules.MoralRationalization(1f, 1f), Eps);
            // 私利0.8・観察者0.5 → 0.4
            Assert.AreEqual(0.4f, ImpartialObserverRules.MoralRationalization(0.8f, 0.5f), Eps);
        }

        /// <summary>良心は道徳的実践で育ち腐敗圧で衰える。</summary>
        [Test]
        public void ConscienceTick_実践で育ち腐敗で衰える()
        {
            // 実践1・腐敗圧0・dt1 → 0.5 + 0.1 = 0.6
            Assert.AreEqual(0.6f, ImpartialObserverRules.ConscienceTick(0.5f, 1f, 0f, 1f), Eps);
            // 実践0・腐敗圧1・dt1 → 0.5 - 0.1 = 0.4
            Assert.AreEqual(0.4f, ImpartialObserverRules.ConscienceTick(0.5f, 0f, 1f, 1f), Eps);
            // 実践と腐敗圧が拮抗 → 不変
            Assert.AreEqual(0.5f, ImpartialObserverRules.ConscienceTick(0.5f, 1f, 1f, 1f), Eps);
            // dt<=0 → 不変
            Assert.AreEqual(0.5f, ImpartialObserverRules.ConscienceTick(0.5f, 1f, 0f, 0f), Eps);
        }

        /// <summary>観察者が私利に呑まれたら自己腐敗（私利が観察者を threshold 超過）。</summary>
        [Test]
        public void IsSelfCorrupted_観察者が私利に負ける()
        {
            // 私利0.9・観察者0.2・閾値0.3 → 0.7>0.3 ＝腐敗
            Assert.IsTrue(ImpartialObserverRules.IsSelfCorrupted(0.2f, 0.9f, 0.3f));
            // 私利0.5・観察者0.4・閾値0.3 → 0.1<=0.3 ＝健全
            Assert.IsFalse(ImpartialObserverRules.IsSelfCorrupted(0.4f, 0.5f, 0.3f));
            // 強い観察者は呑まれない
            Assert.IsFalse(ImpartialObserverRules.IsSelfCorrupted(0.9f, 0.9f, 0.3f));
        }

        /// <summary>実徳と虚栄（称賛に値することvs称賛されること）の乖離。</summary>
        [Test]
        public void PraiseworthinessVsPraise_実徳と虚栄の差()
        {
            // 実徳0.8・虚栄0.2 → +0.6（真の徳）
            Assert.AreEqual(0.6f, ImpartialObserverRules.PraiseworthinessVsPraise(0.8f, 0.2f), Eps);
            // 実徳0.2・虚栄0.9 → -0.7（称賛を求めるだけの虚栄）
            Assert.AreEqual(-0.7f, ImpartialObserverRules.PraiseworthinessVsPraise(0.2f, 0.9f), Eps);
            // 一致 → 0
            Assert.AreEqual(0f, ImpartialObserverRules.PraiseworthinessVsPraise(0.5f, 0.5f), Eps);
        }
    }
}
