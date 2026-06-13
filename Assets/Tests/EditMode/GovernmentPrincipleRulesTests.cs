using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>政体の原動力＝原理（モンテスキュー・#1439）の純ロジックの担保。原理の強さ・服従コスト・原理の腐敗・政体の活力・原理のミスマッチ・恐怖の逓減・徳の持続可能性・政体腐敗判定。</summary>
    public class GovernmentPrincipleRulesTests
    {
        private const float Eps = 0.0001f;

        /// <summary>政体ごとに原理の情念が異なる＝共和政は徳・君主政は名誉・専制政は恐怖を主因にする。</summary>
        [Test]
        public void PrincipleStrength_政体ごとに情念が変わる()
        {
            // civicVirtue=0.9, honorCulture=0.5, fearLevel=0.4。
            float vertu = GovernmentPrincipleRules.PrincipleStrength(GovernmentPrinciple.徳, 0.9f, 0.5f, 0.4f);
            float honneur = GovernmentPrincipleRules.PrincipleStrength(GovernmentPrinciple.名誉, 0.9f, 0.5f, 0.4f);
            // 共和政は徳を主因＝0.9・君主政は名誉を主因＝0.5。
            Assert.AreEqual(0.9f, vertu, Eps);
            Assert.AreEqual(0.5f, honneur, Eps);
            // 専制政の恐怖は逓減を通す＝0.4×(1−0.6×0.4)＝0.4×0.76＝0.304。
            float crainte = GovernmentPrincipleRules.PrincipleStrength(GovernmentPrinciple.恐怖, 0.9f, 0.5f, 0.4f);
            Assert.AreEqual(0.304f, crainte, Eps);
        }

        /// <summary>原理が強いほど服従コストが低い＝原理が市民を自発的に従わせて統治が安くなる。</summary>
        [Test]
        public void ObedienceCost_原理が強いほど安い()
        {
            // 原理満点＝最小コスト0.2・原理皆無＝最大コスト0.9。
            Assert.AreEqual(0.2f, GovernmentPrincipleRules.ObedienceCost(1f), Eps);
            Assert.AreEqual(0.9f, GovernmentPrincipleRules.ObedienceCost(0f), Eps);
            // 強いほど単調に下がる＝0.5なら 0.9→0.2 の中点 0.55。
            Assert.AreEqual(0.55f, GovernmentPrincipleRules.ObedienceCost(0.5f), Eps);
            Assert.Greater(GovernmentPrincipleRules.ObedienceCost(0f),
                GovernmentPrincipleRules.ObedienceCost(1f));
        }

        /// <summary>原理が損なわれると政体が腐敗する＝侵食ぶん原理が削られ、恐怖は麻痺で速く損なわれる。</summary>
        [Test]
        public void PrincipleCorruption_侵食で原理が損なわれ恐怖は速い()
        {
            // 徳：strength=1, erosion=1, dt=1 ＝1 − 1×0.2×1×1.0＝0.8。
            float vertu = GovernmentPrincipleRules.PrincipleCorruption(GovernmentPrinciple.徳, 1f, 1f, 1f);
            Assert.AreEqual(0.8f, vertu, Eps);
            // 恐怖：麻痺で速い＝1 − 1×0.2×(1+0.6)×1＝1 − 0.32＝0.68。
            float crainte = GovernmentPrincipleRules.PrincipleCorruption(GovernmentPrinciple.恐怖, 1f, 1f, 1f);
            Assert.AreEqual(0.68f, crainte, Eps);
            Assert.Less(crainte, vertu);
            // 侵食が無ければ損なわれない。
            Assert.AreEqual(1f, GovernmentPrincipleRules.PrincipleCorruption(GovernmentPrinciple.徳, 1f, 0f, 1f), Eps);
        }

        /// <summary>政体の活力＝原理が強く法が原理に沿うほど高い（積で効く＝法が背けば活力なし）。</summary>
        [Test]
        public void RegimeVitality_原理と法の一致で活力()
        {
            // 原理1×法1＝1.0。
            Assert.AreEqual(1f, GovernmentPrincipleRules.RegimeVitality(1f, 1f), Eps);
            // 原理が強くても法が背けば活力は出ない（積）。
            Assert.AreEqual(0f, GovernmentPrincipleRules.RegimeVitality(1f, 0f), Eps);
            // 0.8×0.5＝0.4。
            Assert.AreEqual(0.4f, GovernmentPrincipleRules.RegimeVitality(0.8f, 0.5f), Eps);
        }

        /// <summary>原理のミスマッチ＝政体が要求する原理に社会のエートスが届かないほど機能不全。</summary>
        [Test]
        public void PrincipleMismatch_エートスがずれると機能不全()
        {
            // 専制に徳を求める＝徳のエートスが低い(0.1)とミスマッチ大＝1−0.1＝0.9。
            Assert.AreEqual(0.9f, GovernmentPrincipleRules.PrincipleMismatch(GovernmentPrinciple.徳, 0.1f), Eps);
            // エートスが要求に応える(1)ならミスマッチ無し＝0。
            Assert.AreEqual(0f, GovernmentPrincipleRules.PrincipleMismatch(GovernmentPrinciple.恐怖, 1f), Eps);
        }

        /// <summary>恐怖の逓減＝専制の恐怖は高水準で麻痺して効かなくなる（慣れ＝凹カーブ）。</summary>
        [Test]
        public void FearDiminishingReturns_高水準で麻痺する()
        {
            // 低水準は損が小さい＝0.2×(1−0.6×0.2)＝0.2×0.88＝0.176。
            Assert.AreEqual(0.176f, GovernmentPrincipleRules.FearDiminishingReturns(0.2f), Eps);
            // 高水準は慣れで目減り＝1.0×(1−0.6×1)＝0.4。
            Assert.AreEqual(0.4f, GovernmentPrincipleRules.FearDiminishingReturns(1f), Eps);
            // 恐怖を倍（0.2→0.4）にしても実効は倍にならない（逓減）＝f(0.4)=0.304 < f(0.2)×2=0.352。
            Assert.Less(GovernmentPrincipleRules.FearDiminishingReturns(0.4f),
                GovernmentPrincipleRules.FearDiminishingReturns(0.2f) * 2f);
        }

        /// <summary>共和政の徳の持続可能性＝平等が崩れる（不平等が増す）ほど維持しにくい。</summary>
        [Test]
        public void VirtueSustainability_平等が崩れると徳が痩せる()
        {
            // 不平等0なら徳そのまま＝0.8。
            Assert.AreEqual(0.8f, GovernmentPrincipleRules.VirtueSustainability(0.8f, 0f), Eps);
            // 不平等0.5なら半減＝0.8×0.5＝0.4。
            Assert.AreEqual(0.4f, GovernmentPrincipleRules.VirtueSustainability(0.8f, 0.5f), Eps);
            // 不平等が極大なら徳は維持できない＝0。
            Assert.AreEqual(0f, GovernmentPrincipleRules.VirtueSustainability(0.8f, 1f), Eps);
        }

        /// <summary>政体腐敗判定＝原理の強さが閾値を下回ると腐敗とみなす（原理を失えば崩壊へ）。</summary>
        [Test]
        public void IsRegimeCorrupted_閾値割れで腐敗()
        {
            // 既定閾値0.3：原理0.2は腐敗・0.5は健全。
            Assert.IsTrue(GovernmentPrincipleRules.IsRegimeCorrupted(0.2f));
            Assert.IsFalse(GovernmentPrincipleRules.IsRegimeCorrupted(0.5f));
            // 明示閾値も同様。
            Assert.IsTrue(GovernmentPrincipleRules.IsRegimeCorrupted(0.4f, 0.5f));
            Assert.IsFalse(GovernmentPrincipleRules.IsRegimeCorrupted(0.6f, 0.5f));
        }
    }
}
