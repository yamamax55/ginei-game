using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// LegalGeneralityRules の純ロジック検証（HAYK-4 #1549）。法の支配指数・恣意性・法的安定性・
    /// 合意の侵食・抵抗権・予測可能性・法の前の平等・rule by law 判定を既定パラメータで固定する。
    /// </summary>
    public class LegalGeneralityRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>法の支配指数＝一般性×予測可能性×平等適用の積。一つ欠ければ痩せる。</summary>
        [Test]
        public void RuleOfLawIndex_一般性予測可能性平等適用の積()
        {
            // 0.8 * 0.5 * 1.0 = 0.4
            Assert.AreEqual(0.4f, LegalGeneralityRules.RuleOfLawIndex(0.8f, 0.5f, 1.0f), Eps);
            // どれか0なら法の支配は0
            Assert.AreEqual(0f, LegalGeneralityRules.RuleOfLawIndex(1.0f, 1.0f, 0f), Eps);
            // クランプ（過大入力でも1で頭打ち）
            Assert.AreEqual(1f, LegalGeneralityRules.RuleOfLawIndex(2f, 2f, 2f), Eps);
        }

        /// <summary>恣意性＝個別命令(0.6)×裁量(0.4)の加重和。</summary>
        [Test]
        public void ArbitrarinessLevel_個別命令と裁量の加重和()
        {
            // (1-0.4)*1.0 + 0.4*0.5 = 0.6 + 0.2 = 0.8
            Assert.AreEqual(0.8f, LegalGeneralityRules.ArbitrarinessLevel(1.0f, 0.5f), Eps);
            // 恣意ゼロ
            Assert.AreEqual(0f, LegalGeneralityRules.ArbitrarinessLevel(0f, 0f), Eps);
        }

        /// <summary>法的安定性＝法の支配指数そのまま（一般ルールが将来を読ませる）。</summary>
        [Test]
        public void LegalCertainty_法の支配を安定性に写す()
        {
            Assert.AreEqual(0.4f, LegalGeneralityRules.LegalCertainty(0.4f), Eps);
            Assert.AreEqual(1f, LegalGeneralityRules.LegalCertainty(1.5f), Eps);
            Assert.AreEqual(0f, LegalGeneralityRules.LegalCertainty(-0.3f), Eps);
        }

        /// <summary>合意の侵食＝恣意性×侵食率×dt。恣意的命令ほど合意が速く撤回される。</summary>
        [Test]
        public void ConsentErosion_恣意的命令が合意を時間で蝕む()
        {
            // 0.8 * 0.2 * 1.0 = 0.16
            Assert.AreEqual(0.16f, LegalGeneralityRules.ConsentErosion(0.8f, 1.0f), Eps);
            // dt が大きいほど多く蝕む（単調増加）
            float small = LegalGeneralityRules.ConsentErosion(0.8f, 0.5f);
            float large = LegalGeneralityRules.ConsentErosion(0.8f, 2.0f);
            Assert.Less(small, large);
            // dt 0 以下は侵食なし
            Assert.AreEqual(0f, LegalGeneralityRules.ConsentErosion(0.8f, 0f), Eps);
        }

        /// <summary>抵抗権＝恣意性が閾値(0.6)を超えると正当化される。</summary>
        [Test]
        public void ResistanceRight_恣意性が閾値超で発動()
        {
            Assert.IsTrue(LegalGeneralityRules.ResistanceRight(0.7f));   // 0.7 > 0.6
            Assert.IsFalse(LegalGeneralityRules.ResistanceRight(0.6f));  // 等しいだけでは発動しない
            Assert.IsFalse(LegalGeneralityRules.ResistanceRight(0.3f));
        }

        /// <summary>予測可能性ボーナス＝1+法の支配×上限0.4。高いほど取引・統治が円滑。</summary>
        [Test]
        public void PredictabilityBonus_法の予測可能性が効率を上げる()
        {
            // 1 + 1.0 * 0.4 = 1.4
            Assert.AreEqual(1.4f, LegalGeneralityRules.PredictabilityBonus(1.0f), Eps);
            // 法の支配0なら係数1（ボーナスなし）
            Assert.AreEqual(1.0f, LegalGeneralityRules.PredictabilityBonus(0f), Eps);
        }

        /// <summary>法の前の平等＝平等適用から特権ぶん(×0.5)を差し引く。特権が法の支配を損なう。</summary>
        [Test]
        public void EqualityBeforeLaw_特権が法の前の平等を損なう()
        {
            // 1.0 - 0.4*0.5 = 0.8
            Assert.AreEqual(0.8f, LegalGeneralityRules.EqualityBeforeLaw(1.0f, 0.4f), Eps);
            // 特権なしなら平等適用そのまま
            Assert.AreEqual(0.9f, LegalGeneralityRules.EqualityBeforeLaw(0.9f, 0f), Eps);
            // 大きな特権でも0で下げ止まる
            Assert.AreEqual(0f, LegalGeneralityRules.EqualityBeforeLaw(0.2f, 1.0f), Eps);
        }

        /// <summary>rule BY law＝一般性が低くかつ恣意性が閾値(0.7)超で「法を権力の道具に」堕したと判定。</summary>
        [Test]
        public void IsRuleByLaw_法による支配への堕落を判定()
        {
            // 恣意性0.8>0.7 かつ 一般性0.2<0.3 → rule BY law
            Assert.IsTrue(LegalGeneralityRules.IsRuleByLaw(0.2f, 0.8f));
            // 一般性が高ければ恣意性が高くても rule OF law 側に踏みとどまる
            Assert.IsFalse(LegalGeneralityRules.IsRuleByLaw(0.9f, 0.8f));
            // 恣意性が低ければ堕落していない
            Assert.IsFalse(LegalGeneralityRules.IsRuleByLaw(0.2f, 0.3f));
        }
    }
}
