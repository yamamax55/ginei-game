using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// ヴィルトゥーとフォルトゥーナ（力量と運命）の純ロジックのテスト（MKV-4 #1142）。
    /// 既定 Params（運命取り分0.5・備え緩和0.7・基準振れ0.2・不安定増幅0.6・好機下限0.1・制御閾値0.5）で期待値を固定。
    /// </summary>
    public class VirtuFortunaRulesTests
    {
        private const float Eps = 0.0001f;

        /// <summary>結果修正子＝運命の半分＋力量の半分。中庸（virtu0.5/fortuna0）は 0.5×0.5+0.5×0.5=0.5。</summary>
        [Test]
        public void OutcomeModifier_運命の半分と力量の半分を合成する()
        {
            // fortuna0 → 寄与0.5、virtu0.5 → 0.5*0.5 + 0.5*0.5 = 0.5
            Assert.AreEqual(0.5f, VirtuFortunaRules.OutcomeModifier(0.5f, 0f), Eps);
            // 凶運(fortuna-1=寄与0)でも高力量(virtu1)なら 0.5*0 + 0.5*1 = 0.5
            Assert.AreEqual(0.5f, VirtuFortunaRules.OutcomeModifier(1f, -1f), Eps);
            // 好運(fortuna+1=寄与1)＋高力量 → 0.5*1 + 0.5*1 = 1.0
            Assert.AreEqual(1f, VirtuFortunaRules.OutcomeModifier(1f, 1f), Eps);
        }

        /// <summary>備え（堤防）が運命の打撃を和らげる＝残る打撃の割合が下がる。備えと力量が高いほど被害が軽い。</summary>
        [Test]
        public void FortunePreparedness_備えが運命の打撃を和らげる()
        {
            // virtu0/foresight0 → 緩和なし＝残る打撃1.0
            Assert.AreEqual(1f, VirtuFortunaRules.FortunePreparedness(0f, 0f), Eps);
            // virtu1/foresight1 → defense1.0、緩和0.7×1=0.7、残る打撃0.3
            Assert.AreEqual(0.3f, VirtuFortunaRules.FortunePreparedness(1f, 1f), Eps);
            // 備えが厚いほど残る打撃は小さい（単調）
            float low = VirtuFortunaRules.FortunePreparedness(0.2f, 0.2f);
            float high = VirtuFortunaRules.FortunePreparedness(0.8f, 0.8f);
            Assert.Less(high, low);
        }

        /// <summary>好機を果断につかむ＝力量×好機で成功率が上がり、決定論 roll で判定する。</summary>
        [Test]
        public void SeizeOpportunity_力量と好機で果断につかむ()
        {
            // virtu1/opportunity1 → 成功率 0.1 + 0.9*1*1 = 1.0
            Assert.AreEqual(1f, VirtuFortunaRules.SeizeChance(1f, 1f), Eps);
            // virtu0/opportunity0 → 下限0.1
            Assert.AreEqual(0.1f, VirtuFortunaRules.SeizeChance(0f, 0f), Eps);
            // 高力量×熟した好機は roll0.5 で成功、低力量は失敗
            Assert.IsTrue(VirtuFortunaRules.SeizeOpportunity(0.9f, 0.9f, 0.5f));
            Assert.IsFalse(VirtuFortunaRules.SeizeOpportunity(0.2f, 0.2f, 0.5f));
        }

        /// <summary>運命の振れ幅＝不安定な時代ほど偶然が大きく振れる（安定で縮み不安定で増幅）。</summary>
        [Test]
        public void FortuneVolatility_不安定な時代ほど偶然が大きく振れる()
        {
            // stability1 → baseVolatility0.2 まで縮む
            Assert.AreEqual(0.2f, VirtuFortunaRules.FortuneVolatility(1f), Eps);
            // stability0 → 0.2 + 0.6*1 = 0.8
            Assert.AreEqual(0.8f, VirtuFortunaRules.FortuneVolatility(0f), Eps);
            // 不安定なほど振れは大きい（単調減少）
            Assert.Greater(VirtuFortunaRules.FortuneVolatility(0.2f), VirtuFortunaRules.FortuneVolatility(0.8f));
        }

        /// <summary>時代の変化への適応＝激変期は力量の二乗が効き、凡庸は脱落する。</summary>
        [Test]
        public void AdaptationToTimes_激変期は力量の二乗が効く()
        {
            // 変化なし(0) → 力量どおり
            Assert.AreEqual(0.6f, VirtuFortunaRules.AdaptationToTimes(0.6f, 0f), Eps);
            // 激変(1) → virtu² = 0.36（凡庸は大きく落ちる）
            Assert.AreEqual(0.36f, VirtuFortunaRules.AdaptationToTimes(0.6f, 1f), Eps);
            // 高力量は激変でも残る（0.9²=0.81）
            Assert.AreEqual(0.81f, VirtuFortunaRules.AdaptationToTimes(0.9f, 1f), Eps);
        }

        /// <summary>果断と慎重の時代相性＝順風では果断が報われ、逆風では慎重が守る。</summary>
        [Test]
        public void BoldnessVsCaution_順風は果断逆風は慎重が報われる()
        {
            // 全力果断(1)＋順風(+1) → 1.0
            Assert.AreEqual(1f, VirtuFortunaRules.BoldnessVsCaution(1f, 1f), Eps);
            // 全力果断(1)＋逆風(-1) → boldReward -1、守りなし → -1.0
            Assert.AreEqual(-1f, VirtuFortunaRules.BoldnessVsCaution(1f, -1f), Eps);
            // 全力慎重(0)＋逆風(-1) → 守り (1-0)*1 = 1.0（慎重が逆風を守る）
            Assert.AreEqual(1f, VirtuFortunaRules.BoldnessVsCaution(0f, -1f), Eps);
        }

        /// <summary>逆境の回復力＝負の運命を力量で跳ね返す。好運なら被害なし＝1.0。</summary>
        [Test]
        public void ResilienceToMisfortune_力量が凶運を跳ね返す()
        {
            // 好運(fortuna+0.5) → 1.0
            Assert.AreEqual(1f, VirtuFortunaRules.ResilienceToMisfortune(0.5f, 0.5f), Eps);
            // 凶運(-1)＋力量0 → 1 - 1*(1-0) = 0
            Assert.AreEqual(0f, VirtuFortunaRules.ResilienceToMisfortune(0f, -1f), Eps);
            // 凶運(-1)＋力量1 → 1 - 1*(1-1) = 1.0（力量が完全に跳ね返す）
            Assert.AreEqual(1f, VirtuFortunaRules.ResilienceToMisfortune(1f, -1f), Eps);
            // 同じ凶運でも力量が高いほど回復力が高い
            Assert.Greater(VirtuFortunaRules.ResilienceToMisfortune(0.8f, -1f),
                           VirtuFortunaRules.ResilienceToMisfortune(0.2f, -1f));
        }

        /// <summary>運命制御の判定＝凶運でも十分な力量があれば修正子が閾値を超え、運命をねじ伏せる。</summary>
        [Test]
        public void IsFortuneMastered_力量が運命をねじ伏せる()
        {
            // 凶運(-1)＋高力量(1) → 修正子0.5 ≥ 閾値0.5 → ねじ伏せた
            Assert.IsTrue(VirtuFortunaRules.IsFortuneMastered(1f, -1f));
            // 凶運(-1)＋低力量(0.2) → 修正子0.1 < 0.5 → ねじ伏せられない
            Assert.IsFalse(VirtuFortunaRules.IsFortuneMastered(0.2f, -1f));
            // 閾値を厳しくすれば同じ条件でも届かない
            Assert.IsFalse(VirtuFortunaRules.IsFortuneMastered(1f, -1f, 0.7f));
        }
    }
}
