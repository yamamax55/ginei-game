using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 寡頭支配と実権（#164）を固定する：肩書(形式権力)と実権(非公式影響力＋策謀)を分けて合成し、
    /// 「位は高いが操られる傀儡」「位は低いが実権を握る黒幕」を判定できること。境界・クランプ・各分岐。
    /// </summary>
    public class PowerRulesTests
    {
        // --- コンストラクタのクランプ（基準値の入口を固定） ---

        [Test]
        public void Ctor_ClampsValues()
        {
            var a = new PowerActor(-5, informalInfluence: 1.5f, intrigue: -0.2f);
            Assert.AreEqual(0, a.formalRank);          // 負の tier は 0 へ
            Assert.AreEqual(1f, a.informalInfluence);  // 1 超は 1 へ
            Assert.AreEqual(0f, a.intrigue);           // 負は 0 へ
        }

        // --- FormalPower：正規化と上限クランプ ---

        [Test]
        public void FormalPower_NormalizesAndClamps()
        {
            var p = PowerParams.Default; // rankScale=10
            Assert.AreEqual(0.5f, PowerRules.FormalPower(new PowerActor(5), p), 1e-4f);  // 代表：5/10
            Assert.AreEqual(1f, PowerRules.FormalPower(new PowerActor(20), p), 1e-4f);   // 上限クランプ：20/10→1.0
            Assert.AreEqual(0f, PowerRules.FormalPower(null, p));                        // null 安全
        }

        // --- EffectiveInfluence：策謀で底上げ（基準非破壊）＋クランプ ---

        [Test]
        public void EffectiveInfluence_BoostedByIntrigue_AndClamped()
        {
            var p = PowerParams.Default; // intrigueBoost=0.3
            var a = new PowerActor(3, informalInfluence: 0.4f, intrigue: 0.5f);
            // 0.4 + 0.5*0.3 = 0.55
            Assert.AreEqual(0.55f, PowerRules.EffectiveInfluence(a, p), 1e-4f);
            Assert.AreEqual(0.4f, a.informalInfluence, 1e-4f); // 基準フィールドは非破壊

            // 上限クランプ：0.9 + 1.0*0.3 = 1.2 → 1.0
            var hi = new PowerActor(3, informalInfluence: 0.9f, intrigue: 1f);
            Assert.AreEqual(1f, PowerRules.EffectiveInfluence(hi, p), 1e-4f);
        }

        // --- EffectivePower：形式と実権の大きい方 ---

        [Test]
        public void EffectivePower_TakesMaxOfFormalAndInfluence()
        {
            var p = PowerParams.Default;
            // 形式優位：tier10→1.0 が実権0.2 を上回る
            var formal = new PowerActor(10, informalInfluence: 0.2f);
            Assert.AreEqual(1f, PowerRules.EffectivePower(formal, p), 1e-4f);

            // 実権優位：tier1(=0.1)より実効影響力0.8 が勝る
            var shadow = new PowerActor(1, informalInfluence: 0.8f);
            Assert.AreEqual(0.8f, PowerRules.EffectivePower(shadow, p), 1e-4f);

            Assert.AreEqual(0f, PowerRules.EffectivePower(null));
        }

        // --- IsPuppet：肩書高・実権低の分岐 ---

        [Test]
        public void IsPuppet_HighRankLowInfluence()
        {
            // 肩書最高位だが実権ほぼ無し＝傀儡
            var puppet = new PowerActor(10, informalInfluence: 0.1f, intrigue: 0f);
            Assert.IsTrue(PowerRules.IsPuppet(puppet));

            // 肩書も実権も高い＝傀儡でない
            var strong = new PowerActor(10, informalInfluence: 0.8f);
            Assert.IsFalse(PowerRules.IsPuppet(strong));

            // 肩書が低い＝そもそも傀儡の条件を満たさない
            var lowRank = new PowerActor(1, informalInfluence: 0.1f);
            Assert.IsFalse(PowerRules.IsPuppet(lowRank));

            Assert.IsFalse(PowerRules.IsPuppet(null));
        }

        // --- IsShadowRuler：形式下位なのに実権最高 ---

        [Test]
        public void IsShadowRuler_LowRankButHighestPower()
        {
            // 黒幕：肩書は低い(tier2)が実権は最高（0.6+0.4*0.3=0.72）
            var shadow = new PowerActor(2, informalInfluence: 0.6f, intrigue: 0.4f);
            // 飾りの最高位：肩書は高い(tier8→形式0.8)が実権は低い（傀儡）
            var figurehead = new PowerActor(8, informalInfluence: 0.1f);
            var minor = new PowerActor(3, informalInfluence: 0.2f);
            var others = new List<PowerActor> { shadow, figurehead, minor };

            Assert.IsTrue(PowerRules.IsShadowRuler(shadow, others));    // 形式下位×実権最高＝黒幕
            Assert.IsFalse(PowerRules.IsShadowRuler(figurehead, others)); // 形式最高位は黒幕でない
        }

        [Test]
        public void IsShadowRuler_FalseWhenPeerHasEqualPower_OrNullArgs()
        {
            // 実権で並ぶ者が居れば黒幕でない
            var a = new PowerActor(2, informalInfluence: 0.8f);
            var rival = new PowerActor(10, informalInfluence: 0.8f);
            var others = new List<PowerActor> { a, rival };
            Assert.IsFalse(PowerRules.IsShadowRuler(a, others));

            // 形式上位が居なければ（自分が最高位）黒幕でない
            var top = new PowerActor(10, informalInfluence: 0.9f);
            var below = new PowerActor(1, informalInfluence: 0.2f);
            Assert.IsFalse(PowerRules.IsShadowRuler(top, new List<PowerActor> { top, below }));

            // null 安全
            Assert.IsFalse(PowerRules.IsShadowRuler(null, others));
            Assert.IsFalse(PowerRules.IsShadowRuler(a, null));
        }
    }
}
