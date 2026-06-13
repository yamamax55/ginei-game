using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 艦隊反乱（DisciplineRules=個別抗命 と CoupRules=国家転覆 の中間スケール）を固定する：
    /// 三因の加重不満、軍紀の蓋とカリスマ首謀者の触媒、決定論の発生判定、不満を燃料にした
    /// 伝播と鎮圧、忠誠派との同士討ち分裂、そして「鎮圧しても原因は残る」禍根。境界を担保。
    /// </summary>
    public class MutinyRulesTests
    {
        private static readonly MutinyParams P = MutinyParams.Default;
        // 待遇0.4/思想0.3/敗勢0.3・軍紀抑止0.7・首謀者1.5・伝播0.4・鎮圧0.5・同士討ち0.3・禍根0.2+0.5

        [Test]
        public void GrievanceAccumulation_ThreeFactorsWeighted()
        {
            // 三因すべて最大＝1.0（重みの和）
            Assert.AreEqual(1f, MutinyRules.GrievanceAccumulation(1f, 1f, 1f, P), 1e-5f);
            // 俸給半年遅配のみ＝0.5×0.4=0.2
            Assert.AreEqual(0.2f, MutinyRules.GrievanceAccumulation(0.5f, 0f, 0f, P), 1e-5f);
            // 思想の完全乖離のみ＝0.3
            Assert.AreEqual(0.3f, MutinyRules.GrievanceAccumulation(0f, 1f, 0f, P), 1e-5f);
            // 入力は0..1にクランプ
            Assert.AreEqual(1f, MutinyRules.GrievanceAccumulation(5f, 5f, 5f, P), 1e-5f);
        }

        [Test]
        public void MutinyRisk_DisciplineLids_RingleaderIgnites()
        {
            // 軍紀皆無＝不満がそのままリスク
            Assert.AreEqual(0.5f, MutinyRules.MutinyRisk(0.5f, 0f, false, P), 1e-5f);
            // 完全な軍紀＝0.5×(1−0.7)=0.15 まで抑える
            Assert.AreEqual(0.15f, MutinyRules.MutinyRisk(0.5f, 1f, false, P), 1e-5f);
            // 同条件でも首謀者がいれば1.5倍＝0.225
            Assert.AreEqual(0.225f, MutinyRules.MutinyRisk(0.5f, 1f, true, P), 1e-5f);
            // 上限1（不満1×軍紀0×首謀者でも確率を超えない）
            Assert.AreEqual(1f, MutinyRules.MutinyRisk(1f, 0f, true, P), 1e-5f);
        }

        [Test]
        public void Erupts_Deterministic()
        {
            Assert.IsTrue(MutinyRules.Erupts(0.15f, 0.1f));
            Assert.IsFalse(MutinyRules.Erupts(0.15f, 0.15f));
            // リスク0なら決して起きない
            Assert.IsFalse(MutinyRules.Erupts(0f, 0f));
        }

        [Test]
        public void SpreadTick_GrievanceFuels_SuppressionCuts()
        {
            // 反乱核0.5×不満1・鎮圧なし＝0.4×1×0.5×0.5=0.1 広がる
            Assert.AreEqual(0.6f, MutinyRules.SpreadTick(0.5f, 1f, 0f, 1f, P), 1e-5f);
            // 全力鎮圧＝0.5×1×0.5=0.25 削る → 0.5+0.1−0.25=0.35
            Assert.AreEqual(0.35f, MutinyRules.SpreadTick(0.5f, 1f, 1f, 1f, P), 1e-5f);
            // 核が無ければ不満だけでは広がらない（勃発は Erupts が与える）
            Assert.AreEqual(0f, MutinyRules.SpreadTick(0f, 1f, 0f, 1f, P), 1e-5f);
            // 長時間鎮圧でも下限0
            Assert.AreEqual(0f, MutinyRules.SpreadTick(0.1f, 0f, 1f, 100f, P), 1e-5f);
        }

        [Test]
        public void LoyalistSplit_FratricidePeaksAtEvenSplit()
        {
            // 3割反乱＝忠誠0.7/反乱0.3・同士討ち0.3×2×0.3=0.18
            MutinySplit s = MutinyRules.LoyalistSplit(0.3f, P);
            Assert.AreEqual(0.7f, s.loyalistShare, 1e-5f);
            Assert.AreEqual(0.3f, s.mutineerShare, 1e-5f);
            Assert.AreEqual(0.18f, s.internalAttrition, 1e-5f);
            // 拮抗（50:50）で同士討ちは最大0.3
            Assert.AreEqual(0.3f, MutinyRules.LoyalistSplit(0.5f, P).internalAttrition, 1e-5f);
            // 反乱なしなら損耗なし
            Assert.AreEqual(0f, MutinyRules.LoyalistSplit(0f, P).internalAttrition, 1e-5f);
        }

        [Test]
        public void SuppressionAftermath_CausesRemain_HarshnessSeedsNext()
        {
            // 苛烈な処断＝0.5×(0.2+1×0.5)=0.35 の禍根
            Assert.AreEqual(0.35f, MutinyRules.SuppressionAftermath(0.5f, 1f, P), 1e-5f);
            // 寛大でも原因（待遇・思想・敗勢）は除かれない＝基礎分0.5×0.2=0.1 が残る
            Assert.AreEqual(0.1f, MutinyRules.SuppressionAftermath(0.5f, 0f, P), 1e-5f);
            // 反乱が無ければ禍根もない
            Assert.AreEqual(0f, MutinyRules.SuppressionAftermath(0f, 1f, P), 1e-5f);
        }

        [Test]
        public void DefaultParams_CtorClamps()
        {
            // 負値・範囲外はctorでクランプされる
            var p = new MutinyParams(-1f, -1f, -1f, 2f, 0.5f, -1f, -1f, 2f, -1f, -1f);
            Assert.AreEqual(0f, p.payWeight, 1e-6f);
            Assert.AreEqual(1f, p.disciplineSuppression, 1e-6f);
            Assert.AreEqual(1f, p.ringleaderBoost, 1e-6f); // 首謀者倍率は1未満にならない
            Assert.AreEqual(1f, p.fratricideLoss, 1e-6f);
            // 既定値の固定
            Assert.AreEqual(0.4f, P.payWeight, 1e-6f);
            Assert.AreEqual(0.7f, P.disciplineSuppression, 1e-6f);
            Assert.AreEqual(1.5f, P.ringleaderBoost, 1e-6f);
        }
    }
}
