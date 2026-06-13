using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>断言・反復・感染（CRWD-2 #1821・ル・ボン）の純ロジックを既定Paramsの具体値で固定するテスト。</summary>
    public class AssertionRepetitionRulesTests
    {
        const float Eps = 0.0001f;
        const float EpsPow = 0.001f;   // 除算/逓減級数のある式は緩める

        /// <summary>断言の強さ＝単純さ×確信×断言スケール1.0（既定）。</summary>
        [Test]
        public void AssertionStrength_tanjun_to_kakushin_no_seki()
        {
            // 0.8 × 0.7 × 1.0 = 0.56
            Assert.AreEqual(0.56f, AssertionRepetitionRules.AssertionStrength(0.8f, 0.7f), Eps);
            // クランプ（過大入力でも0..1）
            Assert.AreEqual(1f, AssertionRepetitionRules.AssertionStrength(2f, 2f), Eps);
            // 確信ゼロなら断言ゼロ
            Assert.AreEqual(0f, AssertionRepetitionRules.AssertionStrength(1f, 0f), Eps);
        }

        /// <summary>真理性の錯覚＝1−1/(1+0.3×r)。反復ゼロで0、回数とともに逓減的に上昇。</summary>
        [Test]
        public void IllusoryTruth_hanpuku_de_shinrisei_ga_agaru()
        {
            Assert.AreEqual(0f, AssertionRepetitionRules.IllusoryTruth(0), Eps);
            // r=3: 1 - 1/1.9 = 0.473684
            Assert.AreEqual(0.473684f, AssertionRepetitionRules.IllusoryTruth(3), EpsPow);
            // 単調増加かつ逓減（増分が小さくなる）
            float a = AssertionRepetitionRules.IllusoryTruth(1);
            float b = AssertionRepetitionRules.IllusoryTruth(2);
            float c = AssertionRepetitionRules.IllusoryTruth(3);
            Assert.Less(a, b);
            Assert.Less(b, c);
            Assert.Less(c - b, b - a, "逓減：後の反復ほど効果が小さい");
            // 負の回数は0扱い
            Assert.AreEqual(0f, AssertionRepetitionRules.IllusoryTruth(-5), Eps);
        }

        /// <summary>反復累積＝現在の被信度に断言×錯覚×残余×累積スケールを上乗せ。</summary>
        [Test]
        public void RepetitionAccumulation_hisindo_ga_tsumiagaru()
        {
            // cb0.2, as0.56, r3: illusory=0.473684; gain=0.56*0.473684*(0.8)*0.8=0.169768; →0.369768
            float acc = AssertionRepetitionRules.RepetitionAccumulation(0.2f, 0.56f, 3);
            Assert.AreEqual(0.369768f, acc, EpsPow);
            Assert.Greater(acc, 0.2f, "反復で被信度が増す");
            // 反復回数が多いほど累積が高い（単調）
            float few = AssertionRepetitionRules.RepetitionAccumulation(0.2f, 0.56f, 1);
            float many = AssertionRepetitionRules.RepetitionAccumulation(0.2f, 0.56f, 8);
            Assert.Greater(many, few);
        }

        /// <summary>飽和減衰＝飽和開始10回までは無減衰、超過分で頭打ち。</summary>
        [Test]
        public void SaturationDecay_hanpuku_shisugiru_to_atouchi()
        {
            Assert.AreEqual(1f, AssertionRepetitionRules.SaturationDecay(5), Eps);   // 飽和前は無減衰
            Assert.AreEqual(1f, AssertionRepetitionRules.SaturationDecay(10), Eps);  // 境界も無減衰
            Assert.AreEqual(0.5f, AssertionRepetitionRules.SaturationDecay(20), Eps); // 超過10で半減
            // 超過が増すほど減衰（単調減少）
            Assert.Less(AssertionRepetitionRules.SaturationDecay(30), AssertionRepetitionRules.SaturationDecay(20));
        }

        /// <summary>感染段階＝累積被信度が閾値超で入る（決定論bool）。</summary>
        [Test]
        public void ContagionThreshold_iki_koe_de_kansen_dankai()
        {
            Assert.IsTrue(AssertionRepetitionRules.ContagionThreshold(0.7f, 0.5f));
            Assert.IsFalse(AssertionRepetitionRules.ContagionThreshold(0.3f, 0.5f));
            Assert.IsFalse(AssertionRepetitionRules.ContagionThreshold(0.5f, 0.5f)); // 同値は不成立
        }

        /// <summary>感染伝播速度＝種被信度×結合度×被暗示性×感染スケール0.5（既定）。</summary>
        [Test]
        public void ContagionSpread_hito_kara_hito_e_hirogaru()
        {
            // 0.8 × 0.5 × 0.6 × 0.5 = 0.12
            Assert.AreEqual(0.12f, AssertionRepetitionRules.ContagionSpread(0.8f, 0.5f, 0.6f), Eps);
            // 結合度ゼロなら伝播ゼロ（孤立した群衆には広がらない）
            Assert.AreEqual(0f, AssertionRepetitionRules.ContagionSpread(0.8f, 0f, 0.6f), Eps);
        }

        /// <summary>反証耐性＝植え付いた観念ほど反証が効かず被信度が残る。</summary>
        [Test]
        public void CounterMessageResistance_shinjiru_hodo_kutsugaeranai()
        {
            // belief0.8, counter0.5: effective=0.5*(1-0.8*0.6)=0.26; 残存=0.74
            Assert.AreEqual(0.74f, AssertionRepetitionRules.CounterMessageResistance(0.8f, 0.5f), Eps);
            // 未植え付け(belief0)では反証がそのまま効く＝残存0.5
            Assert.AreEqual(0.5f, AssertionRepetitionRules.CounterMessageResistance(0f, 0.5f), Eps);
            // 強く信じるほど反証後の残存が高い（覆りにくい）
            Assert.Greater(AssertionRepetitionRules.CounterMessageResistance(0.9f, 0.5f),
                           AssertionRepetitionRules.CounterMessageResistance(0.3f, 0.5f));
        }

        /// <summary>観念植え付け判定＝累積被信度が閾値超で成立。</summary>
        [Test]
        public void IsImplanted_iki_koe_de_uetsuku()
        {
            Assert.IsTrue(AssertionRepetitionRules.IsImplanted(0.8f, 0.6f));
            Assert.IsFalse(AssertionRepetitionRules.IsImplanted(0.5f, 0.6f));
        }

        /// <summary>
        /// 物語：弱い断言を反復するほど被信度が累積し閾値を超えて感染へ至るが、反復しすぎると飽和して
        /// 浸透度はかえって頭打ちになる＝断言・反復・感染の三段とその限界。
        /// </summary>
        [Test]
        public void Monogatari_hanpuku_de_kansen_dakaga_hou_wa_houwa()
        {
            float assertion = AssertionRepetitionRules.AssertionStrength(0.9f, 0.9f); // 強い単純な断言

            // 反復を重ねて被信度を積み上げる（証明抜きでも繰り返しで信じ込む）
            float belief = 0.1f;
            for (int i = 1; i <= 6; i++)
                belief = AssertionRepetitionRules.RepetitionAccumulation(belief, assertion, i);

            // 累積被信度が感染閾値を超え、観念が植え付いて感染段階に入る
            Assert.IsTrue(AssertionRepetitionRules.IsImplanted(belief, 0.5f), "反復で観念が植え付く");
            Assert.IsTrue(AssertionRepetitionRules.ContagionThreshold(belief, 0.5f), "植え付いた観念は感染段階へ");

            // 感染速度は被信度が高いほど速い
            float spreadLow = AssertionRepetitionRules.ContagionSpread(0.3f, 0.7f, 0.7f);
            float spreadHigh = AssertionRepetitionRules.ContagionSpread(belief, 0.7f, 0.7f);
            Assert.Greater(spreadHigh, spreadLow, "信じる者が多いほど速く感染する");

            // しかし反復しすぎると飽和して浸透度が頭打ち＝適度な反復(3回)が飽和反復(25回)を上回る
            float penModerate = AssertionRepetitionRules.BeliefPenetration(assertion, 3, 1f);
            float penSaturated = AssertionRepetitionRules.BeliefPenetration(assertion, 25, 1f);
            Assert.Greater(penModerate, penSaturated, "反復しすぎは飽和して逆効果＝食傷");
        }
    }
}
