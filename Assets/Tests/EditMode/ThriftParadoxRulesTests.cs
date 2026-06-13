using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// 節約のパラドックス＝合成の誤謬の純ロジックのテスト（#1552 KEYN-5）。
    /// 既定Params（個人便益1.0/需要感度0.8/合成閾値0.5/デフレ罠0.6）で期待値を固定する。
    /// </summary>
    public class ThriftParadoxRulesTests
    {
        const float Eps = 0.0001f;

        /// <summary>個人にとって貯蓄を増やすのは合理的＝貯蓄率に比例して便益が増える（ミクロの美徳）。</summary>
        [Test]
        public void IndividualBenefit_貯蓄率に比例して増える()
        {
            var p = ThriftParadoxParams.Default;
            float low = ThriftParadoxRules.IndividualBenefit(0.2f, 1f, p);
            float high = ThriftParadoxRules.IndividualBenefit(0.6f, 1f, p);
            Assert.AreEqual(0.2f, low, Eps);   // 0.2×1×1.0
            Assert.AreEqual(0.6f, high, Eps);  // 0.6×1×1.0
            Assert.Greater(high, low);         // 貯蓄を増やすほど個人には得
        }

        /// <summary>全員が一斉に貯蓄を増やすと消費が減り総需要が落ちる。</summary>
        [Test]
        public void AggregateDemandDrop_集団貯蓄で総需要が落ちる()
        {
            var p = ThriftParadoxParams.Default;
            // 0.5×0.8＝0.4
            Assert.AreEqual(0.4f, ThriftParadoxRules.AggregateDemandDrop(0.5f, p), Eps);
            // 集団貯蓄率が高いほど需要の落ち込みは大きい
            Assert.Greater(ThriftParadoxRules.AggregateDemandDrop(0.8f, p),
                           ThriftParadoxRules.AggregateDemandDrop(0.3f, p));
        }

        /// <summary>需要減が乗数で増幅され所得が縮む（連鎖崩壊）。</summary>
        [Test]
        public void IncomeContraction_乗数で所得が縮む()
        {
            // 需要減0.4×(1+乗数0.5)＝0.6
            Assert.AreEqual(0.6f, ThriftParadoxRules.IncomeContraction(0.4f, 0.5f), Eps);
            // 乗数が大きいほど所得の縮みも大きい
            Assert.Greater(ThriftParadoxRules.IncomeContraction(0.4f, 0.8f),
                           ThriftParadoxRules.IncomeContraction(0.4f, 0.2f));
        }

        /// <summary>意図した貯蓄増が所得減で相殺され、総貯蓄はかえって減りうる（パラドックスの核）。</summary>
        [Test]
        public void ParadoxicalSavingsChange_所得減で総貯蓄がかえって減る()
        {
            // 意図0.3−所得減0.5＝−0.2＝節約したのに総貯蓄は減る
            Assert.AreEqual(-0.2f, ThriftParadoxRules.ParadoxicalSavingsChange(0.3f, 0.5f), Eps);
            Assert.Less(ThriftParadoxRules.ParadoxicalSavingsChange(0.3f, 0.5f), 0f);
            // 所得減が小さければ素直に貯蓄は増える（個人レベルの直観）
            Assert.AreEqual(0.2f, ThriftParadoxRules.ParadoxicalSavingsChange(0.3f, 0.1f), Eps);
        }

        /// <summary>個人の合理が集団で非合理になる＝合成の誤謬は閾値超で顕在化し、個人非合理なら生じない。</summary>
        [Test]
        public void FallacyOfComposition_閾値超で顕在化し個人非合理なら無()
        {
            var p = ThriftParadoxParams.Default; // 閾値0.5
            // 閾値以下は誤謬なし
            Assert.AreEqual(0f, ThriftParadoxRules.FallacyOfComposition(true, 0.5f, p), Eps);
            // 0.75：(0.75−0.5)/(1−0.5)＝0.5
            Assert.AreEqual(0.5f, ThriftParadoxRules.FallacyOfComposition(true, 0.75f, p), Eps);
            // 全員貯蓄で最大1
            Assert.AreEqual(1f, ThriftParadoxRules.FallacyOfComposition(true, 1f, p), Eps);
            // 個人が合理的でなければ誤謬は生じない
            Assert.AreEqual(0f, ThriftParadoxRules.FallacyOfComposition(false, 1f, p), Eps);
        }

        /// <summary>自分だけ使うと損だから皆使わない＝集合的行為問題は参加率×離反便益で強まる。</summary>
        [Test]
        public void CollectiveActionProblem_皆が緊縮へ走る()
        {
            // 0.8×0.5＝0.4
            Assert.AreEqual(0.4f, ThriftParadoxRules.CollectiveActionProblem(0.8f, 0.5f), Eps);
            // 離反（自分だけ消費）が損なほど全員が緊縮へ
            Assert.Greater(ThriftParadoxRules.CollectiveActionProblem(0.8f, 0.9f),
                           ThriftParadoxRules.CollectiveActionProblem(0.8f, 0.2f));
        }

        /// <summary>協調の失敗で需要が底に張り付く＝貯蓄率が高いほど残存需要は小さい。</summary>
        [Test]
        public void CoordinationFailure_需要が底に張り付く()
        {
            // 地力0.5×(1−貯蓄0.6)＝0.2
            Assert.AreEqual(0.2f, ThriftParadoxRules.CoordinationFailure(0.6f, 0.5f), Eps);
            // 貯蓄率が高いほど残る需要は少ない
            Assert.Less(ThriftParadoxRules.CoordinationFailure(0.8f, 0.5f),
                        ThriftParadoxRules.CoordinationFailure(0.2f, 0.5f));
        }

        /// <summary>節約の連鎖がデフレ不況の罠に陥ったか＝集団貯蓄率が閾値0.6を超えたとき真。</summary>
        [Test]
        public void IsDeflationaryTrap_閾値超でデフレ罠()
        {
            // 既定閾値0.6
            Assert.IsTrue(ThriftParadoxRules.IsDeflationaryTrap(0.7f));
            Assert.IsFalse(ThriftParadoxRules.IsDeflationaryTrap(0.5f));
            // 明示閾値オーバーロード
            Assert.IsTrue(ThriftParadoxRules.IsDeflationaryTrap(0.4f, 0.3f));
            Assert.IsFalse(ThriftParadoxRules.IsDeflationaryTrap(0.4f, 0.5f));
        }
    }
}
