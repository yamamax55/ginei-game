using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// エンディング分岐（Almagest・#1061）を固定する：評判・同盟数・経験イベントの全充足で結末条件が開き、
    /// 満たす中で優先度最高＝最も特別な結末が選ばれる。近さ（足りない要素のヒント）・王道/覇道の分岐・
    /// 全条件達成の真エンディングも担保。
    /// </summary>
    public class EndingBranchRulesTests
    {
        private static readonly EndingBranchParams P = EndingBranchParams.Default; // 評判1.0 / 同盟0.5 / イベント0.3 / 王道閾値0.0

        // 割拠均衡：軽い条件（評判0.3・同盟1・イベント不問）。
        private static EndingCondition Hegemony() =>
            new EndingCondition(EndingType.割拠均衡, 0.3f, 1, null, priority: 1);

        // 覇道統一：中程度（評判0.6・同盟2・"会戦勝利"必須）。
        private static EndingCondition HaoUnify() =>
            new EndingCondition(EndingType.覇道統一, 0.6f, 2, new[] { "会戦勝利" }, priority: 5);

        // 王道統一：最難関＝最も特別（評判0.9・同盟3・"会戦勝利"+"民心掌握"必須）。
        private static EndingCondition WangUnify() =>
            new EndingCondition(EndingType.王道統一, 0.9f, 3, new[] { "会戦勝利", "民心掌握" }, priority: 10);

        [Test]
        public void MeetsCondition_AllThreeAxes_MustBeSatisfied()
        {
            var c = HaoUnify(); // 評判0.6・同盟2・"会戦勝利"
            // 全充足。
            Assert.IsTrue(EndingBranchRules.MeetsCondition(c, 0.6f, 2, new[] { "会戦勝利" }));
            // 評判不足。
            Assert.IsFalse(EndingBranchRules.MeetsCondition(c, 0.59f, 2, new[] { "会戦勝利" }));
            // 同盟不足。
            Assert.IsFalse(EndingBranchRules.MeetsCondition(c, 0.6f, 1, new[] { "会戦勝利" }));
            // 必須イベント未経験。
            Assert.IsFalse(EndingBranchRules.MeetsCondition(c, 0.6f, 2, new[] { "別の出来事" }));
        }

        [Test]
        public void EligibleEndings_ListsOnlyMetConditions()
        {
            var conds = new[] { Hegemony(), HaoUnify(), WangUnify() };
            // 評判0.6・同盟2・"会戦勝利"のみ＝割拠均衡と覇道統一が到達可能（王道は評判/同盟/イベント不足）。
            var eligible = EndingBranchRules.EligibleEndings(conds, 0.6f, 2, new[] { "会戦勝利" });
            Assert.AreEqual(2, eligible.Length);
            Assert.AreEqual(EndingType.割拠均衡, eligible[0].ending);
            Assert.AreEqual(EndingType.覇道統一, eligible[1].ending);
        }

        [Test]
        public void SelectEnding_PrefersHighestPriority_MostSpecial()
        {
            var conds = new[] { Hegemony(), HaoUnify(), WangUnify() };
            // 割拠均衡(1)と覇道統一(5)を満たす→優先度最高の覇道統一が選ばれる。
            Assert.AreEqual(EndingType.覇道統一,
                EndingBranchRules.SelectEnding(conds, 0.6f, 2, new[] { "会戦勝利" }));
            // 全条件達成→王道統一(10)が最も特別＝選ばれる。
            Assert.AreEqual(EndingType.王道統一,
                EndingBranchRules.SelectEnding(conds, 1f, 3, new[] { "会戦勝利", "民心掌握" }));
        }

        [Test]
        public void SelectEnding_NoneMet_FallsBackToRuin()
        {
            var conds = new[] { Hegemony(), HaoUnify(), WangUnify() };
            // どの結末条件も満たさない＝滅亡（凡庸な結末）にフォールバック。
            Assert.AreEqual(EndingType.滅亡,
                EndingBranchRules.SelectEnding(conds, 0f, 0, new string[0]));
        }

        [Test]
        public void EndingProximity_HintsHowFarFromTarget()
        {
            var c = WangUnify(); // 評判0.9・同盟3・イベント2件
            // すでに満たす→1.0。
            Assert.AreEqual(1f, EndingBranchRules.EndingProximity(c, 1f, 3, new[] { "会戦勝利", "民心掌握" }, P), 1e-4f);
            // 評判0.8(不足0.1)・同盟1(不足2)・イベント1件不足。
            // penalty = 0.1*1.0 + 2*0.5 + 1*0.3 = 0.1+1.0+0.3 = 1.4 → clamp → 近さ0。
            Assert.AreEqual(0f, EndingBranchRules.EndingProximity(c, 0.8f, 1, new[] { "会戦勝利" }, P), 1e-4f);
            // 評判だけ0.1不足＝近さ0.9（あと評判だけ、というヒント）。
            Assert.AreEqual(0.9f, EndingBranchRules.EndingProximity(c, 0.8f, 3, new[] { "会戦勝利", "民心掌握" }, P), 1e-4f);
        }

        [Test]
        public void PathDivergence_WangDaoSplitsUnification()
        {
            // 道が王道側(+)＝王道統一。
            Assert.AreEqual(EndingType.王道統一, EndingBranchRules.PathDivergence(1f, 0.5f, P));
            // 道が覇道側(−)＝覇道統一。
            Assert.AreEqual(EndingType.覇道統一, EndingBranchRules.PathDivergence(1f, -0.5f, P));
            // 閾値ちょうど(0)は王道側に含む。
            Assert.AreEqual(EndingType.王道統一, EndingBranchRules.PathDivergence(0.5f, 0f, P));
        }

        [Test]
        public void IsTrueEnding_OnlyWhenAllConditionsMet_AndUnification()
        {
            // 全条件達成＋統一＝真エンディング。
            Assert.IsTrue(EndingBranchRules.IsTrueEnding(EndingType.王道統一, allConditionsMet: true));
            Assert.IsTrue(EndingBranchRules.IsTrueEnding(EndingType.覇道統一, allConditionsMet: true));
            // 全条件未達成＝真エンディングでない。
            Assert.IsFalse(EndingBranchRules.IsTrueEnding(EndingType.王道統一, allConditionsMet: false));
            // 統一でない結末は真エンディングにならない（途中放棄/敗北）。
            Assert.IsFalse(EndingBranchRules.IsTrueEnding(EndingType.隠遁, allConditionsMet: true));
            Assert.IsFalse(EndingBranchRules.IsTrueEnding(EndingType.滅亡, allConditionsMet: true));
        }

        [Test]
        public void ProgressOfPlay_AccumulatesTowardSpecialEndings()
        {
            var conds = new[] { Hegemony(), HaoUnify(), WangUnify() };
            // 積み上げの物語：序盤(評判0.3/同盟1)＝割拠均衡のみ。
            Assert.AreEqual(EndingType.割拠均衡, EndingBranchRules.SelectEnding(conds, 0.3f, 1, new string[0]));
            // 中盤(評判0.6/同盟2/会戦勝利)＝覇道統一へ昇格。
            Assert.AreEqual(EndingType.覇道統一, EndingBranchRules.SelectEnding(conds, 0.6f, 2, new[] { "会戦勝利" }));
            // 終盤(評判0.9/同盟3/民心掌握)＝王道統一へ＝最も特別な結末に到達。
            Assert.AreEqual(EndingType.王道統一,
                EndingBranchRules.SelectEnding(conds, 0.9f, 3, new[] { "会戦勝利", "民心掌握" }));
        }
    }
}
